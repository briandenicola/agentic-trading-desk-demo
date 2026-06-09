# Phase 0 — Research: Reactive Event Cockpit

**Feature**: `002-reactive-event-cockpit` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This document resolves the open technical questions before design. The spec's **Assumptions** section
already locked the major product defaults (in-memory store, SSE, `/admin` route, full re-synthesis,
reuse, fan-out); the research below confirms the *mechanism* for each and records the chosen approach,
rationale, and rejected alternatives. **No `NEEDS CLARIFICATION` markers remain** (see the final
section).

---

## R1. Event store location & lifecycle

- **Decision**: Hold events in an **in-memory `EventStore` inside `src\mock-api\`**, seeded at startup
  from a new fictional fixture `src\mock-api\Data\events_overnight.json` (≥3 overnight events spanning
  CB customers and TD securities/holdings). Expose it behind a `/mock/events` HTTP surface
  (`EventEndpoints.cs`) so orchestration reads/writes it **only over HTTP** (Principle II). On
  mock-api restart the seed reloads; admin-injected intraday events are lost (spec Assumptions).
- **Rationale**: Mirrors the existing `CbDataStore` / `TdDataStore` pattern (constructed in
  `Program.cs`, loaded from `Data\*.json`), keeps the event store a first-class mock-api resource on
  the swappable HTTP seam, and satisfies FR-001/FR-002 with zero new infra.
- **Alternatives considered**:
  - *Persisted store (Cosmos/SQLite)* — rejected: durability is explicitly out of scope (spec
    Assumptions); adds infra + state management for no demo benefit.
  - *Event store inside orchestration-api memory* — rejected: would let orchestration read data
    in-process, violating Principle II and the "tools over HTTP" rule.

## R2. Live-push transport (SSE vs WebSocket) through Container Apps + nginx

- **Decision**: **Server-Sent Events (SSE)** over `text/event-stream` from an orchestration endpoint,
  consumed by the browser's native `EventSource`. WebSocket is a **documented fallback only**, used
  if SSE cannot traverse the proxy cleanly.
- **Rationale**: The reactive requirement is one-way server→client (alert + re-ranked DTO), which SSE
  serves with minimal moving parts; `EventSource` gives automatic reconnect + `Last-Event-ID`
  out of the box (supports FR-012 reconcile-on-reconnect). It rides the existing single
  `/api/` reverse-proxy hop, so no new ingress is needed (FR-013).
- **nginx / ACA specifics** (verified against `src\ui-app\nginx.conf`): the existing `/api/` location
  already sets `proxy_http_version 1.1` and `proxy_ssl_server_name on`. For the SSE location the proxy
  MUST additionally **disable response buffering** and idle timeouts so events flush immediately:
  `proxy_buffering off;`, `proxy_cache off;`, `proxy_read_timeout` raised (e.g. `3600s`), and
  `Connection ''` (clear the hop-by-hop close). Azure Container Apps ingress supports long-lived HTTP
  streaming responses; no special ACA setting beyond the existing external ingress on `ui-app` and
  internal ingress on `orchestration-api`.
- **Alternatives considered**:
  - *WebSocket* — rejected as primary: bidirectional channel is unnecessary, adds `Upgrade`/`Connection`
    proxy handling and a client library; kept as fallback per spec.
  - *Client polling* — rejected: would not meet the "no manual reload / live" UX and wastes the
    10-second budget (SC-002) on poll intervals.

## R3. Update coalescing & burst handling (FR-012, Edge: event storm)

- **Decision**: The orchestration SSE hub (`BriefingEventStream.cs`) **debounces** ingest
  notifications per affected persona/scene over a short window (e.g. ~750 ms, configurable via env)
  and emits **one consolidated re-synthesis + one consolidated alert** listing all events in the
  burst, rather than one push per event.
- **Rationale**: Prevents UI thrash (spec Edge "event storm"); a single re-synthesis already reflects
  the full current event set because composers read **all** current events (FR-006), so coalescing is
  natural — the latest synthesis supersedes earlier ones.
- **Alternatives considered**:
  - *Per-event push* — rejected: thrashes ranking and produces an alert spam stream.
  - *Fixed-interval batch* — rejected: adds latency even when only one event arrives, risking SC-002.

## R4. Reconnect & reconciliation (FR-012, Edge: stale open page)

- **Decision**: Each SSE message carries a monotonic **`id:` (event sequence)**; on reconnect the
  browser sends `Last-Event-ID`, and the hub responds by **re-synthesizing the current briefing** and
  pushing it as the next message (snapshot-on-reconnect). The client always renders the latest
  full DTO, so no missed-update divergence is possible.
- **Rationale**: Because every update is a **full re-synthesis returning the complete DTO** (spec
  Assumptions: reaction granularity), reconciliation is trivial — the newest snapshot is
  authoritative. Satisfies FR-012 and the "stale open page" edge case.
- **Alternatives considered**:
  - *Replay missed deltas* — rejected: deltas aren't stored (in-memory ephemeral) and full-snapshot
    reconciliation is simpler and parity-safe.

## R5. Event → portfolio-entity mapping (both scenes)

- **Decision**: Each `Event` declares `affectedEntities` with typed selectors:
  **`customerIds`** (CB-100xx) for the RM scene, and **`tickers` / `sectors` / `issuers`** for the TD
  scene. Mapping logic:
  - **RM Daily Briefing**: match events to customers in the RM's book (`/mock/cb` customers) by
    `customerId` directly, or by `sector`/`industry` and `region` for broader macro/sector events.
  - **Trading Morning Brief**: match events to held securities (`/mock/td` securities/holdings) by
    `ticker`, then widen by `sector`/`issuer`; surface the clients holding the affected security.
  - An event matching **no** portfolio entity is recorded as **"no portfolio impact"** and does not
    alter ranking (FR per US1 AS3 / US2 AS3 / Edge "no impact").
- **Rationale**: Keeps mapping deterministic and explainable (SC-005) using identifiers already
  present in the CB/TD fixtures; honors persona-scope (events outside a book stay global but don't
  alter that persona's briefing — spec Assumptions / Edge).
- **Alternatives considered**:
  - *Free-text NLP matching of headlines to entities* — rejected for DEMO: non-deterministic, breaks
    SC-004 byte-stability. (LIVE specialist agents may reason richly, but must resolve to the same
    typed selectors so the emitted DTO shape and linkage remain parity-stable.)

## R6. Event impact on scoring & conflict resolution (FR-008, Edge: conflicting events)

- **Decision**: Introduce a pure, deterministic **`EventImpactResolver`** in
  `src\orchestration-api\Agents\Demo\`. Each matched event contributes a signed **event-relevance
  delta** to the affected entity; when multiple events hit one entity the deltas **net** (sum)
  deterministically, and **every contributing event remains visible** as a driver on that item.
  - **RM scene**: the netted event delta is added on top of the existing `RmCallScorer.Score(...)`
    signal (the scorer itself is reused unchanged); ranking re-sorts by the combined score.
  - **Trading scene**: events adjust the existing affected-client / holding ranking the morning-brief
    composer already produces (the established `0.40 wallet / 0.30 engagement / 0.30 event-relevance`
    blend, Decision #7, already reserves an event-relevance component — events now populate it).
- **Rationale**: Reuses the locked, ground-truth-derived scoring (no re-derivation), keeps DEMO
  byte-stable, and makes conflict resolution explainable: net score + all drivers shown (SC-005,
  Edge "conflicting events").
- **Alternatives considered**:
  - *Last-event-wins / max-severity-wins* — rejected: discards drivers, less explainable, and the
    spec mandates "net deterministically and all contributing events remain visible".
  - *Re-weighting the whole RmCallScorer* — rejected: would alter the locked baseline ranking and risk
    parity with the ground-truth sample.

## R7. Additive DTO design preserving parity (FR-007, FR-009, SC-004)

- **Decision**: Extend the **existing** `RmBriefing` and `MorningBrief` records **additively**:
  - Briefing root: add `EventsConsidered` (the list of events the synthesis weighed, overnight +
    intraday), as a new optional/required-with-default collection.
  - Per affected item: add a **driving-events linkage** collection
    (`PriorityCall.DrivingEvents` on the RM scene; an equivalent on the morning-brief affected
    client/holding), each entry naming the event id, headline, and its score contribution + rationale.
  - DEMO and LIVE both populate these from the same `EventImpactResolver` output so the JSON shape is
    identical (Principle III).
- **Rationale**: No existing field changes type or meaning; new fields are present in both modes →
  parity holds and SC-004 byte-stability is preserved for fixed seeds. Front-end remains mode-blind.
- **Alternatives considered**:
  - *New parallel DTO (e.g. `EventAwareRmBriefing`)* — rejected: violates FR-009 (must emit the
    existing DTO shapes) and forces UI/scene rework.
  - *Side-channel events array decoupled from items* — rejected: loses per-item "why" required by
    SC-005.

## R8. LIVE multi-agent fan-out + synthesizer topology (US4, reframes backlog 011)

- **Decision**: In LIVE mode the briefing run **fans out one specialist/per-event agent run per event**
  (or per event-cluster), each assessing that event's impact and returning structured linkage; a
  central **synthesizer agent** combines them and emits the unchanged briefing DTO. Independent
  per-event runs execute **concurrently** (`Task.WhenAll`), bounded by `MAX_TOOL_HOPS` and a
  concurrency cap. Agents are wrapped with the existing
  `.AsBuilder().UseOpenTelemetry(sourceName: OrchestrationTelemetry.SourceName, …)` so the
  synthesizer → per-event agent → tool-call graph is fully traceable in Foundry (SC-007).
- **Rationale**: Realizes backlog `011-multi-agent-synthesis` as the mechanism for event-reactivity
  (FR-018/FR-019), reuses the existing Agent Framework resolve-by-name pattern (`GetAIAgentAsync` with
  fallback `CreateAIAgentAsync`) in `RmAgentRunner` / `AgentRunner`, and keeps observability via the
  existing `OrchestrationTelemetry` source/meter (no new packages).
- **DEMO equivalent**: the deterministic composers simulate the same fan-out structurally (resolve
  each event's impact, then compose) **offline with no Foundry** (FR-020), emitting the identical DTO
  shape (SC-004).
- **Alternatives considered**:
  - *Keep the single tool-calling loop* — rejected: doesn't satisfy US4's independently-traceable
    per-event runs or the 011 reframe.
  - *One agent per data source (not per event)* — rejected: the spec's traceability unit is the
    **event** (each affected item must name its driving event), so per-event/specialist runs are the
    right granularity.

## R9. Agent provisioning for the new agents (FR-021)

- **Decision**: Add `AgentSpec` entries for **`event-specialist`** and **`briefing-synthesizer`** to
  the `src\agent-provisioner\Program.cs` loop, with instructions from new
  `src\orchestration-api\Prompts\event-specialist.md` and `briefing-synthesizer.md`. Registration
  stays **idempotent** (the loop calls `GetAIAgentAsync(name, …)` first and only `CreateAIAgentAsync`
  when absent), consistent with the existing `rm-daily-briefing` / `morning-brief` registration.
- **Rationale**: Honors the persistent, idempotent provisioning model (FR-021, Principle X) and the
  architecture doc's "single registrar" statement; runtime runners resolve the new agents by name.
- **Alternatives considered**:
  - *Create agents inline at request time only* — rejected: breaks the persistent-registration model
    and the one-agent-per-name Foundry portal view described in `docs\architecture.md`.

## R10. Admin UI placement & validation (US3, FR-014/FR-015/FR-017)

- **Decision**: Add a new **`/admin` route** to the existing SPA (`src\ui-app\src\App.tsx`), reachable
  from `CockpitNav.tsx`. `AdminScene` provides a **NewsForm** (headline, summary, source, severity,
  affected entities, scene/portfolio targeting) and an **EventList** of current events (FR-017).
  Client-side validation rejects submissions missing a headline or ≥1 affected entity with a clear
  message and ingests nothing (FR-015); the server re-validates on ingest. A successful submit POSTs
  to the **same ingestion endpoint** a real intraday event uses (FR-016), so open briefings react via
  the SSE path (US2).
- **Rationale**: Keeps it one SPA (spec Assumptions), reuses the existing Axios `client.ts` + routing,
  and routes admin injections through the identical reactive path for a single code path.
- **Alternatives considered**:
  - *Separate admin app/deployable* — rejected: spec says new route in the existing SPA; avoids a
    fourth container app.
  - *Direct store mutation bypassing ingestion* — rejected: would diverge admin from real intraday
    behavior (FR-016).

## R11. Empty / duplicate / out-of-book edge cases

- **Decision**:
  - **Empty store**: briefings render the **baseline pre-event ranking** and state "no events
    processed" (Edge / US1 AS handling).
  - **Duplicate injection**: ingestion is **idempotent by event identity** (dedupe by a stable key —
    e.g. normalized headline + source + affected set); duplicates are flagged, not double-counted
    (Edge).
  - **Out-of-book event**: stays in the global store but does **not** alter a persona whose book it
    doesn't touch (spec Assumptions / Edge).
- **Rationale**: Direct, deterministic handling of each enumerated edge case; keeps scoring honest and
  avoids double-counting drivers.

---

## Open Questions / NEEDS CLARIFICATION

**None.** All spec `Assumptions` are treated as decided design defaults (per task instruction), and
the mechanisms above resolve every technical unknown surfaced while planning. Two demo-scoped
simplifications (non-durable store, unauthenticated `/admin`) are recorded in
[plan.md → Complexity Tracking](./plan.md#complexity-tracking) for visibility; they are sanctioned by
the spec and are **not** blocking clarifications.

### Watch-items carried into implementation (not blockers)

1. **SSE nginx tuning** (R2) — must verify `proxy_buffering off` + raised read-timeout traverse ACA
   ingress in the deployed environment; WebSocket fallback documented if not.
2. **LIVE Agent Framework obsolete-API surface** — Decision #7 notes `CreateAIAgentAsync` is
   `[Obsolete]` in the current AzureAI RC; the new fan-out reuses the same isolated path and inherits
   that tracked risk (revisit on GA).
3. **Concurrency cap for fan-out** (R8) — choose a sensible default bound (env-configurable) to avoid
   Foundry rate pressure during an event storm.
