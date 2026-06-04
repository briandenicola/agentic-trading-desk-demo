# Azure AI Foundry Agent Migration

## Priority: P3 (Medium)

## Description
Migrate from direct Azure OpenAI SDK calls to Azure AI Foundry Agent Service
so agents are managed, versioned, and monitored centrally.

## Scope
- Register each agent (prioritization, exposure, allocation, client360) in Foundry.
- Import `openapi/tools.yaml` as the tool specification.
- Replace `runner.py` LIVE mode with Foundry Agent SDK calls.
- Keep DEMO mode unchanged (no Foundry dependency for offline use).
- Agent versioning: pin to specific deployed versions per environment.

## Acceptance Criteria
- [ ] All four agents registered in Azure AI Foundry.
- [ ] LIVE mode routes through Foundry Agent Service.
- [ ] Tool calls still hit the same mock/real APIs (no behavior change).
- [ ] DEMO mode unaffected.
- [ ] Agent versions pinned and documented.

## Dependencies
- 003-container-deployment (needs Azure infrastructure)
- 001-real-data-connectors (more useful with real data)

## Notes
This unlocks Foundry's built-in monitoring, prompt management, and
evaluation features. The `openapi/tools.yaml` is already Foundry-ready.
