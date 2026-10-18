# Student submissions – Phase 1

Student endpoints require `Authorization: Bearer <PACE access token>` with role
`Student`. The token UUID from `ICurrentUserService.UserId` identifies the student;
MSSV and email are no longer accepted as identity fields. The service also checks
that the user still exists and has the Student role in the database.
Responses contain question text, never SuggestedAnswer or internal scoring data.

## POST /api/student-submissions/lookup

No request body is required. The current student's UUID determines the assigned
repository and its Businesses/questions.

200: `{ repository: { analysisRunId, repoName, repoUrl }, businesses:
[{ businessId, businessName, questions: [{ questionId, questionText }] }] }`.
Only Businesses with questions are included. An incomplete repository can return
fewer than three Businesses; submit rejects it with 422.

## POST /api/student-submissions

```json
{
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
{ "submissionId": "<guid>" }
```

200: `{ submissionId, submittedAt, repository, answers:
[{ businessId, businessName, questionId, questionText, answerText }] }`.
Ownership is filtered in the database query. A different student's submission and
an unknown submission both return 404. Uses the submission's original AnalysisRun,
so reassignment of the student's current repo does not hide their old submission.

## GET /api/student-submissions — Admin only

Requires a PACE JWT with role `Admin`. Returns all submissions across students,
paginated (default `page=1&pageSize=20`, maximum page size 200), newest first.
Optional filters: `userId`, `analysisRunId`, `search` (MSSV or email, case-insensitive).

Example: `GET /api/student-submissions?page=1&pageSize=20&search=SE123`

200 response shape:

```json
{
  "items": [{
    "userId": "<student UUID>",
    "studentId": "SE123456",
    "email": "student@gmail.com",
    "submission": {
      "submissionId": "<submission UUID>",
      "submittedAt": "2026-10-15T12:00:00Z",
      "repository": { "analysisRunId": "<UUID>", "repoName": "repo", "repoUrl": "https://example.com/repo" },
      "answers": []
    }
  }],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Each answer has the same fields as Retrieve. `studentId` maps to nullable
`Users.StudentCode`. Student tokens cannot use this endpoint; Admin tokens cannot
use the three Student endpoints.

## Errors and verification

Uses the existing ErrorResponse/GlobalExceptionHandler:
- 400: invalid ID, wrong answer count, repeated Business,
  blank answer, foreign Business or mismatched Question.
- 401: unauthenticated, missing/invalid UUID, or deleted account.
- 403: wrong role (including a Student whose DB role has changed).
- 404: submission not owned/found.
- 409: user already submitted for this AnalysisRun.
- 422: no assigned repository or fewer than three Businesses with questions.

Run `dotnet build PACE.sln` and `dotnet test PACE.Tests/PACE.Tests.csproj`.
Tests use repository/UnitOfWork stubs, never a database or API host. PostgreSQL
transaction/row-lock behavior has not been integration-tested. Schema migration
from the previous task must already be applied by an authorized operator before
using these endpoints; this task does not apply migrations or alter DB settings.

## Read full submission history and details

All routes below require JWT authentication. No schema migration is needed.

| Role | Method and path | Purpose |
| --- | --- | --- |
| Admin | `GET /api/student-submissions` | All submissions, paginated |
| Student | `GET /api/student-submissions/me` | Own history, paginated |
| Admin, Student | `GET /api/student-submissions/{id}` | Full detail; Student can only read an owned submission |
| Student | `POST /api/student-submissions/retrieve` | Existing own-detail endpoint, with the same expanded detail payload |

List filters: `UserId` (Admin), `AnalysisRunId`, `IsSubmitted`, `Search` (email or student code), `Page` (default 1), `PageSize` (default 20, capped at 200).
Filters can be combined. Omit `IsSubmitted` to include both active sessions and submitted work. Student history always scopes UserId to the authenticated student, ignoring a supplied UserId. History remains available when the student's assigned repository changes.

Examples:

```http
GET /api/student-submissions?UserId=<student-guid>&Page=1&PageSize=20
GET /api/student-submissions?AnalysisRunId=<repo-guid>&IsSubmitted=true
GET /api/student-submissions?Search=student@example.com
GET /api/student-submissions/me?IsSubmitted=true
GET /api/student-submissions/<submission-guid>
```

Admin list items retain their existing `userId`, `studentId`, `email`, `submission` wrapper. Student list items are submission details directly. Both lists use `items`, `page`, `pageSize`, `totalCount`, and the existing paging metadata.

Each submission detail includes:

- `submissionId`, `createdAt`, `submittedAt` (null for an active session).
- `student`: `userId`, `studentCode`, `email`.
- `repository`: `analysisRunId`, `repoName`, `repoUrl`.
- `answers`: `studentAnswerId`, `businessId`, `businessName`, `questionId`, `questionText`, `suggestedAnswer`, `difficulty`, `contextType`, `questionCreatedAt`, `answerText`, `answerCreatedAt`, `aiFeedback`, `feedbackFormat`, `studentReply`, `rubricScores`.
- Each rubric score includes `id`, `criterionId`, `code`, `name`, `statement`, `score`.

`aiFeedback` preserves the stored string (usually JSON); no LLM is called while reading. Missing feedback remains null. Rubric scores are returned only when stored in the rubric score table; feedback-only rubric content remains in `aiFeedback`. Question and criterion metadata reflect the current stored records, not historical snapshots.

A Student requesting another student's submission gets 404, the same as a missing submission. Unsupported roles get 403; unauthenticated requests get 401. Suggested answers are included in owned submission details as requested; lookup still returns question text only.
