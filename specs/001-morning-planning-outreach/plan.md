# Implementation Plan: Morning Planning & Prioritized Outreach (Interactive Demo)

**Branch**: `001-morning-planning-outreach` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-morning-planning-outreach/spec.md`

## Summary

Deliver an interactive Demo 1 ("Morning Pre-Market Planning + Prioritized Outreach") as a working,
cloud-native app modeled on `briandenicola/online-banking-demo` (Azure Container Apps instead of
AKS). A **React** cockpit calls a **C# orchestration API** which runs a **Morning Brief agent** on
**Azure AI Foundry** via the **Microsoft Agent Framework (.NET)**. The agent reaches data only
through a **C# mock system-of-record API** that implements `openapi/tools.yaml` over fictional JSON.
The same scene runs in a deterministic **DEMO mode** (offline, no model) and a **LIVE mode**
(Foundry tool-calling), returning an identical JSON shape so the frontend is mode-blind. Everything
is provisioned with **Terraform** (`azurerm ~> 4` + `azapi ~> 2`) and shipped via **GitHub Actions →
ACR → Container Apps**, with **Entra ID** auth and **Key Vault** secrets.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, `global.json` pinned `10.0.100`); TypeScript 6
**Primary Dependencies**:
- Agents: `Microsoft.Agents.AI`, `Microsoft.Agents.AI.AzureAI`, `Azure.AI.Agents.Persistent`
  (`PersistentAgentsClient`), `Azure.AI.Projects`, `Azure.Identity` (`DefaultAzureCredential`)
- Web/API: `Microsoft.NET.Sdk.Web` minimal APIs/controllers, `Swashbuckle.AspNetCore`,
  `Microsoft.AspNetCore.Authentication.JwtBearer` (Entra ID), typed `HttpClient`
- Observability: `Serilog.AspNetCore`, `OpenTelemetry.*`, `Azure.Monitor.OpenTelemetry.Exporter`
- Frontend: React 19, React Router v7, MUI v9, Axios, TypeScript 6 (Vite build → Nginx)
**Storage**: None required for the demo flow (stateless). Fictional source-of-record data is JSON
files served by the mock API. (Optional Cosmos for plan persistence is out of scope.)
**Testing**: `xunit` + `Moq` + `FluentAssertions` + `Microsoft.AspNetCore.Mvc.Testing`
(WebApplicationFactory) for .NET; Vitest/RTL for React; `task e2e` smoke (curl/Playwright optional).
**Target Platform**: Linux containers on **Azure Container Apps** (Foundry-hosted agents); local via
Docker Compose.
**Project Type**: Web application (React frontend + C# backend services) + agent runtime + IaC.
**Performance Goals**: DEMO-mode morning brief renders < 10s end-to-end (SC-001); deterministic,
byte-identical output across runs (SC-002).
**Constraints**: LIVE/DEMO JSON-shape parity (FR-010); no in-process fixture reads — all data over
HTTP through `openapi/tools.yaml` (FR-002); no hardcoded secrets, non-root containers (FR-014);
capped tool hops in LIVE (FR-011); all data fictional (FR-013).
**Scale/Scope**: Single scene (morning brief), ~6–10 fictional clients, ~9 mock tool endpoints,
1 Foundry agent, 3 container apps (UI, orchestration API, mock API).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The active constitution (`.specify/memory/constitution.md`, v0.1.0) mandates **Python/FastAPI**
(Principle V), a three-layer architecture rooted at `api/`/`frontend/` (Principle II), and `pytest`/
`ruff` quality gates (§17). This feature is **C#/.NET + React** per stakeholder decision, which
**conflicts** with Principle V and parts of II/VII and §17.

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Mission Alignment & Spec-Driven | PASS | One feature, traceable to spec; follows specify→plan. |
| II. Three-Layer Architecture | PASS (adapted) | Experience → Agents → Mock Data preserved; layer paths move to `src/` per amended constitution. |
| III. LIVE/DEMO Parity | PASS | Same JSON shape both modes; DEMO default & offline (FR-008/010). |
| IV. Secrets & Configuration | PASS | Env/Key Vault only; data fictional (FR-013/014). |
| V. Python & FastAPI Standards | **VIOLATION → waived by ADR-0002** | Stack is C#/.NET; amendment per §22 replaces Principle V with .NET standards. |
| VI. API-First / Schema-Driven | PASS | `openapi/tools.yaml` remains the tool contract (FR-012). |
| VII. Testing Discipline | PASS (adapted) | Pyramid kept; `pytest`→`xunit`/Vitest per ADR-0002. |
| VIII. Error Handling & Observability | PASS | Structured JSON errors, Serilog+OTEL, max tool hops. |
| IX. Security Hardening | PASS | Non-root containers, secrets out of code, CORS tightened for deploy. |
| X. Extension Surface | PASS (adapted) | Scene/data-source recipe re-expressed for C# in plan + amended constitution. |
| XI. Commit & Workflow | PASS | Conventional Commits + Copilot trailer. |

**Gate result**: Conditional PASS — proceeds **only with ADR-0002 amending the constitution to a
C#/.NET stack** (per §22). See [Complexity Tracking](#complexity-tracking) and
`docs/adr/0002-csharp-foundry-aca-stack.md`. The ADR MUST land before/with the first implementation
PR; until then the violation of Principle V is explicitly tracked and waived for planning.

## Project Structure

### Documentation (this feature)

```text
specs/001-morning-planning-outreach/
├── plan.md              # This file
├── spec.md              # Feature spec
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── morning-brief.schema.json   # Scene response contract (LIVE/DEMO parity)
│   └── agent-api.yaml              # POST /api/agent/morning-brief contract
└── tasks.md             # Created later by /speckit.tasks (NOT in this command)
```

> The mock system-of-record tool contract remains the repo-root `openapi/tools.yaml` (referenced,
> not duplicated).

### Source Code (repository root)

```text
Directory.Packages.props          # Central NuGet version management (net10.0)
global.json                       # SDK pin 10.0.100
Taskfile.yaml                     # Root orchestrator (includes tasks/*.yml)
tasks/
├── Taskfile.local.yml            # docker compose up/down/logs
├── Taskfile.cloud.yml            # terraform apply, az acr build, ACA deploy
└── Taskfile.build.yml            # az acr build per image
openapi/tools.yaml                # Mock SoR tool contract (exists)

src/
├── shared/
│   └── Observability/            # Serilog + OTEL extensions (UseSerilog, AddOpenTelemetry, CorrelationId)
├── orchestration-api/            # C# — POST /api/agent/morning-brief; LIVE(Foundry)+DEMO modes
│   ├── Program.cs
│   ├── Agents/                   # MorningBriefAgent: Foundry wiring (Microsoft.Agents.AI.AzureAI)
│   │   ├── AgentRunner.cs        # LIVE tool-calling loop (MAX_TOOL_HOPS) + DEMO composer switch
│   │   ├── Tools/                # ToolFunctions calling the mock API over HTTP (typed HttpClient)
│   │   └── Demo/                 # Deterministic composer (offline, no model)
│   ├── Models/                   # MorningBrief, OutreachItem, ClientExposure DTOs (matches schema)
│   ├── Prompts/                  # morning-brief.md agent instructions + output JSON schema
│   └── Dockerfile
├── mock-api/                     # C# — implements openapi/tools.yaml over fictional JSON
│   ├── Program.cs
│   ├── Endpoints/                # tableau, dynamics, trading, calendar, marketdata, news, coalition
│   ├── Data/                     # *.json fictional data (clients, holdings, axes, deals, news, benchmarks)
│   └── Dockerfile
├── agent-provisioner/            # C# console init job: idempotently register Foundry agent (PersistentAgentsClient)
│   └── Dockerfile
└── ui-app/                       # React 19 + MUI v9 cockpit (port of mockup/demos/01-morning-prep.html)
    ├── src/{api,components,scenes,theme}/
    ├── nginx.conf                # serve static build; reverse-proxy /api → orchestration-api
    └── Dockerfile

tests/
├── orchestration-api.Tests/      # xunit: DEMO determinism, schema parity, tool error handling
├── mock-api.Tests/               # xunit: endpoint contract tests vs openapi/tools.yaml
└── ui-app (vitest/RTL)           # render + fetch-wiring tests

infra/                            # Terraform (flat .tf files; azurerm ~>4 + azapi ~>2)
├── providers.tf  variables.tf  locals.tf  random.tf  outputs.tf
├── networking.tf
├── acr.tf  identity.tf  keyvault.tf  keyvault-secrets.tf  monitoring.tf
├── containerapps.tf              # ACA environment + 3 container apps + ingress (replaces aks.tf)
├── ai.tf                         # azapi: AI Services account + model deployment + Foundry project
└── ai-connections.tf             # azapi: project connections + capability host

docs/adr/
├── README.md                     # ADR index
└── 0002-csharp-foundry-aca-stack.md   # Constitution amendment (§22)

.github/workflows/
├── ci.yml                        # build + test (.NET + React) on PR
└── cd.yml                        # az acr build → update ACA revisions on merge to main
```

**Structure Decision**: Web-application layout mirroring `online-banking-demo`: `src/<service>` per
deployable, a `src/shared/Observability` class library, flat-file Terraform under `infra/` (ACA
swapped in for AKS), and `tasks/` Taskfiles. The constitution's three layers map to **Experience** =
`src/ui-app`, **Agents** = `src/orchestration-api` (+ `agent-provisioner`), **Mock Data** =
`src/mock-api`. The data seam (Principle II) is the mock API's HTTP surface defined by
`openapi/tools.yaml` — swapping its `Data/` JSON for real connectors goes live without touching the
agent or UI.

## Complexity Tracking

> Filled because the Constitution Check has a tracked violation.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Principle V (Python/FastAPI) replaced by C#/.NET | Stakeholder mandate to mirror `online-banking-demo` (C#/.NET 10 + Microsoft Agent Framework) and host agents in Foundry. | Staying on Python would not satisfy the explicit "Agent Framework and C#" requirement nor the reference-architecture parity goal. |
| Three deployables (UI, orchestration-api, mock-api) + provisioner | Container Apps + Foundry pattern separates experience, agent orchestration, and the swappable mock data seam. | A monolith would break the "tools call data over HTTP / swappable seam" guarantee (Principle II) and the LIVE/DEMO parity boundary. |
| `azapi` provider in addition to `azurerm` | Azure AI Foundry account/project/capability-host resources are only available via `azapi`. | `azurerm` lacks resource types for the Foundry project + capability host. |

## Phase 0 — Research

See [research.md](./research.md). Resolves: .NET Agent Framework + Foundry wiring, ACA-vs-AKS infra
deltas, LIVE/DEMO parity mechanism, CI/CD (GitHub Actions vs Taskfile), and the React cockpit port.

## Phase 1 — Design & Contracts

Outputs: [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md),
and the agent-context update. All NEEDS CLARIFICATION resolved in Phase 0.

## Phase 2 — (Deferred)

`tasks.md` is generated by `/speckit.tasks`, not this command.
