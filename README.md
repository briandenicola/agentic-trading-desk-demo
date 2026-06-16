# AgenticTradersDesk — Morning Planning & Prioritized Outreach

Interactive **Client CV** demo for Commercial Banking RMs and a Municipal Sales desk. The React
cockpit runs event-reactive morning briefings that combine a market narrative, the overnight **and**
intraday events considered, the most-affected fictional clients, ranked outreach, and an editable
human-in-the-loop plan. New events injected through the News Desk fan out to per-event specialist
agents and push a re-synthesized briefing to the open cockpit live over SSE.

> All demo data is fictional. DEMO mode is deterministic, offline, and the default.

## Scenes

| Route | Scene | Purpose |
|---|---|---|
| `/` , `/rm-briefing` | **RM Daily Briefing** | Commercial Banking RM briefing + prioritized call list |
| `/desk` , `/desk/morning-brief` | **Trading Desk** (Institutional Sales & Trading) | Coverage-salesperson morning planning + prioritized client call list ranked **engagement-first** — open RFQs, inquiries/chat mentions, inventory-axe matches, and especially clients who **engaged but didn't trade** — with news/research demoted to a supporting **catalyst** (public headlines paired to our internal research view by issuer); trade ideas + talking points; re-ranks live over SSE. Each call card has a seeded **Open Chat** grounded in `/mock/td/*` |
| `/desk/new-issue` | **New Issue Radar** (Institutional Sales & Trading) | Guided new-issue storyboard: a concurrent debt+equity issue prints, the desk cross-references an existing equity holder who's actively trading the new note, and lands on a prioritized "call now" with allocation, talking points + draft message. Features a **Lead-Left Board** that highlights deals we run lead-left, and lets you **upload a spreadsheet (.xlsx/.csv) of possible lead-left deals** — parsed entirely in-browser — and "Drive radar" to re-run the radar focused on an uploaded deal |
| `/morning-brief` | **Trading Morning Brief** | Municipal-sales morning brief + ranked outreach |
| `/cockpit` | **Cockpit** | 3-column M.INT dashboard (Client / Ticker / Overall "Morning Call") with the live alert banner |
| `/chat` | **AI Chat** | Grounded Markets-Intelligence assistant — multi-turn Q&A over the same systems-of-record (who to call, a customer, the market, complaints, pipeline) |
| `/admin` | **News Desk** | Operator UI to inject intraday news the agents react to |

## Stack

- **Agents/API**: C# / .NET 10, Microsoft Agent Framework, Azure AI Foundry in LIVE mode
- **UI**: React 19 + TypeScript + MUI v9
- **Mock systems of record**: C# Minimal API serving fictional fixtures through `openapi/tools.yaml` endpoints
- **Runtime**: Azure Container Apps, ACR, Key Vault, managed identity, Terraform
- **Observability**: Serilog + OpenTelemetry via `src\shared\Observability`

## Architecture

```
src\ui-app\  ── /api/agent/{scene} ─────────►  src\orchestration-api\
        ▲   ── /api/agent/{scene}/stream (SSE)        │
        │   ── /api/events (admin inject) ────────────┤
   live briefing-update                               │  DEMO: deterministic C# composer
   (re-synthesized DTO)                               │  LIVE: Foundry synthesizer + per-event fan-out
                                                      │ HTTP only
                                                      ▼
                                       src\mock-api\ implements openapi\tools.yaml
                                       + reactive event store (/mock/events)
```

The frontend is mode-blind: DEMO and LIVE return the same `MorningBrief` / `RmBriefing` JSON shape
(including the `eventsConsidered` it weighed). The orchestration API reaches data only through the
mock API HTTP seam; it never reads fixtures in-process. A background poller diffs the event store and
broadcasts one consolidated re-synthesized briefing per scene over SSE when events arrive.

### Orchestrator vs. agent

- **Orchestrator** (the per-scene runners, the tool functions, `MAX_TOOL_HOPS`, the event fan-out, JSON→DTO mapping) runs **inside the `orchestration-api` Container App**.
- **Agents** (instructions + model `gpt-5.4-mini`) are **persistent in Azure AI Foundry**, on the Agent Service / capability host, reached via `FOUNDRY_PROJECT_ENDPOINT`. Seven are registered: `rm-daily-briefing`, `morning-brief`, `trading-desk-morning` (the scene synthesizers), `event-specialist` (run once per event in the fan-out), `markets-assistant` (the grounded CB AI Chat agent), `trading-desk-assistant` (the grounded Trading Desk Open Chat agent), and `briefing-synthesizer` (the shared synthesis contract).
- **Tool execution** happens back in the Container App: the agent only *decides* which tool to call; the C# function runs locally and fetches data from `mock-api` over HTTP.

In LIVE mode each briefing is a **per-event multi-agent fan-out into a synthesizer** (002 US4): the
runner lists the current events, fans out one `event-specialist` assessment per event (concurrently,
bounded by `EVENT_FANOUT_MAX_CONCURRENCY`, each in its own trace span), and feeds the assessments to
the scene agent, which folds them into the ranking and emits the unchanged DTO.

The agents are **registered once** by `src\agent-provisioner\` and **reused by name** on every
request (no per-request agent churn), so all runs consolidate under those agents in the Foundry
portal. See [`docs/architecture.md`](docs/architecture.md) for the full flow, the reactive event
store, the SSE channel, and the fan-out topology. For which parts of each page are agent-driven
(LIVE) vs. deterministic (DEMO) and what reacts to injected news, see
[`docs/agentic-vs-synthetic.md`](docs/agentic-vs-synthetic.md); for a trader-facing demo script see
[`docs/demo-talk-track.md`](docs/demo-talk-track.md).

## Quickstart — local DEMO mode

Prerequisites: .NET 10 SDK, Node.js 20+, Docker Desktop, and `go-task`.

```powershell
Copy-Item .env.example .env

task local:up
# open http://localhost:8080
```

Try the scene through the UI reverse proxy:

```powershell
curl.exe -X POST http://localhost:8080/api/agent/morning-brief
```

DEMO mode needs no Azure credentials. It defaults to `DEMO_MODE=1` and produces repeatable output for on-stage use.

## LIVE mode

Set `DEMO_MODE=0` and provide `FOUNDRY_PROJECT_ENDPOINT` plus `FOUNDRY_MODEL` (typically from `terraform -chdir=infra output`). LIVE uses `DefaultAzureCredential` with the Foundry project and the same mock API tools, capped by `MAX_TOOL_HOPS`.

The persistent agents are registered by `src\agent-provisioner\` (the `task cloud:provision` job in
FULL mode) and reused by the runtime. To avoid HTTP 429 throttling, each role runs on its **own model
deployment (separate quota pool)** so the high-concurrency fan-out never competes with the synthesizers:

| Agent | Model deployment | Env var |
|---|---|---|
| `rm-daily-briefing` (primary synthesizer) | `gpt-5.4-mini` | `FOUNDRY_MODEL` |
| `trading-desk-morning` (Trading Desk synthesizer) | `gpt-5.4-mini` | `FOUNDRY_MODEL_TRADING` |
| `trading-desk-new-issue` (New Issue Radar storyboard) | `gpt-5.4-mini` | `FOUNDRY_MODEL_TRADING` |
| `morning-brief` (synthesizer) | `gpt-4o-mini` | `FOUNDRY_MODEL_MORNING` |
| `event-specialist` (per-event fan-out) | `gpt-5.4-nano` | `FOUNDRY_MODEL_SPECIALIST` |
| `markets-assistant` (AI Chat) | `gpt-4o-mini` | `FOUNDRY_MODEL_CHAT` |
| `trading-desk-assistant` (Trading Desk Open Chat) | `gpt-4o-mini` | `FOUNDRY_MODEL_CHAT` |
| `briefing-synthesizer` (shared contract) | `gpt-5.4-mini` | `FOUNDRY_MODEL` |

`FOUNDRY_MODEL_MORNING` / `FOUNDRY_MODEL_SPECIALIST` fall back to `FOUNDRY_MODEL` if unset;
`FOUNDRY_MODEL_CHAT` falls back to `FOUNDRY_MODEL_MORNING` (the gpt-4o-mini pool), so AI Chat never
competes with the briefing synthesizers for quota. The
provisioner is authoritative about the model: it recreates each agent on its target deployment, so a
model change actually takes effect (a stored agent binds its deployment at run time). If the
provisioner has not run, the runners self-create the scene agent so LIVE still works. Both LIVE runners
attach `eventsConsidered` from the authoritative event store (not the model output) so the LIVE DTO
matches DEMO.

> Splitting the agents across **different model families** gives each a separate Azure OpenAI quota
> pool, which is what actually adds aggregate throughput (two deployments of the *same* model share one
> pool). The runners also retry transient throttling with backoff + jitter (honoring `Retry-After`,
> tunable via `FOUNDRY_RETRY_MAX_ATTEMPTS` / `FOUNDRY_RETRY_BASE_DELAY_MS`) and otherwise degrade
> gracefully; raise the per-deployment capacity for sustained heavy use.

## Deploy to Azure Container Apps

Deployment is a **3-step flow per region** (the Container Apps reference ACR images, so the
environment + images must exist before the apps). `task up -- <region>` runs all steps; the
Terraform workspace is named after the region and `DEMO_MODE` selects DEMO vs. FULL/Foundry.

```powershell
# Canada — DEMO mode (default; no Foundry, deterministic)
task up -- canadacentral

# Sweden — FULL mode (LIVE + Azure AI Foundry agent)
$env:DEMO_MODE = 'false'; task up -- swedencentral
```

`task up` runs: `cloud:apply-infra` (environment incl. ACR/Key Vault/Foundry) → `build:all`
(push images to ACR) → `cloud:apply-apps` (Container Apps) → `cloud:provision` (Foundry agent job,
FULL only) → `cloud:url`. Tear down with `task down -- <region>`.

Terraform provisions the Container Apps environment, ACR, Key Vault, managed identity, App Insights, and — in FULL mode — Azure AI Foundry (account, project, gpt-5.4-mini deployment, connection, capability hosts). Only `ui-app` has public ingress; the APIs are internal.

## Observability & traceability

Serilog structured JSON logs, a correlation id (`X-Correlation-ID`) propagated per request, and
OpenTelemetry tracing/metrics (ASP.NET Core + outbound HTTP) are wired in `src\shared\Observability`.
Tool calls surface as HTTP dependency spans, the event fan-out emits a `briefing_synthesis.fanout`
parent span with one `event_specialist.assess` child per event, and the agents' runs/tool-steps are
visible in the Foundry portal. Full agent traceability to Application Insights (Azure Monitor exporter,
GenAI spans, per-tool-call spans, token usage) is tracked in
[`specs/_backlog/005-observability.md`](specs/_backlog/005-observability.md).

## Quality gates

```powershell
dotnet build AgenticTradersDesk.sln --nologo
dotnet test AgenticTradersDesk.sln --nologo
npm --prefix src\ui-app test
terraform -chdir=infra fmt -check
terraform -chdir=infra validate
gitleaks detect --source . --no-banner
```

## Layout

```
src\ui-app\              React cockpit (Landing, Trading Desk, RM, Trading, Cockpit, AI Chat, News Desk) + nginx reverse proxy
src\orchestration-api\   /api/agent/{scene}(+/stream), /api/events, /api/chat, DEMO/LIVE runners + event fan-out
src\mock-api\            fictional system-of-record endpoints + reactive event store (/mock/events)
src\agent-provisioner\   idempotent Foundry agent registration job (7 agents)
src\shared\Observability\ Serilog, OTEL, correlation id, JSON errors
infra\                   Terraform for ACA, ACR, Key Vault, Foundry
tasks\                   Taskfile includes for local, build, and cloud workflows
contracts\               morning-brief and agent API schemas
openapi\tools.yaml       tool contract for Foundry/MCP import
samples\                 sample upload files (e.g. lead-left-deals.csv for the New Issue Radar)
```
