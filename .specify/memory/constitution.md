# Client CV — Muni Sales Agentic Demo: Constitution

> **This document is the non-negotiable contract for how this project is built.**
> Every AI agent session must read this file first. Every PR must comply.
> Deviations require an explicit, documented waiver (ADR — see §22).

**Version**: 0.1.0 | **Ratified**: 2026-06-04 | **Last Amended**: 2026-06-04

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
- Layers: `Experience (frontend/) → Agents (api/agents/) → Mock Data (api/routers/ + api/mock_data/)`.
  Dependencies flow left-to-right only.
- The **frontend is mode-blind**: it never knows whether it's talking to LIVE or DEMO mode.
- **Tools call data over HTTP** — never read fixtures in-process. The seam is `api/data.py`;
  replace its bodies with real connectors to go live, leave signatures intact.
- FastAPI routers stay thin; logic lives in `api/agents/`.

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

### V. Python & FastAPI Standards
- **Python 3.11+**, type hints everywhere, Pydantic v2 for models.
- `ruff` for linting and formatting (replaces black, isort, flake8).
- Async where beneficial; sync is fine for tool functions calling httpx.
- Prefer small, typed functions with docstrings.
- Dependencies pinned in `requirements.txt` (or `pyproject.toml` when migrated).
- `uvicorn` for dev server; target runtime is Azure App Service or Container Apps.

**Rationale**: Consistent tooling reduces cognitive load and CI failures.

### VI. API-First & Schema-Driven Contracts
- The agent tool spec lives in `openapi/tools.yaml` — it is the contract for all tools.
- Tools are importable into Azure AI Foundry or wrappable as MCP servers.
- Every new tool must be added to both `api/agents/tools.py` AND `openapi/tools.yaml`.

**Rationale**: A stable, explicit contract enables Foundry/MCP integration without code changes.

### VII. Testing Discipline
- **Test pyramid**: unit (fast, no I/O) → integration (TestClient) → E2E (curl examples).
- Tests run with `pytest`. Use `pytest-asyncio` for async tests.
- Coverage target: 80%+ on `api/agents/` and `api/routers/`.
- Every new scene or tool must have at least one demo-mode integration test.

**Rationale**: A trustworthy test suite is the safety net for autonomous AI changes.

### VIII. Error Handling & Observability
- API errors as structured JSON (not HTML error pages).
- Structured logging via Python `logging`; never log secrets or PII.
- `MAX_TOOL_HOPS` in runner.py caps runaway tool loops.

**Rationale**: Observable systems are debuggable systems.

### IX. Security Hardening
- CORS is open for local demo convenience; tighten for any deployment.
- Secrets never committed (`.env` in `.gitignore`; `gitleaks` runs pre-commit).
- Container deployments run as non-root.

**Rationale**: Even demos get cloned and deployed — secure-by-default prevents incidents.

### X. Extension Surface (Adding Scenes & Data Sources)
- **New scene**: (1) prompt in `api/agents/prompts/<scene>.md`, (2) registry entry,
  (3) demo composer in `demo.py`, (4) frontend fetch + render.
- **New data source**: (1) fixture in `api/mock_data/`, (2) loader in `data.py` + route
  in `routers/mock.py`, (3) tool wrapper + schema in `tools.py`, (4) operation in
  `openapi/tools.yaml`.
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

- [ ] `ruff check .` clean (no lint errors)
- [ ] `ruff format --check .` clean (formatting)
- [ ] `pytest` passes
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
1. Code passes `ruff check` and `ruff format --check`.
2. `pytest` passes for touched modules.
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
