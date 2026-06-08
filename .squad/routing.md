# Routing — Who Handles What

> Work assignment rules for Demo 1. Match meaning, not exact strings.
> All paths use the canonical `plan.md` Project Structure. Never invent paths (§18 Never).

## By Domain / Path

| Signal / Path | Owner |
|---------------|-------|
| `src\orchestration-api\**` (composer, AgentRunner, Tools, Models, Program.cs, Prompts) | Livingston |
| `src\mock-api\**` (Endpoints, Data fixtures, Program.cs) | Livingston |
| `src\shared\Observability\**` | Livingston |
| `Directory.Packages.props`, `global.json`, `*.sln` | Livingston |
| `src\ui-app\**` (React scenes, components, theme, api/client.ts, nginx.conf) | Linus |
| `infra\**` (Terraform `.tf`) | Basher |
| `tasks\**`, root `Taskfile.yaml`, `docker-compose.yml`, `.env.example` | Basher |
| `**\Dockerfile` | Basher |
| `.github\workflows\**` (ci.yml, cd.yml) | Basher |
| `src\agent-provisioner\**` (Foundry registration) | Basher |
| `tests\**` (xUnit), `src\ui-app\**` Vitest/RTL specs | Yen |
| Prompt / agent-instruction design (`Prompts\*.md`) | Rusty (with Livingston wiring) |
| Scope, architecture, ADR, decisions, PR review, governance | Rusty |
| Backlog cards, docs alignment (`README`, copilot-instructions) | Rusty (Scribe records) |

## By Phase / Task Ownership

| Phase | Tasks | Primary Owner(s) |
|-------|-------|------------------|
| 1 Setup | T001–T002, T006 | Livingston · T003 Linus · T004–T005 Basher |
| 2 Foundational | T007–T008, T010–T011 | Livingston · T009, T012 Yen |
| 3 US1 (MVP) | T015–T016, T018–T019 | Livingston · T013–T014, T047 Yen · T017 Rusty · T020 Basher · T021 Linus |
| 4 US2 | T024–T025 | Livingston · T022–T023 Yen · T026 Linus |
| 5 US3 | T029–T030 | Linus · T027–T028 Yen |
| 6 US4 | T033–T034 | Livingston · T031–T032 Yen |
| 7 Deploy | T035–T042 | Basher |
| 8 Polish | T044 Basher · T045 Yen · T043, T046 Rusty | (T047 Yen) |

## Reviewer Routing

- Any orchestration-api / mock-api / IaC PR → **Rusty** approves against §17/§21.
- Any task with tests → **Yen** verifies pyramid + DEMO/LIVE parity before "done".
- Rejection → strict lockout: original author cannot self-revise.

## Ceremonies

- Architecture sync before a multi-agent phase kickoff (Rusty facilitates).
- Definition-of-Done review after each user story checkpoint (Rusty + Yen).
