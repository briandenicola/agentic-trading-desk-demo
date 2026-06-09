# Implementation Plan: Reactive Event Cockpit

**Branch**: `002-reactive-event-cockpit` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-reactive-event-cockpit/spec.md`

## Summary

Make both cockpit scenes **event-reactive**. The mock API gains an in-memory **event store**
seeded with multiple fictional overnight events at startup, plus HTTP operations to list, query,
and ingest events. Both the **RM Daily Briefing** (`POST /api/agent/rm-briefing` → `RmBriefing`,
Commercial Banking) and the **Trading Morning Brief** (`POST /api/agent/morning-brief` →
`MorningBrief`, Capital Markets) synthesize the combined impact of **all current events** and expose
**per-call / per-holding event linkage**, while preserving the **existing DTO shapes** and LIVE/DEMO
parity (constitution Principle III). A new intraday event ingested during the session is pushed to
open briefings over **Server-Sent Events (SSE)**, which trigger a full re-synthesis of the affected
briefing; the UI applies a live alert banner and re-rank in place. A new **`/admin` route** in the
existing React SPA lets an operator compose and inject a news item (which becomes an intraday event)
and view the current event store. In LIVE mode, event processing fans out to **per-event / specialist
agents** combined by a **synthesizer** agent (reframing backlog `011-multi-agent-synthesis`); DEMO
remains a deterministic offline composer. All new agents register idempotently through the existing
`src\agent-provisioner\`, and the feature ships on the existing Sweden Azure Container Apps footprint.
This feature is **additive** — it reuses the CB + Trading Desk fixtures, `RmCallScorer`, the existing
DTOs, the agent provisioner, and the existing deployment.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, `global.json` pinned `10.0.100`, `rollForward:
latestFeature`); `Nullable` + `ImplicitUsings` enabled; TypeScript (React 19, MUI v9, Vite).
**Primary Dependencies**:
- Agents (LIVE): `Microsoft.Agents.AI`, `Microsoft.Agents.AI.AzureAI`, `Azure.AI.Agents.Persistent`
  (`AIProjectClient` / `GetAIAgentAsync`), `Azure.Identity` (`DefaultAzureCredential`) — no new
  packages anticipated; multi-agent fan-out uses the existing Agent Framework surface.
- Web/API: `Microsoft.NET.Sdk.Web` minimal APIs, typed `HttpClient` (`MockApiClient`),
  `System.Text.Json` source-gen options (`RmBriefingJson` / `MorningBriefJson` style).
- SSE: native ASP.NET Core response streaming (`text/event-stream`) — no new server dependency.
- Frontend: React 19, React Router (existing routes in `src\ui-app\src\App.tsx`), MUI v9, Axios for
  request/response, native `EventSource` for the SSE subscription, Vitest/RTL tests.
- Observability: existing `src\shared\Observability` (Serilog + OpenTelemetry) and
  `OrchestrationTelemetry` source/meter — new spans for fan-out reuse the existing pattern.
**Storage**: **In-memory event store inside `src\mock-api\`**, seeded from a new fictional fixture at
startup and reloaded on restart (no persistence). Admin-injected intraday events are lost on
mock-api restart (acceptable for the demo, per spec Assumptions). All existing CB/TD fixtures unchanged.
**Testing**: `xunit` (+ `Microsoft.AspNetCore.Mvc.Testing` WebApplicationFactory) for `.NET`; the
`orchestration-api.Tests` project already hosts the real `mock-api` in-memory via `extern alias
MockApiHost` (Decision #7) so the composer→mock-api HTTP seam is exercised end-to-end; Vitest/RTL for
the React UI and the new `/admin` route.
**Target Platform**: Linux containers on **Azure Container Apps** (Sweden, TF workspace
`stable-gator-8350-rg`, ACR `stablegator8350acr`); local via Docker Compose. SSE traverses the
existing `src\ui-app\nginx.conf` reverse proxy (`/api/` → `${ORCHESTRATION_API_URL}`).
**Project Type**: Web application (React frontend + C# orchestration/mock-api services) + agent
runtime + IaC. Additive to the three existing deployables; **no new container app** is introduced.
**Performance Goals**: An intraday event (including Admin-injected) is reflected in an already-open
briefing — live alert + re-rank — within **10 seconds** of ingestion (SC-002, SC-003). DEMO output is
byte-stable and deterministic across repeated runs from identical seeded events (SC-004).
**Constraints**:
- **Three-layer architecture (Principle II)**: orchestration reaches the event store **only over
  HTTP** through the mock-api surface; no in-process fixture reads. New event tools are added to
  `src\orchestration-api\Agents\Tools\` AND `openapi\tools.yaml` (Principle VI).
- **LIVE/DEMO parity (Principle III)**: identical `RmBriefing` / `MorningBrief` JSON shape in both
  modes for identical event inputs; event data is **additive** to the existing DTOs.
- **All data fictional (Principle IV)**; no real market-data vendors.
- **SSE through Container Apps ingress (FR-013)**: must traverse the UI reverse proxy; WebSocket only
  as a fallback if SSE cannot traverse cleanly.
- **Idempotent agent provisioning (FR-021 / Principle X)**: per-event/specialist + synthesizer agents
  registered through `src\agent-provisioner\Program.cs`.
**Scale/Scope**: ≥3 seeded overnight events spanning CB customers and TD securities; two scenes;
~3–5 new mock-api event operations; ~2–N new Foundry agents (specialist + synthesizer); 1 new SPA
route (`/admin`); no new container app.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against `.specify\memory\constitution.md` v0.2.1 (post ADR-0002 C#/.NET stack). This feature
is **additive** and introduces **no new constitutional violations**; the Principle V (Python/FastAPI)
deviation remains waived by the standing **ADR-0002** amendment, exactly as in
`specs\001-morning-planning-outreach\plan.md` (not re-litigated here).

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Mission Alignment & Spec-Driven | PASS | One feature, traceable to spec FR-001…FR-021 / SC-001…SC-007; follows specify→plan. Reframes backlog `011` explicitly (US4). |
| II. Three-Layer Architecture | PASS | Event store lives in `src\mock-api\`; orchestration reads/writes it **only over HTTP** via `MockApiClient` + new event tools. SSE endpoint is a thin orchestration surface; synthesis logic stays under `src\orchestration-api\Agents\`. |
| III. LIVE/DEMO Mode Parity | PASS | Event data is additive to `RmBriefing` / `MorningBrief`; DEMO composers stay offline + byte-stable (SC-004); LIVE fan-out returns the same shape (FR-009/FR-020). |
| IV. Secrets & Configuration | PASS | Events are fictional; ingestion/SSE config via env vars; no secrets. Admin UI has **no auth hardening** (demo) — see Complexity Tracking. |
| V. .NET & C# Standards | PASS (waived via ADR-0002) | net10.0, central package management, Microsoft Agent Framework + `DefaultAzureCredential` (LIVE only). No per-reference package versions. The Principle-V Python wording is superseded by ADR-0002 (standing amendment), not a new waiver. |
| VI. API-First & Schema-Driven | PASS | New event operations added to `openapi\tools.yaml` **and** `src\orchestration-api\Agents\Tools\`. Contracts published under `contracts/`. |
| VII. Testing Discipline | PASS | xunit unit + WebApplicationFactory integration (composer→mock-api seam), DEMO determinism test per scene, Vitest/RTL for `/admin`. ≥1 DEMO integration test per reactive scene. |
| VIII. Error Handling & Observability | PASS | Event tools return structured `{"error": …}` (never throw); `MAX_TOOL_HOPS` still caps loops; fan-out runs add spans under the existing `OrchestrationTelemetry` source (SC-007 trace). |
| IX. Security Hardening | PASS (with noted demo exception) | Non-root containers + CORS tightening reused; SSE honors existing CORS policy. Admin route deliberately unauthenticated for the demo — tracked. |
| X. Extension Surface | PASS | Follows the documented "new data source" recipe (fixture → loader/endpoint → tool wrapper → `openapi\tools.yaml`) and the agent-provisioner registration path. |
| XI. Commit & Workflow | PASS | Conventional Commits + `Co-authored-by: Copilot` trailer; one task = one PR. |

**Gate result**: **PASS.** No unjustified violations. The only items in Complexity Tracking are
demo-scoped deliberate simplifications (unauthenticated Admin route, in-memory non-durable store)
already sanctioned by the spec's Assumptions section, plus the standing ADR-0002 stack waiver.

## Project Structure

### Documentation (this feature)

```text
specs/002-reactive-event-cockpit/
├── plan.md              # This file (/speckit.plan output)
├── spec.md              # Feature spec (approved)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── event-ingestion.openapi.yaml   # Mock-api event store ops (list/query/ingest) — mirrors openapi/tools.yaml style
│   ├── event-stream.sse.md            # SSE channel contract (event types, payloads, reconnect/Last-Event-ID)
│   ├── live-update.schema.json        # SSE "briefing updated" envelope (alert + re-synthesized DTO)
│   └── admin-submission.schema.json   # Admin News Submission payload + validation rules
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by this command)
```

> The mock system-of-record tool contract remains the repo-root `openapi/tools.yaml`; the new event
> operations are **added there** (Principle VI) and previewed in `contracts/event-ingestion.openapi.yaml`.
> The existing scene response contracts (`specs/001-.../contracts/`) are unchanged — event fields are
> additive.

### Source Code (repository root) — files touched/added by this feature

```text
openapi/tools.yaml                     # + event operations: list_events, get_events_by_entity, ingest_event

src/
├── mock-api/
│   ├── Data/
│   │   └── events_overnight.json       # NEW fictional seed: ≥3 overnight events (CB + TD entities)
│   ├── EventStore.cs                   # NEW in-memory store: seed-on-startup, list/query/ingest, reload-on-restart
│   ├── Endpoints/
│   │   └── EventEndpoints.cs           # NEW /mock/events surface (list, by-entity, ingest) — mirrors Cb/TdEndpoints
│   └── Program.cs                      # + new EventStore() + app.MapEventEndpoints()
│
├── orchestration-api/
│   ├── Agents/
│   │   ├── Tools/
│   │   │   ├── EventTools.cs           # NEW HTTP wrappers: list_events, get_events_by_entity, ingest_event
│   │   │   ├── RmBriefingTools.cs      # (event tools available to RM agent)
│   │   │   └── MorningBriefTools.cs    # (event tools available to morning-brief agent)
│   │   ├── Demo/
│   │   │   ├── RmBriefingComposer.cs   # + apply overnight/intraday events to scoring & per-call linkage
│   │   │   ├── MorningBriefComposer.cs # + apply events to affected securities/holdings & per-item linkage
│   │   │   ├── EventImpactResolver.cs  # NEW pure, deterministic net-score resolver (conflict resolution)
│   │   │   └── RmCallScorer.cs         # (reused unchanged; event delta feeds into Score())
│   │   ├── RmAgentRunner.cs            # LIVE: fan-out to per-event/specialist agents → synthesizer
│   │   ├── AgentRunner.cs              # LIVE: same fan-out for the morning brief
│   │   └── EventSynthesis/             # NEW (LIVE): specialist-agent fan-out + synthesizer orchestration
│   ├── Models/
│   │   ├── RmBriefing.cs               # + additive event-linkage fields (e.g. PriorityCall.DrivingEvents, briefing.EventsConsidered)
│   │   ├── MorningBrief.cs             # + additive event-linkage fields on affected items + EventsConsidered
│   │   └── EventModels.cs              # NEW shared event/linkage/live-update DTOs (mirror mock-api event shape)
│   ├── Live/
│   │   └── BriefingEventStream.cs      # NEW SSE hub: coalesces ingests, re-synthesizes affected briefings, pushes updates
│   ├── Prompts/
│   │   ├── event-specialist.md         # NEW per-event specialist agent instructions
│   │   └── briefing-synthesizer.md     # NEW synthesizer agent instructions
│   └── Program.cs                      # + map SSE endpoint(s) + register EventTools / synthesis / stream services
│
├── agent-provisioner/
│   └── Program.cs                      # + AgentSpec entries for event-specialist + briefing-synthesizer (idempotent)
│
└── ui-app/
    └── src/
        ├── api/client.ts               # + subscribeToEvents(scene) via EventSource; + admin ingest/list calls
        ├── App.tsx                     # + /admin route
        ├── components/
        │   ├── CockpitNav.tsx          # + Admin entry point (nav/link)
        │   └── LiveAlertBanner.tsx     # NEW alert banner driven by SSE updates
        ├── scenes/
        │   ├── Admin/                  # NEW: AdminScene.tsx (compose + inject + event list), NewsForm.tsx, EventList.tsx
        │   ├── RmBriefing/             # + consume SSE: apply alert + re-rank in place
        │   └── MorningBrief/           # + consume SSE: apply alert + re-rank in place
        └── nginx.conf                  # verify SSE pass-through (buffering off for text/event-stream)

tests/
├── orchestration-api.Tests/           # + event-aware DEMO determinism, conflict net-score, SSE re-synthesis, parity
├── mock-api.Tests/                    # + EventStore seed/list/query/ingest contract tests
└── src/ui-app (vitest/RTL)            # + AdminScene validation, LiveAlertBanner, SSE-driven re-rank

infra/                                 # No new container app; verify SSE ingress works through existing ui-app proxy
```

**Structure Decision**: Reuse the established `online-banking-demo`-style layout. The three
constitution layers are unchanged: **Experience** = `src\ui-app` (+ new `/admin` scene), **Agents** =
`src\orchestration-api` (+ `agent-provisioner`), **Mock Data** = `src\mock-api` (+ event store). The
new event store is a **first-class mock-api resource** behind the `/mock/events` HTTP surface so the
orchestration layer reaches it only over HTTP (Principle II). The SSE channel is a thin orchestration
endpoint that re-runs the existing composers/runners and streams the resulting DTO; **no new
deployable** is added, keeping the Sweden Container Apps footprint and the agent-provisioner model intact.

## Complexity Tracking

> The Constitution Check has **no unjustified violations**. The rows below record deliberate,
> spec-sanctioned demo simplifications (spec §Assumptions) so they are visible and revisitable —
> they are not Principle waivers requiring an ADR.

| Decision / Deviation | Why Needed (demo) | Simpler/Stricter Alternative Rejected Because |
|----------------------|-------------------|-----------------------------------------------|
| In-memory, non-durable event store (admin items lost on restart) | "Art of the possible" demo; seeded overnight events reload, intraday items are ephemeral (spec Assumptions). | Persisted store (Cosmos/SQLite) adds infra + state management out of scope for the demo and unneeded for SC-001…SC-007. |
| Unauthenticated `/admin` route (no auth hardening) | Demo is driven live on stage; the operator is trusted (spec Assumptions / US3). | Entra ID gating on `/admin` is out of scope; JWT scaffolding already exists and can be enabled later without shape changes. |
| Full re-synthesis per intraday event (not client-side incremental diffs) | Guarantees LIVE/DEMO parity and explainability (SC-004/SC-005) by re-running the same composer/runner. | Client-side incremental diffing would diverge from the canonical DTO and complicate parity/traceability. |
| SSE chosen over WebSocket | One-way server→client is sufficient for alerts + re-rank and traverses the existing nginx/ACA ingress simply (FR-013). | WebSocket adds bidirectional complexity and proxy config; kept only as a documented fallback if SSE cannot traverse. |

## Phase 0 — Research

See [research.md](./research.md). Resolves: SSE-through-ACA/nginx mechanics (buffering, Last-Event-ID
reconnect, coalescing), event→entity mapping across CB customers and TD securities/holdings, the
deterministic net-score conflict-resolution model, additive DTO design that preserves parity, and the
LIVE multi-agent fan-out + synthesizer topology (reframing backlog `011`).

## Phase 1 — Design & Contracts

Outputs: [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md),
and the agent-context update. All NEEDS CLARIFICATION resolved in Phase 0.

## Phase 2 — (Deferred)

`tasks.md` is generated by `/speckit.tasks`, not this command.
