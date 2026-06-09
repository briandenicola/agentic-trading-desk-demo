# Feature Specification: Reactive Event Cockpit

**Feature Branch**: `002-reactive-event-cockpit`
**Created**: 2026-06-09
**Status**: Draft
**Input**: User description: "The client wants the demo to react to multiple events over night — not just one event — and they want the app to react to new events over the day. We need to update the agents to be reactive to new events coming in and we need an Admin UI to manually inject news items into the system the agents will react to."

> **Reframes backlog `011-multi-agent-synthesis`.** The per-event multi-agent fan-out + synthesizer
> topology from card 011 is folded into this feature as the mechanism that makes the cockpit
> event-reactive. Card 011's acceptance criteria (independently traceable specialist agents,
> synthesizer producing the unchanged DTO, concurrent fan-out, full graph trace, DEMO parity)
> are carried forward here under User Story 4.

## User Scenarios & Testing *(mandatory)*

Both cockpit scenes are in scope and must react to events:

- **RM Daily Briefing** (PRIMARY, `/`) — Commercial Banking. Events affect **customers** (CB-100xx)
  via their industry/sector, region, exposure, opportunities, and complaints.
- **Trading Morning Brief** (SECONDARY, `/morning-brief`) — Capital Markets. Events affect
  **securities / holdings** (tickers, sectors, issuers) and the clients that hold them.

### User Story 1 - Overnight multi-event briefing (Priority: P1)

When the RM or trader opens their morning briefing, the agents have already processed **multiple
overnight events** (not a single hand-picked headline). The briefing synthesizes the combined
impact of every overnight event across the portfolio, and each ranked call / flagged holding shows
**which event(s)** drove it.

**Why this priority**: This is the headline client ask ("react to multiple events overnight") and
the MVP. It is demonstrable on its own without live push or the admin UI — simply seed several
overnight events and show a briefing that reflects all of them.

**Independent Test**: Seed ≥3 overnight events (e.g., a rate move, a sector downgrade, a
client-specific headline). Open each scene and confirm the brief reflects all of them, with
per-event linkage visible on the affected calls/holdings, and that scoring/ranking changed versus
a no-event baseline.

**Acceptance Scenarios**:

1. **Given** ≥3 overnight events are present in the event store, **When** the RM opens the RM Daily
   Briefing, **Then** the briefing lists the overnight events considered and each affected priority
   call shows the event(s) that influenced its rank/priority.
2. **Given** the same overnight events, **When** the trader opens the Trading Morning Brief,
   **Then** affected securities/holdings are surfaced with their driving event(s).
3. **Given** an event that affects no portfolio entity, **When** a briefing is generated, **Then**
   that event is acknowledged as "no portfolio impact" and does not alter any ranking.
4. **Given** DEMO mode, **When** the briefing is generated twice from the same seeded events,
   **Then** the output is byte-stable and deterministic (offline, no Foundry).

---

### User Story 2 - Intraday live event push (Priority: P2)

While a briefing is open on screen, a **new event arrives during the day**. The open briefing
updates **automatically** — a live alert banner announces the new event, and the affected calls /
holdings are re-ranked in place — without the user manually reloading.

**Why this priority**: Delivers the "react to new events over the day" half of the ask and is the
"art of the possible" wow moment. Depends on US1's event-aware synthesis being in place.

**Independent Test**: Open a briefing, post a new intraday event to the ingestion endpoint, and
observe the open page receive a live alert and re-ranked content within the target latency, with no
manual refresh.

**Acceptance Scenarios**:

1. **Given** an RM has the RM Daily Briefing open, **When** a new intraday event affecting one of
   their customers is ingested, **Then** within the target latency the page shows a live alert and
   the affected call moves to its new rank/priority.
2. **Given** a trader has the Trading Morning Brief open, **When** an intraday event affecting a
   held security is ingested, **Then** the page shows a live alert and the affected holding is
   re-surfaced/re-ranked.
3. **Given** an intraday event with no portfolio impact, **When** it is ingested, **Then** open
   briefings show a low-priority "new event, no impact" alert and rankings are unchanged.
4. **Given** the live push transport drops, **When** the connection is re-established, **Then** the
   briefing reconciles to the current event-aware state (no missed-update divergence).

---

### User Story 3 - Admin news injection UI (Priority: P2)

An operator uses an **Admin UI** to manually compose and inject a news item into the system. The
injected item becomes an event the agents react to, triggering the same reactive update path as a
real intraday event (US2).

**Why this priority**: This is how the demo is driven live on stage — it lets the presenter inject a
headline and watch both cockpits react. It rides on the US2 push path.

**Independent Test**: From the Admin UI, submit a news item targeting a known customer/security,
then confirm it appears in the event store and that an open briefing reacts (alert + re-rank).

**Acceptance Scenarios**:

1. **Given** the Admin UI, **When** the operator submits a news item with a headline, summary,
   source, severity, and affected entities, **Then** the item is ingested as an intraday event and
   acknowledged in the UI.
2. **Given** a briefing is open in another tab/window, **When** the operator injects a relevant news
   item, **Then** that briefing reacts live (per US2).
3. **Given** an incomplete submission (missing headline or affected entities), **When** the operator
   submits, **Then** the Admin UI rejects it with a clear validation message and nothing is ingested.
4. **Given** the Admin UI, **When** the operator views it, **Then** they can see the list of events
   currently in the system (overnight + injected) for situational awareness.

---

### User Story 4 - Per-event multi-agent fan-out + synthesizer (Priority: P3)

Event processing is distributed across **per-event / specialist agents** that each assess one event's
impact and return structured data to a central **synthesizer** agent, which composes the final
briefing DTO. This makes each event's contribution independently traceable as an agent graph rather
than one tool-calling loop. (Reframes card 011.)

**Why this priority**: Deepens the agent architecture and traceability story. The reactive behavior
(US1–US3) must work first; this restructures *how* it is produced. Builds on observability (005).

**Independent Test**: Generate an event-aware briefing in LIVE mode and confirm the trace shows the
synthesizer fanning out to per-event/specialist agents and collecting their structured impact before
emitting the DTO; confirm DEMO mode produces the same DTO shape deterministically and offline.

**Acceptance Scenarios**:

1. **Given** multiple events, **When** a LIVE briefing is generated, **Then** each event is assessed
   by an independently traceable agent run and the synthesizer composes the unchanged briefing DTO.
2. **Given** events with no inter-dependency, **When** processed, **Then** their per-event agent runs
   execute concurrently.
3. **Given** the same events, **When** DEMO mode runs, **Then** it produces the same briefing shape
   deterministically with no Foundry calls.
4. **Given** a full trace, **When** inspected, **Then** the graph (synthesizer → per-event/specialist
   agents → tool calls) is visible end-to-end.

### Edge Cases

- **Conflicting events**: two overnight events push the same customer/security in opposite directions
  → the synthesizer applies a defined, deterministic resolution (net score) and both events remain
  visible as drivers.
- **Event storm**: many intraday events arrive in quick succession → updates are coalesced so the
  briefing is not thrashed; the user sees a consolidated alert rather than one per event.
- **Stale open page**: a briefing left open for a long time, then a new event arrives → it still
  receives the live update (or reconciles on reconnect).
- **Event affecting an entity outside the current persona's book** (e.g., a customer not assigned to
  the open RM) → it does not alter that persona's briefing but remains in the global event store.
- **Empty event store** (no overnight events) → briefings render the baseline (pre-event) ranking and
  state "no events processed".
- **Admin injects a duplicate** of an existing headline → handled idempotently or flagged, not
  double-counted.
- **Mock-api restart** mid-demo → the in-memory event store reloads its seeded overnight events;
  admin-injected items from the prior run are gone (acceptable for the demo).

## Requirements *(mandatory)*

### Functional Requirements

**Event ingestion & store**

- **FR-001**: System MUST maintain an event store that holds multiple events, each tagged as
  `overnight` (seeded) or `intraday` (arriving during the session), served over HTTP via the mock API
  surface (tools over HTTP; no in-process fixture reads by orchestration).
- **FR-002**: System MUST seed **multiple fictional overnight events** at startup that affect both CB
  customers and Capital Markets securities/holdings.
- **FR-003**: System MUST expose an **event ingestion** operation that accepts a new event (intraday)
  and adds it to the store, returning an acknowledgement.
- **FR-004**: System MUST expose operations to **list current events** and to **query events by
  affected entity** (customer / security / sector) so agents and the Admin UI can read them.
- **FR-005**: Each event MUST identify the **entities it affects** (e.g., tickers, sectors, issuers,
  customerIds) so impact can be mapped to portfolio holdings and customer books.

**Event-reactive briefings (both scenes)**

- **FR-006**: Both the RM Daily Briefing and the Trading Morning Brief MUST synthesize the combined
  impact of **all current events** (overnight + any intraday) when generated.
- **FR-007**: Each ranked call / flagged holding in a briefing MUST expose the **event(s)** that
  drove its rank/priority (per-event linkage), and the briefing MUST list the events it considered.
- **FR-008**: Event impact MUST influence the existing scoring/ranking so that adding a relevant
  event changes the resulting order versus a no-event baseline, in a defined and explainable way.
- **FR-009**: Briefings MUST continue to emit the **existing DTO shapes** (`RmBriefing`,
  `MorningBrief`); event data is additive and MUST preserve **LIVE/DEMO parity** (same JSON shape in
  both modes for the same inputs).

**Live intraday push**

- **FR-010**: An open briefing MUST receive **live updates** when a new intraday event is ingested,
  without the user manually reloading the page.
- **FR-011**: A live update MUST surface a **visible alert** describing the new event and apply the
  **re-ranked** calls/holdings in place.
- **FR-012**: Live updates MUST **coalesce** bursts of events so the briefing is not thrashed, and an
  open page MUST **reconcile** to current state after a dropped/restored connection.
- **FR-013**: The live-push path MUST work through the deployed Container Apps ingress (the UI's
  reverse proxy) as well as locally.

**Admin injection UI**

- **FR-014**: System MUST provide an **Admin UI** where an operator can compose a news item
  (headline, summary, source, severity, affected entities, scene/portfolio targeting) and inject it.
- **FR-015**: The Admin UI MUST **validate** submissions (e.g., require headline and at least one
  affected entity) and reject incomplete ones with a clear message, ingesting nothing on failure.
- **FR-016**: A successful injection MUST flow through the same ingestion + reactive path as a real
  intraday event (FR-003, FR-010), causing open briefings to react.
- **FR-017**: The Admin UI MUST display the **current events** in the system (overnight + injected)
  for situational awareness.

**Agent topology & traceability (reframes 011)**

- **FR-018**: LIVE mode MUST process events via **per-event / specialist agents** whose results are
  combined by a **synthesizer** agent that emits the briefing DTO; each agent run MUST be
  independently traceable in Foundry.
- **FR-019**: Independent (non-dependent) per-event agent runs MUST be able to execute
  **concurrently**.
- **FR-020**: DEMO mode MUST remain a **deterministic, offline** composer that produces the same
  event-aware DTO shape without calling Foundry.
- **FR-021**: All event/specialist agents MUST be registered idempotently through the agent
  provisioner (persistent registration), consistent with the existing provisioning model.

### Key Entities *(include if feature involves data)*

- **Event**: A fictional news/market signal. Attributes: id, type (e.g., macro/rate, sector,
  issuer/credit, client headline), headline, summary, source, publishedAt, ingestedAt, severity,
  scope (`overnight` | `intraday`), origin (`seed` | `admin` | `feed`), and affected entities
  (tickers / sectors / issuers / customerIds). Relationships: maps to one or more portfolio
  entities (customers, securities, holdings).
- **Event Impact / Linkage**: The derived connection between an Event and an affected portfolio entity
  (customer or holding), including the rationale and the contribution it makes to that entity's
  score/priority. Drives the per-call / per-holding "why" shown in the briefing.
- **Briefing (existing `RmBriefing` / `MorningBrief`)**: Now event-aware — references the events it
  considered and exposes per-call/per-holding event linkage. DTO shape unchanged otherwise.
- **Live Update / Alert**: A pushed message to open briefings describing what changed (new event +
  resulting re-rank), used to drive the alert banner and in-place updates.
- **Admin News Submission**: The operator-authored payload from the Admin UI that becomes an
  intraday Event on injection.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With ≥3 seeded overnight events, an opened briefing (each scene) reflects **all** of
  them, and every affected call/holding shows ≥1 linked event.
- **SC-002**: A new intraday event is reflected in an **already-open** briefing (alert + re-rank)
  within **10 seconds** of ingestion, with no manual reload.
- **SC-003**: A news item injected from the Admin UI produces a live reaction in an open briefing
  within **10 seconds**, end-to-end.
- **SC-004**: For identical event inputs, **DEMO and LIVE return the same DTO shape**, and DEMO is
  byte-stable across repeated runs (LIVE/DEMO parity preserved).
- **SC-005**: For any briefing, **100% of event-driven rank changes are explainable** — each affected
  item names the event(s) responsible.
- **SC-006**: **Both** scenes (RM Daily Briefing and Trading Morning Brief) demonstrably react to the
  same injected event (each against its own portfolio domain).
- **SC-007**: A LIVE trace shows the **synthesizer → per-event/specialist agent → tool-call** graph
  for a multi-event briefing.

## Assumptions

Reasonable defaults chosen where the request did not specify (open for review):

- **Event store durability**: events live **in-memory in the mock API**, seeded at startup; this is
  sufficient for the demo. Admin-injected items are lost on mock-api restart; seeded overnight events
  reload. (Persisted storage is out of scope — this is "art of the possible", not a hardened
  production site.)
- **Live-push transport**: **Server-Sent Events (SSE)** from the orchestration/mock surface through
  the UI reverse proxy (one-way server→client is sufficient for live alerts + re-ranking). WebSocket
  is an acceptable alternative if SSE does not traverse the proxy cleanly.
- **Admin UI location**: a **new `/admin` route in the existing React SPA** (not a separate app),
  with **no auth hardening** (demo only). Tightening is out of scope.
- **Reaction granularity**: an intraday event triggers a **full re-synthesis** of the affected
  briefing returning the updated DTO; the UI applies the alert + re-rank from that result (rather than
  computing incremental diffs client-side).
- **Scope of events**: events are **fictional** and affect fictional customers/securities only — no
  real market-data vendors are wired (constitution hard rule).
- **Persona scope**: live updates target briefings whose persona/book includes an affected entity;
  events outside a persona's book stay in the global store but do not alter that persona's briefing.
- **Reuse**: the existing CB and Trading Desk fixtures, scoring (`RmCallScorer`), DTOs, agent
  provisioner, and Sweden Container Apps deployment are reused; this feature is additive.
- **Conflict resolution**: when multiple events affect one entity, their score contributions **net**
  deterministically and all contributing events remain visible as drivers.
