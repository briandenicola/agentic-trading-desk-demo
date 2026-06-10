# Architecture — Agents, Orchestration & Traceability

> All demo data is fictional. DEMO mode is deterministic, offline, and the default;
> LIVE mode engages Azure AI Foundry. For each scene, DEMO and LIVE return the same
> JSON shape (Principle III / FR-010): `RmBriefing` for the RM Daily Briefing and
> `MorningBrief` for the municipal morning brief.

## Scenes & datasets

| Scene | Endpoint | Output | Data family (`mock-api`) |
|---|---|---|---|
| **RM Daily Briefing** (PRIMARY — "Morning Planning & Prioritized Outreach") | `POST /api/agent/rm-briefing` | `RmBriefing` | Commercial Banking RM — `/mock/cb/*` (customers, RMs, opportunities, complaints, interactions) |
| **Morning Brief** (municipal cross-asset) | `POST /api/agent/morning-brief` | `MorningBrief` | `/mock/{tableau,dynamics,trading,calendar,marketdata,news,coalition}/*` |
| **Trading cockpit** (Capital Markets) | (UI/tools) | — | Trading Desk — `/mock/td/*` (clients, securities, trades, rfqs, crm, holdings, inventory, inquiries, news, research, narrative-themes) |

The course-correction datasets (`/mock/cb/*`, `/mock/td/*`) come from real client sample
data and are described in `openapi\tools.yaml` (v0.2.0) alongside the original tools.

## Three-layer flow

```
src\ui-app\ ─ POST /api/agent/{rm-briefing,morning-brief} ─► src\orchestration-api\
                                                     │
                            DEMO: deterministic C# composer (offline)
                            LIVE: Foundry agent + client-side tool loop
                                                     │ HTTP only
                                                     ▼
                                      src\mock-api\ (openapi\tools.yaml)
```

Data flows left-to-right only. The frontend is mode-blind. Orchestration code reaches
data **exclusively** over the mock-api HTTP seam — it never reads fixtures in-process
(Principle II / FR-002).

## Where the orchestrator vs. the agent live

| Concern | Lives in | Notes |
|---|---|---|
| **Orchestrator** (`AgentRunner`, tool functions, `MAX_TOOL_HOPS`, JSON→`MorningBrief` mapping) | **Container App** `orchestration-api` | Owns the flow; constructs `DefaultAzureCredential` only in LIVE. |
| **Agent** (instructions + model `gpt-5.4-mini`) | **Azure AI Foundry** | Hosted on the Foundry Agent Service / capability host. Connected via `FOUNDRY_PROJECT_ENDPOINT`. |
| **Tool *execution*** | **Container App** | Tools are client-side function tools; the agent only *decides* which to call. |
| **Tool *data*** | **Container App** `mock-api` | Each tool is an HTTP call to a mock system-of-record endpoint. |

A LIVE request bounces between the two:

```
UI → orchestration-api (Container App)
      └─ resolve persistent agent → Foundry agent (gpt-5.4-mini) reasons
            ⇄ tool-call request ──▶ container app runs get_market_data / get_news / …
                                       └─ HTTP ──▶ mock-api (Container App)
            ◀── tool result ──┘   (loops up to MAX_TOOL_HOPS)
      └─ final JSON ──▶ mapped to MorningBrief ──▶ UI
```

## Agent persistence (Foundry)

Each scene's agent is **persistent** in Foundry — registered once, reused on every request.

- **`src\agent-provisioner\`** is the single registrar. It idempotently creates/updates the
  persistent agent(s) on the **new Foundry surface** (versioned "prompt" agents), with
  instructions from the matching `Prompts\*.md` and model `FOUNDRY_MODEL`. It runs as a
  Container Apps **Job** in FULL mode (`task cloud:provision`).
- **`AgentRunner`** (morning brief) and **`RmAgentRunner`** (RM daily briefing) look the agent up
  **by name** via `AIProjectClient.GetAIAgentAsync(name, tools)` and reuse it, attaching the
  mock-api tools for client-side execution. If the agent is not found (provisioner has not run yet)
  they fall back to `CreateAIAgentAsync(name, model, instructions, …)` so LIVE still works.

Both runners use **only** the new-Foundry surface — the same one the reference
`online-banking-demo` uses. The earlier classic Assistants (`/assistants`) path was removed in the
Foundry surface fix, so the runtime and the provisioner now register/resolve agents on the same
surface and all runs/threads appear under one agent per scene in the Foundry portal.

### The tools

- **Morning brief** (`MorningBriefTools`): `get_market_data`, `get_news`, `get_relative_value`,
  `get_client_value_all`, `get_engagement`, `search_holdings`, `get_axes`.
- **RM daily briefing** (`RmBriefingTools`): `get_rm_book`, `get_open_opportunities`,
  `get_active_complaints`, `get_due_followups`, `get_customer`, `get_customer_opportunities`,
  `get_customer_interactions`.

All are wrappers over `openapi\tools.yaml` endpoints. They never throw; failures return a
structured `{"error": …}` object so the loop degrades gracefully (FR-011).

## Traceability (current state)

**Today**

- Serilog structured JSON logs with a correlation id propagated per request
  (`X-Correlation-ID`), wired in `src\shared\Observability`.
- OpenTelemetry tracing/metrics with ASP.NET Core + outbound-HTTP instrumentation, so tool calls
  surface as HTTP dependency spans.
- The persistent agent's runs and tool-call steps are visible natively in the **Foundry portal**
  (thread/run view). The runtime reuses the scene's agent (`morning-brief` / `rm-daily-briefing`) by
  name when it is found; otherwise it falls back to creating one. Runtime and provisioner now use the
  same new-Foundry surface, so the earlier reuse gap is resolved.
- **Azure Monitor OpenTelemetry exporter** (`Azure.Monitor.OpenTelemetry.Exporter`) attaches
  automatically when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, so traces **and** metrics flow
  to Application Insights in the container apps. The OTLP exporter still attaches in parallel when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set. DEMO mode stays fully offline — neither exporter attaches
  unless its key is present (`src\shared\Observability\ObservabilityExtensions.cs`).
- **Agent Framework GenAI spans**: the resolved Foundry agent is wrapped with
  `.AsBuilder().UseOpenTelemetry(sourceName: "WF.Garage.Orchestration", …)`, emitting `gen_ai.*`
  spans (model, tool selection, token usage). `EnableSensitiveData` captures prompts/responses
  (data is fictional) and is gated by `OTEL_CAPTURE_MESSAGE_CONTENT` (default on).
- **Agent-run span**: each LIVE run opens one run span (`morning_brief.run` / `rm_briefing.run`)
  tagged with the scene key (event id / rm id), mode, model, and
  `gen_ai.usage.{input,output,total}_tokens`, correlating the full UI → agent → tool → mock-api chain.
- **Per-tool-call spans**: every tool invocation runs inside an `execute_tool <name>` child span
  recording the tool name, string arguments, duration, and response size
  (`src\orchestration-api\Agents\AgentRunner.cs`, `RmAgentRunner.cs`).
- **Metrics**: token usage (`wf.morning_brief.tokens`) and per-tool duration (`wf.tool.duration`)
  histograms are emitted under the `WF.Garage.Orchestration` meter
  (`src\orchestration-api\Agents\OrchestrationTelemetry.cs`).

All custom source/meter names are registered with OpenTelemetry in
`src\orchestration-api\Program.cs` via `AddOpenTelemetry(ServiceName, additionalSources, additionalMeters)`.
Delivered under `specs\_backlog\005-observability.md`.

## Reactive event cockpit (002)

The cockpit reacts to overnight **and** intraday events. The event store lives in the mock-api;
orchestration reaches it only over HTTP (Principle II / FR-004).

- **Event store** (`src\mock-api\EventStore.cs`, `Endpoints\EventEndpoints.cs`): seeds overnight
  events and accepts intraday ingests. On ingest it server-sets the id (`evt-yyyyMMdd-aNNN`), stamps
  `scope=intraday` and `origin=admin` (for operator/feed submissions), validates
  (headline/summary/severity/type/≥1 affected selector), and dedups by normalized headline+type.
- **HTTP surface** (`/mock/events`): `GET /mock/events[?scope=]`, `GET /mock/events/by-entity?value=&kind=`,
  `POST /mock/events`. Wrapped by `EventTools` in orchestration.
- **Admin proxy** (`GET|POST /api/events` in `Program.cs`): the browser's **only** path to the store.
  `POST` re-validates, maps the admin submission to a `MarketEvent` (`origin=admin`), and ingests it
  through `EventTools` so an injected item follows the **same** ingestion + reactive path as a real
  feed (FR-016). The `/admin` "News Desk" scene drives it.
- **SSE channel** (`GET /api/agent/{scene}/stream`, `Live\BriefingEventStream.cs` +
  `EventStreamPollingService.cs`): a long-lived `text/event-stream`. A background poller diffs the
  store every `SSE_POLL_INTERVAL_MS`, coalesces bursts over `SSE_COALESCE_WINDOW_MS`, re-synthesizes
  the briefing, and broadcasts ONE consolidated `briefing-update` (full re-synthesized DTO + a
  `LiveAlert`) per (scene, persona). Reconnects get a snapshot frame; `heartbeat` frames hold the
  connection open. The `ui-app` nginx config passes SSE through unbuffered (`X-Accel-Buffering: no`).

## Multi-agent fan-out / synthesis (LIVE)

LIVE briefing generation is a **per-event multi-agent fan-out** into a synthesizer (002 US4):

```
list current events ─► EventFanOut (bounded by EVENT_FANOUT_MAX_CONCURRENCY)
                          ├─ event-specialist (event A) ⇄ get_events_by_entity tool
                          ├─ event-specialist (event B) ⇄ …
                          └─ event-specialist (event N) ⇄ …
                                     │  EventImpactAssessment[] (typed selectors + signed contribution)
                                     ▼
                       briefing synthesizer (scene agent) ⇄ scene tools ─► unchanged DTO
```

- **`EventFanOut`** (`Agents\EventSynthesis\EventFanOut.cs`) runs one specialist per event
  concurrently (capped by `EVENT_FANOUT_MAX_CONCURRENCY`, default 4), each inside an
  `event_specialist.assess <id>` child span under a `briefing_synthesis.fanout` parent — giving the
  **synthesizer → specialist → tool-call** trace graph (SC-007). A failing specialist is skipped, not
  fatal (FR-011).
- **`FoundryEventSpecialist`** (`event-specialist` agent, `Prompts\event-specialist.md`) assesses one
  event and resolves it to typed `kind:value` selectors with a signed `contribution`; it has one tool
  (`get_events_by_entity`) so each run shows a tool-call. Unusable model output degrades to a
  deterministic fallback mirroring the DEMO `EventImpactResolver`.
- **Synthesizer**: the per-scene agent (`rm-daily-briefing` / `morning-brief`) fulfils the synthesis
  contract (`Prompts\briefing-synthesizer.md`) — it folds each assessment's contribution into the
  affected items' scores, re-ranks, and lists every contributing event as a driver, emitting the
  **unchanged** DTO. DEMO stays deterministic/offline with the identical shape (SC-004).
- **Provisioning**: `agent-provisioner` idempotently registers `rm-daily-briefing`, `morning-brief`,
  `event-specialist`, and `briefing-synthesizer` (GetAIAgentAsync-first, create only when absent).

