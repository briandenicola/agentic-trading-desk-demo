# History — Livingston (Backend Engineer)

## Core Context (seeded 2026-06-08)
- **Project**: Client CV — Muni Sales Agentic Demo. Data fictional. Requested by Brian Denicola.
- **Stack**: C#/.NET 10 (`net10.0`, pin `10.0.100`), central package mgmt
  (`Directory.Packages.props`), Microsoft Agent Framework, Azure AI Foundry, Serilog +
  OpenTelemetry, xunit + Moq + FluentAssertions + WebApplicationFactory.
- **You own**: `src\orchestration-api`, `src\mock-api`, `src\shared\Observability`, solution
  plumbing (`global.json`, `Directory.Packages.props`, `WF-Garage.sln`).
- **Contracts**: `openapi\tools.yaml` (mock SoR), `contracts\morning-brief.schema.json` (scene
  response), `contracts\agent-api.yaml` (`POST /api/agent/morning-brief`).
- **Seed clients** (fictional, from mockup): Atlas Pension/ATLAS, Brookline Bank/BROOK,
  Cedar Asset Mgmt/CEDAR. Event: `fed_surprise_hike`.

## Learnings
- 2026-06-08: Hired. MVP path = DEMO composer (T015) before LIVE AgentRunner (T019) so the
  brief is demoable offline first.
- Architecture seam: orchestration tools reach mock-api over HTTP via typed `HttpClient`;
  never in-process fixture reads.
