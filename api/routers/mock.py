"""Mock "system of record" REST APIs.

Each route emulates one real platform from the Muni desk's stack. The agent
tools (api/agents/tools.py) call THESE endpoints over HTTP — so the demo runs a
true "data served via APIs" architecture, not in-process shortcuts.

Replace the body of each route with a real connector when you go live; the URL
contract stays the same.
"""
from fastapi import APIRouter, HTTPException

from api import data

router = APIRouter(prefix="/mock", tags=["mock-systems"])


# ---- Tableau: client revenue, rankings, share of wallet --------------------
@router.get("/tableau/clients")
def tableau_clients():
    return list(data.clients().values())


@router.get("/tableau/clients/{cid}")
def tableau_client(cid: str):
    c = data.client(cid)
    if not c:
        raise HTTPException(404, f"client {cid} not found")
    return c


# ---- Dynamics: coverage + engagement footprint -----------------------------
@router.get("/dynamics/clients/{cid}/engagement")
def dynamics_engagement(cid: str):
    c = data.client(cid)
    if not c:
        raise HTTPException(404, f"client {cid} not found")
    return {"id": c["id"], "coverage": c["coverage"], **c["engagement"],
            "behavior": c["behavior"], "open_asks": c["open_asks"]}


# ---- Trading book: axes / IOIs + holdings ----------------------------------
@router.get("/trading/axes")
def trading_axes():
    return data.axes()


@router.get("/trading/holdings")
def trading_holdings(cusip: str | None = None, state: str | None = None,
                     sector: str | None = None):
    """Search positions across all clients by cusip / state / sector."""
    out = []
    for cid, positions in data.holdings().items():
        for p in positions:
            if cusip and p["cusip"] != cusip:
                continue
            if state and p.get("state") != state:
                continue
            if sector and p.get("sector") != sector:
                continue
            out.append({"client": cid, **p})
    return out


@router.get("/trading/holdings/{cid}")
def trading_holdings_for(cid: str):
    return data.holdings(cid)


# ---- New-issue calendar ----------------------------------------------------
@router.get("/calendar/newissues")
def calendar_newissues():
    return data.new_issues()


# ---- Market data / relative value ------------------------------------------
@router.get("/marketdata")
def marketdata():
    return data.market_data()


@router.get("/marketdata/relval/{event_id}")
def marketdata_relval(event_id: str):
    rv = data.market_data().get("relval_events", {}).get(event_id)
    if not rv:
        raise HTTPException(404, f"no relval for event {event_id}")
    return rv


# ---- News feed -------------------------------------------------------------
@router.get("/news/{event_id}")
def news_event(event_id: str):
    n = data.news(event_id)
    if not n:
        raise HTTPException(404, f"event {event_id} not found")
    return n


# ---- Coalition benchmarking ------------------------------------------------
@router.get("/coalition/{cid}")
def coalition_client(cid: str):
    b = data.coalition().get("by_client", {}).get(cid.upper())
    if not b:
        raise HTTPException(404, f"no benchmark for {cid}")
    return b


@router.get("/coalition/sector/{sector}")
def coalition_sector(sector: str):
    return data.coalition().get("by_deal_sector", {}).get(sector, {})
