# History — Rusty (Lead / Architect)

## Core Context (seeded 2026-06-08)
- **Project**: Client CV — Muni Sales Agentic Demo. All data fictional.
- **Requested by**: Brian Denicola.
- **Stack** (ADR-0002 / constitution v0.2.1): C#/.NET 10 (`net10.0`, pin `10.0.100`),
  Microsoft Agent Framework (`Microsoft.Agents.AI[.AzureAI]`), Azure AI Foundry
  (`Azure.AI.Agents.Persistent`, `Azure.AI.Projects`, `Azure.Identity`), React 19 + MUI v9 +
  TS6, Terraform (`azurerm ~>4`, `azapi ~>2`), Azure Container Apps, GitHub Actions.
- **Active feature**: `specs\001-morning-planning-outreach\` — 47 tasks (T001–T047), 8 phases.
- **MVP slice**: Phases 1→2→3 (T001–T021) + T047; DEMO path before LIVE within US1.
- **Canonical layout**: `plan.md` Project Structure — `src\{ui-app,orchestration-api,mock-api,
  agent-provisioner,shared\Observability}`, `tests\`, `infra\`, `tasks\`, `.github\workflows\`.
- **Reference architecture**: `briandenicola/online-banking-demo` (ACA instead of AKS).

## Learnings
- 2026-06-08: Hired as Lead. copilot-instructions.md is stale (Python/FastAPI); constitution
  §0 + ADR-0002 supersede it — the stack is C#/.NET. Flagged for T043 cleanup.
- Three-layer rule (Principle II): tools call data over HTTP via mock-api; never read
  `Data\` fixtures in-process from orchestration-api.
- LIVE/DEMO parity (Principle III): same JSON shape both modes; DEMO is offline default.
