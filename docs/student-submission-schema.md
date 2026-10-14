# Student submission schema

Migration: `UpdateStudentSubmissionSchema`.

## Safety and deployment

This change has NOT been applied to a database. No database connection was made.
The active startup project is `PACE.API` (case-sensitive on Linux), which has no
Development connection file. The ignored file in the older `PACE.Api` directory
points to Neon and must not be used for this task.

Before any application, verify the effective Development configuration including
environment variables, user secrets, and command-line overrides. Only use a
verified local PostgreSQL database. Do not copy the old Neon configuration.

Dropping `student_answers.AiScore` permanently removes any values in that column.
Back up those values before applying if they need to be retained. `Down` restores
an empty nullable column; it cannot recover the removed scores.

Run these read-only checks on the verified local database before applying:

```sql
SELECT current_database(), inet_server_addr(), inet_server_port();
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT COUNT(*) AS answer_count,
       COUNT("AiScore") AS scores_that_will_be_lost
FROM student_answers;
SELECT "UserId", "QuestionId", COUNT(*)
FROM student_answers
GROUP BY "UserId", "QuestionId"
HAVING COUNT(*) > 1;
SELECT indexname, indexdef FROM pg_indexes
WHERE schemaname = 'public' AND tablename = 'student_answers';
```

Existing answers get `SubmissionId = NULL`; no backfill, answer deletion, or
feedback update is performed. All existing users get `StudentCode = NULL`.
Therefore new partial unique indexes cannot conflict with legacy rows during Up.
The old unique `(UserId, QuestionId)` index is replaced by a nonunique UserId
index and unique `(SubmissionId, QuestionId) WHERE "SubmissionId" IS NOT NULL`.
Legacy answers without a submission are not subject to question uniqueness.

Offline validation (no database connection):

```bash
dotnet build PACE.sln --no-restore -m:1 /p:UseSharedCompilation=false
dotnet ef migrations has-pending-model-changes --project PACE.Infrastructure --startup-project PACE.API --no-build -- --environment Development
dotnet ef migrations script AddSourceCodeAndDataFlow UpdateStudentSubmissionSchema --project PACE.Infrastructure --startup-project PACE.API --no-build --output /tmp/pace-submission-up.sql -- --environment Development
dotnet ef migrations script UpdateStudentSubmissionSchema AddSourceCodeAndDataFlow --project PACE.Infrastructure --startup-project PACE.API --no-build --output /tmp/pace-submission-down.sql -- --environment Development
```

Down refuses to run when duplicate `(UserId, QuestionId)` pairs would prevent
restoring the old index. It never deletes answers to resolve duplicates. Down
removes StudentCode and submission metadata/links, so back those up before rollback.

## Model and Task #3 responsibilities

- `User.StudentCode`: nullable string, max 20, unique for non-null values (all roles).
  PostgreSQL uniqueness is case-sensitive; trimming/case normalization and rejecting
  empty MSSV values are future input validation responsibilities.
- `StudentSubmission`: Guid PK, required UserId and AnalysisRunId, UTC SubmittedAt
  (`timestamp with time zone`, `now()` DB default), collection of answers.
- New submission FKs use Restrict. Existing User/Question/AnalysisRun delete
  behaviors remain unchanged.
- `StudentAnswer`: nullable SubmissionId, original UserId and QuestionId retained;
  AiFeedback, FeedbackFormat, StudentReply, and RubricScore are unchanged.
- Repository lookup remains User → AnalysisRun (RepoUrl), then Businesses →
  GeneratedQuestions. BusinessFlow is not the selection unit.
- Task #3 must validate exactly three distinct Businesses, one question per
  Business, and consistency between answer UserId, submission UserId, submission
  AnalysisRunId, and each question's Business.AnalysisRunId. Simple FKs do not
  enforce these cross-entity business rules. Save submission and answers atomically.
- No User DTO, mapping, create/update service, or Excel importer currently exists
  in this repository; no new implementation was added for those layers.
