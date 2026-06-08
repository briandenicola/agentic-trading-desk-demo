# Skill: LIVE/DEMO parity + the HTTP data seam

- **Confidence**: low (seeded at team hire 2026-06-08; bump when first applied)
- **Domain**: orchestration-api (Livingston), ui-app (Linus), tests (Yen)

## When to use
Any work touching the morning-brief scene response, the mode switch, or the mock-data seam.

## Pattern
- **One shape, two modes** (Principle III). The DEMO composer
  (`Agents\Demo\MorningBriefComposer.cs`) and the LIVE `AgentRunner` both emit a `MorningBrief`
  that validates against `contracts\morning-brief.schema.json`. Add a field in one mode → add it
  in the other.
- **DEMO is the default and offline**: `DEMO_MODE=1` overrides any LIVE config; no
  `DefaultAzureCredential`, no model call. Deterministic ordering + stable JSON serialization
  options → byte-identical output across runs (SC-002).
- **The seam is HTTP**: both modes pull data from `src\mock-api` over a typed `HttpClient`
  implementing `openapi\tools.yaml`. To go live for real, swap `mock-api\Data\` loaders for real
  connectors — agents and UI untouched.
- **Frontend is mode-blind** (Linus): render the scene JSON only; never branch on LIVE/DEMO.
- **Ranking blend** (US2): compositeScore = 0.4·wallet + 0.3·engagement + 0.3·eventRelevance;
  outreach sorted by score, ranks contiguous from 1; talking points reference the event + a
  relevant axis/holding.

## Guardrails (Yen verifies)
- Determinism test (T031), parity test (T032), perf < 10s (T047, SC-001).
- Tool errors degrade with a `notes` entry + structured JSON, never HTML (T014, FR-011).
- Unknown `eventId` → structured 400; no materially-affected clients → empty lists + `notes`.

## References
- `contracts\morning-brief.schema.json`, `contracts\agent-api.yaml`
- `specs\001-morning-planning-outreach\spec.md` (Edge Cases, SC-001..SC-007)
