"""Tiny fixture loader. Reads the JSON files in mock_data/ once and caches them.

In a real build, swap these functions for calls to Tableau, Dynamics, the
trading book, the new-issue calendar, the MMD feed, and Coalition. The function
signatures are the seam — keep them and the agents/tools don't change.
"""
import json
from functools import lru_cache
from pathlib import Path

_DATA = Path(__file__).resolve().parent / "mock_data"


@lru_cache(maxsize=None)
def _load(name: str):
    with open(_DATA / f"{name}.json", "r", encoding="utf-8") as fh:
        return json.load(fh)


def clients() -> dict:
    return _load("clients")


def client(cid: str) -> dict | None:
    return _load("clients").get(cid.upper())


def holdings(cid: str | None = None):
    h = _load("holdings")
    return h.get(cid.upper(), []) if cid else h


def axes() -> list:
    return _load("axes")


def new_issues() -> list:
    return _load("newissues")


def coalition() -> dict:
    return _load("coalition")


def market_data() -> dict:
    return _load("marketdata")


def news(event_id: str | None = None):
    n = _load("news")
    return n.get(event_id) if event_id else n
