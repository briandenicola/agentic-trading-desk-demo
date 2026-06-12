# Prompt-tuning demo — change the prompt, change the outcome

> A ready-to-run way to show the **"superpower" of prompt engineering** live: edit the morning-planning
> agent's prompt, re-run the scene, and watch the **prioritized call list visibly re-order** — same data,
> same tools, same UI, different instructions. Mirrors the [News Desk headline injects](news-desk-headlines.md)
> in spirit (a single, grounded, paste-ready change with a known outcome). **All data is fictional.**

The morning briefing on `/desk` is produced by the **`trading-desk-morning`** Foundry agent
(Microsoft Agent Framework, LIVE) from one prompt file:

```
src/orchestration-api/Prompts/trading-desk-morning.md
```

That prompt is the *only* thing that decides **who the desk calls first and why**. The agent reaches all
data through the same `/mock/td/*` tools no matter what the prompt says — so when you change the prompt,
the audience sees the **reasoning and ranking change**, not the data. That is the point: the business logic
lives in editable English, not in compiled code.

> ⚠️ **This is a LIVE-mode showcase.** DEMO mode is a deterministic C# composer (`TdBriefingComposer`) and
> is **prompt-independent by design** (Principle III parity) — it will *not* reflect a prompt edit. Run the
> demo with `DEMO_MODE=false` against Foundry.

---

## Why a prompt edit can change the call list

The default prompt ranks each client by a **composite score** that leads with overnight **news & research**
(weighted `≤100`), with live trading activity (RFQs `≤60`, inquiries `≤60`), desk-axe match (`≤60`) and
CRM urgency (`0/10/22`) underneath. So on a normal morning the **news-driven names** rise to the top.

Change that single instruction and the desk's whole morning changes. The canonical demo edit flips the desk
into a **"flow-trading day"**: *live client trading intent beats overnight news.* Clients with hot **open
RFQs / inquiries** jump the queue, and every call now **leads with the trade the client is already trying to
do** instead of the macro headline.

---

## Where the prompt actually lives (read this first)

The prompt file is **real**, but it's the **seed**, not the live copy:

1. `trading-desk-morning` is *runtime-managed* — the provisioner deletes it, and the LIVE runner
   **creates** the agent in Foundry **from the file** on the first `/desk` run.
2. After that, every run calls `GetAIAgentAsync(name, tools)` → it **reuses the agent that already lives in
   Foundry**, whose instructions are stored **server-side**. The file is no longer read on reuse.

That gives you **two ways** to tune the prompt for the demo. Pick based on where you're running:

| Method | Best for | Edit happens in | Needs redeploy? | Version-controlled? |
|---|---|---|---|---|
| **A — Foundry portal** | The **deployed** demo (e.g. Sweden) | The agent's *Instructions* in the Azure AI Foundry portal | ❌ No | ❌ No (lives only in Foundry until the agent is recreated) |
| **B — Prompt file + toggle** | **Local** iteration; committing the change | `Prompts/trading-desk-morning.md` in the repo | Local: no · Deployed: yes (image bake) | ✅ Yes |

---

## Method A — edit the agent in the Foundry portal (recommended for the deployed demo)

Because the runner **reuses** the server-side agent, editing its Instructions in the portal takes effect on
the very next `/desk` run — **no code, no redeploy, no toggle.** This is the cleanest "very obvious" lever
against the deployed environment.

1. **Materialize the agent once.** Hit `/desk` in LIVE so the runner creates `trading-desk-morning` in
   Foundry from the shipped prompt. (Skip if it already exists.)
2. In the **Azure AI Foundry portal**, open your project → **Agents** → **`trading-desk-morning`** →
   **Instructions**. This text is the live prompt — note the current top of the call list on `/desk` first.
3. **Paste the edit** (the [flow-trading-day block](#the-canonical-edit--flow-trading-day) or the
   [risk-off variant](#variant--change-the-persona-guaranteed-visible-text-change)) into Instructions →
   **Save**.
4. **Re-run `/desk`.** The reused agent applies your portal edit immediately — the call list re-orders.

> ⚠️ **Two things that will wipe a portal edit:**
> - Setting `FOUNDRY_RECREATE_AGENTS=true` (Method B) — it deletes + rebuilds the agent from the *file* each run.
> - Re-running the **agent-provisioner** — it deletes runtime-managed agents (the next `/desk` recreates from the file).
>
> So for Method A, keep the toggle **off** and don't re-provision mid-demo. The portal edit is **ephemeral**
> (not in git) — to make it permanent, also land it in `Prompts/trading-desk-morning.md`.

**Revert (Method A):** restore the original Instructions text in the portal and Save, **or** delete the
agent (portal or provisioner) and re-run `/desk` to recreate it from the shipped file.

---

## Method B — edit the prompt file (recommended for local iteration)

For local LIVE work — or to commit the change — edit the repo prompt file. Normally the runner reuses the
persistent agent, so a file edit only lands once the agent is **recreated**. The orchestration API has an
opt-in toggle that makes this a clean **edit → re-run** loop:

| Env var | Effect |
|---|---|
| `FOUNDRY_RECREATE_AGENTS=true` | Before each LIVE `/desk` run, the runner **deletes** the persisted `trading-desk-morning` agent and **rebuilds it from the current prompt file**. Your edit is live on the very next re-run — no provisioner step, no redeploy. |
| *(unset / `false`)* | Normal behavior — the persistent agent is reused (faster, cheaper). Leave it off outside the demo. |

> Implemented in `TdAgentRunner.CreateFoundryAgentAsync` (`ShouldRecreateAgents()`); accepts
> `true`/`1`/`yes`/`on`. Costs one extra agent delete+create per run (a few seconds) — fine for a demo,
> not for production.

### Recommended demo setup (local LIVE — fast iteration)

```powershell
# Terminal 1 — mock data
task local:mock-api            # or: dotnet run --project src\mock-api

# Terminal 2 — orchestration API in LIVE with the demo toggle on
$env:DEMO_MODE = "false"
$env:FOUNDRY_PROJECT_ENDPOINT = "<your Foundry project endpoint>"
$env:FOUNDRY_RECREATE_AGENTS = "true"
dotnet run --project src\orchestration-api

# Terminal 3 — UI
npm --prefix src\ui-app run dev
```

The runner reads the prompt from its **build output** (`bin/.../Prompts/trading-desk-morning.md`,
copied `PreserveNewest`). Two equally good ways to get your edit in front of the running process:

- **Edit the source** `src\orchestration-api\Prompts\trading-desk-morning.md` and run with
  **`dotnet watch run`** — the save rebuilds, copies the prompt, and restarts; then re-run `/desk`.
- **Or** edit the already-copied file `src\orchestration-api\bin\Debug\net10.0\Prompts\trading-desk-morning.md`
  directly — no rebuild needed, just re-run `/desk` (the runner re-reads it every request).

> **Deployed (Sweden) variant:** the prompt *file* ships **inside the orchestration-api container image**,
> so a file edit there means rebuild + redeploy that image (then the toggle, or one agent-provisioner run,
> recreates the agent). To tune the prompt against the deployed demo **without redeploying, use
> [Method A — the Foundry portal](#method-a--edit-the-agent-in-the-foundry-portal-recommended-for-the-deployed-demo)**
> instead.

---

## The canonical edit — "flow-trading day"

**Before** you start, load `/desk` and note the current **#1 call** and the top of the list (each card leads
with a **news** "why now"). Then **paste this block** into `trading-desk-morning.md`, immediately after the
`## Operating rules` section (around line 27):

```markdown
## ⚡ STRATEGY OVERRIDE — flow-trading day (demo)

Today is a **flow-trading day**: the desk wants to capture live client trading intent, so
**live activity outranks overnight news.** Override the scoring in step 3:

- Rank the call list **strictly by live trading activity** — Open RFQs first, then inquiries.
  Treat **Open RFQ weight as the dominant signal (≤200)** and **demote News & research to a
  tiebreaker (≤20)**. Inventory-axe match stays ≤60.
- Lead every client's `whyNow` with the **RFQ / inquiry** catalyst, not the news.
- In `talkingPoints` and `tradeIdeas`, open with **the trade the client is already trying to do**
  (their live RFQ / inquiry), then bring in the desk axe.
- In `suggestedFirstAction`, state that **today is a flow day** and name the client with the
  **hottest open RFQ**.
```

**Re-run `/desk`** (refresh / "Run morning brief"). Within one run you should see:

- The **#1 call changes** — the client with the hottest **open RFQ** now leads, even if no news touches them.
- Every card's **"why now" leads with the RFQ / inquiry** instead of the macro headline.
- `suggestedFirstAction` explicitly frames it as a **flow day**.

> 📌 **Run it once before the room is watching.** Which client surfaces depends on the live RFQ/inquiry data
> in the fixtures, so confirm your before/after pair ahead of time and call out the *specific* name that
> jumps (e.g. "*See — yesterday we'd have led with the macro story; today the desk calls the client with the
> live RFQ first.*").

### Revert

Delete the `## ⚡ STRATEGY OVERRIDE …` block (or `git checkout -- src/orchestration-api/Prompts/trading-desk-morning.md`)
and re-run `/desk` — the list returns to the news-led default. With `FOUNDRY_RECREATE_AGENTS=true` the revert
is live on the next run too.

---

## Variant — "change the persona" (guaranteed-visible text change)

If you want a change that's obvious **regardless of which client has live RFQs**, swap the desk's *posture*
instead of the ranking. Replace the persona paragraph or paste this after `## Operating rules`:

```markdown
## ⚡ STRATEGY OVERRIDE — risk-off / defensive desk (demo)

The desk is **risk-off** today. Reframe every client interaction around **protection and
de-risking**, not adding exposure:

- `talkingPoints` lead with **downside / hedging** angles (trim into strength, hedge concentration,
  reduce duration), not momentum.
- `tradeIdeas` favour **client-side Sell / hedge** structures over Buys; pair each with the matched
  desk axe where the desk can take the other side.
- Keep the same clients and data — only the **stance and recommended actions** change.
```

Re-run `/desk`: same names, but the **talking points and trade ideas flip from "add / lean in" to "trim /
hedge."** The audience sees the *advice itself* change from one English edit — a clean way to show the model
is reasoning from the prompt, not replaying a script.

---

## Notes for presenters

- **LIVE only.** Confirm the top of the page shows the LIVE mode tag before you start; DEMO won't move.
- **One lever, two effects.** The "flow-trading day" edit changes **ranking**; the "risk-off" edit changes
  **content/voice**. Lead with whichever the room cares about (portfolio managers love the re-rank; risk and
  strategy folks love the posture swap).
- **Tie it back to the architecture.** The data, tools and UI never changed — only the prompt. That's the
  message: *"the desk's playbook is editable English, version-controlled like code, and the agent follows it
  faithfully and auditably."*
- **Pairs naturally with the news inject.** Run this first (prompt changes *strategy*), then a
  [News Desk inject](news-desk-headlines.md) (news changes *the world*) — together they show both knobs:
  change the instructions **and** change the inputs. See the [demo talk track](demo-talk-track.md).
- **Turn the toggle off** when you're done so normal demos reuse the persistent agent.
