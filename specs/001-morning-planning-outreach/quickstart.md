# Quickstart: Morning Planning & Prioritized Outreach

**Feature**: `001-morning-planning-outreach`

This is the developer + presenter quickstart for the interactive Demo 1. It mirrors the
`online-banking-demo` Taskfile workflow (Container Apps instead of AKS).

## Prerequisites
- .NET 10 SDK (`global.json` pins `10.0.100`)
- Node.js 20+ (React 19 UI)
- Docker + Docker Compose (local run)
- `go-task` (Taskfile runner)
- For cloud: Azure CLI, Terraform ≥ 1.x, an Azure subscription with permission to create AI Foundry,
  Container Apps, ACR, and Key Vault

## Layout (after implementation)
```
src/orchestration-api/   POST /api/agent/morning-brief (LIVE Foundry + DEMO composer)
src/mock-api/            implements openapi/tools.yaml over fictional JSON
src/agent-provisioner/   registers the Foundry agent (idempotent)
src/ui-app/              React 19 + MUI v9 cockpit
infra/                   Terraform (ACA + Foundry)
tasks/                   Taskfile includes
```

## Run locally — DEMO mode (no Azure, no keys)
DEMO mode is the default and the on-stage path.
```bash
cp .env.example .env          # DEMO_MODE=1 by default
task local:up                 # docker compose: ui-app + orchestration-api + mock-api
# open http://localhost:8080  (React cockpit)
```
Try the scene endpoint directly:
```bash
curl -X POST localhost:8081/api/agent/morning-brief \
  -H "content-type: application/json" \
  -d '{"payload":{"eventId":"fed_surprise_hike"}}'
```
Expected: a `MorningBrief` JSON (macro narrative + most-affected clients + ranked outreach) that
validates against `contracts/morning-brief.schema.json`, identical on repeated runs.

## Turn on LIVE mode (Azure AI Foundry)
```bash
# fill AZURE_* / FOUNDRY_PROJECT_ENDPOINT / FOUNDRY_MODEL in .env, then:
# unset DEMO_MODE (or set DEMO_MODE=0) and restart
task local:run
```
LIVE runs the Foundry agent tool-calling loop (capped at MAX_TOOL_HOPS) against the mock API and maps
the result into the same `MorningBrief` shape — the React UI is unchanged.

## Deploy to Azure (Container Apps + Foundry)
```bash
task cloud:up        # terraform apply: ACA env, ACR, Key Vault, identity, Foundry (azapi)
task cloud:build     # az acr build for ui-app, orchestration-api, mock-api, agent-provisioner
task cloud:deploy    # roll Container Apps to the new image tags; run agent-provisioner job
task cloud:output-env
```
The public HTTPS endpoint is the `ui-app` Container App ingress (Terraform output).

> Foundry note: capability-host creation waits ~90s for project MSI RBAC propagation (encoded in
> Terraform) to avoid `CapabilityHostOperationFailed`.

## CI/CD
- PRs: `.github/workflows/ci.yml` builds/tests .NET + React, runs `gitleaks`, `terraform validate`.
- Merge to `main`: `.github/workflows/cd.yml` (OIDC → `az acr build` → `az containerapp update`).

## Validate the acceptance criteria
- **SC-001/US1**: run brief from UI → narrative + most-affected + ranked outreach < 10s (DEMO).
- **SC-002**: run the curl twice → byte-identical output.
- **SC-003**: schema-validate LIVE and DEMO responses against `contracts/morning-brief.schema.json`.
- **SC-004/US3**: edit + remove + approve a plan entry in the UI → `sent` stays false.
- **SC-007**: confirm the agent/composer only call the mock API (no in-process fixture reads).

## Quality gate (per amended constitution §17)
```bash
dotnet build && dotnet test          # .NET unit/integration
npm --prefix src/ui-app test         # React
gitleaks detect --source .           # secret scan
terraform -chdir=infra fmt -check && terraform -chdir=infra validate
```
