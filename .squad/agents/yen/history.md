# History — Yen (QA / Test Engineer)

## Core Context (seeded 2026-06-08)
- **Project**: Client CV — Muni Sales Agentic Demo. Data fictional. Requested by Brian Denicola.
- **Stack**: xunit + Moq + FluentAssertions + Microsoft.AspNetCore.Mvc.Testing
  (WebApplicationFactory) for .NET; Vitest + React Testing Library for UI.
- **You own**: `tests\mock-api.Tests`, `tests\orchestration-api.Tests`, UI Vitest/RTL specs,
  quickstart validation. Coverage target 80%+ on orchestration-api + mock-api.
- **Key contracts**: `morning-brief.schema.json`, `agent-api.yaml`, `openapi\tools.yaml`.
- **Success criteria**: SC-001 (<10s render), SC-002 (byte-identical DEMO), SC-003 (parity).

## Learnings
- 2026-06-08: Hired. TDD where practical — tests T013/T014 before composer T015. Ranking blend
  is 0.4·wallet + 0.3·engagement + 0.3·eventRelevance; ranks contiguous from 1.
- DEMO determinism means stable JSON serialization options + deterministic ordering/text.
