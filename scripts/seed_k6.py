#!/usr/bin/env python3
"""Seed marked test students against existing repositories; never create source fixtures."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import random
import re
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
NS = uuid.UUID('03c10bd8-d598-44eb-a0fb-c5c800d0f5ac')


def lit(value):
    return 'NULL' if value is None else "'" + str(value).replace("'", "''") + "'"


def db(sql):
    connection = os.environ.get('ConnectionStrings__DefaultConnection')
    if not connection:
        connection = json.loads((ROOT / 'PACE.API/appsettings.Development.json').read_text())['ConnectionStrings']['DefaultConnection']
    parts = dict((k.strip().lower(), v.strip()) for k, v in
                 (item.split('=', 1) for item in connection.split(';') if '=' in item))
    env = os.environ.copy()
    env.update(PGPASSWORD=parts.get('password', ''), PGCONNECT_TIMEOUT='15',
               PGSSLMODE=parts.get('ssl mode', 'require').lower().replace('verifyfull', 'verify-full'))
    result = subprocess.run(['psql', '-X', '-q', '-t', '-A', '-v', 'ON_ERROR_STOP=1',
                             '-h', parts.get('host', 'localhost'), '-p', parts.get('port', '5432'),
                             '-U', parts.get('username', 'postgres'), '-d', parts.get('database', 'postgres')],
                            input=sql, text=True, env=env, capture_output=True)
    if result.returncode:
        raise SystemExit(result.stderr)
    return result.stdout.strip()


def build(batch, count, pool):
    rng = random.Random(batch)
    students = []
    sql = ['BEGIN;', 'SET LOCAL standard_conforming_strings=on;']
    # All batches use a reserved prefix, deterministic IDs and matching synthetic email.
    sql.append(f'''DO $guard$ BEGIN
      IF EXISTS (SELECT 1 FROM users WHERE "StudentCode" LIKE {lit('K6' + batch.upper() + '%')})
      THEN RAISE EXCEPTION 'Batch already exists or prefix collides; choose another batch'; END IF;
    END $guard$;''')
    for i in range(1, count + 1):
        repo = rng.choice(pool)
        selected = rng.sample(repo['questions'], min(4, len(repo['questions'])))
        user_id = str(uuid.uuid5(NS, batch + ':user:' + str(i)))
        code = f'K6{batch.upper()}X{i:04d}'
        email = f'pace-k6-{batch}-{i:04d}@example.com'
        sql.append(f'''INSERT INTO users ("Id", "Email", "StudentCode", "Role", "AnalysisRunId")
VALUES ({lit(user_id)}::uuid, {lit(email)}, {lit(code)}, 'Student', {lit(repo['repoId'])}::uuid);''')
        students.append(dict(seedBatch=batch, userId=user_id, studentCode=code, email=email,
                             repoId=repo['repoId'], answers=selected))
    sql.append('COMMIT;')
    return '\n'.join(sql), students


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--count', type=int, default=100)
    parser.add_argument('--batch', default=datetime.now(timezone.utc).strftime('%m%d%H%M') + uuid.uuid4().hex[:2])
    parser.add_argument('--repo-id', action='append', default=[], help='Restrict existing repo IDs (repeatable)')
    parser.add_argument('--output', type=Path, default=ROOT / 'scripts/k6/students.json')
    parser.add_argument('--dry-run', action='store_true', help='Read existing DB pool and print planned SQL; no writes')
    args = parser.parse_args()
    if not re.fullmatch('[a-zA-Z0-9]{1,12}', args.batch) or not 1 <= args.count <= 9999:
        parser.error('batch: 1–12 alphanumeric characters; count: 1–9999')
    args.batch = args.batch.lower()
    ids = [str(uuid.UUID(x)) for x in args.repo_id]
    restriction = ' AND r."Id" IN (' + ','.join(lit(x) + '::uuid' for x in ids) + ')' if ids else ''
    pool = json.loads(db('''SELECT coalesce(json_agg(p), '[]') FROM (
SELECT r."Id" AS "repoId", json_agg(json_build_object(
 'businessId', b."Id", 'questionId', q."Id", 'answerText',
 coalesce(nullif(q."SuggestedAnswer", ''), 'Load test response: explain validation, processing, persistence and error handling.')) ORDER BY q."Id") AS questions
FROM analysis_runs r JOIN businesses b ON b."AnalysisRunId"=r."Id"
JOIN generated_questions q ON q."BusinessId"=b."Id"
WHERE r."RepoName" NOT LIKE '[K6:%' ''' + restriction + '''
GROUP BY r."Id" ORDER BY r."Id") p;'''))
    if not pool:
        raise SystemExit('No existing non-K6 repositories with questions found; nothing written.')
    sql, students = build(args.batch, args.count, pool)
    if args.dry_run:
        print(sql)
        return
    args.output.parent.mkdir(parents=True, exist_ok=True)
    manifest = args.output.parent / 'batches' / (args.batch + '.json')
    manifest.parent.mkdir(parents=True, exist_ok=True)
    if manifest.exists():
        raise SystemExit('Batch manifest already exists; choose another batch.')
    # Durable recovery manifest before DB write; cleanup verifies actual DB identities.
    manifest.write_text(json.dumps(students, ensure_ascii=False, indent=2) + '\n')
    db(sql)
    args.output.write_text(manifest.read_text())
    print(f'Seeded batch {args.batch}: {args.count} students across {len(set(s["repoId"] for s in students))} existing repositories.')
    print('Created no repositories, businesses, questions, submissions or feedback.')
    print(f'Accounts: {args.output.resolve()}\nCleanup manifest: {manifest.resolve()}')


if __name__ == '__main__':
    main()
