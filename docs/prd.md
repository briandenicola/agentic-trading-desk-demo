# Product Requirements — WF-Garage: Morning Planning & Prioritized Outreach

> **Status**: Living document for the active demo build.
> **Scope**: An interactive **Client CV** demo for Commercial Banking RMs and a Municipal Sales desk.
> All data is **fictional**. DEMO mode is deterministic, offline, and the default; LIVE mode engages
> Azure AI Foundry. The binding rules for this product live in
> [`.specify/memory/constitution.md`](../.specify/memory/constitution.md); the stack decision is
> [ADR-0002](adr/0002-csharp-foundry-aca-stack.md).

## 1. Problem & opportunity

A Relationship Manager (or Municipal Sales VP) starts the day asking *"What do I need to know this
morning, and who should I call first?"* Today that situational awareness is assembled by hand from
market data, the CRM, the complaints queue, and the pipeline. It is slow, inconsistent, and stale by
mid-morning when new events break.

The opportunity is an **AI-assembled, event-reactive morning briefing**: a single screen that fuses a
market narrative, the overnight **and** intraday events considered, the most-affected fictional
clients, a ranked outreach list with talking points, and an editable human-in-the-loop call plan —
that keeps reacting as new events arrive during the day.

## 2. Goals & non-goals

**Goals**
- Turn the static *Demo 1 — Morning Pre-Market Planning* storyboard into a working interactive demo.
- Produce a prioritized, explainable call list grounded only in the mock systems-of-record.
- React to new events (overnight seeds + operator-injected intraday news) and push updates live.
- Run agents in **Azure AI Foundry** via the **Microsoft Agent Framework (C#/.NET 10)**; run the UI
  and APIs in **Azure Container Apps**.
- Maintain strict **LIVE/DEMO parity**: the same JSON shape per scene in both modes.

**Non-goals**
- No real market-data vendors or real customer data — everything is fictional.
- No production authentication/authorization (only scaffolding); see backlog `002-authentication`.
- No real-data connectors; see backlog `001-real-data-connectors`.

## 3. Personas

| Persona | Need |
|---|---|
| **Commercial Banking RM** (primary) | A prioritized daily call list with the reason each client matters (complaints, stuck opportunities, overdue follow-ups, today's events). |
| **Municipal Sales VP / Trader** | A cross-asset morning brief with a market narrative, most-affected clients, and ranked outreach + talking points. |
| **Sales-desk operator** | A News Desk to inject intraday news the agents react to, simulating an event breaking mid-day. |

## 4. Functional surfaces (scenes)

| Route | Scene | Output | Purpose |
|---|---|---|---|
| `/`, `/rm-briefing` | **RM Daily Briefing** (PRIMARY) | `RmBriefing` | RM briefing + prioritized call list with KPIs. |
| `/morning-brief` | **Trading Morning Brief** | `MorningBrief` | Municipal-sales morning brief + ranked outreach + editable plan. |
| `/cockpit` | **Cockpit** | — | 3-column M.INT dashboard (Client / Ticker / Overall "Morning Call") with a live alert banner. |
| `/chat` | **AI Chat** | `ChatReply` | Grounded Markets-Intelligence assistant — multi-turn Q&A over the same systems-of-record. |
| `/admin` | **News Desk** | `MarketEvent` | Operator UI to inject intraday news the agents react to. |

## 5. Key user stories (from `specs/001-*` and `specs/002-*`)

1. **Run the morning brief (P1)** — Ask "What do I need to know this morning?" and receive one brief:
   a macro narrative of overnight events, the most-affected clients with the reason, and a ranked
   outreach list — sourced only through the mock systems-of-record.
2. **Prioritized outreach with talking points (P1)** — The outreach list is ranked by a blend of
   wallet, recent engagement, and today's event relevance; each entry has a suggested topic and
   personalized talking points.
3. **Editable, human-in-the-loop plan** — The RM/VP can edit talking points and remove clients; the
   plan tracks an edited state.
4. **React to multiple & intraday events (002)** — Overnight events fan out to per-event specialist
   agents; an operator can inject new events through the News Desk and open briefings re-synthesize
   and update live over SSE.
5. **Grounded AI Chat** — Ask follow-up questions (who to call, a specific customer, the market,
   complaints, pipeline) and get answers grounded in the same data, in DEMO or LIVE.

## 6. Non-functional requirements

- **Three-layer architecture** (constitution Principle II): UI → orchestration-api → mock-api. Data
  flows left-to-right only; the frontend is mode-blind.
- **Tools over HTTP** (Principle II / FR-002): orchestration code reaches data **only** through the
  mock-api HTTP seam defined by [`openapi/tools.yaml`](../openapi/tools.yaml); it never reads fixtures
  in-process.
- **LIVE/DEMO parity** (Principle III / FR-010): identical JSON shape per scene in both modes,
  including the `eventsConsidered` it weighed.
- **Resilience**: per-role model deployments (separate quota pools) + retry-with-backoff on transient
  429/503/408 to avoid throttling; LIVE briefings have a deterministic safety net so the call list is
  never empty.
- **Security baseline**: no secrets in source (`gitleaks`); configuration via env vars / Key Vault;
  CORS tightened for deployed origins; containers run as non-root; Key Vault public network access
  disabled in deployed environments.
- **Observability**: Serilog structured logs, a propagated correlation id, and OpenTelemetry
  tracing/metrics; the event fan-out and agent runs are traceable (see
  [`docs/architecture.md`](architecture.md)).

## 7. Success criteria

- The RM Daily Briefing and Trading Morning Brief render end-to-end in DEMO with deterministic,
  repeatable output suitable for on-stage use, sourced only through the mock APIs.
- In LIVE mode the same scenes return the same shape via Foundry agents + the per-event fan-out, with
  a populated call list.
- An operator-injected News Desk event updates an open briefing live within seconds.
- AI Chat answers grounded follow-up questions in both DEMO and LIVE.
- All quality gates pass: `dotnet build`/`dotnet test`, UI build/tests, `terraform fmt`/`validate`,
  `gitleaks`.

## 8. Out of scope / backlog

Tracked under [`specs/_backlog/`](../specs/_backlog/): real-data connectors (001), authentication
(002), full observability export (005), production frontend (006), Foundry migration hardening (007),
MCP server (008), multi-turn memory (009), evaluation framework (010), multi-agent synthesis (011).

## 9. References

See [`docs/references.md`](references.md) for the full list, [`README.md`](../README.md) for the
quickstart, and [`docs/architecture.md`](architecture.md) for the agent/orchestration/traceability
design.
