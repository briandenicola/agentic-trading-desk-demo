# Copilot Instructions

> Repository-level instructions for GitHub Copilot (IDE, CLI, and code review).
> This file is automatically read by Copilot on every interaction.

## Project Overview

**AgenticTradersDesk / Client CV** — an interactive Municipal Sales cockpit. The active feature, `001-morning-planning-outreach`, turns the Demo 1 storyboard into a C#/.NET 10 + React app with Azure AI Foundry agents and Azure Container Apps deployment. All data is fictional.

| Layer | Tech | Path |
|---|---|---|
| Experience | React 19 + TypeScript + MUI v9 | `src\ui-app\` |
| Agents | C#/.NET 10 + Microsoft Agent Framework + Azure AI Foundry | `src\orchestration-api\`, `src\agent-provisioner\` |
| Mock Data | C# Minimal API + JSON fixtures | `src\mock-api\` |
| Shared | Serilog + OpenTelemetry helpers | `src\shared\Observability\` |
| Infra | Terraform + Azure Container Apps/ACR/Key Vault/Foundry | `infra\` |

## AI Development Foundation

This repo ships with two stacked, self-documenting frameworks:

- **Spec Kit** (`.specify\`) — spec-driven delivery. Pipeline:
  `/speckit.constitution` → `/speckit.specify` → `/speckit.clarify` → `/speckit.plan`
  → `/speckit.tasks` → `/speckit.implement` (prompts in `.github\prompts\`,
  agents in `.github\agents\`). The binding rules live in `.specify\memory\constitution.md`.
- **Squad** (`.squad\`) — in-repo AI team artifacts: charters, decisions, routing, and logs.

## Document Hierarchy

All decisions must respect `.specify\memory\constitution.md` §0:
**Constitution → PRD → active spec → plan → tasks → backlog → `.squad\decisions.md` → agent judgment.**

## Session Protocol

### Always
- Read the constitution, `.squad\decisions.md`, the active `specs\NNN-*\spec.md`, and your agent charter before editing code.
- Quote spec section IDs (for example `§17` or Principle II) in commit messages and PR descriptions.
- Run the relevant Quality Gate locally (constitution §17) before declaring a task done.

### Never
- Invent file paths, package names, APIs, or facts — re-read or grep first.
- Retroactively modify locked files (constitution, landed spec, merged ADR) without an amendment per §22.
- Introduce `SESSION-NOTES.md` or `.copilot-state.md`; use `.squad\log\` and `.squad\decisions.md`.
- Hardcode Azure keys, endpoints, or any secrets.

## Build, Test, and Lint

```powershell
# .NET
dotnet restore AgenticTradersDesk.sln
dotnet build AgenticTradersDesk.sln --nologo
dotnet test AgenticTradersDesk.sln --nologo

# React UI
npm --prefix src\ui-app install
npm --prefix src\ui-app run build
npm --prefix src\ui-app test

# Terraform and secrets
terraform -chdir=infra fmt -check
terraform -chdir=infra validate
gitleaks detect --source . --no-banner

# Taskfile shortcuts
task local:up
task check
```

## Architecture

Three-layer architecture (constitution Principle II):

```
src\ui-app\ → POST /api/agent/morning-brief → src\orchestration-api\ → HTTP tools → src\mock-api\
                                      │
                         LIVE: Azure AI Foundry agent
                         DEMO: deterministic C# composer
```

- Data flows left-to-right only. The frontend is mode-blind (LIVE vs DEMO).
- **Tools call data over HTTP** through the mock API surface defined in `openapi\tools.yaml`; orchestration code must not read fixtures in-process.
- Minimal API endpoints stay thin; logic lives under `src\orchestration-api\Agents\`.
- DEMO and LIVE return the same `MorningBrief` JSON shape.

## Code Conventions

- **C# / .NET 10** (`net10.0`), nullable and implicit usings enabled.
- Central package management in `Directory.Packages.props`; do not put package versions on individual `<PackageReference>` items.
- Use Microsoft Agent Framework packages (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.AzureAI`) and `DefaultAzureCredential` for Foundry LIVE mode.
- React UI lives in `src\ui-app\`; tests use Vitest/React Testing Library.
- Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`.
- AI commits include: `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`

## Hard Rules
- **Never hardcode secrets.** Configuration comes from env vars (`.env`, `.env.example`) or Key Vault in deployment.
- **Mock data is fictional.** Do not wire real market-data vendors.
- **LIVE/DEMO parity.** Same JSON shape per scene in both modes.
- **Tools over HTTP.** Never read fixtures in-process from orchestration code.
- **Deployment security.** Tighten CORS for deployed origins and run containers as non-root.

## How to add a new scene
1. Add prompt instructions in `src\orchestration-api\Prompts\<scene>.md`.
2. Register services/tools in the orchestration API DI/agent runner path.
3. Add a deterministic DEMO composer under `src\orchestration-api\Agents\Demo\`.
4. Wire the React fetch/render path in `src\ui-app\`.

## How to add a new data source
1. Add fictional fixture data under `src\mock-api\Data\`.
2. Add a loader/endpoint in `src\mock-api\`.
3. Add a tool wrapper in `src\orchestration-api\Agents\Tools\`.
4. Add the operation to `openapi\tools.yaml`.

## Stack
C#/.NET 10, Microsoft Agent Framework, Azure AI Foundry, React 19, MUI v9, Terraform, and Azure Container Apps.
Tools are OpenAPI-described (`openapi\tools.yaml`) and can be imported into Foundry or exposed through MCP.

## Security Baseline
- Secrets never committed (`gitleaks` in CI/pre-commit workflows).
- `.env.example` documents required variables; production secrets come from Azure Key Vault.
- CORS is open only when `CORS_ALLOWED_ORIGINS` is unset for local development; Terraform sets it for deployed `ui-app`.
- Container deployments run as non-root.
