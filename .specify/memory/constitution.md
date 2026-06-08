# Client CV — Muni Sales Agentic Demo: Constitution

> **This document is the non-negotiable contract for how this project is built.**
> Every AI agent session must read this file first. Every PR must comply.
> Deviations require an explicit, documented waiver (ADR — see §22).

**Version**: 0.2.1 | **Ratified**: 2026-06-04 | **Last Amended**: 2026-06-08

> **v0.2.0 amendment (ADR-0002)**: The primary stack is **C#/.NET 10 + Microsoft Agent Framework +
> Azure AI Foundry + Azure Container Apps**, modeled on `briandenicola/online-banking-demo`. Where
> this document still reads "Python/FastAPI/`api/`/`frontend/`/`pytest`/`ruff`", apply the .NET
> equivalents defined in Principle V and §17. See `docs/adr/0002-csharp-foundry-aca-stack.md`.

## §0. Hierarchy of Authority

When two artifacts conflict, the higher-authority document wins. Lower-authority artifacts
MUST be updated to match within the same PR, or the conflict MUST be escalated to amend the
higher artifact (§22). Highest authority first:

1. **This Constitution** — `.specify/memory/constitution.md`
2. **Product Requirements** — `docs/prd.md`
3. **Active Feature Spec** — `specs/NNN-*/spec.md`
4. **Active Implementation Plan** — `specs/NNN-*/plan.md`
5. **Active Task List** — `specs/NNN-*/tasks.md`
6. **Backlog Card** — `specs/_backlog/*.md`
7. **Project Decisions Ledger** — `.squad/decisions.md`
8. **Agent Judgment** (lowest) — MUST be voiced in the PR description or in
   `.squad/decisions/inbox/`; never silently assumed.

## Core Principles

### I. Mission Alignment & Spec-Driven Delivery
- Every change MUST serve a documented goal (PRD goal, spec, backlog item, or explicit
  instruction). No work proceeds without one.
- Follow the Spec Kit pipeline: specify → clarify → plan → tasks → implement.
- **Scope creep is the #1 enemy.** Defer to `Non-Goals` aggressively; one feature per session.

**Rationale**: Traceability from intent to code keeps the project focused and auditable.

### II. Three-Layer Architecture
- Layers: `Experience (src/ui-app/) → Agents (src/orchestration-api/) → Mock Data (src/mock-api/)`.
  Dependencies flow left-to-right only.
- The **frontend is mode-blind**: it never knows whether it's talking to LIVE or DEMO mode.
- **Tools call data over HTTP** — never read fixtures in-process. The seam is the mock API's HTTP
  surface; orchestration tools reach it via a typed `HttpClient`. Replace the `src/mock-api/Data/`
  loaders with real connectors to go live, leave the endpoint contracts intact.
- Minimal-API endpoints stay thin; logic lives in `src/orchestration-api/Agents/`.

**Rationale**: This layering means we can swap mock → real systems without touching agents or frontend.

### III. LIVE/DEMO Mode Parity
- Both modes return the **same JSON shape** per scene. If you add a field in one mode,
  add it in the other.
- DEMO mode must work **offline with no API keys**. It is the default.
- LIVE mode is opt-in via `AZURE_OPENAI_API_KEY` env var; `DEMO_MODE=1` overrides it.

**Rationale**: Stage demos must be deterministic and repeatable. LIVE mode is for development/prod.

### IV. Secrets & Configuration
- **Never hardcode secrets.** Azure config comes from env vars (`.env`, see `.env.example`).
- All clients, holdings, revenue, rankings, and headlines in mock data are **fictional**.
  Do not wire real market-data vendors into this repo.
- Production secrets via Azure Key Vault; `.env.example` documents required vars.

**Rationale**: Secrets in code are the #1 security vulnerability in demos that go live.

### V. .NET & C# Standards *(amended v0.2.0, ADR-0002)*
- **C# / .NET 10** (`net10.0`), `global.json` pinned; `Nullable` and `ImplicitUsings` enabled.
- **Central package management**: `Directory.Packages.props` at the repo root; no `Version` on
  individual `<PackageReference>`.
- Agents use the **Microsoft Agent Framework** (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.AzureAI`)
  with **Azure AI Foundry** via `Azure.AI.Agents.Persistent` / `Azure.AI.Projects` and
  `Azure.Identity.DefaultAzureCredential`.
- `dotnet format` for style; structured logging + tracing via **Serilog + OpenTelemetry** through a
  shared `src/shared/Observability` library; expose `/healthz` and `/readyz`.
- Multi-stage **Alpine** Docker images (`sdk:10.0-alpine` → `aspnet:10.0-alpine`) running **non-root**.
- The React UI uses React 19 + MUI v9 + TypeScript. Python is acceptable only for auxiliary scripts.

**Rationale**: Consistent tooling reduces cognitive load and CI failures, and mirrors the reference
architecture (`briandenicola/online-banking-demo`).

### VI. API-First & Schema-Driven Contracts
- The agent tool spec lives in `openapi/tools.yaml` — it is the contract for all tools.
- Tools are importable into Azure AI Foundry or wrappable as MCP servers.
- Every new tool must be added to both `src/orchestration-api/Agents/Tools/` AND `openapi/tools.yaml`.

**Rationale**: A stable, explicit contract enables Foundry/MCP integration without code changes.

### VII. Testing Discipline
- **Test pyramid**: unit (fast, no I/O) → integration (in-memory test host) → E2E (curl examples).
- Tests run with `xunit` (.NET) and `vitest` + React Testing Library (UI).
- Coverage target: 80%+ on `src/orchestration-api/` and `src/mock-api/`.
- Every new scene or tool must have at least one demo-mode integration test.

**Rationale**: A trustworthy test suite is the safety net for autonomous AI changes.

### VIII. Error Handling & Observability
- API errors as structured JSON (not HTML error pages).
- Structured logging via **Serilog**; never log secrets or PII.
- `MAX_TOOL_HOPS` in `AgentRunner.cs` caps runaway tool loops.

**Rationale**: Observable systems are debuggable systems.

### IX. Security Hardening
- CORS is open for local demo convenience; tighten for any deployment.
- Secrets never committed (`.env` in `.gitignore`; `gitleaks` runs pre-commit).
- Container deployments run as non-root.

**Rationale**: Even demos get cloned and deployed — secure-by-default prevents incidents.

### X. Extension Surface (Adding Scenes & Data Sources)
- **New scene**: (1) prompt in `src/orchestration-api/Prompts/<scene>.md`, (2) registry/DI entry,
  (3) demo composer in `src/orchestration-api/Agents/Demo/`, (4) frontend fetch + render.
- **New data source**: (1) fixture in `src/mock-api/Data/`, (2) loader + endpoint in
  `src/mock-api/Endpoints/`, (3) tool wrapper + schema in `src/orchestration-api/Agents/Tools/`,
  (4) operation in `openapi/tools.yaml`.
- Keep changes small and traceable; one scene or data source per PR.

**Rationale**: A documented extension surface lowers the bar for new contributors.

### XI. Commit & Workflow Convention
- **Conventional Commits**: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`.
- One logical change per commit; reference the task ID / spec section.
- AI-assisted commits MUST include the trailer:
  `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
- **One task = one PR**; keep diffs small. Trunk-based, short-lived feature branches.

**Rationale**: Standardized commits and small PRs enable clean history and fast review.

## §17. Quality Gate

Every PR MUST pass before merge:

- [ ] `dotnet format --verify-no-changes` clean (style) and ESLint/Prettier clean for the UI
- [ ] `dotnet build` clean (no warnings-as-errors violations)
- [ ] `dotnet test` (xunit) passes; UI Vitest/RTL passes
- [ ] `terraform fmt -check` and `terraform validate` clean (for `infra/` changes)
- [ ] `gitleaks` secret scan clean
- [ ] LIVE and DEMO modes return same JSON shape for affected scenes
- [ ] `openapi/tools.yaml` updated if tools changed
- [ ] Conventional Commits + `Co-authored-by: Copilot` trailer when AI-assisted

## §18. AI Agent Operating Rules

### Always
- Read this constitution at session start; check `.squad/decisions.md` for recent decisions.
- Read the active `specs/NNN-*/{spec,plan,tasks}.md` for the current feature.
- Cite this constitution by Principle/section in commit messages and PR descriptions.
- Run the full Quality Gate (§17) before declaring a task done.

### Never
- Invent facts, file paths, package names, APIs, or symbols — re-read or grep first.
- Modify locked files (constitution, landed specs, merged ADRs) without an amendment (§22).
- Hardcode Azure keys, endpoints, or any secrets.
- Introduce `SESSION-NOTES.md` or `.copilot-state.md` — use `.squad/log/` instead.

### Context Discipline
- Load only the files needed for the task; reference large docs by path.
- Record cross-cutting decisions in `.squad/decisions/inbox/`.
- If work diverges from the active spec: **stop**, commit WIP, write a drift note.

## §19. Documentation Requirements
- Material design choices recorded as ADRs in `docs/adr/NNNN-*.md`.
- The canonical documentation surface lives in `docs/`.

## §20. Audit & Continuous Improvement
- Periodically run `/audit` to detect drift between constitution and code.
- Audit reports appended to `docs/audits/YYYY-MM-DD.md`.

## §21. Definition of Done
A task is **done** only when:
1. Code passes `dotnet format --verify-no-changes` (and `eslint` for the React UI).
2. `dotnet test` / `vitest` passes for touched modules.
3. LIVE/DEMO parity confirmed for affected scenes.
4. `openapi/tools.yaml` updated if tools changed.
5. Active `specs/NNN-*/tasks.md` items checked off.
6. Secrets scan clean.
7. Conventional Commit + `Co-authored-by: Copilot` trailer when AI-assisted.

## §22. Amendment Process
1. **Propose** — open an ADR (`docs/adr/NNNN-*.md`, status `PROPOSED`).
2. **PR** — submit the constitution change with the ADR linked.
3. **Semver bump** of the header version.
4. **Revision History** — append a row to §23.

## §23. Revision History

| Version | Date | Change |
|---------|------|--------|
| 0.1.0 | 2026-06-04 | Initial constitution from template starter, adapted for Python/FastAPI. |
| 0.2.0 | 2026-06-08 | Amended to C#/.NET 10 + Microsoft Agent Framework + Azure AI Foundry + Azure Container Apps (Principle V, §17). See ADR-0002. |
| 0.2.1 | 2026-06-08 | Doc-drift cleanup: replaced residual Python/FastAPI paths & tooling in Principles II, VI, VII, VIII, X and §21 with their C#/.NET `src/` equivalents (ADR-0002). |
