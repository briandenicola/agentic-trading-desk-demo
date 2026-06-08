# WF-Garage — Morning Planning & Prioritized Outreach

Interactive **Client CV** demo for a Municipal Sales desk. The React cockpit runs a morning brief that combines a market narrative, affected fictional clients, ranked outreach, and an editable human-in-the-loop call plan.

> All demo data is fictional. DEMO mode is deterministic, offline, and the default.

## Stack

- **Agents/API**: C# / .NET 10, Microsoft Agent Framework, Azure AI Foundry in LIVE mode
- **UI**: React 19 + TypeScript + MUI v9
- **Mock systems of record**: C# Minimal API serving fictional fixtures through `openapi/tools.yaml` endpoints
- **Runtime**: Azure Container Apps, ACR, Key Vault, managed identity, Terraform
- **Observability**: Serilog + OpenTelemetry via `src\shared\Observability`

## Architecture

```
src\ui-app\  ── /api/agent/morning-brief ──►  src\orchestration-api\
                                                     │
                         DEMO: deterministic C# composer
                         LIVE: Foundry agent + tool-calling loop
                                                     │ HTTP only
                                                     ▼
                                      src\mock-api\ implements openapi\tools.yaml
```

The frontend is mode-blind: DEMO and LIVE return the same `MorningBrief` JSON shape. The orchestration API reaches data only through the mock API HTTP seam; it never reads fixtures in-process.

## Quickstart — local DEMO mode

Prerequisites: .NET 10 SDK, Node.js 20+, Docker Desktop, and `go-task`.

```powershell
Copy-Item .env.example .env

task local:up
# open http://localhost:8080
```

Try the scene through the UI reverse proxy:

```powershell
curl.exe -X POST http://localhost:8080/api/agent/morning-brief
```

DEMO mode needs no Azure credentials. It defaults to `DEMO_MODE=1` and produces repeatable output for on-stage use.

## LIVE mode

Set `DEMO_MODE=0` and provide `FOUNDRY_PROJECT_ENDPOINT` plus `FOUNDRY_MODEL` (typically from `terraform -chdir=infra output`). LIVE uses `DefaultAzureCredential` with the Foundry project and the same mock API tools, capped by `MAX_TOOL_HOPS`.

## Deploy to Azure Container Apps

```powershell
task cloud:up
task build:all
task cloud:deploy
task cloud:output-env
terraform -chdir=infra output -raw ui_app_url
```

Terraform provisions the Container Apps environment, ACR, Key Vault, managed identity, App Insights, and optional Azure AI Foundry resources. Only `ui-app` has public ingress; the APIs are internal.

## Quality gates

```powershell
dotnet build WF-Garage.sln --nologo
dotnet test WF-Garage.sln --nologo
npm --prefix src\ui-app test
terraform -chdir=infra fmt -check
terraform -chdir=infra validate
gitleaks detect --source . --no-banner
```

## Layout

```
src\ui-app\              React cockpit and nginx reverse proxy
src\orchestration-api\   POST /api/agent/morning-brief, DEMO/LIVE runner
src\mock-api\            fictional system-of-record endpoints
src\agent-provisioner\   idempotent Foundry agent registration job
src\shared\Observability\ Serilog, OTEL, correlation id, JSON errors
infra\                   Terraform for ACA, ACR, Key Vault, Foundry
tasks\                   Taskfile includes for local, build, and cloud workflows
contracts\               morning-brief and agent API schemas
openapi\tools.yaml       tool contract for Foundry/MCP import
```
