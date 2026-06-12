---
description: "Task list for Morning Planning & Prioritized Outreach (Demo 1)"
---

# Tasks: Morning Planning & Prioritized Outreach (Interactive Demo)

**Input**: Design documents from `/specs/001-morning-planning-outreach/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓
**Stack**: C#/.NET 10 + Microsoft Agent Framework + Azure AI Foundry + React 19/MUI v9 + Azure
Container Apps + Terraform (per ADR-0002 / constitution v0.2.0).

**Tests**: INCLUDED — the constitution mandates the testing pyramid (Principle VII) and the §17
quality gate requires `dotnet test`. Write tests before/with implementation per TDD where practical.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 (morning brief), US2 (prioritized outreach), US3 (human-in-the-loop), US4 (DEMO mode)
- All paths are repo-root relative and match `plan.md` → Project Structure.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: .NET 10 + React solution scaffolding and shared tooling.

- [ ] T001 Create `global.json` (pin `10.0.100`) and `Directory.Packages.props` (central package
      management, `ManagePackageVersionsCentrally=true`) at repo root with the pinned versions from
      `plan.md` (Microsoft.Agents.AI, Microsoft.Agents.AI.AzureAI, Azure.AI.Agents.Persistent,
      Azure.AI.Projects, Azure.Identity, Serilog.*, OpenTelemetry.*, Swashbuckle, xunit, Moq,
      FluentAssertions, Microsoft.AspNetCore.Mvc.Testing).
- [ ] T002 Create `AgenticTradersDesk.sln` and add empty project stubs: `src/shared/Observability`,
      `src/mock-api`, `src/orchestration-api`, `src/agent-provisioner`, `tests/mock-api.Tests`,
      `tests/orchestration-api.Tests`.
- [ ] T003 [P] Scaffold `src/ui-app` (Vite + React 19 + TypeScript 6 + MUI v9 + React Router v7 +
      Axios); add `src/ui-app/nginx.conf` reverse-proxying `/api/*` to the orchestration API.
- [ ] T004 [P] Create root `Taskfile.yaml` with `includes:` → `tasks/Taskfile.local.yml`,
      `tasks/Taskfile.cloud.yml`, `tasks/Taskfile.build.yml`; add `dotenv: ['.env']`.
- [ ] T005 [P] Create `docker-compose.yml` (ui-app, orchestration-api, mock-api) and `.env.example`
      with `DEMO_MODE=1`, `MOCK_API_BASEURL`, `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_MODEL`,
      `MAX_TOOL_HOPS` (no secret values).
- [ ] T006 [P] Implement `src/shared/Observability` extensions: `UseSerilog(serviceName)`,
      `AddOpenTelemetry(serviceName)`, `UseCorrelationId()`, `UseGlobalExceptionHandler()`
      (structured JSON errors).

**Checkpoint**: Solution builds empty; `task local:up` boots stub containers.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The mock-data seam + shared DTOs + endpoint skeleton that BOTH DEMO and LIVE depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T007 [P] Author fictional JSON fixtures in `src/mock-api/Data/` (clients, engagement, axes,
      holdings, marketdata, relval, news `fed_surprise_hike`, newissues, coalition) seeded from
      `mockup/demos/01-morning-prep.html` (Atlas Pension/ATLAS, Brookline Bank/BROOK,
      Cedar Asset Mgmt/CEDAR) per `data-model.md`. All fictional.
- [ ] T008 Implement `src/mock-api` endpoints for every operation in `openapi/tools.yaml`
      (`/mock/tableau/clients[/{cid}]`, `/mock/dynamics/clients/{cid}/engagement`,
      `/mock/trading/axes`, `/mock/trading/holdings`, `/mock/calendar/newissues`,
      `/mock/marketdata[/relval/{event_id}]`, `/mock/news/{event_id}`,
      `/mock/coalition/{cid}` + `/sector/{sector}`) reading `Data/`; add `/healthz` + `/readyz`.
- [ ] T009 [P] Add `tests/mock-api.Tests` contract tests asserting each endpoint's shape/status
      matches `openapi/tools.yaml` (WebApplicationFactory).
- [ ] T010 [P] Create response DTOs in `src/orchestration-api/Models/` (`MorningBrief`,
      `MarketStripItem`, `ReasoningStep`, `MacroNarrative`, `AffectedClient`, `Concern`,
      `OutreachItem`, `RankingRationale`) matching `contracts/morning-brief.schema.json`.
- [ ] T011 Implement `src/orchestration-api/Program.cs`: Serilog+OTEL, CORS, JWT-bearer scaffolding,
      typed `HttpClient` to mock-api (base URL from env), `/healthz` + `/readyz`, global JSON error
      handler, and a `MODE`/`DEMO_MODE` configuration switch.
- [ ] T012 [P] Add a shared response-validation test helper in `tests/orchestration-api.Tests`
      that validates a `MorningBrief` payload against `contracts/morning-brief.schema.json`.

**Checkpoint**: mock-api serves all tool endpoints; orchestration-api boots with DTOs + mode switch.

---

## Phase 3: User Story 1 - Run the morning brief (Priority: P1) 🎯 MVP

**Goal**: `POST /api/agent/morning-brief` returns macro narrative + most-affected clients + ranked
outreach (basic), rendered in the cockpit. Works in DEMO (offline) and LIVE (Foundry).

**Independent Test**: Trigger the brief from the UI / curl; response validates against the schema and
shows narrative + most-affected + outreach sections.

### Tests for User Story 1 ⚠️ (write first)
- [ ] T013 [P] [US1] Contract/integration test in `tests/orchestration-api.Tests` for
      `POST /api/agent/morning-brief` (DEMO): 200 + schema-valid + non-empty `macroNarrative`,
      `mostAffectedClients`, `outreach`.
- [ ] T014 [P] [US1] Tool-error test: when mock-api returns 5xx, the brief degrades with a `notes`
      entry and structured JSON (no HTML), per FR-011.

### Implementation for User Story 1
- [ ] T015 [US1] Implement DEMO composer `src/orchestration-api/Agents/Demo/MorningBriefComposer.cs`:
      pull marketdata + news + clients + holdings + engagement via the mock-api HttpClient and
      assemble `marketStrip`, `reasoning`, `macroNarrative`, `mostAffectedClients`, and a baseline
      `outreach` list (deterministic ordering/text).
- [ ] T016 [US1] Implement the scene endpoint `POST /api/agent/morning-brief` + request model
      (`eventId`, `date`) wiring the DEMO composer behind the mode switch (per `contracts/agent-api.yaml`).
- [ ] T017 [P] [US1] Write the agent prompt `src/orchestration-api/Prompts/morning-brief.md`
      (instructions + the output JSON schema mirroring `morning-brief.schema.json`).
- [ ] T018 [US1] Implement LIVE tool functions `src/orchestration-api/Agents/Tools/` wrapping the
      mock-api endpoints as Agent Framework tools (typed HttpClient; return JSON; never throw).
- [ ] T019 [US1] Implement `src/orchestration-api/Agents/AgentRunner.cs`: Foundry chat client via
      `Microsoft.Agents.AI.AzureAI` + `DefaultAzureCredential`, tool-calling loop capped at
      `MAX_TOOL_HOPS`, map model output → `MorningBrief` DTO (LIVE path of the mode switch).
- [ ] T020 [P] [US1] Implement `src/agent-provisioner` console job: idempotently register the
      morning-brief agent version in Foundry via `PersistentAgentsClient` (mirrors `init_agents.py`).
- [ ] T021 [US1] Build `src/ui-app/src/scenes/MorningBrief/`: market strip, agent-reasoning steps,
      macro-narrative card, most-affected-clients table, "Run morning brief" action; Axios call to
      `/api/agent/morning-brief` via `src/ui-app/src/api/client.ts` (baseURL `/api`).

**Checkpoint**: MVP — the morning brief runs end-to-end in DEMO mode from the UI.

---

## Phase 4: User Story 2 - Prioritized outreach ranking with talking points (Priority: P1)

**Goal**: Outreach ranked by wallet + engagement + event-relevance with rationale and personalized
talking points tied to each client's axes/holdings.

**Independent Test**: Inspect `outreach`; each item has rank, suggested topic, talking points, and a
`rationale` whose `compositeScore` reflects the documented weighted blend; ordering matches scores.

### Tests for User Story 2 ⚠️ (write first)
- [x] T022 [P] [US2] Ranking test in `tests/orchestration-api.Tests`: composite = 0.4·wallet +
      0.3·engagement + 0.3·eventRelevance (±epsilon); `outreach` sorted by rank; ranks contiguous from 1.
- [x] T023 [P] [US2] Talking-points test: each `OutreachItem.talkingPoints` references the event and
      at least one relevant axis/holding for that client.

### Implementation for User Story 2
- [x] T024 [US2] Implement `src/orchestration-api/Agents/Demo/OutreachRanker.cs`: compute
      wallet/engagement/event-relevance + weighted `compositeScore` + `explanation`; produce ordered
      `OutreachItem[]` with `suggestedTopic` and `talkingPoints` (axes/holdings-aware). Wire into the
      DEMO composer (T015).
- [x] T025 [US2] Extend `Prompts/morning-brief.md` + `AgentRunner` mapping so LIVE produces the same
      ranked `outreach` shape with rationale and talking points.
- [x] T026 [US2] Extend `src/ui-app/src/scenes/MorningBrief/`: ranked outbound-priority table with
      suggested topic, talking points, and an inspectable ranking rationale.

**Checkpoint**: US1 + US2 both functional; outreach is ranked, explained, and personalized.

---

## Phase 5: User Story 3 - Human-in-the-loop plan editing (Priority: P2)

**Goal**: VP edits the generated call plan (reorder, remove client, add note) and approves; nothing
is sent automatically.

**Independent Test**: Edit + remove + approve in the UI; approved plan reflects edits; `sent` stays false.

### Tests for User Story 3 ⚠️ (write first)
- [x] T027 [P] [US3] UI test (Vitest/RTL) in `src/ui-app`: editing a talking point and removing a
      client updates the plan and sets an "edited" state.
- [x] T028 [P] [US3] UI test: clicking Approve sets `approvalState=approved` and asserts no outbound
      request is issued (`sent=false`).

### Implementation for User Story 3
- [x] T029 [US3] Implement `CallPlan` UI state in `src/ui-app/src/scenes/MorningBrief/` derived from
      `outreach` (reorder, remove-client, editable note, approve), demo-only — no outbound action.
- [x] T030 [US3] Add the "editable / approved" affordances + human-in-the-loop hint copy matching the
      mockup (`mockup/demos/01-morning-prep.html`).

**Checkpoint**: US1–US3 work independently.

---

## Phase 6: User Story 4 - Deterministic DEMO mode & LIVE/DEMO parity (Priority: P2)

**Goal**: DEMO is the default, runs offline with no credentials, and is byte-identical across runs;
LIVE and DEMO return the same JSON shape.

**Independent Test**: With `DEMO_MODE=1` and no Foundry credentials, the brief returns complete and
identical output on repeated runs; both modes validate against the same schema.

### Tests for User Story 4 ⚠️ (write first)
- [x] T031 [P] [US4] Determinism test in `tests/orchestration-api.Tests`: two DEMO runs with the same
      input produce byte-identical serialized `MorningBrief` (SC-002).
- [x] T032 [P] [US4] Parity test: a DEMO payload and a representative LIVE-shaped payload both pass
      the schema validator (SC-003), and the React renderer is mode-agnostic.

### Implementation for User Story 4
- [x] T033 [US4] Ensure `DEMO_MODE=1` default + a no-credential code path (no `DefaultAzureCredential`
      acquisition in DEMO); deterministic ordering/text + stable JSON serialization options.
- [x] T034 [US4] Add empty-state handling (no materially-affected clients → empty lists + `notes`)
      and unknown-`eventId` → structured 400, per spec Edge Cases.

**Checkpoint**: Demo is stage-safe (deterministic, offline) with full mode parity.

---

## Phase 7: Deployment & Infrastructure (Terraform + CI/CD)

**Purpose**: Containerize and deploy to Azure Container Apps with Foundry; ship via GitHub Actions.
Many tasks are [P] (different files); ACA deploy depends on Dockerfiles + built images.

- [ ] T035 [P] Multi-stage non-root Alpine `Dockerfile` for each of `src/mock-api`,
      `src/orchestration-api`, `src/agent-provisioner` (`sdk:10.0-alpine` → `aspnet:10.0-alpine`,
      `USER $APP_UID`); multi-stage `node:alpine` → `nginx:alpine` for `src/ui-app`.
- [ ] T036 [P] Terraform base in `infra/`: `providers.tf` (`azurerm ~>4`, `azapi ~>2`, `random ~>3`),
      `variables.tf`, `locals.tf` (random_pet+random_id naming), `random.tf`, `networking.tf`,
      `monitoring.tf` (Log Analytics + App Insights).
- [ ] T037 [P] Terraform `infra/acr.tf`, `infra/identity.tf` (one UAI; `AcrPull`, Key Vault Secrets
      User, `Cognitive Services OpenAI User`, `Azure AI Project Manager`), `infra/keyvault.tf` +
      `infra/keyvault-secrets.tf`.
- [ ] T038 Terraform `infra/ai.tf` + `infra/ai-connections.tf` (azapi): AI Services account
      (kind `AIServices`) → model deployment (`FOUNDRY_MODEL`) → Foundry project → connections →
      **90s RBAC-propagation wait/dependency** → capability host (per research R4).
- [ ] T039 Terraform `infra/containerapps.tf` + `infra/outputs.tf`: ACA environment + container apps
      for ui-app (external ingress), orchestration-api + mock-api (internal ingress), agent-provisioner
      job; KV secret references via UAI; export ui-app FQDN + `FOUNDRY_PROJECT_ENDPOINT`.
- [ ] T040 [P] `tasks/Taskfile.build.yml` (`az acr build` per image) + `tasks/Taskfile.cloud.yml`
      (`terraform apply`, deploy/roll ACA revisions, run provisioner, `output-env`) +
      `tasks/Taskfile.local.yml` (docker compose up/down/logs).
- [ ] T041 [P] `.github/workflows/ci.yml`: `dotnet build`/`dotnet test`, UI `npm ci && test && build`,
      `gitleaks`, `terraform fmt -check && validate` on PR.
- [ ] T042 `.github/workflows/cd.yml`: Azure OIDC login → `az acr build` per image →
      `az containerapp update` to roll each app on merge to `main`.

**Checkpoint**: `task cloud:up && task cloud:build && task cloud:deploy` yields a public HTTPS demo
(SC-005); agents registered in Foundry.

---

## Phase 8: Polish & Cross-Cutting

- [x] T043 [P] Update `README.md` for the C#/Foundry/ACA stack; align `.github/copilot-instructions.md`
      and `.env.example`; remove/retire Python-only `requirements.txt` references (ADR-0002).
- [x] T044 [P] Tighten CORS for deployment; confirm containers run non-root; `gitleaks detect` clean
      (FR-013/014, SC-006).
- [ ] T045 Run `quickstart.md` validation end-to-end (DEMO local, then cloud); record results.
- [x] T046 [P] Move `007-foundry-migration` / `003-container-deployment` backlog cards to reflect that
      this feature realizes them; note in `.squad/decisions.md`.
- [x] T047 [P] Add a DEMO-mode performance check asserting `POST /api/agent/morning-brief` returns and
      the brief renders in **< 10s** (SC-001): a timed xUnit assertion in `orchestration-api.Tests/`
      plus a note in the T045 quickstart validation.

---

## Dependencies & Execution Order

### Phase dependencies
- **Setup (P1)** → no deps.
- **Foundational (P2)** → depends on Setup; **BLOCKS all user stories** (mock-api + DTOs + endpoint skeleton).
- **US1 (P3)** → after Foundational. MVP.
- **US2 (P4)** → after Foundational; builds on US1's composer/endpoint (T015–T016) and UI scene (T021).
- **US3 (P5)** → after US1 (consumes `outreach` in the UI); otherwise independent.
- **US4 (P6)** → after US1 (hardens the DEMO/parity behavior US1 introduces).
- **Deployment (P7)** → services must exist (P2–P6); `ai.tf` before `containerapps.tf`; images before `cd.yml` deploy.
- **Polish (P8)** → after the desired stories + deployment.

### Within each story
- Tests first (TDD) → models → services/composer → endpoint → UI.
- DEMO path before LIVE path within US1 (composer before AgentRunner) so the MVP is demoable offline.

### Parallel opportunities
- Setup: T003, T004, T005, T006 in parallel.
- Foundational: T007 & T009 (mock-api data/tests) parallel with T010 & T012 (DTOs/validator).
- US1 tests T013/T014 parallel; T017 (prompt) & T020 (provisioner) parallel with composer work.
- US2 tests T022/T023 parallel. US3 tests T027/T028 parallel. US4 tests T031/T032 parallel.
- Deployment: T035/T036/T037/T040/T041 largely parallel (different files).

---

## Implementation Strategy

### MVP first
1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (DEMO path) → **STOP & VALIDATE** the
   morning brief offline → demo.

### Incremental delivery
US1 (MVP) → US2 (ranked/personalized outreach) → US3 (human-in-the-loop) → US4 (stage-hardening &
parity) → Deployment (ACA + Foundry) → Polish. Each story is independently testable and demoable.

### Notes
- [P] = different files, no dependencies. [Story] label maps each task to a user story.
- All agent/composer data access goes through the mock-api over HTTP (FR-002 / SC-007) — never read
  `Data/` JSON in-process from the orchestration API.
- Keep LIVE and DEMO outputs schema-identical (FR-010); validate with the T012 helper.
- Commit per task or logical group; Conventional Commits + `Co-authored-by: Copilot` trailer.
