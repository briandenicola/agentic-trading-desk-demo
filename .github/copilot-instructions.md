# Copilot Instructions

> Repository-level instructions for GitHub Copilot (IDE, CLI, and code review).
> This file is automatically read by Copilot on every interaction.

## Project Overview

**Client CV** — a multi-agent demo cockpit for a Municipal Sales desk. Turns a
clickable storyboard into a working app: specialist agents serve data from mock
"system of record" APIs. All data is fictional.

| Layer | Tech | Path |
|---|---|---|
| Experience | Static HTML/JS | `frontend/` |
| Agents | FastAPI + Azure OpenAI | `api/agents/` |
| Mock Data | FastAPI routes + JSON fixtures | `api/routers/mock.py` + `api/mock_data/` |

## AI Development Foundation

This repo ships with two stacked, self-documenting frameworks:

- **Spec Kit** (`.specify/`) — spec-driven delivery. Pipeline:
  `/speckit.constitution` → `/speckit.specify` → `/speckit.clarify` → `/speckit.plan`
  → `/speckit.tasks` → `/speckit.implement` (prompts in `.github/prompts/`,
  agents in `.github/agents/`). The binding rules live in `.specify/memory/constitution.md`.
- **Squad** (`.squad/`) — your in-repo AI team. Run the **Squad** coordinator agent
  (`.github/agents/squad.agent.md`). On first run (no `.squad/team.md`) it proposes and
  hires a team, then persists charters, decisions, skills, and logs under `.squad/`.

## Document Hierarchy

All decisions must respect the Hierarchy of Authority defined in
`.specify/memory/constitution.md` §0. Walk the list top-down:
**Constitution → PRD → active spec → plan → tasks → backlog → `.squad/decisions.md` → agent judgment.**

## Session Protocol

### Always
- Read the constitution, `.squad/decisions.md`, the active `specs/NNN-*/spec.md`, and your
  agent charter **before editing code**.
- Quote spec section IDs (e.g., `§17`, Principle II) in commit messages and PR descriptions.
- Run the Quality Gate locally (constitution §17) before declaring a task done.

### Never
- Invent file paths, package names, APIs, or facts — re-read or grep first.
- Retroactively modify a locked file (constitution, landed spec, merged ADR) without an
  amendment per §22.
- Introduce `SESSION-NOTES.md` or `.copilot-state.md`. The `.squad/log/` + `decisions.md`
  pair is the canonical handoff surface.
- Hardcode Azure keys, endpoints, or any secrets.

## Build, Test, and Lint

```bash
# Run the app (demo mode — no keys needed)
uvicorn api.main:app --reload --port 8000

# Lint
ruff check .

# Format
ruff format .

# Test (full suite)
pytest

# Test (single file / single test)
pytest tests/test_agents.py
pytest tests/test_agents.py::test_exposure_demo -v

# Secret scan
gitleaks detect --source .

# All quality gates
task check
```

## Architecture

Three-layer architecture (constitution **Principle II**):

```
frontend/  →  POST /api/agent/{scene}  →  runner  →  tools  →  mock REST APIs
                                            │
                              LIVE: Azure OpenAI tool-calling
                              DEMO: deterministic composer
```

- Data flows left-to-right only. Frontend is mode-blind (LIVE vs DEMO).
- **Tools call data over HTTP** — never read fixtures in-process. The seam is
  `api/data.py`; replace its bodies with real connectors to go live.
- FastAPI routers stay thin; logic lives in `api/agents/`.
- Both modes return the **same JSON shape** per scene.

## Code Conventions

- **Python 3.11+**: type hints everywhere, Pydantic v2 for models.
- **Formatting/linting**: `ruff` (replaces black + isort + flake8).
- Prefer small, typed functions with docstrings.
- Async where beneficial; sync fine for tool functions.
- Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`.
- AI commits include: `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`

## Hard Rules
- **Never hardcode secrets.** Azure config from env vars (`.env`, see `.env.example`).
- **Mock data is fictional.** Do not wire real market-data vendors.
- **LIVE/DEMO parity.** Same JSON shape per scene in both modes.
- **Tools over HTTP.** Never read fixtures in-process.

## How to add a new scene
1. Write `api/agents/prompts/<scene>.md` (agent instructions + output JSON schema).
2. Register in `api/agents/registry.py` with allowed `tools`.
3. Add deterministic composer in `api/agents/demo.py` (`_COMPOSERS[<scene>]`).
4. Wire `fetch('/api/agent/<scene>', ...)` + render in frontend (copy Scene 3 pattern).

## How to add a new data source
1. Add fixture in `api/mock_data/<name>.json`.
2. Add loader in `api/data.py` and route in `api/routers/mock.py`.
3. Add tool wrapper + JSON schema in `api/agents/tools.py`.
4. Add operation to `openapi/tools.yaml` (for Foundry/MCP import).

## Stack
Python 3.11+, FastAPI, httpx, Pydantic v2, Azure OpenAI (function calling).
Target runtime: Azure AI Foundry Agent Service or Azure Container Apps.
Tools are OpenAPI-described (`openapi/tools.yaml`) — also exposable as MCP servers.

## Security Baseline
- Secrets never committed (`gitleaks` pre-commit + CI).
- CORS open for local dev only; tighten for deployment.
- `.env.example` documents required vars; production secrets via Azure Key Vault.
- Container deployments run as non-root.
