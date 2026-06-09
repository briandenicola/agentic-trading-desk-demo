# Azure AI Foundry Agent Migration

## Status: Delivered / realized by `001-morning-planning-outreach` (confirmed 2026-06-09 — persistent morning-brief agent on gpt-5.4-mini, LIVE Foundry brief validated in Sweden).

## Priority: P3 (Medium)

## Description
Migrate from direct Azure OpenAI SDK calls to Azure AI Foundry Agent Service
so agents are managed, versioned, and monitored centrally.

`specs\001-morning-planning-outreach\` realizes this backlog card for the active Demo 1 scope under ADR-0002: the Python-era multi-scene runner was replaced by the C# `src\orchestration-api\` LIVE path using Microsoft Agent Framework + Azure AI Foundry, with `src\agent-provisioner\` registering the morning-brief agent and DEMO mode remaining offline.

## Scope
- [x] Register the Demo 1 morning-brief agent in Foundry through `src\agent-provisioner\`.
- [x] Keep `openapi\tools.yaml` as the tool contract.
- [x] Replace the Python `runner.py` concept with `src\orchestration-api\Agents\AgentRunner.cs` for LIVE mode.
- [x] Keep DEMO mode unchanged by Foundry (no credentials required; `DEMO_MODE=1` default).
- [x] Pin the runtime model/deployment through `FOUNDRY_MODEL` and Terraform outputs.

## Acceptance Criteria
- [x] Demo 1 morning-brief agent can be registered in Azure AI Foundry.
- [x] LIVE mode routes through the Foundry/Microsoft Agent Framework path.
- [x] Tool calls still hit the mock API HTTP seam (no in-process fixture reads).
- [x] DEMO mode is unaffected and offline.
- [x] Agent/model deployment is documented through `FOUNDRY_MODEL` and infra outputs.

## Dependencies
- 003-container-deployment (realized by `001-morning-planning-outreach`)

## Notes
The original four-agent Python scope was superseded by ADR-0002 and the active Demo 1 morning-brief feature. Future scenes can add more Foundry agents using the same `src\orchestration-api\Prompts\`, `src\orchestration-api\Agents\Tools\`, and `src\agent-provisioner\` pattern.
