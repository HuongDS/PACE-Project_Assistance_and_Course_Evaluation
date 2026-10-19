# Deploy backend only to Azure Container Apps

This deploys the .NET API, with Git installed for repository analysis. The frontend is unchanged. The existing PostgreSQL database is reused; the script does not reset data or apply migrations.

## Current deployment (2026-10-20)

- Subscription: Azure for Students
- Resource group: `pace-demo-rg`
- Region: `japaneast` (Japan East; allowed by the Student subscription policy)
- Container App: `pace-api-jp`; environment: `pace-environment-jp`
- Backend: https://pace-api-jp.bluerock-e6f4ebd4.japaneast.azurecontainerapps.io
- API base URL: https://pace-api-jp.bluerock-e6f4ebd4.japaneast.azurecontainerapps.io/api
- Health: https://pace-api-jp.bluerock-e6f4ebd4.japaneast.azurecontainerapps.io/health
- Verified over public HTTPS: health, admin login, and authenticated account lookup returned HTTP 200.

GitHub repository variable `AZURE_CONTAINER_APP=pace-api-jp` selects the deployment target. The workflow falls back to `pace-api` when this variable is absent. Its OIDC identity has the existing narrow deployment role scoped to the Japan app. The registry and PostgreSQL database are reused. The old Hong Kong app remains available during the trial; it is not automatically deleted. Frontend hosting must set `BACKEND_API_URL` to the Japan API URL above and redeploy.

Azure CLI authentication for the initial deployment used a temporary configuration directory; authenticate again if that session has expired before managing these resources.

## Prerequisites

- Docker, Python 3, Azure CLI, and an Azure subscription with permission to create resources.
- `az login` completed and the intended subscription selected with `az account set --subscription <id>`.
- Local `PACE.API/appsettings.Development.json` containing the database connection, JWT signing key, `AdminLogin:PasswordHash`, `Auth:SeedAdminEmails`, Gemini API key and DeepSeek API key. This ignored file is read locally and excluded from the image.

```bash
python3 scripts/deploy-azure-backend.py
```

Defaults: resource group `pace-demo-rg`, region `eastasia`, API name `pace-api`. Override with `--resource-group`, `--location`, `--app-name` or `--config`. The deployment creates a Consumption environment without a Log Analytics workspace, an ACR Basic registry, and one public HTTPS Container App (0.5 vCPU / 1 GiB, min 0 / max 1 replicas). Provider registration and image builds can take several minutes. Check regional quota availability and allowed-region policies in the Student subscription if provisioning fails.

The registry has an ongoing charge independent of application usage. Scaling the app to zero does not remove registry storage costs. Resources remain in the resource group if a deployment fails; inspect them before rerunning or deleting anything.

Configuration secrets are uploaded as Container Apps secrets using a temporary mode-0600 file. Registry credentials use a temporary Docker configuration. Do not run the deployment script with shell tracing or publish its temporary files. The script enables ACR admin credentials for this initial demo deployment; a managed identity with AcrPull is preferable for a longer-lived environment.

## Verify

The script prints the backend and health URLs. `GET /health` should return HTTP 200. This is a process health check, not a database or AI availability check. Swagger is available in Development or when `Swagger:Enabled` is true. The deployment script sets `Swagger__Enabled=true` for this demo API. Open `/swagger` for the interactive API documentation.

Verify `POST /api/auth/student/quick` with an existing student's studentCode and a required email input (lookup uses MSSV only), and `POST /api/auth/login` with the configured admin email and password. These operations verify database connectivity as well. Production uses the explicit environment variables supplied by the deployment, not Development settings. Admin seeding runs at startup; it adds missing configured emails and does not change the role of an existing account.

Frontend integration later: set its server-side `BACKEND_API_URL` to `https://<backend-fqdn>/api`. For direct cross-origin browser requests, set the exact frontend origin in `Cors:AllowedOrigins` before deployment. The current Next.js server proxy does not require browser CORS between the frontend and backend.

## Current limits

- A cold start is expected with min replicas zero. Before a demo, set min replicas to one and return it to zero afterwards.
- Repository analysis and AI evaluation are synchronous today. Requests can exceed the default ingress timeout; large workloads need a job/polling flow. This deployment does not change that workflow.
- Repository clones live under `/tmp/pace_repos` and are temporary. They are not durable application storage.
- Email + MSSV entry remains the explicitly requested temporary authentication flow.

## Automatic deployment from GitHub main

`.github/workflows/backend-azure.yml` runs for pushes to `main`, PRs targeting
`main`, and manual dispatches. PRs only test/build/start the image. A main run tests
the backend and rollback safeguards, builds a SHA-tagged image, signs in to Azure
using OIDC, checks image startup, pushes the image and updates the existing app.
Deployments are serialized; a newer push does not cancel an in-flight rollout.

GitHub repository variables: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and
`AZURE_SUBSCRIPTION_ID`. The user-assigned identity `pace-github-actions` trusts
only this repository's `refs/heads/main`. It has a custom read/write role scoped
to the one Container App and AcrPush scoped to the one registry. It cannot delete
resources or list runtime secrets. AI/database credentials stay in Azure.

Before mutation, the deployment captures the old image and deployment marker.
Test, build, or local startup failure leaves Azure untouched. On update/health
failure, it restores the prior image/marker and checks health, then fails the
workflow so GitHub shows the unsuccessful deployment. `/health` returns an
`X-PACE-Revision` header when configured, so success requires the new commit to
actually serve requests, not just a healthy old instance.

Express is single-revision; rollback may involve a brief interruption. This is
not a zero-downtime blue/green guarantee. If Azure's management API is unavailable
or the runner is killed during rollback, automatic restoration cannot be guaranteed;
the run reports failure and requires operator inspection. Runtime secrets and
schema are never modified by the workflow. No destructive migrations run.
The health check verifies process startup, not every business flow or AI balance.

Monitor: GitHub repository → Actions → Backend CI and Azure deployment. Old image
tags are retained for rollback and may incur registry storage charges. Configure
branch protection to require the backend check if failed PRs must be prevented
from merging; failed main builds still leave the deployed app unchanged.
