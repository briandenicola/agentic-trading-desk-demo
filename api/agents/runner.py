"""Agent runner.

Two modes:
  * LIVE  — when AZURE_OPENAI_API_KEY is set, runs a real tool-calling loop
            against Azure OpenAI. The model decides which tools to call,
            reads the mock data, and synthesizes the answer.
  * DEMO  — when no key is set (or DEMO_MODE=1), composes a deterministic
            response from the tools directly. Guarantees the stage demo works
            offline and identically every time.

Both modes return the SAME JSON shape per scene, so the frontend is mode-blind.
"""
import json
import os

from api.agents import demo
from api.agents.registry import AGENTS
from api.agents.tools import TOOLS, openai_tool_specs

MAX_TOOL_HOPS = 6


def _live_enabled() -> bool:
    return bool(os.getenv("AZURE_OPENAI_API_KEY")) and os.getenv("DEMO_MODE") != "1"


def run(scene: str, payload: dict) -> dict:
    if scene not in AGENTS:
        raise KeyError(f"unknown scene '{scene}'")
    if not _live_enabled():
        return {"mode": "demo", "scene": scene, **demo.compose(scene, payload)}
    return {"mode": "live", "scene": scene, **_run_live(scene, payload)}


def _run_live(scene: str, payload: dict) -> dict:
    from openai import AzureOpenAI  # imported lazily so demo mode needs no SDK

    agent = AGENTS[scene]
    client = AzureOpenAI(
        api_key=os.environ["AZURE_OPENAI_API_KEY"],
        azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
        api_version=os.getenv("AZURE_OPENAI_API_VERSION", "2024-08-01-preview"),
    )
    deployment = os.getenv("AZURE_OPENAI_DEPLOYMENT", "gpt-4.1")

    messages = [
        {"role": "system", "content": agent["prompt"]},
        {"role": "user", "content": json.dumps(payload)},
    ]
    tool_specs = openai_tool_specs(agent["tools"])

    for _ in range(MAX_TOOL_HOPS):
        resp = client.chat.completions.create(
            model=deployment, messages=messages, tools=tool_specs,
            temperature=0.2, response_format={"type": "json_object"},
        )
        msg = resp.choices[0].message
        if not msg.tool_calls:
            return json.loads(msg.content)
        messages.append(msg.model_dump(exclude_none=True))
        for call in msg.tool_calls:
            fn = TOOLS[call.function.name]
            args = json.loads(call.function.arguments or "{}")
            result = fn(**args)
            messages.append({
                "role": "tool", "tool_call_id": call.id,
                "content": json.dumps(result),
            })
    return {"error": "max tool hops reached"}
