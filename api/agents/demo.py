"""Deterministic demo-mode composers.

These produce the same JSON shape the LIVE agents return, but assembled straight
from the tools — no LLM, no API key, no variance. This is what runs on stage by
default. The draft/talking-point text here is canned; in LIVE mode the model
writes it from the same facts.
"""
from api.agents import tools


def compose(scene: str, payload: dict) -> dict:
    return _COMPOSERS[scene](payload)


# ---- Scene 1: morning prioritization ---------------------------------------
def _prioritization(payload):
    clients = tools.get_client_value()
    ranked = sorted(clients, key=lambda c: (c["firmwide_revenue"], c["muni_wallet"]), reverse=True)
    why = {
        "KEYS": "Holds IL GO — overnight headline risk. Two axes match their curve buys.",
        "MERI": "Cash to deploy from June 1 coupon roll; axed bonds fit their ladder.",
        "GRAN": "Long-duration buyer; CA GO retail order period is a fit. Wallet upside.",
        "CARD": "Holds IL Toll Hwy revenue — adjacent risk to today's news.",
        "SIER": "Retail ladder demand; TX Water AAA paper matches recent RFQs.",
        "HARB": "Relative-value HY shop; volatility in IL/HY names = their wheelhouse.",
        "LAKE": "Whitespace account — under-covered. Coalition shows miss vs peers.",
    }
    return {"ranked": [
        {"id": c["id"], "name": c["name"], "tier": c["tier"],
         "revenue": c["revenue"]["muni"], "wallet": c["muni_wallet"],
         "signal": c["signal"], "why": why.get(c["id"], "")}
        for c in ranked
    ]}


# ---- Scene 3: breaking-news exposure ---------------------------------------
def _exposure(payload):
    event_id = payload.get("event", "IL_GO_downgrade")
    news = tools.get_news(event_id)
    relval = tools.get_relative_value(event_id)
    direct_states = news["states"]
    hits = tools.search_holdings(state=direct_states[0])
    impacted = [{
        "client": h["client"], "position": f"${h['par_usd']//1_000_000}M {h['desc']}",
        "side": "Likely sell" if h["sector"] == "State GO" else "Hold / watch",
    } for h in hits]
    neighbor = [
        {"client": "HARB", "exposure": "NJ / CT GO, HY muni book", "side": "Opportunistic buy"},
        {"client": "GRAN", "exposure": "BBB state GO ladder", "side": "Add on weakness"},
        {"client": "MERI", "exposure": "Benchmark IL weight", "side": "Inquiry likely"},
    ]
    draft = (
        "Quick heads-up on the IL pension headline this morning — Moody's flagged a "
        "possible downgrade to Baa2. IL GO 5s '38 (your ~$45M line) are "
        f"~+{relval['il_go_spread_to_mmd_chg_bp']}bp wider to MMD on the open. "
        "We're seeing two-way interest — happy to show a firm bid if you want to lighten, "
        "or send the relative-value vs comparable BBB states if you'd rather hold. "
        "Wanted you to hear it from us first."
    )
    return {
        "headline": news["headline"], "source": news["source"],
        "commentary": relval["commentary"],
        "reasoning": [
            f"Parsed headline -> entity {news['entities'][0]}, event {news['event_type']}, sectors {news['sectors']}.",
            "Scanned holdings + IOI book for direct exposure.",
            "Expanded to nearest-neighbor (adjacent low-grade GO / HY muni).",
            "Pulled relative-value context and matched coverage owners.",
        ],
        "impacted": impacted, "nearest_neighbor": neighbor,
        "draft": {"to": "J. Alvarez, Keystone Asset Mgmt", "channel": "Bloomberg chat", "body": draft},
    }


# ---- Scene 4: new-issue allocation -----------------------------------------
def _allocation(payload):
    deal_id = payload.get("deal", "CA-GO")
    deals = {d["id"]: d for d in tools.get_new_issues()}
    deal = deals.get(deal_id, deals["CA-GO"])
    sector_rank = tools.get_coalition_sector(deal["sector"])
    recs = [
        {"client": "GRAN", "tier": 1, "fit": "Strong", "suggested_usd": 140, "est_rev_k": 310,
         "why": "Long-duration insurer; under-allocated last 2 CA deals. Wallet upside."},
        {"client": "MERI", "tier": 1, "fit": "Strong", "suggested_usd": 120, "est_rev_k": 265,
         "why": "Benchmark CA weight; reliable secondary follow-through."},
        {"client": "KEYS", "tier": 1, "fit": "Good", "suggested_usd": 90, "est_rev_k": 200,
         "why": "#2 revenue account; rotating out of IL into high-grade GO."},
        {"client": "LAKE", "tier": 3, "fit": "Win-back", "suggested_usd": 25, "est_rev_k": 55,
         "why": "Coalition miss — buys CA GO away from firm. Small allo to re-engage."},
    ]
    return {
        "deal": deal, "coalition_sector": sector_rank,
        "recommendations": recs,
        "totals": {
            "allocated_usd": sum(r["suggested_usd"] for r in recs),
            "est_rev_k": sum(r["est_rev_k"] for r in recs),
            "winback_flags": sum(1 for r in recs if r["fit"] == "Win-back"),
        },
    }


# ---- Scene 5: client360 ----------------------------------------------------
def _client360(payload):
    cid = payload.get("client", "KEYS")
    c = tools.get_client_value(cid)
    eng = tools.get_engagement(cid)
    bench = tools.get_coalition(cid)
    talking_points = [
        "Lead with the IL move — they trimmed IL GO this week; bring relative-value vs BBB states and offer bids on residual '38s.",
        "Position CA GO — rotating to high-grade; we can allocate $90M today.",
        "Open the wallet — 68% muni but 31% firmwide; tee up an intro to Rates coverage on the 15y shift.",
        "Open ask — they flagged water/sewer interest; TX Water AAA on the calendar is a fit.",
    ]
    return {"client": c, "engagement": eng, "coalition": bench, "talking_points": talking_points}


_COMPOSERS = {
    "prioritization": _prioritization,
    "exposure": _exposure,
    "allocation": _allocation,
    "client360": _client360,
}
