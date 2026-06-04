# Tasks: Integrated AI Sales Assistant PoC (Demos 1–3)

**Input**: Static HTML mockups (`src/demos/01-03`), existing `openapi/tools.yaml`, `.env.example`, `requirements.txt`
**Scope**: Demos 1 (Morning Prep), 2 (Client Update Broadcast), 3 (Inbound Query Response) only
**Architecture**: React+Vite frontend (Azure Static Web Apps) → Azure Functions (Mock APIs) + Azure AI Foundry Agent Service → Cosmos DB/Redis for state
**Auth**: Entra ID everywhere — no access keys
**IaC**: Terraform for all Azure infrastructure
**Mode**: DEMO (deterministic composers, no Azure keys required) with LIVE toggle

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1=Morning Prep, US2=Client Update, US3=Inbound Query)
- Include exact file paths in descriptions

## Path Conventions

```
infra/                  Terraform IaC for Azure
  main.tf              Provider config + resource group
  variables.tf         Input variables (env, location, project name)
  outputs.tf           Exported endpoints and IDs
  modules/
    static-web-app/    Azure Static Web Apps (React frontend)
    functions/         Azure Functions (Mock APIs + Agent endpoint)
    cosmos/            Cosmos DB (state, conversation history)
    redis/             Azure Cache for Redis (session/cache)
    foundry/           Azure AI Foundry project + model deployments
    identity/          Entra ID app registrations + managed identities
frontend/               React + Vite app (migrated from src/ static HTML)
  src/
    components/         Shared UI components
    scenes/             Per-demo scene components
    hooks/              Custom hooks (agent calls, state)
    types/              TypeScript types
api/
  main.py              FastAPI app entry (deployed as Azure Function)
  config.py            Env config + mode switching
  models/              Pydantic response models
  routers/
    mock.py            Mock data REST APIs (cross-asset)
    agents.py          POST /api/agent/{scene}
  agents/
    registry.py        Scene → prompt + tools mapping
    tools.py           Tool functions calling mock APIs
    runner.py          LIVE (Foundry Agent Service) + DEMO mode dispatch
    demo.py            Deterministic composers per scene
    prompts/           Agent instruction markdown files
  mock_data/           JSON fixtures (cross-asset: rates, credit, equities)
openapi/tools.yaml     Cross-asset tool spec (Foundry/MCP import)
```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, tooling, and migration from static HTML to a proper dev structure

- [ ] T001 Create project directory structure per path conventions above (`frontend/`, `api/`, `api/agents/`, `api/mock_data/`, `api/routers/`, `api/models/`, `infra/`, `infra/modules/`)
- [ ] T002 Initialize React + Vite + TypeScript frontend in `frontend/` with `package.json`, `vite.config.ts`, `tsconfig.json`
- [ ] T003 [P] Configure Python backend with updated `requirements.txt` (add azure-ai-projects, azure-identity, azure-cosmos, pydantic-settings) and `pyproject.toml` (ruff config)
- [ ] T004 [P] Create `.env.example` with updated vars: `AZURE_AI_FOUNDRY_ENDPOINT`, `AZURE_AI_FOUNDRY_PROJECT`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `DEMO_MODE=1`, `MOCK_API_BASE`, `COSMOS_ENDPOINT`, `REDIS_HOST`
- [ ] T005 [P] Update `Taskfile.yaml` to add `dev` (run both frontend+backend), `dev:api`, `dev:ui`, `infra:plan`, `infra:apply` tasks
- [ ] T006 [P] Create `api/config.py` with pydantic-settings `Settings` class (DEMO_MODE, FOUNDRY config, MOCK_API_BASE, COSMOS_ENDPOINT, REDIS_HOST — all using Entra ID/managed identity, no keys)

---

## Phase 1.5: Terraform Infrastructure

**Purpose**: Provision all Azure resources using Terraform. Entra ID for auth everywhere — no access keys.

### Core Infrastructure

- [ ] T050 Create `infra/main.tf` with AzureRM provider config, resource group, and backend state config (Azure Storage)
- [ ] T051 [P] Create `infra/variables.tf` with inputs: `project_name`, `environment` (dev/staging/prod), `location`, `tags`
- [ ] T052 [P] Create `infra/outputs.tf` exporting: Static Web App URL, Function App URL, Cosmos endpoint, Redis host, Foundry endpoint

### Identity & Auth (Entra ID)

- [ ] T053 Create `infra/modules/identity/main.tf` — Entra ID app registration for the SPA (React frontend), system-assigned managed identity for the Function App, role assignments granting Function App identity access to Cosmos DB (Data Contributor), Redis (Data Owner), and AI Foundry (Cognitive Services User)
- [ ] T054 [P] Create `infra/modules/identity/outputs.tf` exporting client IDs, tenant ID, managed identity principal ID

### Compute & Hosting

- [ ] T055 Create `infra/modules/static-web-app/main.tf` — Azure Static Web App for React frontend (Standard tier, linked to repo for CI/CD)
- [ ] T056 [P] Create `infra/modules/functions/main.tf` — Azure Functions (Python 3.11, Consumption plan, system-assigned identity enabled, app settings wired to Cosmos/Redis/Foundry endpoints — NO connection strings with keys)

### Data Stores

- [ ] T057 Create `infra/modules/cosmos/main.tf` — Cosmos DB account (serverless, NoSQL API) with database and containers: `conversations` (partition: /session_id), `agent_state` (partition: /scene). Disable key-based auth, enable Entra-only access.
- [ ] T058 [P] Create `infra/modules/redis/main.tf` — Azure Cache for Redis (Basic C0 for dev, Entra ID auth enabled, disable access keys). Used for session caching and rate limiting.

### AI Foundry

- [ ] T059 Create `infra/modules/foundry/main.tf` — Azure AI Foundry hub + project, model deployment (GPT-4.1), configure agent with tools from `openapi/tools.yaml`. Grant Function App managed identity "Cognitive Services User" role on the Foundry resource.

### Wiring

- [ ] T060 Create `infra/environments/dev.tfvars` with dev-specific values (small SKUs, short names)
- [ ] T061 [P] Add `.github/workflows/infra.yml` — Terraform plan on PR, apply on merge to main (uses OIDC federated credentials for GitHub Actions → Azure, no stored secrets)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-asset mock data layer, shared Pydantic models, agent framework, and React shell — MUST complete before any demo scene

**⚠️ CRITICAL**: No demo scene work can begin until this phase is complete

### Mock Data Layer (Cross-Asset Realignment)

- [ ] T007 Create cross-asset market data fixture in `api/mock_data/market_snapshot.json` (10y UST, 2y UST, S&P futures, IG credit spreads, VIX, MOVE index, mortgage 30y, swap 10y)
- [ ] T008 [P] Create cross-asset client portfolio fixtures in `api/mock_data/clients.json` (Atlas Pension: long-duration bonds; Brookline Bank: swap book; Cedar Asset Mgmt: floating-rate; Crestline Credit Fund: HY/distressed)
- [ ] T009 [P] Create client engagement fixtures in `api/mock_data/engagement.json` (coverage history, interaction logs, 30/60/90d touchpoints per client)
- [ ] T010 [P] Create trading axes fixture in `api/mock_data/axes.json` (cross-asset: 10Y swap axes, credit bond axes including ACME Corp 2030 bid $5MM)
- [ ] T011 [P] Create macro events fixture in `api/mock_data/macro_events.json` (Fed surprise hike scenario with market reactions across rates, equities, credit)
- [ ] T012 [P] Create credit research fixture in `api/mock_data/credit_research.json` (ACME Corp: earnings miss, Moody's review, CDS levels, bond prices)
- [ ] T013 [P] Create client holdings fixture in `api/mock_data/holdings.json` (per-client positions across rates, credit, equities with notional amounts)

### Mock API Routes

- [ ] T014 Implement `api/routers/mock.py` with cross-asset endpoints: `GET /mock/market/snapshot`, `GET /mock/market/macro-events`, `GET /mock/clients`, `GET /mock/clients/{cid}`, `GET /mock/clients/{cid}/holdings`, `GET /mock/clients/{cid}/engagement`, `GET /mock/trading/axes`, `GET /mock/credit/{issuer}`
- [ ] T015 Rewrite `openapi/tools.yaml` replacing muni-focused endpoints with cross-asset operations matching the routes in T014

### Pydantic Response Models

- [ ] T016 [P] Create shared Pydantic models in `api/models/market.py` (MarketSnapshot, MacroEvent, MarketReaction)
- [ ] T017 [P] Create shared Pydantic models in `api/models/client.py` (Client, ClientHolding, ClientEngagement, OutreachItem)
- [ ] T018 [P] Create shared Pydantic models in `api/models/trading.py` (Axe, CreditResearch, Quote)
- [ ] T019 [P] Create agent response models in `api/models/scenes.py` (MorningBriefResponse, ClientUpdateResponse, InboundQueryResponse — each with reasoning steps, data sections, draft content, and action items)

### Agent Framework

- [ ] T020 Create `api/agents/tools.py` with tool functions that call mock API endpoints via httpx (get_market_snapshot, get_macro_events, get_client_portfolio, get_client_engagement, get_trading_axes, get_credit_research)
- [ ] T021 Create `api/agents/registry.py` mapping scenes to allowed tools and prompts: `morning_prep` → [market, clients, axes], `client_update` → [market, macro], `inbound_query` → [client, credit, axes]
- [ ] T022 Create `api/agents/runner.py` with mode dispatch: if DEMO_MODE call deterministic composer, else call Azure AI Agent Framework SDK (`azure.ai.agent`) with Entra ID DefaultAzureCredential — no API keys
- [ ] T023 Create `api/routers/agents.py` with `POST /api/agent/{scene}` endpoint accepting scene-specific payloads, returning scene-specific response models

### FastAPI App

- [ ] T024 Create `api/main.py` mounting mock router, agents router, and static frontend serving (production) with CORS middleware for dev

### React Frontend Shell

- [ ] T025 Create shared React components in `frontend/src/components/`: TopBar, MarketStrip, AgentReasoningCard, EditableDraft, ActionButtons, SourceTags
- [ ] T026 [P] Create TypeScript types in `frontend/src/types/api.ts` mirroring backend Pydantic models (MorningBriefResponse, ClientUpdateResponse, InboundQueryResponse)
- [ ] T027 [P] Create `frontend/src/hooks/useAgent.ts` custom hook: POST to `/api/agent/{scene}`, manage loading/error/data states
- [ ] T028 Create `frontend/src/App.tsx` with router (react-router-dom): `/` → DemoIndex, `/demo/1` → MorningPrep, `/demo/2` → ClientUpdate, `/demo/3` → InboundQuery
- [ ] T029 [P] Create `frontend/src/scenes/DemoIndex.tsx` — landing page (migrate design from `src/index.html`)
- [ ] T030 [P] Migrate `src/assets/theme.css` to `frontend/src/styles/theme.css` (adapt for component-based usage)

**Checkpoint**: Foundation ready — mock APIs serve cross-asset data, agent framework dispatches, React shell renders. Demo scenes can now be built independently.

---

## Phase 3: User Story 1 — Morning Pre-Market Planning (Priority: P1) 🎯 MVP

**Goal**: Sales VP asks "What do I need to know this morning?" and receives a macro narrative, impacted-client flags, ranked outreach plan — all editable before acting.

**Independent Test**: Run `POST /api/agent/morning_prep` → verify JSON contains macro_narrative, impacted_clients (3), outreach_plan (3 ranked items). Load `/demo/1` in browser → see market strip, reasoning steps, two-column layout, editable plan with Approve/Edit buttons.

### Agent & Composer

- [ ] T031 [US1] Create agent prompt in `api/agents/prompts/morning_prep.md` (instructions: pull macro events, score client portfolios for rate sensitivity, rank outreach by wallet+engagement+event impact, generate talking points)
- [ ] T032 [US1] Implement deterministic composer `_compose_morning_prep()` in `api/agents/demo.py` — assembles macro_events + client scores + outreach ranking from fixtures into MorningBriefResponse shape

### Frontend Scene

- [ ] T033 [US1] Create `frontend/src/scenes/MorningPrep.tsx` — full scene component with: market strip (10y, 2y, S&P, IG, VIX), prompt bar with "Run morning brief" button, agent reasoning card, macro narrative card, two-column grid (impacted clients table + outreach priority table), editable outreach plan with Approve/Edit/Remove actions
- [ ] T034 [US1] Wire MorningPrep scene to `useAgent('morning_prep')` hook, render loading → reasoning → results states

**Checkpoint**: Demo 1 fully functional — "What do I need to know this morning?" works end-to-end in DEMO mode.

---

## Phase 4: User Story 2 — Client Update Broadcast (Priority: P2)

**Goal**: Desk head says "Draft a client update on this morning's rate volatility" and gets a pre-filled, editable email/broadcast ready to approve and send to 142 clients.

**Independent Test**: Run `POST /api/agent/client_update` → verify JSON contains draft (subject, body, audience, channel), grounding_sources, tone_options. Load `/demo/2` → see editable draft, audience/tone toggles, Approve & Send button.

### Agent & Composer

- [ ] T035 [US2] Create agent prompt in `api/agents/prompts/client_update.md` (instructions: reuse morning macro analysis, pull mortgage+swap knock-on levels, draft broadcast with "what happened, why it matters, how we help" structure, include subject line)
- [ ] T036 [US2] Implement deterministic composer `_compose_client_update()` in `api/agents/demo.py` — assembles macro narrative + rates data into ClientUpdateResponse shape with pre-written draft matching Demo 2 HTML

### Frontend Scene

- [ ] T037 [US2] Create `frontend/src/scenes/ClientUpdate.tsx` — full scene component with: market strip (10y, mortgage 30y, swap 10y, MOVE, distribution count), prompt bar with "Draft update" button, two-column layout (left: editable broadcast draft with From/To/Subject/Body + Approve & Send/Edit/Save as draft buttons; right: grounding card showing sources + tone/audience toggle buttons)
- [ ] T038 [US2] Wire ClientUpdate scene to `useAgent('client_update')` hook, implement tone toggle (reassuring/concise/technical) and audience filter (all rates/insurers only) as local state that re-renders the draft

**Checkpoint**: Demo 2 fully functional — "Draft a client update" works end-to-end. Editable, tone-switchable, approve-gated.

---

## Phase 5: User Story 3 — Inbound Query Response (Priority: P3)

**Goal**: A client pings on Bloomberg asking for a price and context. Sales user hits hotkey, assistant compiles client briefing + market context + trader axe and drafts a reply with quote — verify price with trader, then send.

**Independent Test**: Run `POST /api/agent/inbound_query` with `{"client":"crestline","query":"Price on ACME Corp 2030?"}` → verify JSON contains client_briefing (holdings, recent trades, past interest, trader axe), market_context (price, CDS, news), suggested_reply (with quote). Load `/demo/3` → see incoming chat bubble, context panel, drafted reply with Verify/Send/Edit buttons.

### Agent & Composer

- [ ] T039 [US3] Create agent prompt in `api/agents/prompts/inbound_query.md` (instructions: identify client from message, pull their holdings + engagement, get credit research on the security, check desk axes, draft reply combining quote + context + CTA)
- [ ] T040 [US3] Implement deterministic composer `_compose_inbound_query()` in `api/agents/demo.py` — assembles client briefing (Crestline: $8MM ACME 2030, bought 2028 last month, asked for color twice) + market context (ACME down 2pts, weak earnings, Moody's review) + suggested reply with "bid 78 for $5MM" into InboundQueryResponse shape

### Frontend Scene

- [ ] T041 [US3] Create `frontend/src/scenes/InboundQuery.tsx` — full scene component with: market strip (ACME 2030 price, ACME CDS 5y, HY index, desk axe), two-column top (left: Bloomberg chat with incoming bubble + hotkey button; right: client briefing card + market context card), bottom: suggested reply draft with Verify price/Send reply/Edit buttons + "time to respond" banner
- [ ] T042 [US3] Wire InboundQuery scene to `useAgent('inbound_query')` hook, implement two-step approval flow (verify price → then send becomes enabled)

**Checkpoint**: Demo 3 fully functional — inbound query → instant contextual response with two-step human approval.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Integration, developer experience, documentation, and deployment readiness

- [ ] T043 [P] Create `api/agents/foundry.py` implementing Azure AI Agent Framework SDK client (`azure.ai.agent`) — create agent with tools from openapi/tools.yaml, multi-turn conversation, tool orchestration. Auth via `DefaultAzureCredential` (Entra ID managed identity in prod, az login locally) — no API keys anywhere.
- [ ] T044 [P] Add Vite proxy config in `frontend/vite.config.ts` to forward `/api/*` and `/mock/*` to FastAPI backend during development
- [ ] T045 [P] Update `README.md` with new architecture diagram, quickstart (npm + uvicorn), env var docs, and demo walkthrough
- [ ] T046 [P] Add `frontend/Dockerfile` and `api/Dockerfile` for containerized deployment
- [ ] T047 Validate LIVE/DEMO parity: both modes return identical JSON shapes per scene (same TypeScript types work for both)
- [ ] T048 [P] Add pre-commit hook config for frontend (eslint + prettier) alongside existing ruff for Python
- [ ] T049 End-to-end smoke test: start both servers, navigate all 3 demos, confirm data renders correctly and all human-in-the-loop actions work (approve, edit, send)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Terraform (Phase 1.5)**: Can start in parallel with Phase 1 — independent IaC work
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories (does NOT depend on Terraform; local dev uses mock mode)
- **User Stories (Phases 3–5)**: All depend on Foundational phase completion
  - US1, US2, US3 can proceed in parallel (different scene files, different composers)
- **Polish (Phase 6)**: Depends on all three user stories being complete
- **Deployment**: Depends on Terraform (Phase 1.5) + Polish (Phase 6)

### User Story Dependencies

- **US1 (Morning Prep)**: Can start after Foundational — No dependencies on other stories
- **US2 (Client Update)**: Can start after Foundational — Narratively reuses US1's macro analysis but technically independent (composer reads same fixtures)
- **US3 (Inbound Query)**: Can start after Foundational — Fully independent of US1/US2

### Within Each User Story

- Agent prompt before composer (prompt defines the output contract)
- Composer before frontend scene (scene needs API shape to render)
- Frontend wiring after scene component exists

### Parallel Opportunities

- T003, T004, T005, T006 (all Setup config files — different files)
- T007–T013 (all mock data fixtures — different JSON files)
- T016–T019 (all Pydantic models — different Python files)
- T025–T030 (React shell components — different TSX files)
- After Phase 2: US1 (T031–T034), US2 (T035–T038), US3 (T039–T042) can all run in parallel
- T043–T048 (all Polish tasks — different files)

---

## Parallel Example: All Three Demos After Foundation

```bash
# Once Phase 2 is complete, launch all three demo scenes in parallel:

# Developer A: Morning Prep (US1)
Task: T031 "Agent prompt for morning_prep"
Task: T032 "Deterministic composer for morning_prep"
Task: T033 "MorningPrep React scene"
Task: T034 "Wire MorningPrep to useAgent hook"

# Developer B: Client Update (US2)
Task: T035 "Agent prompt for client_update"
Task: T036 "Deterministic composer for client_update"
Task: T037 "ClientUpdate React scene"
Task: T038 "Wire ClientUpdate to useAgent hook"

# Developer C: Inbound Query (US3)
Task: T039 "Agent prompt for inbound_query"
Task: T040 "Deterministic composer for inbound_query"
Task: T041 "InboundQuery React scene"
Task: T042 "Wire InboundQuery to useAgent hook"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T006)
2. Complete Phase 2: Foundational (T007–T030) — CRITICAL, blocks all scenes
3. Complete Phase 3: User Story 1 — Morning Prep (T031–T034)
4. **STOP and VALIDATE**: Run backend + frontend, hit "Run morning brief", see full output
5. Demo-ready for morning prep scenario

### Incremental Delivery

1. Setup + Foundational → Framework running, mock APIs serving cross-asset data
2. Add US1 (Morning Prep) → "What do I need to know?" works → **First demo-able increment**
3. Add US2 (Client Update) → "Draft a client update" works → **Second demo-able increment**
4. Add US3 (Inbound Query) → Inbound query + response works → **Full PoC scope**
5. Polish → Foundry integration, containers, docs → **Production-path ready**

### Key Design Decisions

- **DEMO mode first**: All composers return hardcoded data matching the static HTML mockups exactly. This ensures demos always work regardless of Azure connectivity.
- **Same JSON contract**: Frontend is mode-blind. LIVE mode (Foundry Agent Service) returns the same response shape as DEMO composers.
- **Human-in-the-loop everywhere**: Every draft is editable. Every action requires explicit approval. Nothing sends automatically.
- **Cross-asset data**: Fixtures cover rates (UST, swaps, mortgage), credit (ACME Corp bonds/CDS), and equities (S&P futures) — replacing the old muni-only endpoints.
- **Entra ID everywhere**: No access keys, no connection strings with secrets. Managed identity for Function→Cosmos/Redis/Foundry. DefaultAzureCredential in code. OIDC for GitHub Actions.
- **Terraform IaC**: All Azure resources provisioned via Terraform modules. State in Azure Storage. Plan on PR, apply on merge.
- **Azure AI Agent Framework SDK** (`azure.ai.agent`): Required SDK for Foundry agent orchestration. Multi-turn, tool-calling, grounded in OpenAPI tools spec.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific demo scene for traceability
- Each demo scene is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate scene independently
- The static HTML mockups in `src/demos/` are the source of truth for UI layout and data content
- `openapi/tools.yaml` rewrite (T015) is the canonical cross-asset API contract for Foundry import
