# Squad build log — Phase 3 / User Story 1 (MVP)

- **When:** 2026-06-08
- **Requested by:** Brian Denicola
- **Coordinator:** Squad v0.9.1
- **Scope:** Execute Phase 3 (US1) — T015–T021, T047, and un-skip T013/T014. DEMO morning
  brief running end-to-end from the React UI, schema-validated against
  `contracts/morning-brief.schema.json`. No git commit (left for human review).

## Team & ownership

| Agent | Role | Tasks |
|-------|------|-------|
| 🏗️ Rusty | Lead / Architect (Reviewer) | T017 prompt; §17/§21 gate on composer/endpoint/UI/prompt |
| 🔧 Livingston | Backend .NET / Agent Framework | T015 composer, T016 endpoint, T018 tools, T019 AgentRunner |
| ⚛️ Linus | Frontend React 19 / MUI v9 | T021 MorningBrief scene |
| ⚙️ Basher | Platform | T020 agent-provisioner |
| 🧪 Yen | QA (Reviewer) | T013/T014 un-skip, T047 perf gate |

> Linus (T021) ran as a background agent in parallel; the interlocked C# backend
> (composer → endpoint → tools → runner → tests) was built on the critical path to keep the
> shared JSON contract coherent. Reviewer gates: Yen verified T013/T014/T047 green; Rusty's
> §17/§21 gate = clean build + full DEMO parity + tools-over-HTTP (Principle II) honored.

## Outcome

- `dotnet build WF-Garage.sln` → **0 warnings, 0 errors**.
- `dotnet test WF-Garage.sln` → **26 passed / 0 skipped / 0 failed**
  (mock-api 20; orchestration-api 6 incl. T013, T014, T047 now passing, previously skipped).
- DEMO smoke: `POST /api/agent/morning-brief {"eventId":"fed_surprise_hike","date":"2026-06-08"}`
  → **HTTP 200**, `application/json`, keys
  `mode, asOf, marketStrip, reasoning, macroNarrative, mostAffectedClients, outreach`
  (`notes` omitted on success). `mode=DEMO`; 3 most-affected (Atlas/Brookline/Cedar);
  outreach ranked 1.0 / 0.8269 / 0.481.
- `src/ui-app` `npm run build` → **success** (tsc -b + vite build).

## Key technical decisions (see decisions.md row 7)

1. **T013 test seam:** `orchestration-api.Tests` now hosts the real `mock-api` in-memory
   (`extern alias MockApiHost`) so the DEMO contract/perf tests exercise the full
   composer → mock-api HTTP path (Principle II / FR-002) without external processes. Avoids the
   global `Program` clash between the two web projects.
2. **LIVE AgentRunner (T019) on rc5:** built against
   `Microsoft.Agents.AI` + `Microsoft.Agents.AI.AzureAI` 1.0.0-rc5 +
   `Azure.AI.Agents.Persistent` + `Azure.Identity`. The whole solution compiles clean — **no LIVE
   blocker**. The Foundry construction (`CreateAIAgentAsync`) is **marked `[Obsolete]` in rc5**
   ("use native `AIProjectClient.Agents` APIs"); suppressed locally (CS0618) and isolated in
   `CreateFoundryAgentAsync` so DEMO is unaffected. **Flag for revisit** when AzureAI GAs / the
   native Agents surface stabilizes. No credential is acquired in DEMO (FR-008) — `AgentRunner`
   construction is side-effect free and only `RunAsync` (LIVE-only) builds `DefaultAzureCredential`.
3. **Baseline outreach ranking (US1):** composite = 0.40·wallet + 0.30·engagement +
   0.30·eventRelevance, each component normalized to [0,1] and rounded to 4dp; `outreach` is the
   top-3 most-affected clients (by wallet) re-ranked by composite → deterministic, byte-stable.
   US2 (T024) will refine talking points/rationale on this base.

## Files created

- `src/orchestration-api/Prompts/morning-brief.md` (T017)
- `src/orchestration-api/Models/MorningBriefRequest.cs` (T016 request model)
- `src/orchestration-api/Agents/Demo/MorningBriefComposer.cs` (T015)
- `src/orchestration-api/Agents/Tools/MorningBriefTools.cs` (T018)
- `src/orchestration-api/Agents/AgentRunner.cs` (T019)
- `src/ui-app/src/scenes/MorningBrief/MarketStrip.tsx` (T021)
- `tests/orchestration-api.Tests/MockApiBackedFactory.cs` (T013/T047 seam)
- `tests/orchestration-api.Tests/MorningBriefPerformanceTests.cs` (T047)

## Files modified

- `src/orchestration-api/Program.cs` (T016 endpoint + DI for composer/tools/runner)
- `src/orchestration-api/orchestration-api.csproj` (Agent Framework + Azure pkg refs; prompt copy)
- `src/agent-provisioner/agent-provisioner.csproj` + `Program.cs` (T020)
- `src/ui-app/src/scenes/MorningBrief/MorningBriefScene.tsx` (T021)
- `tests/orchestration-api.Tests/orchestration-api.Tests.csproj` (aliased mock-api ref)
- `tests/orchestration-api.Tests/UserStory1MorningBriefTests.cs` (un-skip T013/T014)

## Not done (out of scope — Phase 4+)

US2 (T022+) ranking refinement, US3 human-in-the-loop, US4 determinism/parity hardening,
deployment (T035+). No `git commit` — changes left staged for human review.
