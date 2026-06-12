# Agentic vs. Synthetic — what is real intelligence vs. scripted in the demo

> **All demo data is fictional.** This document explains, page by page, which parts of the
> M.INT trading cockpit are produced by an **agent** (Azure AI Foundry, Microsoft Agent
> Framework) versus **synthesized deterministically** in code, and which parts **react to
> injected news events**. It is the companion to [`architecture.md`](architecture.md) and the
> demo script in [`demo-talk-track.md`](demo-talk-track.md).

## The one idea to internalize: "agentic vs. synthetic" is a runtime **mode**, not a feature

Every scene is **mode-blind** (constitution Principle III / FR-010). The same React component
renders the **identical DTO** whether it was produced by:

- **DEMO mode** — a **deterministic C# composer** that reads the fictional systems-of-record over
  HTTP and computes the result with fixed rules. Offline, byte-stable, never calls a model. This is
  the **"synthetic"** path. It is the default and what you should run for a reliable live demo.
- **LIVE mode** — a **persistent Foundry agent** (Microsoft Agent Framework) that reasons over the
  **same** systems-of-record via client-side tools and emits the same DTO shape. This is the
  **"agentic"** path. On any failure or empty/low-quality model output it **degrades to the DEMO
  composer** (re-stamped `LIVE`) so the demo is never blocked.

So "is this agentic?" really means "**which mode is this container running in?**" — set by `MODE` /
`DEMO_MODE`. The data is always the same fictional `/mock/td/*` and `/mock/cb/*` records; the model
**never invents numbers** — it selects and ranks records the tools return.

What is **always synthetic** (never a model), in both modes:

- All **chrome**: header, clock, KPI stat bar, scrolling ticker, sidebar nav, the landing chooser.
- The **systems-of-record data** itself (`mock-api` fixtures) — it is fictional seed data.
- The **DEMO composers** and the **reactive event fold-in** (deterministic ranking / matching code).

What is **agentic only in LIVE**:

- The **reasoning + ranking** that turns raw records into the briefing/storyboard (the synthesizer
  agent), and in the briefings the **per-event multi-agent fan-out** (one `event-specialist` per event
  → `briefing-synthesizer`).

## Per-page breakdown

| Page (route) | Output DTO | Synthetic (always) | Agentic (LIVE only) | Reacts to injected events? |
|---|---|---|---|---|
| **Landing** (`/`) | — | Entire page (static chooser) | — | No |
| **Trading Desk** (`/desk`) | `TdBriefing` | Chrome, KPIs, ticker; DEMO composer ranking; event re-rank fold-in | Synthesizer agent + per-event fan-out builds & ranks the call list | **Yes** — SSE re-rank |
| **Morning Brief** (`/desk/morning-brief`) | `TdBriefing` | Same as `/desk` (shares the brief) | Same as `/desk` | **Yes** — SSE re-rank (shared brief) |
| **New Issue Radar** (`/desk/new-issue`) | `TdNewIssueStoryboard` | Chrome; DEMO composer storyboard; event fold-in (`TdNewIssueLive`) | Storyboard agent (`trading-desk-new-issue`) reasons & emits the 4 beats | **Yes** — SSE fold-in *(new)* |
| **Commercial Banking RM** (`/cb`, `/rm-briefing`, `/`) | `RmBriefing` | Chrome; DEMO composer ranking; event re-rank | Synthesizer + per-event fan-out | **Yes** — SSE re-rank |
| **Municipal Morning Brief** (`/morning-brief`) | `MorningBrief` | Chrome; DEMO composer | Synthesizer agent + tools | **Yes** — SSE re-rank |
| **AI Chat** (`/chat`) | `ChatReply` | Chrome; DEMO intent router | `markets-assistant` agent over the same tools | Indirectly (reads current events) |
| **Trading Desk "Open Chat"** (`/desk*`, `salespersonId`) | `ChatReply` | Chrome; DEMO intent router (`TdChatResponder`) | `trading-desk-assistant` agent over the `/mock/td/*` tools | Indirectly (reads current events) |
| **News Desk** (`/admin`) | `MarketEvent` | Entire operator UI | — | It is the **injector** |

### Trading Desk (`/desk`, `/desk/morning-brief`) — `TdBriefing`

- **Synthetic / always:** the command-center shell, the KPI bar and ticker (derived from the brief),
  and — in DEMO — the entire prioritized call list. The DEMO composer (`TdBriefingComposer` +
  `EventImpactResolver`) caps & sums component scores (news/research, RFQ, inquiry, axe, CRM), ranks,
  and assigns P1–P4 by rank. Byte-stable.
- **Agentic / LIVE:** the `trading-desk-morning` Foundry synthesizer binds `TdBriefingTools`, runs the
  per-event specialist fan-out, and emits the same `TdBriefing`. Degrades to the DEMO composer on
  failure.
- **Reacts:** **yes.** Injecting the **AI-capex breaking print** (tickers `SEC-3003`/`SEC-3002`) from
  the News Desk re-ranks Hyperion & Tradewinds to the top within ~10s, with the driving events
  highlighted on each call card ("⚡ RE-RANKED BY LIVE EVENTS").

### New Issue Radar (`/desk/new-issue`) — `TdNewIssueStoryboard`  *(now reactive)*

- **Synthetic / always:** the shell, the four-beat progress rail, and — in DEMO — the storyboard
  itself. `TdNewIssueComposer` reads the issuer's equity interest, the new debt tranche, and the focus
  client's holdings/RFQs/trades/CRM, then derives every figure deterministically.
- **Agentic / LIVE:** the `trading-desk-new-issue` Foundry agent reasons over the same records and
  emits the four beats (`announcement → holdings → activity → outreach`) + the outreach card. Degrades
  to the DEMO composer on failure.
- **Reacts:** **yes (new).** A shared deterministic helper, `TdNewIssueLive.ApplyEvents`, folds any
  injected event that touches the **issuer** (Prairie Green Renewables), either **tranche**
  (`SEC-3601`/`SEC-3602`), the **sector** (Utilities), or the **focus client** (`CL-2015`) into the
  storyboard: a `LIVE` evidence row on the announcement beat, a `live` metric on the announcement +
  outreach beats, a leading talking point, and `liveEvents[]`. The same helper runs on the one-shot
  `POST` (folds in the current store) and on `GET /api/agent/td-new-issue/stream` (re-synthesizes and
  pushes a snapshot + `LiveAlert` on each new event). Because it runs **after** compose/run, DEMO/LIVE
  stay byte-stable and neither composer nor agent had to change.

  > **Important distinction from the briefings.** The briefings re-rank a *list* — an event changes
  > *who is #1*. The New Issue Radar is a *single guided narrative about one deal*; a live event does
  > not reorder anything, it **augments the story in place** (a fresh "this just crossed" data point
  > the salesperson leads the call with). That is the right behavior for a storyboard: the punchline
  > (call Crestline now) is stable; the evidence gets richer as news breaks.

### Commercial Banking RM / Municipal Morning Brief / AI Chat

Same DEMO-composer-vs-Foundry-agent split. The RM and municipal briefings re-rank reactively over SSE;
AI Chat reads the **current** event store each turn (so it reflects injects) but does not hold an SSE
subscription. The **Trading Desk "Open Chat"** mirrors AI Chat for the institutional desk: the same
floating dock, parameterised with a `salespersonId` so `/api/chat` routes to the trading-desk-grounded
assistant (`TdChatResponder` in DEMO, `trading-desk-assistant` Foundry agent in LIVE) over the
`/mock/td/*` tools. Each `TdCallCard` and the New Issue outreach card seed it with the client in
context.

## How reactivity actually works (shared plumbing)

```
/admin News Desk ──POST /api/events──► mock-api EventStore (server-sets id, scope=intraday, origin=admin)
                                              │
        BriefingEventStream poller diffs the store every SSE_POLL_INTERVAL_MS
                                              │  per (scene, persona)
                                  re-synthesize the scene DTO
                                   ├─ briefings: re-rank list (fan-out in LIVE)
                                   └─ new-issue: TdNewIssueLive.ApplyEvents folds drivers in
                                              │
                       ONE coalesced  event: briefing-update  (full DTO + LiveAlert)
                                              ▼
                 ui-app EventSource ──► applies DTO in place + shows LiveAlertBanner
```

- An injected item flows through the **same** ingestion + reactive path as a real feed (FR-016) —
  there is no "demo-only" shortcut.
- The first frame after (re)connect is a **baseline snapshot** with `noImpact: true` (no toast). Only
  events that arrive **after** you are subscribed and that **match** the scene/persona produce an
  impactful `LiveAlert`.
- Matching is by **typed selectors** (`customerIds`, `tickers`, `sectors`, `issuers`) on the event vs.
  the entities in view — never broad text matching.

## Quick "is it agentic right now?" checklist for presenters

1. Look at the **mode chip** on the page (and the `mode` field in the DTO): `DEMO` = synthetic
   composer; `LIVE` = Foundry agent.
2. The container's `MODE`/`DEMO_MODE` env var decides it. For a guaranteed-smooth demo, run **DEMO**;
   to show the agent + Foundry traces, run **LIVE** (it still degrades to DEMO if the model stumbles).
3. Either way the **numbers are real records**, the **reactivity is identical**, and the **UI is the
   same** — that is the whole point of mode-blindness.
