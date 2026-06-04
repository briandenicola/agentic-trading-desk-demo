"""Agent tools.

Each function is a thin client over the mock REST APIs in routers/mock.py. These
are the callable "tools" the LLM agents invoke. The JSON schemas at the bottom
are what you register with Azure OpenAI (function calling) or expose via MCP.

Because they call the data over HTTP, the same tool definitions work whether the
data is mock fixtures (today) or real Tableau/Dynamics/trading APIs (later) —
only the base URL changes.
"""
import os
import httpx

BASE = os.getenv("MOCK_API_BASE", "http://localhost:8000")
_client = httpx.Client(base_url=BASE, timeout=10.0)


def _get(path: str, **params):
    r = _client.get(path, params={k: v for k, v in params.items() if v is not None})
    r.raise_for_status()
    return r.json()


# ---- tool implementations ---------------------------------------------------
def get_client_value(client_id: str | None = None):
    """Revenue, rankings and share of wallet (Tableau)."""
    return _get(f"/mock/tableau/clients/{client_id}") if client_id else _get("/mock/tableau/clients")


def get_engagement(client_id: str):
    """Coverage + 30/60/90/180d engagement footprint (Dynamics)."""
    return _get(f"/mock/dynamics/clients/{client_id}/engagement")


def get_axes():
    """Live axes / IOIs from the trading book."""
    return _get("/mock/trading/axes")


def search_holdings(cusip: str | None = None, state: str | None = None, sector: str | None = None):
    """Find which clients hold a given cusip / state / sector."""
    return _get("/mock/trading/holdings", cusip=cusip, state=state, sector=sector)


def get_new_issues():
    """Today's new-issue calendar."""
    return _get("/mock/calendar/newissues")


def get_market_data():
    """MMD levels, ratios, supply, flows, HY spread."""
    return _get("/mock/marketdata")


def get_relative_value(event_id: str):
    """Relative-value context for a market event."""
    return _get(f"/mock/marketdata/relval/{event_id}")


def get_news(event_id: str):
    """Resolve a news event into structured entities/sectors/states."""
    return _get(f"/mock/news/{event_id}")


def get_coalition(client_id: str):
    """Competitive benchmarking for a client (rank vs dealers, capture/miss)."""
    return _get(f"/mock/coalition/{client_id}")


def get_coalition_sector(sector: str):
    """Firm rank within a deal sector."""
    return _get(f"/mock/coalition/sector/{sector}")


# Registry the agent runner uses to dispatch tool calls by name.
TOOLS = {
    "get_client_value": get_client_value,
    "get_engagement": get_engagement,
    "get_axes": get_axes,
    "search_holdings": search_holdings,
    "get_new_issues": get_new_issues,
    "get_market_data": get_market_data,
    "get_relative_value": get_relative_value,
    "get_news": get_news,
    "get_coalition": get_coalition,
    "get_coalition_sector": get_coalition_sector,
}

# OpenAI / MCP-style JSON schemas for the tools above.
TOOL_SCHEMAS = {
    "get_client_value": {"type": "object", "properties": {"client_id": {"type": "string"}}},
    "get_engagement": {"type": "object", "properties": {"client_id": {"type": "string"}}, "required": ["client_id"]},
    "get_axes": {"type": "object", "properties": {}},
    "search_holdings": {"type": "object", "properties": {
        "cusip": {"type": "string"}, "state": {"type": "string"}, "sector": {"type": "string"}}},
    "get_new_issues": {"type": "object", "properties": {}},
    "get_market_data": {"type": "object", "properties": {}},
    "get_relative_value": {"type": "object", "properties": {"event_id": {"type": "string"}}, "required": ["event_id"]},
    "get_news": {"type": "object", "properties": {"event_id": {"type": "string"}}, "required": ["event_id"]},
    "get_coalition": {"type": "object", "properties": {"client_id": {"type": "string"}}, "required": ["client_id"]},
    "get_coalition_sector": {"type": "object", "properties": {"sector": {"type": "string"}}, "required": ["sector"]},
}


def openai_tool_specs(names: list[str]) -> list[dict]:
    """Build the `tools=[...]` payload for the Azure OpenAI chat completions call."""
    specs = []
    for n in names:
        specs.append({"type": "function", "function": {
            "name": n,
            "description": (TOOLS[n].__doc__ or "").strip(),
            "parameters": TOOL_SCHEMAS[n],
        }})
    return specs
