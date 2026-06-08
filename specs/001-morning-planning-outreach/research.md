# Phase 0 Research: Morning Planning & Prioritized Outreach

**Feature**: `001-morning-planning-outreach` · **Date**: 2026-06-08

Resolves the Technical Context unknowns and locks the framework/design choices, grounded in the
reference repo `briandenicola/online-banking-demo` and the Microsoft Agent Framework (.NET) docs.

---

## R1. Agent runtime: Microsoft Agent Framework (.NET) on Azure AI Foundry

**Decision**: Build the Morning Brief agent with **Microsoft Agent Framework for .NET**
(`Microsoft.Agents.AI` + `Microsoft.Agents.AI.AzureAI`), backed by an Azure AI Foundry project via
**`Azure.AI.Agents.Persistent`** (`PersistentAgentsClient`) and **`Azure.AI.Projects`**, authenticated
with **`Azure.Identity.DefaultAzureCredential`**. The Foundry project endpoint follows the reference
pattern `https://{account}.services.ai.azure.com/api/projects/{project}` (env `FOUNDRY_PROJECT_ENDPOINT`).
A separate **`agent-provisioner`** console job idempotently registers the agent
version at deploy time (mirrors the reference repo's Python `init_agents.py`, but in C#).

**Rationale**:
- The user explicitly requires "Microsoft Agent Framework and C#" with agents hosted in Foundry. MAF
  is the GA successor to Semantic Kernel/AutoGen with first-class Azure AI/Foundry providers,
  tool-calling, and OpenTelemetry.
- The reference repo proves the Foundry hosting + provisioning pattern, but only in Python
  (`agent-framework-core`/`agent-framework-foundry`). The .NET equivalent is `Microsoft.Agents.AI*`
  + `Azure.AI.Agents.Persistent`; the credential, endpoint, and provisioning patterns map 1:1.

**Alternatives considered**:
- *Direct Azure OpenAI SDK* — rejected: not "Agent Framework", no managed/versioned agent.
- *Semantic Kernel directly* — rejected: MAF supersedes it and is the named requirement.
- *Python Agent Framework (as in the reference)* — rejected: requirement is C#.

---

## R2. LIVE / DEMO mode parity

**Decision**: One scene endpoint `POST /api/agent/morning-brief` returns a fixed DTO
(`MorningBrief`). A `MODE` switch (`DEMO_MODE=1` default) selects between:
- **DEMO**: a deterministic C# composer that calls the mock API for data and assembles the brief with
  fixed ordering/text — no model call, fully offline.
- **LIVE**: `AgentRunner` runs the Foundry agent with tool-calling against the mock API, capped at
  `MAX_TOOL_HOPS`, then maps the model output into the same `MorningBrief` DTO.
Both paths serialize the **same JSON shape** (validated against `contracts/morning-brief.schema.json`).

**Rationale**: Constitution Principle III; on-stage reliability (live LLM calls are the #1 failure
point). Schema-validated parity lets a single React renderer stay mode-blind.

**Alternatives considered**: Separate endpoints per mode (rejected — duplicates the frontend and
risks shape drift); LIVE-only (rejected — fragile for demos, needs Azure to run locally).

---

## R3. Data seam: C# mock API implementing `openapi/tools.yaml`

**Decision**: A standalone **`mock-api`** service implements every operation in the existing
`openapi/tools.yaml` (tableau clients, dynamics engagement, trading axes/holdings, calendar
newissues, marketdata + relval, news, coalition) over **fictional JSON files** in `mock-api/Data/`.
Agent tools and the DEMO composer reach data **only via HTTP** to this service (typed `HttpClient`,
base URL from env). The fictional data is authored in this feature (none exists yet) and seeded from
the Demo 1 mockup content (Atlas Pension, Brookline Bank, Cedar Asset Mgmt; Fed-hike scenario).

**Rationale**: Constitution Principle II / VI and FR-002/FR-012. Keeping the data behind the
`openapi/tools.yaml` HTTP surface means swapping JSON for real Tableau/Dynamics/trading connectors is
a data-layer change only — agents and UI are untouched. The OpenAPI spec is also importable into
Foundry as an OpenAPI tool.

**Alternatives considered**: In-process fixtures in the agent (rejected — violates Principle II /
FR-002); reading JSON directly in the composer (rejected — same reason).

---

## R4. Container Apps instead of AKS (infra deltas)

**Decision**: Provision with **Terraform**, flat `.tf` files, providers `azurerm ~> 4` + `azapi ~> 2`
+ `random ~> 3` (reference convention). Replace the reference repo's `aks.tf`/Istio/Kustomize with:
- `azurerm_container_app_environment` (+ Log Analytics workspace)
- one `azurerm_container_app` each for `ui-app`, `orchestration-api`, `mock-api`, plus the
  `agent-provisioner` as a job/init container
- external ingress on `ui-app` (public HTTPS), internal ingress for `orchestration-api`/`mock-api`
- a single **user-assigned managed identity** with `AcrPull`, **Key Vault Secrets User**, and the
  Foundry roles (`Cognitive Services OpenAI User`, `Azure AI Project Manager`); ACA references Key
  Vault secrets directly via the UAI (no CSI driver)
**Foundry** (`ai.tf`, `ai-connections.tf`) stays on `azapi` exactly as the reference: AI Services
account (`Microsoft.CognitiveServices/accounts`, kind `AIServices`) → model deployment → Foundry
project → connections → **90s RBAC propagation wait** → capability host.

**Rationale**: ACA gives managed HTTPS ingress, scale-to-zero, and KV-secret references without the
operational weight of AKS/Istio — appropriate for a demo. Foundry resource types only exist in
`azapi`. The reference report explicitly enumerated the AKS→ACA swap points.

**Alternatives considered**: AKS (rejected — user chose ACA; heavier); App Service (rejected — weaker
managed-identity + multi-container story than ACA for this layout); Bicep (rejected — reference and
user both use Terraform).

**Open risk**: Foundry capability-host creation depends on project MSI RBAC propagation; encode the
documented ~90s wait / dependency ordering in Terraform to avoid `CapabilityHostOperationFailed`.

---

## R5. CI/CD: GitHub Actions → ACR → Container Apps

**Decision**: Add two workflows (the reference repo has **no** build/deploy workflows — it uses
Taskfile + `az acr build`):
- `ci.yml` (PR): `dotnet build`/`dotnet test` for .NET, `npm ci && npm test && npm run build` for
  React, plus `gitleaks` and `terraform fmt -check`/`validate`.
- `cd.yml` (merge to `main`): OIDC login to Azure → `az acr build` per image → `az containerapp update`
  to roll each app to the new image tag.
Keep `Taskfile.yaml` for local/dev parity (`task local:up`, `task cloud:up`, `task cloud:build`).

**Rationale**: User explicitly asked for GitHub Actions → ACR → Container Apps. `az acr build`
removes the need for a local Docker daemon in CI and matches the reference build approach. OIDC
federated credentials avoid storing cloud secrets in GitHub.

**Alternatives considered**: Taskfile-only like the reference (rejected — user wants GH Actions);
docker build+push in Actions (acceptable, but `az acr build` is simpler and matches reference).

---

## R6. Frontend: React 19 + MUI v9 cockpit port

**Decision**: Build `src/ui-app` as **React 19 + React Router v7 + MUI v9 + TypeScript 6 + Axios**
(reference stack), Vite build served by **Nginx** that reverse-proxies `/api/*` to the orchestration
API. Port the visual structure of `mockup/demos/01-morning-prep.html` (market strip, agent-reasoning
steps, macro narrative, most-affected clients, ranked outreach, editable human-in-the-loop plan) into
MUI components under `src/scenes/MorningBrief/`. Axios client uses `baseURL='/api'`.

**Rationale**: Mirrors the reference UI conventions and the user's "online-banking-demo UI" choice;
the mockup is the pixel/UX reference. Nginx `/api` proxy keeps the frontend mode-blind and origin-clean.

**Alternatives considered**: Reuse the static HTML mockup (rejected — user chose a real React app);
Blazor (rejected — user chose React).

---

## R7. .NET project conventions

**Decision**: Adopt the reference conventions: **`net10.0`**, `global.json` pinned `10.0.100`,
**central package management** (`Directory.Packages.props`, no per-project versions), a shared
`src/shared/Observability` library exposing `UseSerilog(...)` + `AddOpenTelemetry(...)` +
`UseCorrelationId()` + `UseGlobalExceptionHandler()`, multi-stage **Alpine** Dockerfiles
(`sdk:10.0-alpine` → `aspnet:10.0-alpine`) running as **non-root** (`USER $APP_UID`), `/healthz` +
`/readyz` probes, and structured JSON errors.

**Rationale**: Direct parity with the reference repo's coding/design (a stated goal) and constitution
Principles VIII/IX.

**Alternatives considered**: `net9.0` (rejected — reference targets `net10.0`); per-project package
versions (rejected — reference uses central management).

---

## R8. Auth model for the demo

**Decision**: **Entra ID** for service-to-Azure access (managed identity → Foundry, ACR, Key Vault).
For the demo's user-facing surface, keep it open/anonymous on the `ui-app` public ingress initially,
with JWT-bearer scaffolding present in the orchestration API (as the reference does) so Entra user
auth can be switched on later. No real CRM/dialer integration; human-in-the-loop actions are demo-only.

**Rationale**: Matches "Entra ID everywhere — no access keys" for cloud resources while keeping the
demo frictionless to present. Avoids scope creep (full user auth is a separate backlog item).

**Alternatives considered**: Full Entra user sign-in for v1 (deferred — not required to demonstrate
the flow, adds friction on stage).

---

## Resolved unknowns summary

| Unknown | Resolution |
|---------|-----------|
| .NET Agent Framework packages | `Microsoft.Agents.AI`, `Microsoft.Agents.AI.AzureAI`, `Azure.AI.Agents.Persistent`, `Azure.AI.Projects`, `Azure.Identity` |
| Foundry wiring | `PersistentAgentsClient` + `DefaultAzureCredential`; endpoint `https://{acct}.services.ai.azure.com/api/projects/{project}`; provisioned via `azapi` + a C# provisioner job |
| LIVE/DEMO parity | Single endpoint + `MorningBrief` DTO + JSON-schema validation; `DEMO_MODE=1` default |
| Data seam | `mock-api` implementing `openapi/tools.yaml` over fictional JSON; HTTP-only access |
| Infra (ACA vs AKS) | `azurerm_container_app_environment` + 3 apps + provisioner job; UAI + KV refs; Foundry on `azapi` |
| CI/CD | GitHub Actions `ci.yml` + `cd.yml` (OIDC → `az acr build` → `az containerapp update`) |
| Frontend | React 19 + MUI v9 + Vite + Nginx `/api` proxy; port of Demo 1 mockup |
| .NET conventions | `net10.0`, central packages, shared Observability lib, Alpine non-root images |
| Target model | Foundry model deployment (e.g., `gpt-5.4-mini`, GlobalStandard) per reference; configurable via env |
