---
description: "Dependency-ordered task list for 002-reactive-event-cockpit"
---

# Tasks: Reactive Event Cockpit

**Input**: Design documents from `specs/002-reactive-event-cockpit/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R11), data-model.md, contracts/ (event-ingestion.openapi.yaml, event-stream.sse.md, live-update.schema.json, admin-submission.schema.json)

**Tests**: Test tasks ARE included — the constitution mandates Testing Discipline (Principle VII) and plan.md / quickstart.md call out specific xunit + Vitest/RTL suites (EventStore contract, DEMO determinism, conflict net-score, SSE re-synthesis, parity, AdminScene validation).

**Organization**: Tasks are grouped by user story (US1 P1 → US2 P2 → US3 P2 → US4 P3) behind shared Setup + Foundational phases, with a final Polish / Presentation phase that includes the M.INT 3-column dashboard restyle.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 / US2 / US3 / US4 (Setup, Foundational, Polish carry no story label)
- All paths are repo-relative; this feature is **additive** (constitution Principle II/III preserved)

## Constitution guardrails (apply to every task)

- **Three-layer flow** (Principle II): `ui-app → orchestration-api → HTTP tools → mock-api`. Orchestration touches the event store **only over HTTP**; never read fixtures in-process.
- **Tools described in `openapi/tools.yaml`** (Principle VI) — every new mock-api event op gets a tool entry.
- **LIVE/DEMO parity** (Principle III): event data is additive to `RmBriefing` / `MorningBrief`; DEMO stays offline + byte-stable.
- **Idempotent agent registration** (Principle X / FR-021) via `src\agent-provisioner\`.
- All data **fictional** (Principle IV); no secrets (Principle IV/IX).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the additive baseline builds and reserve configuration knobs.

- [ ] T001 Verify baseline builds on branch `002-reactive-event-cockpit`: `dotnet restore WF-Garage.sln`, `dotnet build WF-Garage.sln --nologo`, `npm --prefix src\ui-app install` (no code changes — establishes a green starting point).
- [ ] T002 [P] Add reactive-feature config knobs to `.env.example` and `docker-compose.yml` (e.g. `SSE_COALESCE_WINDOW_MS=750`, `EVENT_FANOUT_MAX_CONCURRENCY`, `EVENTS_SEED_PATH`) with safe DEMO defaults; document in `.env.example` comments. No secrets.
- [ ] T003 [P] Confirm `openapi\tools.yaml` and `src\mock-api\Data\` exist and note the `Cb`/`Td` store + endpoint patterns to mirror (read-only orientation task; record findings in the PR description).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Event store, HTTP surface, tools, and additive DTO fields that EVERY user story depends on.

**⚠️ CRITICAL**: No user-story work (Phase 3+) can begin until this phase is complete.

- [ ] T004 Create fictional overnight seed fixture `src\mock-api\Data\events_overnight.json` with ≥3 events (≥1 CB-customer headline, ≥1 TD ticker/sector, ≥1 macro/rate) per data-model.md §1 (`scope=overnight`, `origin=seed`, typed `affectedEntities`). (FR-002)
- [ ] T005 Implement in-memory `src\mock-api\EventStore.cs` mirroring `CbDataStore`/`TdDataStore`: seed-on-startup from T004, `List()`, `GetByEntity(...)`, `Ingest(...)` with identity dedup (R11), reload-on-restart. (FR-001, FR-003, FR-004, FR-005)
- [ ] T006 Implement `src\mock-api\Endpoints\EventEndpoints.cs` exposing the `/mock/events` surface (list, by-entity query, ingest) per `contracts/event-ingestion.openapi.yaml`; structured `{ "error": ... }` on bad input, never throw. (FR-003, FR-004; Principle VIII)
- [ ] T007 Wire `new EventStore()` + `app.MapEventEndpoints()` into `src\mock-api\Program.cs`. (FR-001)
- [ ] T008 [P] Add event operations (`list_events`, `get_events_by_entity`, `ingest_event`) to `openapi\tools.yaml`, consistent with `contracts/event-ingestion.openapi.yaml`. (Principle VI / FR-004)
- [ ] T009 [P] Create `src\orchestration-api\Models\EventModels.cs` shared DTOs (`MarketEvent`, `AffectedEntities`, `EventLinkage`, `LiveUpdate`, `LiveAlert`, `AdminNewsSubmission`) mirroring the mock-api shape, camelCase/omit-null source-gen style. (data-model.md §1,2,4,5)
- [ ] T010 Implement `src\orchestration-api\Agents\Tools\EventTools.cs` HTTP wrappers (`list_events`, `get_events_by_entity`, `ingest_event`) over the typed `MockApiClient` — HTTP only, no in-process reads. (Principle II / FR-004)
- [ ] T011 [P] Add additive fields to `src\orchestration-api\Models\RmBriefing.cs`: `EventsConsidered` on the root and `DrivingEvents` on `PriorityCall` (no existing field changes). (FR-007, FR-009; data-model.md §3a)
- [ ] T012 [P] Add additive fields to `src\orchestration-api\Models\MorningBrief.cs`: `EventsConsidered` on the root and `DrivingEvents` on the affected-client/holding record. (FR-007, FR-009; data-model.md §3b)
- [ ] T013 Add `tests\mock-api.Tests` EventStore contract tests: seed loads ≥3 events, list, by-entity query, ingest append, duplicate ingest is deduped/flagged (R11). (Principle VII)

**Checkpoint**: Event store reachable over HTTP, tools registered, DTOs ready — user stories can begin.

---

## Phase 3: User Story 1 - Overnight multi-event briefing (Priority: P1) 🎯 MVP

**Goal**: Both scenes synthesize the combined impact of ALL current (overnight) events, with per-call/per-holding event linkage and changed ranking vs a no-event baseline.

**Independent Test**: Seed ≥3 overnight events, POST `/api/agent/rm-briefing` and `/api/agent/morning-brief`; confirm `eventsConsidered` lists all events, each affected item names ≥1 driving event, ranking differs from empty-seed baseline, and DEMO output is byte-stable across two runs. (SC-001, SC-004, SC-005)

### Tests for User Story 1

- [x] T014 [P] [US1] DEMO determinism + conflict net-score tests in `tests\orchestration-api.Tests`: identical seeds → byte-stable JSON; two events hitting one entity net deterministically and BOTH remain visible as drivers. (SC-004, R6)
- [x] T015 [P] [US1] No-impact + empty-store tests in `tests\orchestration-api.Tests`: an event matching no portfolio entity leaves ranking unchanged; empty store renders baseline ranking ("no events processed"). (US1 AS3, R11)

### Implementation for User Story 1

- [x] T016 [US1] Implement deterministic `src\orchestration-api\Agents\Demo\EventImpactResolver.cs`: pure event→entity mapping (R5), signed contribution, net-sum conflict resolution with sorted-stable output and per-driver rationale. (FR-008, SC-005, R6)
- [x] T017 [US1] Extend `src\orchestration-api\Agents\Demo\RmBriefingComposer.cs` to read events via `EventTools`, apply the netted delta on top of `RmCallScorer.Score(...)` (scorer reused unchanged), populate `EventsConsidered` + `PriorityCall.DrivingEvents`, and re-sort. (FR-006, FR-007, FR-008)
- [x] T018 [US1] Extend `src\orchestration-api\Agents\Demo\MorningBriefComposer.cs` to apply events into the existing `0.40 wallet / 0.30 engagement / 0.30 event-relevance` blend, populate `EventsConsidered` + affected-client `DrivingEvents`, and re-rank. (FR-006, FR-007, FR-008)
- [x] T019 [P] [US1] Expose the event tools to the RM agent in `src\orchestration-api\Agents\Tools\RmBriefingTools.cs`. (FR-006)
- [x] T020 [P] [US1] Expose the event tools to the morning-brief agent in `src\orchestration-api\Agents\Tools\MorningBriefTools.cs`. (FR-006)
- [x] T021 [US1] Render `eventsConsidered` + per-call `drivingEvents` (event "why") in `src\ui-app\src\scenes\RmBriefing\RmBriefingScene.tsx` and `PriorityCallCard.tsx` — mode-blind, using existing M.INT primitives. (FR-007, SC-005)
- [x] T022 [US1] Render `eventsConsidered` + per-holding `drivingEvents` in `src\ui-app\src\scenes\MorningBrief\MorningBriefScene.tsx` (and `CallPlan.tsx`/`MarketStrip.tsx` as needed). (FR-007, SC-005)
- [x] T023 [P] [US1] Vitest/RTL: scenes render events-considered list and per-item driving events in `src\ui-app\src\scenes\RmBriefing\RmBriefingScene.test.tsx` and `MorningBrief\MorningBriefScene.test.tsx`. (Principle VII)

**Checkpoint**: US1 is independently demoable — the overnight multi-event MVP. STOP and validate (SC-001/004/005) before proceeding.

---

## Phase 4: User Story 2 - Intraday live event push (Priority: P2)

**Goal**: An already-open briefing receives a live alert + in-place re-rank within 10 s of an intraday ingest, with burst coalescing and reconnect reconciliation — no manual reload.

**Independent Test**: Open a briefing, POST a new intraday event to `/mock/events`; within 10 s the page shows a live alert banner and the affected item re-ranks. Drop/restore the connection → page reconciles to current state. (SC-002, FR-010–FR-013)

### Tests for User Story 2

- [x] T024 [P] [US2] SSE hub tests in `tests\orchestration-api.Tests`: ingest triggers a re-synthesized full DTO push; a burst within the coalesce window yields ONE consolidated update; reconnect with `Last-Event-ID` returns a fresh snapshot. (FR-012, R3, R4)

### Implementation for User Story 2

- [x] T025 [US2] Implement `src\orchestration-api\Live\BriefingEventStream.cs` SSE hub: subscribe per scene/persona, debounce/coalesce ingests (~`SSE_COALESCE_WINDOW_MS`, R3), re-run the composer/runner for the affected scene, emit `LiveUpdate` with monotonic `sequence` as SSE `id:` (R4, R7) and a `LiveAlert` (incl. `noImpact`). (FR-010, FR-011, FR-012)
- [x] T026 [US2] Map SSE endpoints (`GET /api/agent/rm-briefing/stream`, `GET /api/agent/morning-brief/stream`) and register the stream + event services in `src\orchestration-api\Program.cs`, honoring existing CORS. (FR-010, FR-013; `contracts/event-stream.sse.md`)
- [x] T027 [P] [US2] Add an SSE pass-through location to `src\ui-app\nginx.conf` (`proxy_buffering off;`, `proxy_cache off;`, raised `proxy_read_timeout`, clear hop-by-hop `Connection`) so streams traverse the ACA ingress. (FR-013, R2)
- [x] T028 [US2] Add `subscribeToEvents(scene)` via native `EventSource` to `src\ui-app\src\api\client.ts` (auto-reconnect / `Last-Event-ID`). (FR-010, FR-012)
- [x] T029 [P] [US2] Create `src\ui-app\src\components\LiveAlertBanner.tsx` driven by SSE updates, styled with the existing M.INT theme/primitives (info/notice/urgent → severity; `noImpact` low-priority variant). (FR-011)
- [x] T030 [US2] Wire SSE consumption into `src\ui-app\src\scenes\RmBriefing\RmBriefingScene.tsx`: show `LiveAlertBanner` and apply the re-synthesized DTO (alert + in-place re-rank). (FR-010, FR-011)
- [x] T031 [US2] Wire SSE consumption into `src\ui-app\src\scenes\MorningBrief\MorningBriefScene.tsx`: same alert + re-rank from the pushed DTO. (FR-010, FR-011)
- [x] T032 [P] [US2] Vitest/RTL: `LiveAlertBanner` rendering + SSE-driven re-rank (mock `EventSource`) for both scenes. (Principle VII)

**Checkpoint**: US1 + US2 both work — intraday live push demoable within the 10 s budget.

---

## Phase 5: User Story 3 - Admin news injection UI (Priority: P2)

**Goal**: A new `/admin` route lets an operator compose + inject a news item (validated) that flows through the SAME ingestion + reactive path as a real intraday event, plus shows the current event store.

**Independent Test**: From `/admin`, submit incomplete (no headline / no affected entity) → rejected, nothing ingested; submit a valid item targeting a known customer/ticker → acknowledged, appears in the event list, and an open briefing reacts within 10 s. (SC-003, SC-006, FR-014–FR-017)

### Tests for User Story 3

- [x] T033 [P] [US3] Server-side validation + reactive-path tests in `tests\orchestration-api.Tests` / `tests\mock-api.Tests`: admin submission missing headline or affected entity is rejected and ingests nothing; a valid admin ingest (`origin=admin`, `scope=intraday`) triggers the SSE reaction. (FR-015, FR-016)

### Implementation for User Story 3

- [x] T034 [US3] Add admin `ingestNews(submission)` + `listEvents()` calls to `src\ui-app\src\api\client.ts`, POSTing to the same ingestion endpoint a real intraday event uses (FR-016). (FR-014, FR-017)
- [x] T035 [P] [US3] Create `src\ui-app\src\scenes\Admin\NewsForm.tsx`: headline, summary, source, severity, type, affected entities, scene targeting; client-side validation rejecting incomplete submissions with a clear message (FR-015). (FR-014)
- [x] T036 [P] [US3] Create `src\ui-app\src\scenes\Admin\EventList.tsx` listing current events (overnight + injected) for situational awareness. (FR-017)
- [x] T037 [US3] Create `src\ui-app\src\scenes\Admin\AdminScene.tsx` composing `NewsForm` + `EventList`, built from the existing M.INT components (`MintBrand`, `SectionTitle`, `AiInsightPanel`) — do not duplicate them. (FR-014, FR-017)
- [x] T038 [US3] Add the `/admin` route in `src\ui-app\src\App.tsx` and an Admin entry point in `src\ui-app\src\components\CockpitNav.tsx`. (FR-014, R10)
- [x] T039 [P] [US3] Vitest/RTL: `AdminScene` rejects incomplete submissions and ingests nothing; valid submit calls the client once. (FR-015; Principle VII)

**Checkpoint**: US1–US3 all independently functional — the demo can be driven live from `/admin`, both scenes react (SC-006).

---

## Phase 6: User Story 4 - Per-event multi-agent fan-out + synthesizer (Priority: P3)

**Goal**: In LIVE mode, briefing generation fans out one independently-traceable specialist agent run per event to a central synthesizer that emits the unchanged DTO; DEMO stays deterministic/offline with the same shape. (Reframes backlog 011.)

**Independent Test**: Generate a LIVE event-aware briefing; the Foundry trace shows `synthesizer → per-event/specialist agent → tool-call`; confirm DEMO produces the identical DTO shape offline. (SC-007, SC-004, FR-018–FR-021)

### Tests for User Story 4

- [ ] T040 [P] [US4] LIVE/DEMO parity test in `tests\orchestration-api.Tests`: for identical event inputs the `RmBriefing` / `MorningBrief` JSON shape is identical across modes; assert the fan-out produces a traceable synthesizer→specialist→tool graph (via the `OrchestrationTelemetry` source). (SC-004, SC-007, FR-009)

### Implementation for User Story 4

- [ ] T041 [P] [US4] Author `src\orchestration-api\Prompts\event-specialist.md` (per-event impact assessment → structured linkage resolving to typed selectors). (FR-018)
- [ ] T042 [P] [US4] Author `src\orchestration-api\Prompts\briefing-synthesizer.md` (combine specialist outputs → unchanged briefing DTO). (FR-018)
- [ ] T043 [US4] Implement `src\orchestration-api\Agents\EventSynthesis\` fan-out + synthesizer orchestration: one specialist run per event/cluster, concurrent via `Task.WhenAll` bounded by `EVENT_FANOUT_MAX_CONCURRENCY` and `MAX_TOOL_HOPS`, wrapped with `.AsBuilder().UseOpenTelemetry(sourceName: OrchestrationTelemetry.SourceName, …)`. (FR-018, FR-019, SC-007, R8)
- [ ] T044 [US4] Wire the fan-out into `src\orchestration-api\Agents\RmAgentRunner.cs` (LIVE RM path) via the resolve-by-name pattern (`GetAIAgentAsync` → fallback create). (FR-018)
- [ ] T045 [US4] Wire the same fan-out into `src\orchestration-api\Agents\AgentRunner.cs` (LIVE morning-brief path). (FR-018)
- [ ] T046 [US4] Add idempotent `AgentSpec` entries for `event-specialist` and `briefing-synthesizer` to `src\agent-provisioner\Program.cs` (`GetAIAgentAsync` first, `CreateAIAgentAsync` only when absent). (FR-021, Principle X, R9)

**Checkpoint**: All four user stories functional; LIVE traceability story (SC-007) complete; DEMO parity preserved (SC-004).

---

## Phase 7: Polish, Presentation & Cross-Cutting Concerns

**Purpose**: Cross-cutting hardening + the client-requested M.INT 3-column dashboard restyle. **Reuse the already-landed theme + primitives from commit a723203** (`src\ui-app\src\theme\theme.ts`, `MintBrand`, `SectionTitle`, `AiInsightPanel`, restyled `CockpitNav`) — do NOT duplicate or re-create them.

### M.INT 3-column dashboard (per assets/Designer Layout.png)

- [ ] T047 [P] Create a 3-column cockpit dashboard layout shell (Client View / Ticker View / Overall View "Morning Call") in `src\ui-app\src\components\CockpitDashboardLayout.tsx`, matching `assets\Designer Layout.png` and built from the existing `theme.ts` + `SectionTitle`/`MintBrand` primitives (no new theme, no duplicated components).
- [ ] T048 Wire the live event alert banner + in-place re-ranking into the 3-column layout: mount `LiveAlertBanner` (from T029) and route the SSE re-synthesized DTO (US2) into the Client/Ticker/Overall columns of `CockpitDashboardLayout.tsx`. (FR-010, FR-011)
- [ ] T049 Restyle the `/admin` route (`AdminScene`, `NewsForm`, `EventList` from US3) to the M.INT design language using `MintBrand`, `SectionTitle`, and `AiInsightPanel` for visual consistency with the cockpit — reuse, do not duplicate.

### Cross-cutting

- [ ] T050 [P] Update `docs\architecture.md` with the event store, `/mock/events` HTTP surface, SSE channel, and the synthesizer→specialist fan-out topology. (Principle X)
- [ ] T051 [P] Run `specs\002-reactive-event-cockpit\quickstart.md` end-to-end in DEMO mode (`task local:up`) and confirm SC-001, SC-002, SC-003, SC-006.
- [ ] T052 Verify SSE traverses the deployed Sweden ACA ingress through the `ui-app` nginx proxy (FR-013); document the WebSocket fallback decision in the PR if buffering cannot be disabled cleanly. (R2)
- [ ] T053 Run the constitution §17 quality gate: `dotnet build`/`dotnet test WF-Garage.sln`, `npm --prefix src\ui-app run build` + `npm --prefix src\ui-app test`, `terraform -chdir=infra fmt -check` + `validate`, and `gitleaks detect --source . --no-banner`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup. **BLOCKS all user stories.**
- **User Stories (Phase 3–6)**: All depend on Foundational completion.
  - US1 (P1) is the MVP and should land first.
  - US2 (P2) depends on US1's event-aware synthesis (re-synthesis pushes the US1 DTO).
  - US3 (P2) depends on the US2 ingest→SSE path (admin rides the same reactive path).
  - US4 (P3) restructures *how* US1–US3 output is produced in LIVE; functional reactivity must exist first.
- **Polish (Phase 7)**: Depends on the user stories it presents — the 3-column layout consumes US1 rendering + US2 banner; the `/admin` restyle consumes US3.

### User Story Dependencies

- **US1 (P1)**: After Foundational. Independent.
- **US2 (P2)**: After Foundational; builds on US1 (re-synthesis reuses the US1 composers).
- **US3 (P2)**: After Foundational; rides the US2 SSE path (FR-016).
- **US4 (P3)**: After Foundational; reframes US1–US3 production in LIVE (additive, parity-preserving).

### Within Each User Story

- Tests (where present) before/alongside implementation; verify they fail first.
- Models → resolver/services → composers/runners → endpoints → UI.
- Story complete before moving to the next priority.

### Parallel Opportunities

- Setup: T002, T003 in parallel.
- Foundational: T008, T009, T011, T012 in parallel (distinct files); T005→T006→T007 are sequential (same store/Program).
- US1: T014/T015 (tests) parallel; T019/T020 parallel; T023 parallel.
- US2: T024 parallel with hub build; T027, T029, T032 parallel.
- US3: T035, T036, T039 parallel.
- US4: T040, T041, T042, T046 parallel; runner wiring T044/T045 parallel after T043.
- Polish: T047, T050, T051 parallel.
- Once Foundational is done, separate developers can take US1 / (US2→US3) / US4 lanes.

---

## Parallel Example: User Story 1

```bash
# Tests first (parallel):
Task: "DEMO determinism + conflict net-score tests in tests/orchestration-api.Tests" (T014)
Task: "No-impact + empty-store tests in tests/orchestration-api.Tests" (T015)

# Tool exposure (parallel, distinct files):
Task: "Add event tools to RmBriefingTools.cs" (T019)
Task: "Add event tools to MorningBriefTools.cs" (T020)
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL) → 3. Phase 3 US1 → **STOP & VALIDATE** SC-001/004/005 → demo the overnight multi-event briefing.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → test → demo (MVP).
3. US2 → test → demo (live intraday push).
4. US3 → test → demo (stage-driven admin injection; both scenes react — SC-006).
5. US4 → test → demo (LIVE fan-out trace — SC-007).
6. Polish → M.INT 3-column dashboard + `/admin` restyle + quality gate.

### Parallel Team Strategy

After Foundational: Dev A → US1; Dev B → US2 then US3; Dev C → US4. Integrate independently; converge in Phase 7.

---

## Notes

- [P] = different files, no incomplete dependencies. [Story] maps tasks to US1–US4 for traceability.
- Honor the three-layer flow and tools-over-HTTP rule on every orchestration task; keep `openapi\tools.yaml` in sync.
- Preserve LIVE/DEMO parity and DEMO byte-stability (SC-004) — additive DTO fields only.
- Phase 7 UI tasks must reuse the landed M.INT theme + primitives (commit a723203); never duplicate them.
- Conventional Commits + `Co-authored-by: Copilot` trailer; quote spec IDs (e.g. FR-010, SC-002, Principle II) in commit messages.
