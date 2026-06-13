# Demo Talk Track — Institutional Sales & Trading cockpit (M.INT)

> **Audience:** sales-and-trading desk (covers institutional accounts like large asset managers).
> **Persona on screen:** *Theo Wexler*, a coverage salesperson. **All data is fictional.**
> **Goal of the demo:** show an agent-driven "morning planning & prioritized outreach" cockpit that
> (1) tells the desk *who to call first and why*, (2) walks a new-issue opportunity end-to-end, and
> (3) **reacts live** when news breaks — the same intelligence, whether running the deterministic
> composer (DEMO) or the Foundry agent (LIVE).

**Companion docs:** [`agentic-vs-synthetic.md`](agentic-vs-synthetic.md) (what's agent vs. scripted),
[`architecture.md`](architecture.md) (how it's built).

## The big picture (one slide)

![Agentic Trading Intelligence — from market noise to actionable client signals: an AI signal filter feeds an orchestrator that runs a Who → Why → How → Talk Track pipeline plus parallel market-context agents, surfacing prioritized signals to a human-in-the-loop trader dashboard.](<Traders Agent Diagram.png>)

Use this as the opening slide. The story in one breath: *hundreds of market events a second → an AI
filter keeps only what's relevant to **this** desk → agents work out **who** to call, **why** they
care, **how** to engage, and the exact **talk track** → it lands on the trader's dashboard, and the
**trader decides**.* The rest of the demo is this picture, live.

---

## Before you start (presenter setup)

- **URL (Sweden deploy):** `https://ui-app.politefield-f4f3b782.swedencentral.azurecontainerapps.io`
- **Tabs to pre-open:**
  1. Trading Desk — `/desk`
  2. New Issue Radar — `/desk/new-issue`
  3. News Desk (operator) — `/admin`  *(keep this on a second screen / hidden tab)*
- **Mode:** for a guaranteed-smooth run, present in **DEMO** (deterministic, offline, byte-stable). If
  you want to show real agent reasoning + Foundry traces, run **LIVE** — it degrades to DEMO
  automatically if the model stumbles, so the screen never breaks. The mode chip on each page tells the
  audience which is active; the experience is identical either way.
- **One sentence to open with:** *"This is the desk's morning cockpit. Everything you see is grounded
  in our own systems-of-record — holdings, RFQs, trades, CRM calls, the news feed — and it's either
  computed deterministically or reasoned by an Azure AI Foundry agent. Same screen, same numbers."*
- **Let the cockpit load before you inject (LIVE).** In LIVE the first briefing is the slow part — the
  Foundry agent reasons over the book for ~40–60s on a cold replica. Open `/desk` (and
  `/desk/new-issue`) and wait for the call list to render *before* you trigger any News Desk inject.
  Once the baseline is up, the live re-rank lands in **a second or two** — the inject is folded by a
  fast deterministic overlay, not a full agent re-run. (DEMO is instant from the first load.)
- **Reset between runs.** Admin-injected headlines live in the mock-api's in-memory store. To start a
  clean run, restart it: `task cloud:reset-mock-api` — injected events drop and the seed events reload.
  The cockpit self-heals across that reset (the live channel re-seeds), so you can inject → reset →
  inject again all morning without redeploying.
- **LIVE is rate-limited.** Model calls now pass through a global rate limiter
  (`MODEL_MAX_CONCURRENCY` / `MODEL_MIN_INTERVAL_MS`) to curb Foundry `429`s. For a single presenter
  this is invisible; if several people hammer LIVE at once, some briefings may degrade to DEMO (the
  screen stays correct).

---

## Scene 1 — The morning call list (`/desk`) · ~3 min

**What it answers:** *"Which clients do I call this morning, and what do I lead with?"*

1. Land on `/desk`. Point out the **command-center shell**: clock, KPI bar, scrolling ticker, mode
   chip. *"This is the desk at 7am."*
2. Walk the **prioritized call list**. For the top call card, read out:
   - the **priority band** (P1–P4) and the **composite score**,
   - the **why-now drivers** (overnight news/research, open RFQs, client inquiries, desk axes matched to
     the client's holdings),
   - the **talking points** and **trade idea**.
3. **Key message:** *"The desk didn't sort this by hand. The agent pulled every client's holdings, the
   overnight news, the desk's axes, and the recent RFQ flow, scored each account, and ranked them. In
   LIVE mode that's a Foundry agent reasoning over tools; in DEMO it's the same logic computed
   deterministically. The numbers are real records — the model never makes them up."*

> **Talking point if asked "is this just a static page?":** No — see Scene 3. It re-ranks the moment
> news breaks.

---

## Scene 2 — New Issue Radar: a guided opportunity (`/desk/new-issue`) · ~4 min

**What it answers:** *"A new deal just got announced — who in my book do I call first, and what's the
pitch?"* This is a **guided, four-beat storyboard** about one deal.

The narrative: **Prairie Green Renewables** announces a **concurrent debt + equity issue**. The desk's
edge is spotting that an existing client — **Crestline Capital** — both **holds ~$1.0bn of Prairie
Green equity** *and* has been **actively trading the new senior note** (electronic RFQs + calls to the
desk). So they are the obvious first call, with a concrete allocation in hand.

Walk the **four beats** using the Next button (or the clickable progress rail):

1. **Announcement** — *"Overnight, Prairie Green is bringing debt and equity. That's the trigger."*
   Point to the announcement evidence (the news record) and the two tranches (equity `SEC-3601`, the
   new 6.00% 2034 senior note `SEC-3602`).
2. **Holdings cross-reference** — *"Who already owns this name? Crestline is long ~$998.8mm of the
   equity — about 4.8% of their book. They have a direct stake in how this prices."*
3. **Recent flow & conversations** — *"And they've been trading the credit — 5 electronic RFQs,
   $69.6mm lifted this month — and they've **called us** asking to be first in line on new paper. This
   is a live, engaged account, not a cold call."*
4. **Prioritized outreach** — the **call card**: talking points, a **Buy** trade idea on the new note
   (~99.65, against the desk's 50mm distribution axe), the suggested action, and a **ready-to-send
   draft message**.

**Key message:** *"This is the prep a salesperson would normally spend 30 minutes assembling across
five systems. The agent did it as one narrative, with the receipts attached to every claim."*

> **Agentic vs. synthetic note (if asked):** the storyboard's reasoning is the agent in LIVE / the
> deterministic composer in DEMO. The four beats and the call recommendation are the same DTO either
> way. The chrome and the underlying records are always the same fictional systems-of-record.

---

## Scene 3 — It reacts when news breaks (live) · ~4 min

This is the "wow" moment. **Two reactions to show**, both injected from the **News Desk** (`/admin`),
which routes through the *same* ingestion path a real feed would use.

### 3a. The call list re-ranks (`/desk`)

1. Have `/desk` visible. On the News Desk, click **"Inject AI-capex breaking print"** (the one-click
   Marquee Re-Rank preset — an overnight AI-capex upgrade hitting the AI-compute basket, tickers
   `SEC-3003`/`SEC-3002`).
2. Switch back to `/desk`. Within **a second or two** a **live alert banner** appears and **Hyperion &
   Tradewinds jump to the top** of the call list, each card showing a **"⚡ RE-RANKED BY LIVE EVENTS"**
   callout naming the driving event. *(The callout only renders for a real, event-driven re-rank — the
   baseline call list never shows an empty box.)*
3. **Key message:** *"The desk's morning plan isn't a snapshot — it's live. News broke, the agent
   re-scored the book, and the call order changed in front of you. No refresh, no re-run."*

> **Prefer a custom, grounded positive print?** Use the curated **Quartzite Semiconductors (QRTX,
> `SEC-3003`)** earnings beat in [News Desk headlines](news-desk-headlines.md#headline-1--quartzite-semiconductors-qrtx-earnings-blowout--sec-3003)
> — it's engineered to move Theo Wexler's book (clients hold ~$61mm, are actively buying, and the desk
> is axed to sell), so the re-rank has a concrete reason to lead the call.

### 3b. The New Issue Radar folds in breaking news (`/desk/new-issue`)  *(new)*

1. Have `/desk/new-issue` visible (storyboard already loaded).
2. On the **News Desk** (`/admin`), submit a custom event targeting the deal. Fill the form:
   - **Headline:** `Prairie Green senior note upsized to $1.5bn on strong demand`
   - **Summary:** `Books 4x covered; pricing tightens 15bp.`
   - **Type:** `Issuer credit`  ·  **Severity:** `High`  ·  **Direction:** `Positive`
   - **Issuers:** `Prairie Green Renewables`  *(or **Tickers:** `SEC-3602`)*
   - Submit.
3. Switch back to `/desk/new-issue`. Within **a second or two** a **live alert banner** appears, the
   ticker shows the breaking headline, and the storyboard **folds the event in**: a **LIVE** evidence
   row on the *Announcement* beat, a highlighted **LIVE** metric, and a **new leading talking point** —
   *"Just crossed — Prairie Green senior note upsized… Lead the call with it."*
4. **Key message:** *"Notice the difference in behavior. The call list **re-ranks** — news changes
   *who* is #1. The new-issue storyboard is about *one deal*, so news doesn't reorder anything — it
   **enriches the pitch in place**. The salesperson now opens the call with the freshest data point,
   and the rest of the story still holds. That's the right reaction for each surface."*

> **Why this matters technically (optional aside):** both reactions use the **same** event store, the
> same SSE channel, and the same matching by typed entities (issuer / ticker / sector / client). The
> new-issue fold-in is a small deterministic helper applied **after** the agent/composer runs — so
> DEMO and LIVE behave identically and the agent didn't have to change.

---

## Scene 4 — Wrap (~1 min)

- **Three takeaways:**
  1. **Prioritized outreach, grounded.** The cockpit tells the desk *who to call and why*, with every
     claim traceable to a system-of-record.
  2. **Agentic where it counts.** In LIVE, Azure AI Foundry agents (Microsoft Agent Framework) do the
     reasoning and ranking; in DEMO the same logic is deterministic. The UI is mode-blind — same
     experience, fully auditable in the Foundry portal when LIVE.
  3. **Live, not static.** New information re-ranks the call list and enriches the new-issue pitch in
     real time, through the same path a production feed would use.
- **Close:** *"This is a pattern, not a one-off — the same three-layer design (mode-blind UI →
  orchestration → tools over HTTP) drops onto any desk or any book."*

> **Optional "superpower" beat (LIVE only):** show that the desk's playbook is *editable English*. Open
> `src/orchestration-api/Prompts/trading-desk-morning.md`, paste the **"flow-trading day"** override, re-run
> `/desk`, and the **call list re-orders** — same data, different prompt. Full recipe in the
> [prompt-tuning demo](prompt-tuning-demo.md). Pairs naturally with a News Desk inject: change the
> *instructions*, then change the *inputs*.

---

## Quick reference — inject cheat-sheet

| Reaction | Where to watch | News Desk action | Targets | Expected result |
|---|---|---|---|---|
| Call list re-rank | `/desk` | Click **Inject AI-capex breaking print** | tickers `SEC-3003`,`SEC-3002` | Hyperion & Tradewinds jump to top (~1–2s), driving-event callouts |
| Call list re-rank (custom, grounded) | `/desk` | Submit **QRTX earnings beat** — see [News Desk headlines](news-desk-headlines.md#headline-1--quartzite-semiconductors-qrtx-earnings-blowout--sec-3003) | ticker `SEC-3003`, issuer `Quartzite Semiconductors`, Technology / High / Positive | QRTX holders (Forge Hill, Crestline) jump up with RE-RANKED callouts (~1–2s) |
| New Issue fold-in | `/desk/new-issue` | Submit custom: *Prairie Green senior note upsized…*, Issuer credit / High / Positive | issuer `Prairie Green Renewables` **or** ticker `SEC-3602` | LIVE banner + LIVE evidence/metric + new leading talking point (~1–2s) |
| Prompt re-tune (LIVE only) | `/desk` | Paste the **"flow-trading day"** override into `trading-desk-morning.md`, re-run — see [prompt-tuning demo](prompt-tuning-demo.md) | the prompt itself (needs `FOUNDRY_RECREATE_AGENTS=true`) | Call list **re-orders** — hottest open-RFQ client leads, cards lead with RFQ not news |

## Troubleshooting

- **No live reaction within a few seconds?** Confirm the page was already loaded *before* you injected
  (the SSE subscription opens once the page renders), and that the event's targets match (issuer/ticker/
  sector/client). The first frame on connect is a baseline snapshot with no toast — only events that
  arrive *after* you subscribe and *match* the scene trigger an alert. In LIVE, also make sure the
  baseline call list has finished its ~40–60s cold-start *before* you inject; once it's up, re-ranks
  land in a second or two.
- **Model looks slow/odd in LIVE?** It degrades to the deterministic composer automatically; the screen
  stays correct. LIVE model calls are rate-limited to curb Foundry `429`s, so under heavy concurrent
  load some briefings may take the DEMO path. For a high-stakes audience, present in DEMO.
- **Want to reset between runs?** Run `task cloud:reset-mock-api` (restarts mock-api → injected
  headlines drop, seed events reload). The live channel self-heals across the restart, so a fresh inject
  after a reset still fires a re-rank. Re-running a scene (the "Re-run radar" / refresh control) also
  re-pulls a clean baseline; the New Issue storyboard folds in whatever is currently in the event store.
