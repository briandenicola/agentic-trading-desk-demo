# Architecture — Agents, Orchestration & Traceability

> All demo data is fictional. DEMO mode is deterministic, offline, and the default;
> LIVE mode engages Azure AI Foundry. Both return the same `MorningBrief` JSON shape
> (Principle III / FR-010).

## Three-layer flow

```
src\ui-app\ ─ POST /api/agent/morning-brief ─► src\orchestration-api\
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

The morning-brief agent is **persistent** in Foundry — registered once, reused on every request.

- **`src\agent-provisioner\`** is the single registrar. It idempotently creates/updates the
  persistent agent named `morning-brief` (instructions from `Prompts\morning-brief.md`, model
  `FOUNDRY_MODEL`). It runs as a Container Apps **Job** in FULL mode (`task cloud:provision`).
- **`AgentRunner`** (runtime) looks the agent up **by name** via `PersistentAgentsAdministrationClient`
  and reuses it through `AIProjectClient.GetAIAgentAsync(id, tools)`, attaching the mock-api tools
  for client-side execution. If the agent is not found (provisioner has not run yet) it falls back to
  creating one so LIVE still works.

This replaces the earlier per-request `CreateAIAgentAsync` pattern, which created a brand-new agent
on every call — those agents accumulated in the project and fragmented run history. With a single
persistent agent, **all runs/threads appear under one agent** in the Foundry portal.

### The 7 tools

`get_market_data`, `get_news`, `get_relative_value`, `get_client_value_all`, `get_engagement`,
`search_holdings`, `get_axes` — wrappers over `openapi\tools.yaml` endpoints. They never throw;
failures return a structured `{"error": …}` object so the loop degrades gracefully (FR-011).

## Traceability (current state + roadmap)

**Today**

- Serilog structured JSON logs with a correlation id propagated per request
  (`X-Correlation-ID`), wired in `src\shared\Observability`.
- OpenTelemetry tracing/metrics with ASP.NET Core + outbound-HTTP instrumentation, so tool calls
  surface as HTTP dependency spans.
- The persistent agent's runs and tool-call steps are visible natively in the **Foundry portal**
  (thread/run view), because runs are no longer scattered across throwaway agents.

**Known gaps (tracked in `specs\_backlog\005-observability.md`)**

- The OTLP exporter only attaches when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, so traces currently
  export nowhere in the container apps (only `APPLICATIONINSIGHTS_CONNECTION_STRING` is injected).
- No Azure Monitor OpenTelemetry exporter is wired yet (`Azure.Monitor.OpenTelemetry.AspNetCore`
  is pinned but unused).
- The Agent Framework GenAI `ActivitySource` is not registered, so LLM-level "which tool and why"
  (`gen_ai.*`) spans and token usage are not captured.
- No explicit per-tool-call span (tool name, args, duration) or single agent-run span correlating
  the full UI → agent → tool → mock-api chain.

**Roadmap → full traceability** (backlog): add the Azure Monitor OTel exporter keyed off the App
Insights connection string, register the GenAI activity source, emit an agent-run span plus a span
per tool call (with token usage), and enable message-content capture (data is fictional). See
`specs\_backlog\005-observability.md`.

## Future: multi-agent fan-out / synthesis

The current design is a single agent with client-side tools. The intended evolution is a
**multi-agent flow**: each data pull (market, news, clients, holdings, engagement, …) becomes its
own Foundry agent that returns structured data to a central **synthesizer** agent that composes the
brief. Tracked in `specs\_backlog\011-multi-agent-synthesis.md`.
