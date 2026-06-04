"""Agentic endpoints. The frontend POSTs a scene + payload; the runner returns
the synthesized result (live agent or deterministic demo mode)."""
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from api.agents import runner
from api.agents.registry import AGENTS

router = APIRouter(prefix="/api/agent", tags=["agents"])


class AgentRequest(BaseModel):
    payload: dict = {}


@router.get("")
def list_agents():
    return {k: {"label": v["label"], "tools": v["tools"]} for k, v in AGENTS.items()}


@router.post("/{scene}")
def run_agent(scene: str, req: AgentRequest):
    try:
        return runner.run(scene, req.payload)
    except KeyError as e:
        raise HTTPException(404, str(e))
