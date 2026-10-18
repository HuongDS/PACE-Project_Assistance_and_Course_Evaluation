# Student Google authentication

## Temporary direct form entry

Student Google sign-in is temporarily disabled: its controller endpoint is commented out, with the service and validator preserved for restoration. Staff Google sign-in is unchanged.

The frontend should open the Gmail + MSSV form directly and call:

```http
POST /api/auth/student/quick
Content-Type: application/json

{ "email": "student@gmail.com", "studentCode": "SE123456" }
```

This returns the same token and student response as Google student login. The MSSV alone identifies an existing Student account (case-insensitive, surrounding spaces ignored). Email is still a required form field, but is not compared against the imported account email. Unknown or ambiguous MSSV values are rejected. This temporary flow does not verify mailbox ownership. Keep `Auth:Enabled` true.

Use the returned `accessToken` as a Bearer token to call `POST /api/student-submissions/join` with the entered email and studentCode. Join verifies the MSSV against the authenticated student, rather than comparing email addresses. Then call `POST /api/student-submissions/lookup` to load questions and the existing submission APIs to save the form. No frontend exists in this repository, so the consuming frontend must apply this routing change.

To restore Google sign-in, uncomment `GoogleStudentLogin` in `AuthController` and remove or disable `StudentQuickLogin` there.

## Preserved Google integration

The following describes the preserved integration when it is re-enabled.

Set `GoogleAuth:ClientId` (or `GoogleAuth__ClientId`) to the Google OAuth web client ID used by the frontend. Configure `Jwt:Key` with a random signing key of at least 32 bytes and keep `Auth:Enabled` true. Setting it false is an existing development bypass that disables authorization across controllers.

The frontend uses Google Sign-In to obtain an **ID token** and sends it over HTTPS:

```http
POST /api/auth/google/student
Content-Type: application/json

{ "idToken": "<Google credential ID token>" }
```

The backend uses Google's .NET validator to check the Google signature, issuer, configured client audience, and expiration, and requires `email_verified`. It looks up the verified email using the existing case-insensitive repository lookup. Google Workspace student addresses are supported as well as Gmail addresses. Only existing users with the Student role can log in; this endpoint never inserts or updates users.

```json
{
  "accessToken": "<PACE JWT>",
  "expiresAt": "2026-10-15T12:00:00Z",
  "student": {
    "userId": "b14d980e-8bb2-48e9-bd69-5d8c95a908cd",
    "studentId": "SE123456",
    "email": "student@gmail.com"
  }
}
```

`studentId` maps to the existing nullable `Users.StudentCode` (MSSV); `userId` is the UUID used in the PACE JWT's name identifier claim. Use the PACE access token in `Authorization: Bearer <accessToken>` for subsequent requests, including `GET /api/auth/me`.

Invalid or unverified Google credentials, unknown students, and non-Student accounts receive HTTP 401. Missing or malformed request fields receive HTTP 400 through existing model validation. A missing Google client ID fails closed as a server configuration error. Google public key retrieval requires outbound HTTPS access.

`POST /api/auth/login` retains the existing email-based Admin login and response shape. It now rejects Student accounts to prevent bypassing Google verification. The existing Admin flow still relies on email alone; its authentication mechanism is unchanged.

No frontend application exists in this solution; the endpoint accepts the Google Sign-In credential from the consuming frontend. No database migrations are required.
