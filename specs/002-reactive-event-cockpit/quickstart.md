# Quickstart — Reactive Event Cockpit

**Feature**: `002-reactive-event-cockpit` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

How to run, demo, and verify the reactive cockpit locally and in the deployed Sweden Container Apps
environment. Everything is **fictional** and DEMO mode is offline by default (Principle III/IV).

---

## Prerequisites

- .NET 10 SDK (`global.json` pins `10.0.100`, `rollForward: latestFeature`).
- Node.js (for `src\ui-app`) + Docker Desktop (for `task local:up`).
- Optional LIVE mode: Azure access for Foundry (`DefaultAzureCredential`) — **not** needed for the
  DEMO walkthrough.

## 1. Run locally (DEMO, offline)

```powershell
# From repo root
task local:up                 # docker compose: mock-api + orchestration-api + ui-app
# or run services individually:
dotnet run --project src\mock-api
dotnet run --project src\orchestration-api      # DEMO_MODE default; offline, no Foundry
npm --prefix src\ui-app run dev
```

On startup the mock-api seeds **≥3 overnight events** from `src\mock-api\Data\events_overnight.json`
into the in-memory `EventStore` (FR-002). Open:

- `http://localhost:<ui>/`              → RM Daily Briefing (Commercial Banking)
- `http://localhost:<ui>/morning-brief` → Trading Morning Brief (Capital Markets)
- `http://localhost:<ui>/admin`         → Admin news injection + event list (NEW)

## 2. Verify overnight multi-event briefing (US1 / SC-001)

```powershell
# List the seeded events (over the mock-api HTTP seam — Principle II)
curl http://localhost:8080/mock/events

# Generate each event-aware briefing
curl -X POST http://localhost:8081/api/agent/rm-briefing  -H "Content-Type: application/json" -d '{}'
curl -X POST http://localhost:8081/api/agent/morning-brief -H "Content-Type: application/json" -d '{}'
```

Expect (SC-001, FR-006/FR-007):
- `eventsConsidered` lists all seeded overnight events.
- Each affected `priorityCallList[].drivingEvents` (RM) / `mostAffectedClients[].drivingEvents`
  (Trading) names ≥1 linked event.
- Ranking differs from a no-event baseline (set an empty seed to compare).

**Determinism check (SC-004)**: run the same POST twice with identical seeds → byte-identical JSON.

## 3. Verify intraday live push (US2 / SC-002)

1. Open a briefing in the browser (it subscribes to `GET /api/agent/rm-briefing/stream` via
   `EventSource`).
2. Ingest an intraday event affecting a customer in the open RM's book:

```powershell
curl -X POST http://localhost:8080/mock/events -H "Content-Type: application/json" -d '{
  "type": "client_headline",
  "headline": "Fictional: Acme Mfg announces plant expansion",
  "summary": "Capex-positive headline for a held name.",
  "severity": "high",
  "affectedEntities": { "customerIds": ["CB-10042"] }
}'
```

3. Within **10 seconds** the open page shows a **live alert banner** and the affected call moves to its
   new rank — **no manual reload** (SC-002, FR-010/FR-011).
4. Ingest an event with no matching entity → page shows a low-priority "no impact" alert, rankings
   unchanged (US2 AS3).

## 4. Verify Admin injection (US3 / SC-003)

1. Go to `/admin`, fill the news form (headline, summary, source, severity, type, affected entities).
2. Submit incomplete (no headline or no affected entity) → rejected with a clear message, nothing
   ingested (FR-015).
3. Submit a valid item targeting a known customer/ticker → acknowledged; the event appears in the
   `/admin` event list (FR-017); any open briefing reacts within 10 s (SC-003, same path as US2).
4. Confirm **both** scenes react to the same injected event against their own domain (SC-006): inject
   an event with both `customerIds` and `tickers`, watch `/` and `/morning-brief` each update.

## 5. Verify LIVE multi-agent fan-out (US4 / SC-007) — optional

```powershell
# Provision the new agents idempotently (event-specialist + briefing-synthesizer)
task cloud:provision        # runs src\agent-provisioner as a Container Apps Job

# Enable LIVE (Foundry); DEMO_MODE off, FOUNDRY_PROJECT_ENDPOINT set
$env:DEMO_MODE=0
dotnet run --project src\orchestration-api
```

In the **Foundry portal** trace, confirm the graph: `synthesizer → per-event/specialist agent →
tool-call` for a multi-event briefing (SC-007). DEMO produces the same DTO shape offline (FR-020).

## 6. Tests (constitution §17 / Principle VII)

```powershell
dotnet test AgenticTradersDesk.sln --nologo     # xunit: EventStore contract, DEMO determinism, conflict net-score, SSE re-synthesis, parity
npm --prefix src\ui-app test           # vitest/RTL: AdminScene validation, LiveAlertBanner, SSE-driven re-rank
```

Key assertions:
- Event-aware DEMO output is byte-stable for fixed seeds (SC-004).
- Conflicting events net deterministically and both remain visible as drivers (R6 / Edge).
- Orchestration reads events **only over HTTP** from mock-api (Principle II) — the
  `orchestration-api.Tests` host wires the real mock-api in-memory (`extern alias MockApiHost`,
  Decision #7).
- `RmBriefing` / `MorningBrief` JSON shape identical in DEMO and LIVE (Principle III).

## 7. Deploy (Sweden Container Apps)

```powershell
task cloud:plan
task cloud:apply-infra      # no NEW container app — reuses ui-app / orchestration-api / mock-api
task build:all             # az acr build per image (stablegator8350acr)
task cloud:deploy          # update ACA revisions
task cloud:provision       # idempotent agent registration (incl. new event agents)
```

Verify SSE through the deployed ingress: the `ui-app` nginx proxy MUST pass the SSE location with
`proxy_buffering off` and a raised read timeout (see
[`contracts/event-stream.sse.md`](./contracts/event-stream.sse.md)). If SSE cannot traverse cleanly,
fall back to the WebSocket transport (spec Assumptions).

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Live alert never arrives | nginx buffering the SSE stream | Add `proxy_buffering off;` + raise `proxy_read_timeout` on the stream location. |
| Admin items vanish after restart | In-memory store reseeds on restart (by design) | Expected — only overnight seed reloads (spec Assumptions). |
| DEMO output not byte-stable | Non-deterministic ordering in impact resolution | Ensure `EventImpactResolver` sorts events/drivers deterministically. |
| Event ignored by a briefing | Event entity outside that persona's book | Expected — out-of-book events stay global, don't alter the persona (R5 / Edge). |
| LIVE 400 on POST with body | Known request-binding quirk (decisions ledger) | Use the documented payload shape; empty body falls back to defaults. |
