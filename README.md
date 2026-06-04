# Client CV — Muni Sales Agentic Demo

A working demo that turns the clickable **Client CV** storyboard into a real
multi-agent app: specialist agents serve a sales-desk cockpit with data pulled
from mock "system of record" APIs. Built from the Municipal Head of Sales
interview; **all data is fictional**.

> Runs **offline out of the box** (deterministic demo mode). Add Azure OpenAI
> credentials to switch on real agent reasoning.

## Architecture

```
Experience      frontend/  (cockpit)  ── fetch ──►  POST /api/agent/{scene}
                                                          │
Agents          api/agents/  orchestrator + 4 specialist agents
                  prioritization · exposure · allocation · client360
                  LIVE: Azure OpenAI tool-calling   DEMO: deterministic composer
                                                          │  tool calls
Mock data        api/routers/mock.py  +  api/mock_data/*.json
                  /tableau /dynamics /trading /calendar /marketdata /coalition
```

The agents reach data only through the mock REST APIs, so the "served via APIs"
contract holds whether the backend is fixtures (today) or real Tableau /
Dynamics / trading systems (later). See `openapi/tools.yaml` for the tool spec
(importable into Azure AI Foundry or wrappable as MCP).

## Scenes → agents

| Scene | Agent | Tools |
|-------|-------|-------|
| 1 · Morning prioritization | Prioritization | client value, axes, calendar, market data |
| 3 · Breaking news → exposure | Exposure | news, holdings search, relative value, client value |
| 4 · New-issue allocation | Allocation | new issues, client value, Coalition |
| 5 · Meeting prep / Client360 | Client360 | client value, engagement, Coalition |

## Quickstart

```bash
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn api.main:app --reload --port 8000
# open http://localhost:8000/
```

That's it — demo mode needs no keys. Try the agents directly:

```bash
curl -X POST localhost:8000/api/agent/exposure  -H "content-type: application/json" -d '{"payload":{"event":"IL_GO_downgrade"}}'
curl -X POST localhost:8000/api/agent/client360 -H "content-type: application/json" -d '{"payload":{"client":"KEYS"}}'
```

## Turn on live agents

```bash
cp .env.example .env        # fill in AZURE_OPENAI_* values
# unset DEMO_MODE, then restart uvicorn
```

LIVE mode runs a real tool-calling loop: the model decides which mock APIs to
call, reads the data, and synthesizes the ranking / exposure / allocation /
talking points. Same JSON shape as demo mode, so the frontend is unchanged.

> **On stage:** set `DEMO_MODE=1` for deterministic, repeatable runs even with a
> key configured. Live LLM calls are the #1 live-demo failure point.

## Extending
Adding a scene or a data source is a 3–4 file change — see
`.github/copilot-instructions.md`, which also steers GitHub Copilot as you build.

## Layout
```
api/
  main.py              FastAPI app (mounts mock APIs, agents, frontend)
  data.py              fixture loader  ← swap for real connectors to go live
  mock_data/*.json     fictional clients, holdings, axes, deals, news, benchmarks
  routers/mock.py      mock system-of-record REST APIs
  routers/agents.py    POST /api/agent/{scene}
  agents/
    registry.py        scene → prompt + tools
    tools.py           tool fns over the mock APIs + JSON schemas
    runner.py          LIVE (Azure OpenAI) + DEMO mode
    demo.py            deterministic composers
    prompts/*.md       the editable agent instructions
frontend/              the cockpit + fetch-wiring example
openapi/tools.yaml     tool spec for Foundry / MCP import
```
