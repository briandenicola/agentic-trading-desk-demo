# Squad — Team Roster

> The crew assembled for **Demo 1: Morning Planning & Prioritized Outreach**
> (`specs/001-morning-planning-outreach/`). Stack is C#/.NET 10 + Microsoft Agent
> Framework + Azure AI Foundry + React 19/MUI v9 + Terraform → ACR → Azure Container Apps
> (per constitution v0.2.1 / ADR-0002).

## Project Context

- **Project**: Client CV — Muni Sales Agentic Demo Cockpit (all data fictional).
- **Requested by**: Brian Denicola
- **Authority**: `.specify/memory/constitution.md` (v0.2.1, §0 Hierarchy of Authority).
- **Active feature**: `specs/001-morning-planning-outreach/` (47 tasks, T001–T047, 8 phases).
- **Mockup being made interactive**: `mockup\demos\01-morning-prep.html`.
- **Scope discipline**: ONE feature this session (Principle I). Defer everything else.

## Members

| Name | Role | Model | Reviewer | Owns |
|------|------|-------|----------|------|
| Rusty | Lead / Architect | auto | ✅ Reviewer (gate per §17/§21) | Scope, decisions, prompt/agent design, governance, PR review |
| Livingston | Backend Engineer (.NET / Agent Framework) | claude-sonnet-4.5 | — | `src\orchestration-api`, `src\mock-api`, `src\shared\Observability` |
| Linus | Frontend Engineer (React 19 / TypeScript / MUI v9) | claude-sonnet-4.5 | — | `src\ui-app` |
| Basher | Platform / Infra Engineer (Terraform / Azure / CI-CD) | claude-sonnet-4.5 | — | `infra\`, `tasks\`, Dockerfiles, `.github\workflows\`, `src\agent-provisioner` |
| Yen | QA / Test Engineer (xUnit / Vitest / RTL) | claude-sonnet-4.5 | ✅ Reviewer (test gate) | `tests\*`, UI tests, determinism/parity/perf checks |
| Scribe | Memory & Logs | claude-haiku-4.5 | — | `.squad\decisions.md`, `.squad\log\`, orchestration log |
| Ralph | Work Monitor | — | — | 🔄 Work queue / backlog / keep-alive |

## Casting

- **Universe**: Ocean's Eleven (a specialist crew assembled for a single high-stakes job —
  names imply function and pressure, not authority). Easter egg only; never role-play.
- Scribe and Ralph are exempt from casting (fixed names).
- Mapping persisted in `.squad\casting\registry.json`.

## Reviewer Gates (Strict Lockout)

- **Rusty** gates architecture, scope, and the §17 Quality Gate / §21 Definition of Done.
- **Yen** gates test sufficiency (pyramid per Principle VII, parity per Principle III).
- On rejection, the original author is locked out of the revision; a different agent revises.
