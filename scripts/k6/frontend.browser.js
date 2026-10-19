import { browser } from 'k6/browser';
import { check } from 'k6';
export const options = {
  scenarios: { frontend: { executor: 'constant-vus', vus: Number(__ENV.VUS || 100),
    duration: __ENV.DURATION || '5m', gracefulStop: '30s',
    options: { browser: { type: 'chromium' } } } },
  thresholds: { checks: ['rate>0.99'], browser_web_vital_lcp: ['p(95)<2500'],
    browser_web_vital_cls: ['p(95)<0.1'] },
};
export default async function () {
  const page = await browser.newPage();
  try {
    const res = await page.goto((__ENV.SITE_URL || 'https://www.pace1.site').replace(/\/$/, '') + (__ENV.FRONTEND_PATH || '/test'), { waitUntil: 'networkidle' });
    check(res, { 'frontend navigation OK': r => r && r.status() === 200 });
    await page.locator('body').waitFor();
    await page.waitForTimeout(3000);
  } finally { await page.close(); }
}
