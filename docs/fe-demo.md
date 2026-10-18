# FE demo dataset

Seeded in the configured Development PostgreSQL database for `lequanghieu2504@gmail.com`.
Student code: `DEMOBF454F8C`. This is a Google student account; there is no student password.

- Assigned repo `[DEMO] PACE Shop - current`: 3 businesses, 6 questions, 6 methods, 3 Mermaid flows. No submission has been created, so join and submit are available.
- Previous repo `[DEMO] PACE Shop - history`: same fixture structure and a completed submission with 3 answers and synthetic Sandwich, Rubric, Explainable feedback. One stored rubric score and one student reply are included.
- Repo URLs are synthetic labels. Do not clone/reanalyze these demo repositories.
- Feedback in the history record is hand-authored demo content, explicitly labelled `[DEMO]`. A fresh submission calls the configured evaluation provider normally; success depends on valid provider configuration and availability.

## Try from FE

1. Configure `GoogleAuth:ClientId` (or `GoogleAuth__ClientId`) to match the OAuth client used by FE. It was empty in the local Development JSON when seeded. Restart API after configuring it.
2. Sign in with the Google email above. FE sends the Google ID token to `POST /api/auth/google/student` and uses the returned access token for subsequent requests.
3. `POST /api/student-submissions/join` with `{"studentCode":"DEMOBF454F8C","email":"lequanghieu2504@gmail.com"}`.
4. `POST /api/student-submissions/lookup` to choose one question from each of the 3 businesses.
5. `POST /api/student-submissions` with the returned `submissionId` and 3 answers. A sample `submitRequest` is in [fe-demo-fixture.json](fe-demo-fixture.json); optionally add the joined `submissionId`.
6. `GET /api/student-submissions/me` to read history. Seeded historical detail: `GET /api/student-submissions/e4019fea-4ccd-532c-bddf-126b47a634a4`.
7. With an Admin JWT, list the student's work via `GET /api/student-submissions?UserId=bf454f8c-61e2-59e6-98a7-aa00472693a1` or filter by `AnalysisRunId` from the fixture JSON.

The current repo allows one completed submission per student. Rerunning the seed preserves submitted work; it does not reset attempts or overwrite accounts.

## Reproduce

```bash
python3 scripts/seed_fe_demo.py --email your-google-email@example.com
```

Requires `psql`, network access, and the local Development connection string (or `ConnectionStrings__DefaultConnection`). The script writes everything in one transaction, uses stable fixture IDs, and refuses to change an existing unrelated account. It does not print credentials.
