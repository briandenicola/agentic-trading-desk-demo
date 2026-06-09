# SSE Channel Contract — Reactive Briefing Updates

**Feature**: `002-reactive-event-cockpit` | Related: [live-update.schema.json](./live-update.schema.json),
[data-model.md §4](../data-model.md)

This is the **Server-Sent Events (SSE)** contract that pushes live briefing updates to open cockpit
pages (FR-010…FR-013). It is served by an orchestration endpoint (`BriefingEventStream`), traverses
the existing `src\ui-app\nginx.conf` reverse proxy, and is consumed by the browser's native
`EventSource`. WebSocket is a documented fallback only (spec Assumptions, research R2).

## Endpoint

```
GET /api/agent/{scene}/stream
  scene ∈ { rm-briefing, morning-brief }
  Accept: text/event-stream

Response headers:
  Content-Type: text/event-stream
  Cache-Control: no-cache
  Connection: keep-alive
```

Optional persona/book scoping is passed as a query parameter consistent with the POST scene
endpoints (e.g. `?rmId=RM-104` for `rm-briefing`). The stream only pushes updates for events whose
`affectedEntities` intersect that persona's book (persona scope, R5); out-of-book events do not push
to that subscriber.

## Reverse-proxy requirements (nginx / ACA)

The SSE `location` MUST add, on top of the existing `/api/` proxy block:

```nginx
proxy_buffering off;     # flush events immediately (no response buffering)
proxy_cache off;
proxy_read_timeout 3600s; # keep the long-lived stream open
proxy_http_version 1.1;   # already set for /api/
```

Azure Container Apps external ingress (on `ui-app`) and internal ingress (on `orchestration-api`)
support long-lived streaming HTTP responses; no extra ACA configuration beyond the existing ingress is
required (research R2 / FR-013).

## Message framing

Each push is a standard SSE frame. The `id:` line carries a **monotonic sequence** used by the browser
for `Last-Event-ID` reconnect (research R4):

```
id: 42
event: briefing-update
data: { ...LiveUpdate envelope (see live-update.schema.json)... }

```

### Event types (`event:` field)

| `event:` | Meaning | `data` payload |
|----------|---------|----------------|
| `briefing-update` | A new/coalesced event caused a re-synthesis. | `LiveUpdate` (alert + full re-synthesized DTO). |
| `heartbeat` | Keep-alive ping (every ~15 s) to hold the connection open through idle timeouts. | `{ "ts": "<ISO-8601>" }` |
| `ready` | Sent once on (re)connect to confirm subscription + current sequence. | `{ "sequence": <n>, "scene": "<scene>" }` |

## Coalescing (research R3 / FR-012, Edge "event storm")

Bursts of ingests within a short debounce window (~750 ms, env-configurable) produce **one**
`briefing-update` whose `alert.eventIds` lists all coalesced events and whose `briefing` reflects the
full current event set (composers always read all current events, FR-006). The UI shows one
consolidated alert rather than one per event.

## Reconnect & reconciliation (research R4 / FR-012, Edge "stale open page")

- `EventSource` auto-reconnects and sends `Last-Event-ID`.
- On reconnect the server emits `ready` then immediately a `briefing-update` carrying the **current**
  re-synthesized DTO (snapshot-on-reconnect). Because every update is a full snapshot, no missed-delta
  divergence is possible — the latest snapshot is authoritative.

## No-impact updates (US2 AS3)

An intraday event that matches no portfolio entity still emits a `briefing-update` with
`alert.noImpact = true`, `alert.priority = "info"`, and an unchanged ranking in `briefing`.

## Latency target

A `briefing-update` (including Admin-injected events) MUST reach an open page within **10 seconds** of
ingestion (SC-002 / SC-003), end-to-end through the proxy.
