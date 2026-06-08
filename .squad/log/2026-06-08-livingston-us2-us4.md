# 2026-06-08 — Livingston — US2/US4 backend hardening

## Summary
- Added US2/US4 orchestration tests for ranking weights, talking points, DEMO determinism, LIVE/DEMO schema parity, no-credential DEMO behavior, empty affected-client state, and unknown-event 400 handling.
- Extracted deterministic outreach ranking into `src\orchestration-api\Agents\Demo\OutreachRanker.cs` while keeping `MorningBriefComposer` responsible for mock-api HTTP data fetching.
- Updated LIVE prompt/mapping so `outreach` rationale uses the documented 0.40/0.30/0.30 composite and ranks normalize deterministically.
- Mapped unknown mock news event ids to structured HTTP 400 ProblemDetails instead of a degraded 200.

## Verification
- `dotnet test tests\orchestration-api.Tests\orchestration-api.Tests.csproj --nologo -v q` passed (13 tests).
