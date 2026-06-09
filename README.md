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

### Orchestrator vs. agent

- **Orchestrator** (the `AgentRunner`, the 7 tool functions, `MAX_TOOL_HOPS`, JSON→`MorningBrief` mapping) runs **inside the `orchestration-api` Container App**.
- **Agent** (instructions + model `gpt-5.4-mini`) is **persistent in Azure AI Foundry**, on the Agent Service / capability host, reached via `FOUNDRY_PROJECT_ENDPOINT`.
- **Tool execution** happens back in the Container App: the agent only *decides* which tool to call; the C# function runs locally and fetches data from `mock-api` over HTTP.

The morning-brief agent is **registered once** by `src\agent-provisioner\` and **reused by name** on every request (no per-request agent churn), so all runs are consolidated under one agent in the Foundry portal. See [`docs/architecture.md`](docs/architecture.md) for the full flow, persistence model, and traceability roadmap.

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

The persistent `morning-brief` agent (model `gpt-5.4-mini`) is registered by `src\agent-provisioner\` (the `task cloud:provision` job in FULL mode) and reused by the runtime. If the provisioner has not run, `AgentRunner` self-creates the agent so LIVE still works.

## Deploy to Azure Container Apps

Deployment is a **3-step flow per region** (the Container Apps reference ACR images, so the
environment + images must exist before the apps). `task up -- <region>` runs all steps; the
Terraform workspace is named after the region and `DEMO_MODE` selects DEMO vs. FULL/Foundry.

```powershell
# Canada — DEMO mode (default; no Foundry, deterministic)
task up -- canadacentral

# Sweden — FULL mode (LIVE + Azure AI Foundry agent)
$env:DEMO_MODE = 'false'; task up -- swedencentral
```

`task up` runs: `cloud:apply-infra` (environment incl. ACR/Key Vault/Foundry) → `build:all`
(push images to ACR) → `cloud:apply-apps` (Container Apps) → `cloud:provision` (Foundry agent job,
FULL only) → `cloud:url`. Tear down with `task down -- <region>`.

Terraform provisions the Container Apps environment, ACR, Key Vault, managed identity, App Insights, and — in FULL mode — Azure AI Foundry (account, project, gpt-5.4-mini deployment, connection, capability hosts). Only `ui-app` has public ingress; the APIs are internal.

## Observability & traceability

Serilog structured JSON logs, a correlation id (`X-Correlation-ID`) propagated per request, and
OpenTelemetry tracing/metrics (ASP.NET Core + outbound HTTP) are wired in `src\shared\Observability`.
Tool calls surface as HTTP dependency spans, and the persistent agent's runs/tool-steps are visible
in the Foundry portal. Full agent traceability to Application Insights (Azure Monitor exporter,
GenAI spans, per-tool-call spans, token usage) is tracked in
[`specs/_backlog/005-observability.md`](specs/_backlog/005-observability.md).

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
