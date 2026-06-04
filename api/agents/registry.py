"""Agent registry — one entry per scene.

An "agent" here is: a system prompt (editable .md), the subset of tools it may
call, and the scene key the frontend uses. Add a scene = add a prompt file +
one entry here + a demo_mode composer in demo.py. That's the whole extension
surface (also documented in .github/copilot-instructions.md).
"""
from pathlib import Path

_PROMPTS = Path(__file__).resolve().parent / "prompts"


def _prompt(name: str) -> str:
    return (_PROMPTS / f"{name}.md").read_text(encoding="utf-8")


AGENTS = {
    "prioritization": {
        "label": "Prioritization Agent",
        "prompt": _prompt("prioritization"),
        "tools": ["get_client_value", "get_axes", "get_new_issues", "get_market_data"],
    },
    "exposure": {
        "label": "Exposure Agent",
        "prompt": _prompt("exposure"),
        "tools": ["get_news", "search_holdings", "get_relative_value", "get_client_value"],
    },
    "allocation": {
        "label": "Allocation Agent",
        "prompt": _prompt("allocation"),
        "tools": ["get_new_issues", "get_client_value", "get_coalition", "get_coalition_sector"],
    },
    "client360": {
        "label": "Client360 Agent",
        "prompt": _prompt("client360"),
        "tools": ["get_client_value", "get_engagement", "get_coalition"],
    },
}
