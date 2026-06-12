# News Desk headlines — curated injects for the Trading Desk

> Ready-to-paste **positive** market events for the News Desk (`/admin`) that demonstrably move the
> Institutional Sales & Trading cockpit. Each is grounded in the fictional trading-desk
> systems-of-record (`/mock/td/*`) so the re-rank has a real, traceable reason. **All data is
> fictional.**

The News Desk routes through the *same* ingestion path a real feed would use; the trading-desk scenes
match events to the book by typed entities (**ticker / issuer / sector / client**) and re-rank live
over SSE. To make a headline "move the needle," target a security that the covered salesperson's
clients **hold**, are **actively trading/inquiring on**, and that the **desk is axed in** — so a
positive print lets the desk cross client demand against its own inventory.

---

## Headline 1 — Quartzite Semiconductors (QRTX) earnings blowout · `SEC-3003`

The highest-impact single-name positive catalyst in the data. Targets coverage salesperson **Theo
Wexler**'s book.

### Why it moves the needle
- **Theo's clients hold it big** — ~$61mm of `SEC-3003` across his accounts (2nd-largest single name).
- **Live two-way demand already in the fixtures** — Forge Hill (`CL-2018`, Theo's client) chatted
  *"still seeing axes in QRTX?"* (Buy 75k); Crestline (`CL-2015`) wants to *scale in, 25k+*.
- **The desk is axed to sell it** — inventory `INV-4003` is short ~10k, offered at 162.65 (trader Bree
  Halvorsen) → a positive print lets the desk **cross client demand against its own axe**.
- **Sell-side thesis to lean on** — Pasternak Research initiated QRTX at Buy, **$200 TP**, on the QX-9
  accelerator cycle (`RES-2002`).

Expected result on `/desk`: within **~1-2s** a live alert banner appears and **Forge Hill / Crestline
(and other QRTX holders) jump up** the call list with a **"⚡ RE-RANKED BY LIVE EVENTS"** callout naming
the print. (The re-rank is applied by the deterministic overlay in both DEMO and LIVE — see notes — so
LIVE no longer waits ~1 min for a full agent re-run.)

### Paste-ready News Desk values

| Field | Value |
|---|---|
| **Headline** | `Quartzite Semiconductors smashes Q2: QX-9 sells out, FY guidance raised ~18%` |
| **Summary** | `QRTX reported a blowout quarter — revenue +27% YoY and gross margin +340bps as the QX-9 accelerator cycle sold out through year-end. Management raised FY guidance ~18% and announced a $2bn buyback, citing hyperscaler AI-capex commitments. Validates Pasternak's $200 target; desk is short ~10k and offered into firm client demand.` |
| **Source** | `Pasternak Research / company release` |
| **Severity** | `High` |
| **Type** | `Sector` |
| **Direction** | `Positive` |
| **Tickers** | `SEC-3003` |
| **Sectors** | `Technology` |
| **Issuers** | `Quartzite Semiconductors` |
| **Customer IDs** | *(leave blank — the TD scene keys off ticker/issuer/sector)* |

**Ripple wider (optional):** add `SEC-3002` to **Tickers** and `Nimbus Cloud Holdings` to **Issuers**
to also light up Nimbus holders (Hyperion & Tradewinds) — this is the desk's default AI-compute demo
combo and matches the one-click *"Inject AI-capex breaking print"* preset.

---

## Notes for presenters
- Load the scene you want to watch (`/desk`) **before** injecting — the SSE subscription opens on
  render, and only events that arrive *after* you subscribe (and *match* the scene) raise an alert.
- The same event re-ranks the **call list** on `/desk` but **enriches in place** on
  `/desk/new-issue` (one deal, so news doesn't reorder — it adds a LIVE evidence row + leading talking
  point). See the [demo talk track](demo-talk-track.md) Scene 3.
- LIVE and DEMO behave identically (Principle III): the agent/composer produces the same shape, and the
  event match + re-rank is the same deterministic step in both modes. In LIVE the Foundry agent builds
  the **base** briefing once per connect (the slow step); each subsequent News Desk inject is folded by
  the deterministic re-rank **overlay** (`TdBriefingLive`) in ~1-2s, so prompt edits still show in the
  base while injects react instantly — no full agent re-run per push.

## Reset between demo runs
The mock-api event store is **in-memory**: injected (intraday) headlines live only until the container
restarts, at which point the seed/overnight events reload clean. To reset the Trading Desk to baseline
after a run:

```powershell
task cloud:reset-mock-api      # restarts the active mock-api revision (drops admin injects)
# local docker-compose:  docker compose restart mock-api
```

Then **reload `/desk`** — the reconnect rebuilds the LIVE base briefing against the now-clean event set
(the overlay self-invalidates when its events disappear), so the call list returns to its un-injected
ranking.
