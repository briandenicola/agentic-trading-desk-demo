# Build Log — Phase 1 (Setup) + Phase 2 (Foundational): T001–T014

**Date:** 2026-06-08
**Requested by:** Brian Denicola
**Coordinator:** Squad
**Feature:** `001-morning-planning-outreach` (Demo 1 — Morning Planning & Prioritized Outreach)
**Scope:** Waves 1–5 of the delegation plan = tasks T001 through T014. NO Phase 3 (T015+)
implementation beyond the T013/T014 tests.
**Stack:** C#/.NET 10 + Microsoft Agent Framework (deferred to Phase 3) + React 19/MUI v9 + Vite,
per ADR-0002 / constitution v0.2.1.

## Environment
- .NET SDK 10.0.204 installed (global.json pins 10.0.100 with `rollForward: latestFeature`).
- Node v25.9.0 / npm 11.12.1.
- Repo reached via Windows junction `C:\Users\brdenico\Code\AgenticTradersDesk` → OneDrive real path.

## Task-by-task status

| Task | Status | Notes |
|------|--------|-------|
| T001 | ✅ | `global.json` (10.0.100, rollForward latestFeature) + `Directory.Packages.props` (central PM). Also `Directory.Build.props` for shared net10.0 settings. |
| T002 | ✅ | `AgenticTradersDesk.sln` (classic format) + 6 projects: Observability, mock-api, orchestration-api, agent-provisioner (stub), mock-api.Tests, orchestration-api.Tests. |
| T003 | ✅ | `src/ui-app` Vite + React 19.2.7 + TS 6.0.3 + MUI 9.1.0 + React Router 7.17.0 + Axios 1.17.0 + `nginx.conf`. `npm install` + `npm run build` succeed (dist/ produced). |
| T004 | ✅ | Root `Taskfile.yaml` with `dotenv: ['.env']` + includes → `tasks/Taskfile.local.yml` (compose up/down/logs) + cloud/build stubs (full impl Phase 7 T040). Replaced legacy Python Taskfile. |
| T005 | ✅ | `docker-compose.yml` (ui-app, orchestration-api, mock-api, healthcheck, depends_on) + `.env.example` (DEMO_MODE, MOCK_API_BASEURL, FOUNDRY_*, MAX_TOOL_HOPS — no secrets). Dockerfiles referenced are added in Phase 7 (T035). |
| T006 | ✅ | `src/shared/Observability`: `UseSerilog(serviceName)`, `AddOpenTelemetry(serviceName)` (OTLP exporter only when endpoint set → offline-safe), `UseCorrelationId()`, `UseGlobalExceptionHandler()` (structured JSON, never HTML). |
| T007 | ✅ | Fictional fixtures in `src/mock-api/Data/`: clients, engagement, axes, holdings, marketdata, relval, news (`fed_surprise_hike`), newissues, coalition. ATLAS/BROOK/CEDAR + DELTA/EVER. ALL FICTIONAL. |
| T008 | ✅ | `src/mock-api` minimal-API endpoints for every `openapi/tools.yaml` operation + `/healthz` + `/readyz`, reading `Data/` via `MockDataStore`. |
| T009 | ✅ | `tests/mock-api.Tests` — 20 WebApplicationFactory contract tests (shape/status vs openapi). All pass. |
| T010 | ✅ | `src/orchestration-api/Models/`: MorningBrief, MarketStripItem, ReasoningStep, MacroNarrative, AffectedClient, Concern, OutreachItem, RankingRationale + `MorningBriefJson` options (camelCase, omit null notes, stable). |
| T011 | ✅ | `src/orchestration-api/Program.cs`: Serilog+OTEL, CORS, JWT-bearer scaffolding, typed `MockApiClient` (base URL from `MOCK_API_BASEURL`), `/healthz` + `/readyz`, global JSON error handler, `DEMO_MODE`/`MODE` switch (`ModeOptions`). |
| T012 | ✅ | `tests/orchestration-api.Tests/MorningBriefSchemaValidator.cs` (JsonSchema.Net, draft 2020-12) + 3 validator tests. All pass. |
| T013 | ⏸️ Skipped (green) | `UserStory1MorningBriefTests` DEMO contract test written; `Skip=` references pending Phase 3 (T015/T016). Real assertions in place for un-skip. |
| T014 | ⏸️ Skipped (green) | Tool-error degradation test (mock-api 5xx → notes + structured JSON, FR-011) written; `Skip=` references pending Phase 3. |

## Verification gates

- `dotnet build AgenticTradersDesk.sln -c Debug` → **Build succeeded. 0 Warning(s), 0 Error(s).**
- `dotnet test AgenticTradersDesk.sln` → **Passed. Failed: 0, Passed: 23, Skipped: 2 (T013/T014).**
  - mock-api.Tests: 20 passed.
  - orchestration-api.Tests: 3 passed, 2 skipped.
- `src/ui-app`: `npm install` → 0 vulnerabilities; `npm run build` → **dist/ produced** (index.html + assets/index-*.js).
- mock-api `/healthz` smoke (in-process `dotnet run`, port 8088): **200 `{"status":"ok"}`**; `/readyz` 200; sampled tool endpoints 200. Process stopped after probe.

## Binding technical decisions / substitutions (package versions)

- **global.json**: pinned `10.0.100` with `rollForward: latestFeature` so the installed 10.0.204 SDK satisfies it.
- **FluentAssertions 7.2.2** (NOT 8.x): v8 moved to a paid commercial (Xceed) license; pinned last Apache-2.0 release.
- **Microsoft.Agents.AI.AzureAI 1.0.0-rc5**: no stable release exists; release candidate. Used only on the LIVE/Foundry path (Phase 3), not referenced by any Phase 1/2 buildable project yet.
- **JsonSchema.Net 9.2.1**: added (not in plan list) as the draft-2020-12 schema validator for T012/T013. Its `Evaluate` takes a `JsonElement` (node projected via `SerializeToElement`).
- **Vite 7.3.5 + @vitejs/plugin-react 5.2.0** (NOT Vite 8): Vite 8 (and Vite 7's default) emit a relative `index.html` path error because the repo is reached through a Windows junction; pinned `root` to the config's resolved dir + `preserveSymlinks` and used Vite 7 for a reliable build.
- Other pins (stable, as planned): Microsoft.Agents.AI 1.9.0, Azure.AI.Agents.Persistent 1.1.0, Azure.AI.Projects 2.0.1, Azure.Identity 1.21.0, Serilog.AspNetCore 10.0.0, OpenTelemetry.* 1.15.x, Swashbuckle.AspNetCore 10.2.1, Microsoft.AspNetCore.Authentication.JwtBearer 10.0.8, xunit 2.9.3, Microsoft.NET.Test.Sdk 18.6.0, Moq 4.20.72, Microsoft.AspNetCore.Mvc.Testing 10.0.8. React 19.2.7, MUI 9.1.0, React Router 7.17.0, Axios 1.17.0, TypeScript 6.0.3 (all as planned).

## Constitution / ADR compliance
- Principle II honored: orchestration reaches data ONLY via the typed `MockApiClient` over HTTP; no in-process fixture reads. mock-api owns `Data/`.
- Principle III: MorningBrief DTOs + schema validator establish the LIVE/DEMO parity contract.
- No secrets committed; all fixtures fictional. `.env.example` carries no secret values.
- Changes left staged/unstaged for human review — NOT committed.

## Not done (out of scope, by instruction)
- T015+ Phase 3 implementation (DEMO composer, scene endpoint, LIVE AgentRunner, provisioner logic, UI scene).
- Dockerfiles (T035) and full cloud/build Taskfiles (T040) — referenced but stubbed.
