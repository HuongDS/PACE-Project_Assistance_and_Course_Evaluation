# Deploy backend only to Azure Container Apps

This deploys the .NET API, with Git installed for repository analysis. The frontend is unchanged. The existing PostgreSQL database is reused; the script does not reset data or apply migrations.

## Current deployment (2026-10-19)

- Subscription: Azure for Students
- Resource group: `pace-demo-rg`
- Region: `eastasia` (allowed by the subscription's region policy)
- Container App: `pace-api`; environment: `pace-environment`
- Backend: https://pace-api.yellowpond-7676bde9.eastasia.azurecontainerapps.io
- API base URL: https://pace-api.yellowpond-7676bde9.eastasia.azurecontainerapps.io/api
- Health: https://pace-api.yellowpond-7676bde9.eastasia.azurecontainerapps.io/health
- Verified over public HTTPS: health, admin login, and authenticated account lookup returned HTTP 200.

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

The script prints the backend and health URLs. `GET /health` should return HTTP 200. This is a process health check, not a database or AI availability check. Swagger remains available only in Development.

Verify `POST /api/auth/student/quick` with an existing student's email and studentCode, and `POST /api/auth/login` with the configured admin email and password. These operations verify database connectivity as well. Production uses the explicit environment variables supplied by the deployment, not Development settings. Admin seeding runs at startup; it adds missing configured emails and does not change the role of an existing account.

Frontend integration later: set its server-side `BACKEND_API_URL` to `https://<backend-fqdn>/api`. For direct cross-origin browser requests, set the exact frontend origin in `Cors:AllowedOrigins` before deployment. The current Next.js server proxy does not require browser CORS between the frontend and backend.

## Current limits

- A cold start is expected with min replicas zero. Before a demo, set min replicas to one and return it to zero afterwards.
- Repository analysis and AI evaluation are synchronous today. Requests can exceed the default ingress timeout; large workloads need a job/polling flow. This deployment does not change that workflow.
- Repository clones live under `/tmp/pace_repos` and are temporary. They are not durable application storage.
- Email + MSSV entry remains the explicitly requested temporary authentication flow.
