# Getting Started — WF-Garage

This guide gets the **Morning Planning & Prioritized Outreach** demo running locally in DEMO mode,
then points at LIVE (Azure AI Foundry) mode and an Azure deploy. All data is fictional; **DEMO mode is
the default and needs no Azure credentials**.

See [`README.md`](../README.md) for a condensed quickstart, [`docs/architecture.md`](architecture.md)
for the design, and [`docs/prd.md`](prd.md) for the product context.

## 1. Prerequisites

| Tool | Version | Used for |
|---|---|---|
| .NET SDK | **10** (`net10.0`) | orchestration-api, mock-api, agent-provisioner |
| Node.js | 20+ | React UI (`src/ui-app`) |
| Docker Desktop | current | `task local:up` (docker-compose stack) |
| `go-task` | current | Taskfile workflows (`task ...`) |
| Azure CLI (`az`) | current | LIVE/cloud only |
| Terraform | current | infra only (`infra/`) |

## 2. Configure environment

```powershell
Copy-Item .env.example .env
```

`.env.example` documents every variable. The important ones:

| Variable | Default | Meaning |
|---|---|---|
| `DEMO_MODE` | `1` | `1` = deterministic offline DEMO; `0` = Azure AI Foundry LIVE. |
| `MOCK_API_BASEURL` | `http://localhost:8080` | HTTP seam the orchestration API calls for all tool data. |
| `FOUNDRY_PROJECT_ENDPOINT` | _(blank)_ | Foundry project URL (LIVE only). |
| `FOUNDRY_MODEL` / `_MORNING` / `_SPECIALIST` / `_CHAT` | model-deployment names | Per-role model deployments (LIVE). `_CHAT` falls back to `_MORNING`. |
| `MAX_TOOL_HOPS` | `8` | Caps LIVE tool-calling loops. |
| `EVENT_FANOUT_MAX_CONCURRENCY` | `4` | Per-event specialist fan-out concurrency (LIVE). |

> Never put secret values in `.env.example`. Local LIVE uses `DefaultAzureCredential`; deployed
> environments use Key Vault.

## 3. Run locally — DEMO mode

### Option A — full stack with Docker (recommended)

```powershell
task local:up        # docker compose up -d --build (ui-app, orchestration-api, mock-api)
# open http://localhost:8080
task local:logs      # tail logs
task local:down      # stop and remove
```

The browser only talks to `ui-app`, which reverse-proxies `/api/*` to the orchestration API.

### Option B — run the services directly (no Docker)

Useful for quick backend iteration. Each command is a separate shell.

```powershell
# 1) mock-api on :8080
$env:ASPNETCORE_URLS = 'http://localhost:8080'
dotnet run --project src\mock-api\mock-api.csproj

# 2) orchestration-api on :5100 (DEMO), pointed at the mock-api
$env:ASPNETCORE_URLS  = 'http://localhost:5100'
$env:MOCK_API_BASEURL = 'http://localhost:8080'
$env:DEMO_MODE        = 'true'
dotnet run --project src\orchestration-api\orchestration-api.csproj

# 3) UI dev server (Vite proxies /api to the orchestration API)
npm --prefix src\ui-app install
npm --prefix src\ui-app run dev
```

### Smoke-test the API

```powershell
# RM Daily Briefing
curl.exe -X POST http://localhost:5100/api/agent/rm-briefing -H "Content-Type: application/json" -d '{"payload":{"rmId":"RM-104","date":"2026-05-14"}}'

# AI Chat (grounded assistant)
curl.exe -X POST http://localhost:5100/api/chat -H "Content-Type: application/json" -d '{"messages":[{"role":"user","content":"who should I call today?"}],"rmId":"RM-104"}'
```

(Through the Docker stack use `http://localhost:8080/api/...`.)

## 4. Run the tests & quality gates

```powershell
dotnet build WF-Garage.sln --nologo
dotnet test  WF-Garage.sln --nologo        # mock-api + orchestration-api xUnit
npm --prefix src\ui-app run build
npm --prefix src\ui-app test               # Vitest / React Testing Library
terraform -chdir=infra fmt -check
terraform -chdir=infra validate
gitleaks detect --source . --no-banner
# or all .NET gates at once:
task check
```

## 5. LIVE mode (Azure AI Foundry) locally

Set `DEMO_MODE=0` and provide `FOUNDRY_PROJECT_ENDPOINT` plus the model-deployment names (from
`terraform -chdir=infra output`). LIVE uses `DefaultAzureCredential`, so sign in first:

```powershell
az login
$env:DEMO_MODE = '0'
$env:FOUNDRY_PROJECT_ENDPOINT = (terraform -chdir=infra output -raw foundry_project_endpoint)
```

The persistent agents are registered by `src/agent-provisioner` (the `task cloud:provision` job in a
deployed environment); if they are not yet registered, the runtime self-creates the scene agent so
LIVE still works.

## 6. Deploy to Azure Container Apps

Deployment is a **3-step flow per region** (apps reference ACR images, so the environment + images
must exist first). The Terraform workspace is named after the region; `DEMO_MODE` selects DEMO vs.
FULL/Foundry.

```powershell
# Canada — DEMO (no Foundry)
task up -- canadacentral

# Sweden — FULL (LIVE + Azure AI Foundry)
$env:DEMO_MODE = 'false'; task up -- swedencentral

task cloud:url -- swedencentral     # print the public UI URL
task down -- <region>               # tear down
```

`task up` runs `cloud:apply-infra` → `build:all` → `cloud:apply-apps` → `cloud:provision` (FULL only)
→ `cloud:url`. To roll freshly rebuilt images onto already-running apps without a full apply, use
`task cloud:deploy -- <region>` (forces a new revision per app).

> **Key Vault note**: deployed Key Vaults have public network access **disabled**. Container Apps
> resolve secrets via managed identity over the trusted-services path, but `terraform apply` for KV
> secrets must run from inside the VNet (or with the vault temporarily opened). Routine post-deploy
> changes are applied with `az` against the running apps/jobs.

## 7. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Orchestration API returns empty lists in LIVE | The deterministic safety net should populate the call list; ensure the agent-provisioner job has run so agents carry their tools. |
| HTTP 429 from Foundry | Per-role model deployments + retry-with-backoff smooth bursts; raise per-deployment capacity for sustained load. |
| `az acr build` skips files (OneDrive) | The Taskfile stages a hydrated, non-OneDrive build context automatically (`tasks/stage-build-context.ps1`). |
| UI calls 404 in dev | Ensure the orchestration API is running and the Vite dev proxy points at it; in Docker, hit `:8080`. |
