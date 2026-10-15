# Student submissions – Phase 1

All three endpoints are anonymous and identify a Student using StudentCode +
Email. StudentCode comparison is exact after trimming (preserves leading zeros);
Email uses the existing repository's case-insensitive lookup. No OTP/JWT required.
Responses contain question text, never SuggestedAnswer or internal scoring data.

## POST /api/student-submissions/lookup

```json
{ "studentCode": "001ABC", "email": "student@gmail.com" }
```

200: `{ repository: { analysisRunId, repoName, repoUrl }, businesses:
[{ businessId, businessName, questions: [{ questionId, questionText }] }] }`.
Only Businesses with questions are included. An incomplete repository can return
fewer than three Businesses; submit rejects it with 422.

## POST /api/student-submissions

```json
{
  "studentCode": "001ABC",
  "email": "student@gmail.com",
  "answers": [
    { "businessId": "<guid-1>", "questionId": "<guid-1>", "answerText": "..." },
    { "businessId": "<guid-2>", "questionId": "<guid-2>", "answerText": "..." },
    { "businessId": "<guid-3>", "questionId": "<guid-3>", "answerText": "..." }
  ]
}
```

201: `{ submissionId, submittedAt, answerCount }`. Exactly three nonblank answers
from three distinct Businesses, with questions belonging to those Businesses and
the student's current AnalysisRun. Answer text is trimmed. Dates are UTC.

UnitOfWork transaction includes user row lock (`SELECT ... FOR UPDATE`), identity
and repository re-read, duplicate check, and one SaveChanges for submission and
three answers. Concurrent requests through this flow serialize per user across
API instances. Submission ID stays stable during execution-strategy retries.
No schema/unique constraint was added: other future submission writers must follow
the same lock/check protocol. Existing rows for the same user/run also block submit.

## POST /api/student-submissions/retrieve

```json
{ "submissionId": "<guid>", "studentCode": "001ABC", "email": "student@gmail.com" }
```

200: `{ submissionId, submittedAt, repository, answers:
[{ businessId, businessName, questionId, questionText, answerText }] }`.
Ownership is filtered in the database query. A different student's submission and
an unknown submission both return 404. Uses the submission's original AnalysisRun,
so reassignment of the student's current repo does not hide their old submission.

## Errors and verification

Uses the existing ErrorResponse/GlobalExceptionHandler:
- 400: malformed identity, invalid ID, wrong answer count, repeated Business,
  blank answer, foreign Business or mismatched Question.
- 404: identity does not match a Student, or submission not owned/found.
- 409: user already submitted for this AnalysisRun.
- 422: no assigned repository or fewer than three Businesses with questions.

Run `dotnet build PACE.sln` and `dotnet test PACE.Tests/PACE.Tests.csproj`.
Tests use repository/UnitOfWork stubs, never a database or API host. PostgreSQL
transaction/row-lock behavior has not been integration-tested. Schema migration
from the previous task must already be applied by an authorized operator before
using these endpoints; this task does not apply migrations or alter DB settings.
