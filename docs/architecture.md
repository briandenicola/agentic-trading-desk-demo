# Architecture — Agents, Orchestration & Traceability

> All demo data is fictional. DEMO mode is deterministic, offline, and the default;
> LIVE mode engages Azure AI Foundry. For each scene, DEMO and LIVE return the same
> JSON shape (Principle III / FR-010): `RmBriefing` for the RM Daily Briefing and
> `MorningBrief` for the municipal morning brief.

## Conceptual overview — from market noise to actionable client signals

![Agentic Trading Intelligence — an AI signal filter feeds an orchestrator ("My Trader Assistant") that runs a sequential client-intelligence pipeline (Who → Why → How → Talk Track) plus parallel market-context agents (Liquidity Risk, Market Mover, Inventory Analysis) that can escalate a major event, surfacing prioritized signals to a human-in-the-loop trader dashboard.](<Traders Agent Diagram.png>)

The diagram above is the **north-star vision** for the Institutional Sales & Trading cockpit: hundreds
of raw market events per second are reduced by an **AI signal filter** to high-signal items, an
**orchestrator** ("My Trader Assistant") spawns a **sequential client-intelligence pipeline**
(*Who* has exposure → *Why* should each client care → *How* do we engage → a personalized *Talk
Track*) alongside **parallel market-context agents** (liquidity risk, market mover, inventory) that can
**escalate a major event**, and everything surfaces to a **human-in-the-loop** trader dashboard where
the trader reviews and acts. The competitive edge lives in the **prompt, not the model** — the model
layer is swappable.

This repo is a working, demo-scoped realization of that vision: the model layer is **Azure AI Foundry**
(Microsoft Agent Framework), the agents reason over **fictional** systems-of-record exposed over HTTP,
and the deterministic DEMO composers stand in for the same flow offline. The sections below describe
exactly which pieces are built today and how they map onto this picture.

## Scenes & datasets

| Scene | Endpoint | Output | Data family (`mock-api`) |
|---|---|---|---|
| **RM Daily Briefing** (PRIMARY — "Morning Planning & Prioritized Outreach") | `POST /api/agent/rm-briefing` | `RmBriefing` | Commercial Banking RM — `/mock/cb/*` (customers, RMs, opportunities, complaints, interactions) |
| **Morning Brief** (municipal cross-asset) | `POST /api/agent/morning-brief` | `MorningBrief` | `/mock/{tableau,dynamics,trading,calendar,marketdata,news,coalition}/*` |
| **AI Chat** (grounded Markets-Intelligence assistant) | `POST /api/chat` | `ChatReply` | Commercial Banking RM (default) — `/mock/cb/*`; **or** Trading Desk when `salespersonId` is set — `/mock/td/*`; plus the reactive event store |
| **Trading Desk** (Institutional Sales & Trading — "Morning Planning & Prioritized Outreach") | `POST /api/agent/td-briefing` | `TdBriefing` | Trading Desk — `/mock/td/*` (clients, securities, trades, rfqs, crm, holdings, inventory, inquiries, news, research, narrative-themes) |
| **New Issue Radar** (Institutional Sales & Trading — guided new-issue storyboard) | `POST /api/agent/td-new-issue` | `TdNewIssueStoryboard` | Trading Desk — `/mock/td/*` (same family; reuses `securities/{id}/interest` + `clients/{id}/activity` aggregates) |

The course-correction datasets (`/mock/cb/*`, `/mock/td/*`) come from real client sample
data and are described in `openapi\tools.yaml` (v0.2.0) alongside the original tools.

## Three-layer flow

![Application architecture — React UI scenes call POST /api/agent/{scene} into the orchestration-api, which switches on DEMO_MODE between deterministic C# composers and LIVE Foundry runners, calls HTTP-only tools, and reaches data exclusively through the mock-api stores and fixtures.](src-architecture.png)

```
src\ui-app\ ─ POST /api/agent/{rm-briefing,td-briefing,td-new-issue,morning-brief} ─► src\orchestration-api\
                                                     │
                            DEMO: deterministic C# composer (offline)
                            LIVE: Foundry agent + client-side tool loop
                                                     │ HTTP only
                                                     ▼
                                      src\mock-api\ (openapi\tools.yaml)
```

Data flows left-to-right only. The frontend is mode-blind. Orchestration code reaches
data **exclusively** over the mock-api HTTP seam — it never reads fixtures in-process
(Principle II / FR-002). The diagram above maps these layers onto the actual `src/`
folders; the editable source is [`src-architecture.excalidraw`](src-architecture.excalidraw).

## Deployment topology (Azure Container Apps)

![Azure deployment topology — a Resource Group holds a Container Apps Environment running ui-app (external ingress), orchestration-api and mock-api (internal), plus the agent-provisioner job; alongside ACR, Key Vault, a user-assigned managed identity, Application Insights / Log Analytics, and — in FULL mode — Azure AI Foundry.](infra-architecture.png)

Terraform (`infra/*.tf`, one workspace per region) provisions a **Container Apps Environment**
with three apps — `ui-app` (the only public ingress), `orchestration-api`, and `mock-api` (both
internal) — plus the `agent-provisioner` Container App **job**. Supporting platform services are
**ACR** (image pulls via managed identity), **Key Vault** (Foundry endpoint + App Insights
connection secrets), a **user-assigned managed identity** (AcrPull + KV Secrets User), and
**Application Insights / Log Analytics**. `DEMO_MODE` selects DEMO vs. FULL: in FULL mode
`enable_foundry` also stands up **Azure AI Foundry** (AIServices account + project + model
deployments) reached over `FOUNDRY_PROJECT_ENDPOINT`. Editable source:
[`infra-architecture.excalidraw`](infra-architecture.excalidraw).

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
- **`AgentRunner`** (morning brief), **`RmAgentRunner`** (RM daily briefing) and **`TdAgentRunner`**
  (trading desk) look the agent up **by name** via `AIProjectClient.GetAIAgentAsync(name, tools)` and
  reuse it, attaching the mock-api tools for client-side execution. If the agent is not found (provisioner
  has not run yet) they fall back to `CreateAIAgentAsync(name, model, instructions, …)` so LIVE still works.

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
- **Trading Desk** (`TdBriefingTools`): `get_clients`, `get_client_activity`, `get_client_holdings`,
  `get_security_interest`, `get_inventory`, `get_news`, `get_research`, `get_rfqs`, `get_inquiries`,
  `get_crm`, `get_narrative_themes`, `list_events`.

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
  `.AsBuilder().UseOpenTelemetry(sourceName: "AgenticTradersDesk.Orchestration", …)`, emitting `gen_ai.*`
  spans (model, tool selection, token usage). `EnableSensitiveData` captures prompts/responses
  (data is fictional) and is gated by `OTEL_CAPTURE_MESSAGE_CONTENT` (default on).
- **Agent-run span**: each LIVE run opens one run span (`morning_brief.run` / `rm_briefing.run`)
  tagged with the scene key (event id / rm id), mode, model, and
  `gen_ai.usage.{input,output,total}_tokens`, correlating the full UI → agent → tool → mock-api chain.
- **Per-tool-call spans**: every tool invocation runs inside an `execute_tool <name>` child span
  recording the tool name, string arguments, duration, and response size
  (`src\orchestration-api\Agents\AgentRunner.cs`, `RmAgentRunner.cs`).
- **Metrics**: token usage (`atd.morning_brief.tokens`) and per-tool duration (`atd.tool.duration`)
  histograms are emitted under the `AgenticTradersDesk.Orchestration` meter
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
  Streamed scenes: `rm-briefing`, `morning-brief`, `td-briefing`, and `td-new-issue` (the New Issue
  Radar storyboard — folds matching events in via `TdNewIssueLive.ApplyEvents`).

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
  `trading-desk-morning`, `event-specialist`, `markets-assistant` (the CB AI Chat agent),
  `trading-desk-assistant` (the Trading Desk chat agent), and
  `briefing-synthesizer` (GetAIAgentAsync-first, create only when absent).

## Institutional Sales & Trading (Trading Desk)

The **Trading Desk** scene is the institutional analog of the RM Daily Briefing, targeting a
coverage salesperson on a dealer desk who covers a book of hedge funds (demo persona **Theo
Wexler**, covering Hyperion Capital, Tradewinds Partners, Halcyon Multistrat and Forge Hill
Partners). It answers *"which clients do I call this morning, and what do I put in front of
them?"* — a **prioritized client call list** driven by overnight news/research, open RFQs, client
inquiries and the desk's inventory **axes** matched against each client's holdings. The DEMO and
LIVE paths return the identical `TdBriefing` shape (Principle III / FR-010); all data is fictional
(`/mock/td/*`).

- **DEMO** (`Agents\Demo\TdBriefingComposer.cs` + `EventImpactResolver`): a deterministic,
  offline composer. Per client it caps and sums component scores (news+research ≤100, RFQ ≤60,
  inquiry ≤60, axe ≤60, CRM tiers), ranks by composite score (then raw score, exposure, id), and
  assigns priority bands by **rank** (1-2 → P1, 3-4 → P2, 5-6 → P3, else P4). News/research is matched
  to a client by security **or issuer** (equity news bridges to the same issuer's bonds), never by
  broad sector alone. The market strip's direction comes from related-news **sentiment**. Output is
  byte-stable.
- **LIVE** (`Agents\TdAgentRunner.cs`, agent `trading-desk-morning`, `Prompts\trading-desk-morning.md`):
  the persistent Foundry synthesizer binds `TdBriefingTools`, runs the per-event specialist fan-out
  (so the ranking reflects every live event), maps + normalizes the model output (force LIVE, re-rank
  by score, priority by rank), and **degrades to the DEMO composer** when the synthesizer returns no
  calls or Foundry is unavailable (FR-011). Model deployment: `FOUNDRY_MODEL_TRADING` (defaults to
  `FOUNDRY_MODEL`).
- **Reactive re-rank** (`GET /api/agent/td-briefing/stream`): the SSE hub re-synthesizes the
  `TdBriefing` on each new event and pushes a full snapshot + `LiveAlert`. The marquee demo event is an
  **AI-capex breaking print** (tickers `SEC-3003`/`SEC-3002`, issuers Quartzite/Nimbus, sector
  Technology); injecting it from the `/admin` News Desk (a one-click preset) jumps Hyperion & Tradewinds
  to the top of the call list within ~10s, with the driving events highlighted on each call card.

## New Issue Radar (guided storyboard)

The **New Issue Radar** (`POST /api/agent/td-new-issue`, route `/desk/new-issue`) is a guided,
step-by-step storyboard for the same desk. A primary issuer (**Prairie Green Renewables**) announces a
**concurrent debt + equity issue**; the desk's edge is connecting that announcement to an existing
client (**Crestline Capital**, CL-2015) who both **holds ~$1.0bn of the issuer's equity** and has been
**actively trading the new senior note** (electronic RFQs + calls) — so they are the first call, with a
concrete allocation in hand. DEMO and LIVE return the identical `TdNewIssueStoryboard` shape (Principle
III); all data is fictional (`/mock/td/*`). No new mock-api endpoints were added — the storyboard is
built entirely from the existing `securities/{id}/interest` and `clients/{id}/activity` aggregates plus
the Prairie Green fixtures (issuer equity `SEC-3601`, new note `SEC-3602`, holding `HLD-7901`, RFQs
`RFQ-5901..5905`, trades `TRD-8901..8903`, calls `CRM-9901/9902`, announcement `NEWS-1901`, desk
distribution axe `INV-4901`).

The DTO is an ordered list of **four beats** — `announcement` → `holdings` → `activity` → `outreach` —
each with `metrics[]` (headline figures) and `evidence[]` (the source records / "receipts"), plus a final
`outreach` recommendation (talking points, a `TradeIdea`, suggested action, and a ready-to-send draft
message).

- **DEMO** (`Agents\Demo\TdNewIssueComposer.cs`): a deterministic, HTTP-only composer. It reads
  `/mock/td/securities/{equity}/interest` (announcement news + holders), `/mock/td/securities?issuer=`
  (the new debt tranche), and `/mock/td/clients/{id}` + `/activity` (the focus client's holdings, RFQs,
  trades, CRM), then derives every figure (market value, share count, RFQ count, traded notional, axe
  size/price). If the requested client does not hold the equity it falls back to the largest holder; on
  tool failure it returns a degraded storyboard with a `notes` entry.
- **LIVE** (`Agents\TdNewIssueRunner.cs`, agent `trading-desk-new-issue`, `Prompts\td-new-issue.md`):
  the Foundry agent binds a focused subset of `TdBriefingTools`, reasons over the same systems-of-record,
  and emits the storyboard JSON; on any failure or empty output it **degrades to the DEMO composer**
  (re-stamped LIVE), so the demo is never blocked on model quality. Model deployment:
  `FOUNDRY_MODEL_TRADING` (defaults to `FOUNDRY_MODEL`).
- **Reactive fold-in** (`Live\TdNewIssueLive.cs`, `GET /api/agent/td-new-issue/stream`): the New Issue
  Radar reacts to the News Desk the same way the briefings do. After the composer/runner builds the base
  storyboard, the shared `TdNewIssueLive.ApplyEvents` helper folds in any injected `MarketEvent` whose
  affected entities touch the issuer (`Prairie Green Renewables`), either tranche (`SEC-3601`/`SEC-3602`),
  the sector (`Utilities`), or the focus client (`CL-2015`). A matching ("driver") event surfaces as a
  `LIVE` evidence row on the announcement beat, a `live` metric on the announcement + outreach beats, a
  leading outreach talking point, and the storyboard's `liveEvents[]`; non-matching events are ignored.
  The same helper runs on **both** paths — the one-shot `POST` folds in the current event store, and the
  SSE hub (`BriefingEventStream`, scene `td-new-issue`) re-synthesizes + pushes a full snapshot +
  `LiveAlert` on each new event. Because the helper runs after compose/run, DEMO and LIVE stay byte-stable
  and the composer/runner are unchanged (Principle III).

## Grounded chat assistant (AI Chat / Open Chat)

The chat surface (`POST /api/chat`, route `/chat`) is a multi-turn assistant that answers from the
**same systems-of-record** as the briefings (Principle II/III). It is stateless — the client replays
the conversation each turn — and routes to one of **two grounded assistants** by the request context:
a `salespersonId` selects the Trading Desk assistant (grounded in `/mock/td/*`); otherwise the
Commercial Banking RM assistant answers (grounded in `/mock/cb/*`). Both return the same `ChatReply`
shape so the UI is mode-blind.

**Commercial Banking — Markets-Intelligence assistant** (default):

- **DEMO** (`Agents\Demo\ChatResponder.cs`): a deterministic intent router (who-to-call, customer
  lookup, market/events, complaints, pipeline, help) reusing `RmBriefingComposer` / `MockApiClient` /
  `EventTools`.
- **LIVE** (`Agents\ChatAgentRunner.cs`): the persistent `markets-assistant` Foundry agent
  (`Prompts\markets-assistant.md`) reusing the **same RM mock-api tools**; on any failure or empty
  output it degrades gracefully to the DEMO responder (re-stamped LIVE).

**Institutional Sales & Trading — Trading Desk assistant** (`salespersonId` present):

- **DEMO** (`Agents\Demo\TdChatResponder.cs`): a deterministic intent router (who-to-call, client
  profile by `CL-####`, security interest by `SEC-####`, inventory axes, market/events, help)
  grounded in `/mock/td/*` via `TdBriefingComposer` / `MockApiClient` / `EventTools`.
- **LIVE** (`Agents\TdChatAgentRunner.cs`): the persistent `trading-desk-assistant` Foundry agent
  (`Prompts\trading-desk-assistant.md`) binding the **trading-desk mock-api tools** (clients,
  securities, RFQs, inquiries, CRM, inventory, news, research, events); degrades to the DEMO
  `TdChatResponder` (re-stamped LIVE) on any failure or empty output.

- **Model**: both assistants use `FOUNDRY_MODEL_CHAT`, which defaults to the `gpt-4o-mini` morning
  deployment so chat never competes with the briefing synthesizers for quota.

## Frontend / UI (`src\ui-app`)

React 19 + TypeScript + MUI v9, themed with the **M.INT** mint palette
(`src\ui-app\src\theme\theme.ts`). The frontend is mode-blind (Principle III): every scene renders
the same DTO whether it came from DEMO or LIVE. Routes: `/` (landing — workspace chooser),
`/desk` + `/desk/morning-brief` (Trading Desk — the demo focus), `/desk/new-issue` (New Issue Radar),
`/cb` (Commercial Banking RM workspace), `/cockpit`, `/rm-briefing`, `/morning-brief`, `/admin` (News
Desk), `/chat`.

### Trading Desk scenes (`scenes\TradeDesk`)

`/` is a landing chooser between **Institutional Sales & Trading** (`/desk`) and **Commercial
Banking RM** (`/cb`). The trading-desk scenes share one data+live engine, `useTdBriefing`: it
auto-runs `POST /api/agent/td-briefing` once per session, persists the brief (survives navigation),
and once a brief is on screen holds an SSE subscription (`subscribeToEvents('td-briefing', …)`) that
applies each re-synthesized DTO in place and surfaces a `LiveAlertBanner`.

- **`/desk` (`TradeDeskScene`)** — hero greeting + salesperson summary + mode chip, the market strip,
  agent reasoning, and a two-column grid: prioritized call list + suggested first action (left), and
  the inventory axe board + macro themes + events considered (right). Each `TdCallCard` shows the
  priority band, composite score, why-now drivers, trade ideas, talking points, and a "⚡ RE-RANKED BY
  LIVE EVENTS" callout when `drivingEvents` are present.
- **`/desk/morning-brief` (`TdMorningBriefScene`)** — the same `TdBriefing` in a two-column
  morning-brief layout (macro/market context, reasoning, axes, events on the left; the outreach plan
  on the right), sharing the persisted brief via the same store key.
- **`/desk/new-issue` (`NewIssue\TdNewIssueScene`)** — the **New Issue Radar** guided walkthrough.
  `useTdNewIssue` auto-runs `POST /api/agent/td-new-issue` once per session and persists the storyboard,
  and — like the trading-desk scenes — once the storyboard is on screen it holds an SSE subscription
  (`subscribeToEvents('td-new-issue', …)`) that applies each re-synthesized storyboard in place, surfaces
  a `LiveAlertBanner`, and highlights the folded-in `LIVE` metric/evidence. The scene renders the
  issuer/new-issue header, a clickable four-beat progress rail, the active beat (narration + metric chips +
  evidence rows), and Back/Next controls; the concluding `outreach` recommendation card (talking points,
  trade idea, draft message) reveals on the final beat. The landing chooser also links it directly via a
  "New Issue Radar" featured chip on the Trading Desk card.

### Agent-driven main page (`/` — `scenes\Workspace`)

The landing page is the **RM Daily Briefing** rendered live, not a static shell. On first visit it
auto-runs `POST /api/agent/rm-briefing` for `RM-104` and composes:

- **HIGH PRIORITY hero** — `priorityCallList[0]` with a seeded **Open Chat** action.
- **Events in Play** — `eventsConsidered`.
- **RM detail panels** — `kpis` + `portfolio`, `complaintsSnapshot`, `pipelineClosing`, `macroSnapshot`.
- **Live newsfeed** (left) — live incoming events over the overnight baseline signals.
- **Prioritized outreach** (right) — `priorityCallList[1..]` + `suggestedFirstAction`.

`scenes\Workspace\useWorkspaceLive.tsx` is the single data+live engine: it owns the auto-load, holds
the long-lived SSE subscription (`subscribeToEvents('rm-briefing', …, { persona:'RM-104' })`), and on
every `briefing-update` re-ranks the page in place with highly visible cues — hero flash, the
`LiveAlertBanner`, a toast stack, the LIVE pill, KPI pulses and the unread badge. The **same** stream
is fed by the `/admin` News Desk, so an operator post lights up the main page (and the cockpit). The
left rail links to Cockpit / News Desk / Morning Brief; the process strip + trust badges are a fixed
footer bar that shares the bottom row with the floating chat pill.

### Floating chat dock (`scenes\Workspace\ChatOverlay.tsx`)

Chat is a floating overlay, not an inline panel. `ChatDockProvider` exposes `useChatDock()` with
`openChat(seed?)`; a bottom-right launcher expands into an overlay wired to `POST /api/chat`
(`MarkdownMessage` renders assistant replies as Markdown). Panels can pop it open seeded with context
(e.g. the hero "Open Chat" pre-fills a question about the top call). The dock is **grounding-agnostic**:
a `ChatDockConfig` selects the assistant (`send` function), a distinct persisted-state namespace and
the panel copy, so the same dock serves the Commercial Banking RM (default config) and the Trading
Desk (`scenes\TradeDesk\tdChatConfig.ts`, which calls `sendDeskChat` with the coverage salesperson so
`/api/chat` routes to the trading-desk-grounded assistant). It is mounted on the **main page and the
Cockpit** (CB) and on **`/desk`, `/desk/morning-brief` and `/desk/new-issue`** (Trading Desk), where
each `TdCallCard` and the New Issue outreach card expose a seeded **Open Chat** action.

### Cross-navigation persistence (`hooks\usePersistentState.ts`)

Auto-loading scenes (main page, RM briefing, morning brief) must survive navigating away and back
without re-running the agent. Two pieces cooperate:

- `usePersistentState(key, initial)` keeps each scene's result in a **module-level store** for the SPA
  session, so a remount reads the prior value instead of the initial one.
- `loadPersistentOnce(key, loader)` runs the fetch, writes the result **straight into that store**, and
  **dedupes concurrent loads**. This keeps an in-flight request alive across unmount: if the user
  leaves before it resolves, the result still lands in the store, so returning shows the loaded value
  instead of kicking off a fresh run. A second call after resolution starts a new load (explicit
  reload). `clearPersistentState()` (test seam) clears both the store and the in-flight registry.

### Morning Brief (`scenes\MorningBrief`)

A two-column cockpit mirroring the RM Daily Brief column look, under a themed M.INT hero (gradient
title + action bar):

- **Left — Macro Event Analysis & details**: the macro narrative ("why it matters" + sources), the
  *Events considered* signal cards, and the *Most-affected clients* table (with per-client driving
  events).
- **Right — Your outreach plan** (`CallPlan.tsx`): numbered, editable client cards with a topic
  eyebrow, a composite-score chip, structured talking-point editors, a personal-note field, and a
  collapsible "Why this ranking?" rationale (wallet / engagement / event-relevance / composite bars +
  explanation). The plan stays human-in-the-loop and demo-only — approval issues no outbound call.

