import http from 'k6/http';
import { check, sleep, fail } from 'k6';
import exec from 'k6/execution';
import { SharedArray } from 'k6/data';
import { Rate, Trend, Counter } from 'k6/metrics';

const SITE = (__ENV.SITE_URL || 'https://www.pace1.site').replace(/\/$/, '');
const FRONTEND_PATH = __ENV.FRONTEND_PATH || '/test';
// Explicit: the frontend host may not proxy /api to the backend.
const API = (__ENV.API_BASE_URL || 'https://pace-api-jp.bluerock-e6f4ebd4.japaneast.azurecontainerapps.io/api').replace(/\/$/, '');
const VUS = Number(__ENV.VUS || 100);
const MODE = __ENV.MODE || 'read';
const LIVE_LOG = __ENV.LIVE_LOG === 'true';
const LOG_VU = Number(__ENV.LOG_VU || 0);
function liveLog(step, status, duration, ok) {
  if (!LIVE_LOG || (LOG_VU && LOG_VU !== exec.vu.idInTest)) return;
  const message = `[VU ${exec.vu.idInTest} | iteration ${exec.vu.iterationInScenario + 1}] ${step}: ${ok ? 'OK' : 'FAIL'} status=${status} duration=${Math.round(duration)}ms`;
  if (ok) console.log(message); else console.warn(message);
}
if (!/^https?:\/\//.test(API) || /YOUR-BACKEND|your-backend|<|>/.test(API))
  throw new Error('API_BASE_URL must be a real backend URL including /api; do not use a placeholder');
if (!['read', 'llm'].includes(MODE)) throw new Error('MODE must be read or llm');
if (!Number.isInteger(VUS) || VUS < 1) throw new Error('VUS must be a positive integer');
const students = new SharedArray('students', () => JSON.parse(open(__ENV.STUDENTS_FILE || './students.json')));
if (students.length < VUS) throw new Error(`Need at least ${VUS} distinct students`);
const codes = students.map(s => String(s.studentCode || '').trim().toUpperCase());
const emails = students.map(s => String(s.email || '').trim().toLowerCase());
if (codes.some(c => !c) || emails.some(e => !e.includes('@')) ||
    new Set(codes).size !== codes.length || new Set(emails).size !== emails.length)
  throw new Error('Each student must have a unique studentCode and email');

const apiOK = new Rate('api_success');
const apiLatency = new Trend('api_read_ms', true);
const submitLatency = new Trend('submission_ms', true);
const aiOK = new Rate('ai_completed');
const attempts = new Counter('submission_attempts');
const existing = new Counter('already_submitted');
const firstSubmissions = new Counter('fresh_submission_responses');
export const options = {
  scenarios: { students: { executor: 'constant-vus', vus: VUS,
    duration: __ENV.DURATION || '10m', gracefulStop: __ENV.GRACEFUL_STOP || '10m' } },
  thresholds: {
    http_req_failed: ['rate<0.01'], checks: ['rate>0.99'],
    api_success: ['rate>0.99'], api_read_ms: ['p(95)<2000'],
    ...(MODE === 'llm' ? { ai_completed: ['rate>0.95'],
      submission_attempts: [`count==${VUS}`], fresh_submission_responses: [`count==${VUS}`],
      submission_ms: [`p(95)<${Number(__ENV.SUBMIT_P95_MS || 180000)}`] } : {}),
  },
};
let token;
let expiresAt = 0;
let submitAttempted = false;
let frontendFailureLogged = false;
function json(response) { try { return response.json(); } catch (_) { return null; } }
function api(method, path, body, name, isSubmit = false) {
  const res = http.request(method, `${API}${path}`, body == null ? null : JSON.stringify(body), {
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    tags: { name, component: isSubmit ? 'submission_llm' : 'api' },
    timeout: isSubmit ? (__ENV.SUBMIT_TIMEOUT || '8m') : '30s',
  });
  const ok = check(res, { [`${name}: expected status`]: r => r.status === (isSubmit ? 201 : 200) });
  liveLog(name, res.status, res.timings.duration, ok);
  apiOK.add(ok);
  (isSubmit ? submitLatency : apiLatency).add(res.timings.duration);
  if (!ok) {
    sleep(1);
    fail(`${name}: HTTP ${res.status}; ${res.error || 'unexpected status'}; VU ${exec.vu.idInTest}`);
  }
  const data = json(res);
  if (!check(data, { [`${name}: JSON response`]: d => d !== null })) fail(`${name}: invalid JSON`);
  return data;
}
export default function () {
  const student = students[exec.vu.idInTest - 1];
  if (!student) fail('No account for VU; run as one local k6 instance');
  if (!token || Date.now() >= expiresAt - 60000) {
    const login = api('POST', '/auth/student/quick', { email: student.email, studentCode: student.studentCode }, 'login');
    if (!login.accessToken) fail('Missing accessToken');
    token = login.accessToken;
    expiresAt = Date.parse(login.expiresAt);
    if (!Number.isFinite(expiresAt)) fail('Missing/invalid expiresAt');
  }
  const page = http.get(SITE + FRONTEND_PATH, { tags: { name: 'frontend_document', component: 'frontend' } });
  const frontendOK = check(page, { 'frontend: HTML document': r => r.status === 200 && /<html/i.test(r.body || '') });
  liveLog('frontend', page.status, page.timings.duration, frontendOK);
  if (!frontendOK && !frontendFailureLogged) {
    console.warn(`Frontend check failed: status=${page.status}, url=${page.url}, error=${page.error || 'none'}`);
    frontendFailureLogged = true;
  }
  api('GET', '/auth/me', null, 'profile');
  const lookup = api('POST', '/student-submissions/lookup', null, 'questions');
  sleep(1 + Math.random() * 2);
  if (MODE === 'llm' && !submitAttempted) {
    const businesses = (lookup.businesses || []).filter(b => b.questions && b.questions.length);
    const available = businesses.flatMap(b => b.questions.map(q => ({businessId: b.businessId, questionId: q.questionId, businessName: b.businessName})));
    const required = Math.min(4, available.length);
    if (!required) fail('Account has no existing questions');
    const joined = api('POST', '/student-submissions/join', { email: student.email, studentCode: student.studentCode }, 'join');
    if (!joined.submissionId) fail('Missing submissionId');
    submitAttempted = true; // Never automatically retry a potentially billed request.
    if (joined.submittedAt) {
      existing.add(1);
      fail('Account already submitted: use a fresh test account/repository assignment for LLM load');
    }
    // Use seeded random selection, validated against current API lookup.
    const planned = student.answers || [];
    if (planned.length !== required || new Set(planned.map(a => a.questionId)).size !== required ||
        planned.some(a => !available.some(q => q.questionId === a.questionId && q.businessId === a.businessId)))
      fail('Seed question assignments no longer match API; seed a fresh batch');
    const answers = planned.map(a => ({businessId: a.businessId, questionId: a.questionId, answerText: a.answerText}));
    sleep(Number(__ENV.THINK_BEFORE_SUBMIT || 5));
    attempts.add(1);
    const result = api('POST', '/student-submissions', { submissionId: joined.submissionId, answers }, 'submit', true);
    firstSubmissions.add(1);
    const evaluations = result.evaluatedAnswers || [];
    check(evaluations, { 'submission: expected evaluations': a => a.length === required });
    for (let i = 0; i < required; i++) {
      const a = evaluations[i];
      const completed = !!a && a.evaluationStatus === 'Completed' && !!a.aiEvaluation && !a.evaluationError;
      liveLog(`AI answer ${i + 1}: ${a ? a.evaluationStatus : 'missing'}`, result.submissionId ? 201 : 0, 0, completed);
      aiOK.add(completed);
      check(completed, { 'AI feedback completed': v => v });
    }
    api('POST', '/student-submissions/retrieve', { submissionId: joined.submissionId }, 'retrieve');
  }
  api('GET', '/student-submissions/me?page=1&pageSize=10', null, 'history');
  sleep(2 + Math.random() * 4);
}
export function handleSummary(data) {
  return { 'summary.json': JSON.stringify(data, null, 2),
    stdout: `\nPACE ${MODE}: ${VUS} VUs. Full metrics and thresholds: summary.json\n` };
}
