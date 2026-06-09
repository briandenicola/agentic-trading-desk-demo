# Phase 1 — Data Model: Reactive Event Cockpit

**Feature**: `002-reactive-event-cockpit` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

All entities are **fictional** (Principle IV). New event types live in the mock-api (the store) and in
the orchestration-api (`Models\EventModels.cs`, mirroring the mock-api shape over the HTTP seam). The
existing scene DTOs (`RmBriefing`, `MorningBrief`) are extended **additively only** — no existing
field changes type or meaning, preserving LIVE/DEMO parity (Principle III, FR-009). Serialization
follows the existing `RmBriefingJson` / `MorningBriefJson` conventions (camelCase, omit-null).

---

## 1. Event (mock-api store entity → orchestration `MarketEvent` DTO)

The core entity (spec "Key Entities → Event"). Lives in `EventStore` (`src\mock-api\`), seeded from
`Data\events_overnight.json`, and is the payload of the `/mock/events` operations.

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | Stable event id (e.g. `evt-20260609-001`). Dedup key component. |
| `type` | string (enum) | `macro_rate` \| `sector` \| `issuer_credit` \| `client_headline`. |
| `headline` | string | Short title (required for admin submissions). |
| `summary` | string | 1–3 sentence body. |
| `source` | string | Fictional source label (e.g. "Garage Wire"). |
| `severity` | string (enum) | `low` \| `medium` \| `high`. Drives alert priority + magnitude band. |
| `publishedAt` | string (ISO-8601) | When the event "occurred". |
| `ingestedAt` | string (ISO-8601) | When added to the store (server-set). |
| `scope` | string (enum) | `overnight` (seeded) \| `intraday` (arriving in session). |
| `origin` | string (enum) | `seed` \| `admin` \| `feed`. |
| `affectedEntities` | `AffectedEntities` | Typed selectors (below). At least one selector required. |
| `direction` | string (enum, optional) | `positive` \| `negative` \| `neutral` — sign of the impact. |

### 1a. AffectedEntities (selectors → portfolio mapping, R5)

| Field | Type | Maps to |
|-------|------|---------|
| `customerIds` | string[] | CB customers (`/mock/cb`, CB-100xx) — RM Daily Briefing. |
| `tickers` | string[] | TD securities (`/mock/td` securities/holdings) — Trading Morning Brief. |
| `sectors` | string[] | CB industry/sector and TD sector — both scenes (widening match). |
| `issuers` | string[] | TD issuers — Trading Morning Brief. |

**Validation rules** (FR-005, FR-015):
- `headline` non-empty; `summary` non-empty.
- `affectedEntities` MUST contain ≥1 non-empty selector across the four arrays.
- `severity`, `type`, `scope`, `origin` MUST be one of the enum values.
- `scope=intraday` + `origin=admin` for Admin-injected events; `scope=overnight` + `origin=seed` for
  the startup seed.

**State / lifecycle** (R1):
- Seed → `overnight` events loaded at mock-api startup (reload on restart).
- Ingest → `intraday` event appended (deduped by identity, R11); lost on restart.
- No update/delete in the demo (append + reseed only).

---

## 2. EventImpact / Linkage (derived; spec "Event Impact / Linkage")

Computed by the deterministic `EventImpactResolver` (DEMO) or the specialist agents (LIVE), then
surfaced on each affected briefing item. **Not stored** — recomputed each synthesis.

| Field | Type | Notes |
|-------|------|-------|
| `eventId` | string | The driving event. |
| `headline` | string | Echoed for display (no extra lookup in the UI). |
| `entityRef` | string | Affected portfolio entity (customerId / ticker / issuer). |
| `contribution` | number | Signed score delta this event added to the entity (net-able). |
| `rationale` | string | Human-readable "why" (SC-005). |

**Conflict resolution** (R6, Edge "conflicting events"): when multiple impacts hit one entity, their
`contribution` values **net (sum)** deterministically; **all** contributing impacts remain listed as
drivers on that item.

---

## 3. Additive extensions to existing scene DTOs

### 3a. `RmBriefing` (src\orchestration-api\Models\RmBriefing.cs) — additive fields

| New field | Type | On record | Notes |
|-----------|------|-----------|-------|
| `EventsConsidered` | `IReadOnlyList<MarketEvent>` | `RmBriefing` | All current events the synthesis weighed (overnight + intraday). Empty ⇒ "no events processed". |
| `DrivingEvents` | `IReadOnlyList<EventLinkage>` | `PriorityCall` | Per-call linkage; each names the event(s) that changed this call's rank/priority (FR-007, SC-005). Empty for un-affected calls. |

> The existing `PriorityCall.Score` continues to come from `RmCallScorer.Score(...)`; the netted event
> `contribution` is added on top before ranking (R6). No existing `RmBriefing` field changes.

### 3b. `MorningBrief` (src\orchestration-api\Models\MorningBrief.cs) — additive fields

| New field | Type | On record | Notes |
|-----------|------|-----------|-------|
| `EventsConsidered` | `IReadOnlyList<MarketEvent>` | `MorningBrief` | All current events weighed. |
| `DrivingEvents` | `IReadOnlyList<EventLinkage>` | `AffectedClient` | Per-affected-client/holding linkage (FR-007). |

> The existing morning-brief ranking blend (`0.40 wallet / 0.30 engagement / 0.30 event-relevance`,
> Decision #7) already reserves an event-relevance component; events now populate it via the resolver.
> `OutreachItem` / `RankingRationale` are unchanged in shape.

**Parity note (SC-004, FR-009)**: both DEMO composers and LIVE runners populate `EventsConsidered` +
`DrivingEvents` from the same resolver output, so the JSON shape is identical in both modes and
byte-stable for fixed seeds.

---

## 4. LiveUpdate / Alert (SSE message; spec "Live Update / Alert")

The envelope pushed over SSE to open briefings (R2–R4). See
[`contracts/live-update.schema.json`](./contracts/live-update.schema.json) and
[`contracts/event-stream.sse.md`](./contracts/event-stream.sse.md).

| Field | Type | Notes |
|-------|------|-------|
| `sequence` | number | Monotonic; emitted as the SSE `id:` for `Last-Event-ID` reconnect (R4). |
| `scene` | string (enum) | `rm-briefing` \| `morning-brief`. |
| `alert` | `LiveAlert` | Banner content (below). |
| `briefing` | `RmBriefing` \| `MorningBrief` | The **full re-synthesized DTO** (reaction granularity, R7). |

### 4a. LiveAlert

| Field | Type | Notes |
|-------|------|-------|
| `priority` | string (enum) | `info` \| `notice` \| `urgent` (maps from event severity). |
| `headline` | string | Consolidated for a burst (R3): names the new event(s). |
| `eventIds` | string[] | The event(s) that triggered this update (≥1; coalesced bursts list all). |
| `noImpact` | boolean | `true` ⇒ "new event, no portfolio impact"; rankings unchanged (US2 AS3). |

---

## 5. AdminNewsSubmission (Admin UI → ingestion; spec "Admin News Submission")

The operator-authored payload from `/admin` that becomes an `intraday` Event on injection. See
[`contracts/admin-submission.schema.json`](./contracts/admin-submission.schema.json).

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `headline` | string | ✅ | Validation: non-empty (FR-015). |
| `summary` | string | ✅ | Non-empty. |
| `source` | string | ➖ | Defaults to a fictional admin label if omitted. |
| `severity` | string (enum) | ✅ | `low` \| `medium` \| `high`. |
| `type` | string (enum) | ✅ | Same enum as `Event.type`. |
| `affectedEntities` | `AffectedEntities` | ✅ | MUST contain ≥1 selector (FR-015). |
| `sceneTargeting` | string[] (enum) | ➖ | Optional `rm-briefing` / `morning-brief` hints; absence ⇒ resolved by entity mapping (R5). |

On successful POST the server constructs an `Event` with `scope=intraday`, `origin=admin`,
server-set `ingestedAt`, dedup applied (R11), then triggers the reactive path (FR-016 → SSE).

---

## 6. Relationships (summary)

```text
events_overnight.json ──seed──▶ EventStore (mock-api, in-memory)
AdminNewsSubmission ──POST /mock/events (ingest)──▶ EventStore   (origin=admin, scope=intraday)
                                          │
        orchestration EventTools (HTTP only, Principle II) ──list / by-entity──┘
                                          ▼
   RmBriefingComposer / AgentRunner(RM)        MorningBriefComposer / AgentRunner(TD)
        │  EventImpactResolver (net score, drivers)        │
        ▼                                                   ▼
   RmBriefing (+EventsConsidered, PriorityCall.DrivingEvents)   MorningBrief (+EventsConsidered, AffectedClient.DrivingEvents)
        │                                                   │
        └────────── BriefingEventStream (SSE) ──LiveUpdate──┴──▶ open UI briefings (alert + re-rank)
```

**Entity → portfolio mapping** (R5): `customerIds`/`sectors` → CB customers (RM scene);
`tickers`/`sectors`/`issuers` → TD securities/holdings + holders (Trading scene). An event matching no
entity ⇒ recorded, `noImpact=true`, rankings unchanged.
