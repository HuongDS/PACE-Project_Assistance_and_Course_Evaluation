#!/usr/bin/env python3
"""Preview/delete only verified K6 student accounts and their work, never repositories."""
import argparse
import json
import re
import uuid
from seed_k6 import NS, ROOT, db, lit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--batch', required=True, help='Exact seed batch to clean')
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    if not re.fullmatch('[a-z0-9]{1,12}', args.batch):
        parser.error('Invalid batch')
    rows = json.loads(db('SELECT coalesce(json_agg(u), \'[]\') FROM users u WHERE "StudentCode" LIKE '
                         + lit('K6' + args.batch.upper() + 'X%') + ';'))
    for u in rows:
        m = re.fullmatch('K6' + args.batch.upper() + r'X(\d{4})', u['StudentCode'] or '')
        if (not m or u['Role'] != 'Student'
            or u['Id'] != str(uuid.uuid5(NS, args.batch + ':user:' + str(int(m[1]))))
            or u['Email'] != f'pace-k6-{args.batch}-{int(m[1]):04d}@example.com'):
            raise SystemExit('Seed identity mismatch; cleanup aborted.')
    if not rows:
        print('No matching seed students remain.')
        return
    ids = ','.join(lit(u['Id']) + '::uuid' for u in rows)
    # Recheck identities under lock so preview cannot race a changed account.
    identities = ','.join('(' + ','.join([lit(u['Id']) + '::uuid', lit(u['StudentCode']), lit(u['Email'])]) + ')' for u in rows)
    sql = f'''BEGIN;
SET LOCAL lock_timeout='10s';
LOCK TABLE users, student_submissions, student_answers, rubric_scores IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE seed_accounts (id uuid PRIMARY KEY, code text, email text) ON COMMIT DROP;
INSERT INTO seed_accounts VALUES {identities};
DO $guard$ BEGIN
 IF EXISTS (SELECT 1 FROM seed_accounts s LEFT JOIN users u ON u."Id"=s.id
 WHERE u."Id" IS NULL OR u."StudentCode"<>s.code OR u."Email"<>s.email OR u."Role"<>'Student')
 OR EXISTS (SELECT 1 FROM student_answers a JOIN student_submissions s ON s."Id"=a."SubmissionId"
 WHERE (a."UserId" IN ({ids})) <> (s."UserId" IN ({ids})))
 THEN RAISE EXCEPTION 'Account or submission ownership changed; cleanup aborted'; END IF;
END $guard$;
SELECT json_build_object('students', (SELECT count(*) FROM seed_accounts),
 'submissions', (SELECT count(*) FROM student_submissions WHERE "UserId" IN ({ids})),
 'answers', (SELECT count(*) FROM student_answers WHERE "UserId" IN ({ids})));
'''
    if args.apply:
        sql += f'''DELETE FROM rubric_scores WHERE "StudentAnswerId" IN (SELECT "Id" FROM student_answers WHERE "UserId" IN ({ids}));
DELETE FROM student_answers WHERE "UserId" IN ({ids});
DELETE FROM student_submissions WHERE "UserId" IN ({ids});
DELETE FROM users WHERE "Id" IN ({ids});
COMMIT;'''
    else:
        sql += 'ROLLBACK;'
    print(db(sql))
    print('Deleted seed students and their work; existing repositories/questions preserved.' if args.apply else 'Preview only; add --apply to delete.')
    if args.apply:
        fixture = ROOT / 'scripts/k6/students.json'
        if fixture.exists():
            accounts = json.loads(fixture.read_text())
            if accounts and all(a.get('seedBatch') == args.batch for a in accounts):
                fixture.unlink()


if __name__ == '__main__':
    main()
