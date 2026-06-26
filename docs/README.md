# Documentation Index

## Core Documents

| Document | Path | Status |
|----------|------|--------|
| Product Requirements | `docs/prd.md` | ✅ Problem, personas, scope & success criteria |
| Architecture Overview | `docs/architecture.md` | ✅ Agents, orchestration & traceability |
| Agentic vs. Synthetic | `docs/agentic-vs-synthetic.md` | ✅ Per-page: agent (LIVE) vs. deterministic (DEMO) + reactivity |
| Demo Talk Track | `docs/demo-talk-track.md` | ✅ Trader-facing scene-by-scene demo script |
| News Desk Headlines | `docs/news-desk-headlines.md` | ✅ Curated, grounded positive injects for the Trading Desk re-rank |
| Prompt-Tuning Demo | `docs/prompt-tuning-demo.md` | ✅ Edit the agent prompt live → the call list re-orders (prompt-engineering showcase) |
| Getting Started | `docs/getting-started.md` | ✅ Local DEMO/LIVE setup, tests & deploy |
| References | `docs/references.md` | ✅ Internal docs, contracts & external links |

## Diagrams

| Diagram | Source (editable) | Preview |
|---------|-------------------|---------|
| Application architecture (`src/` code map: UI → orchestration-api → mock-api) | `docs/src-architecture.excalidraw` | `docs/src-architecture.png` |
| Azure deployment topology (`infra/*.tf`: RG, Container Apps Env, ACR/Key Vault/MI/App Insights, Foundry) | `docs/infra-architecture.excalidraw` | `docs/infra-architecture.png` |
| Agentic trading intelligence north-star (orchestrator + agent pipeline) | `docs/trader_agent_diagram.excalidraw` | `docs/Traders Agent Diagram.png` |
| Intent engineering | `docs/intent_engineering.excalidraw` | `docs/Intent Engineering.png` |

`.excalidraw` sources are editable at [aka.ms/excalidraw](https://aka.ms/excalidraw).

## Architecture Decision Records

| # | Title | Status |
|---|-------|--------|
| 0001 | Adopt Spec Kit + Squad framework | ACCEPTED |
| 0002 | C# + Azure AI Foundry for the Demo 1 agent path | ACCEPTED |
