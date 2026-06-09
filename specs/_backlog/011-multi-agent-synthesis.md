# Multi-Agent Fan-out / Synthesis Flow

## Priority: P3 (Medium)

## Status: Planned (vision)

## Description
Evolve the morning brief from a single agent with client-side tools into a **multi-agent
flow**: each data pull becomes its own Foundry agent that returns structured data to a central
**synthesizer** agent which composes the final `MorningBrief`.

Today (`001-morning-planning-outreach`) one persistent `morning-brief` agent calls 7 tools in a
loop. The next step distributes those calls across specialist agents, each independently
traceable, so the orchestration becomes an agent graph rather than a single tool-calling loop.

## Scope
- Define per-domain agents (e.g. `market-agent`, `news-agent`, `client-value-agent`,
  `holdings-agent`, `engagement-agent`) each owning the relevant `openapi\tools.yaml` tools.
- Add a central `synthesizer` agent that fans out to the specialists, collects structured data,
  and emits the `MorningBrief` JSON (unchanged DTO; LIVE/DEMO parity preserved).
- Register all agents through `src\agent-provisioner\` (idempotent), keeping persistence.
- Use the Agent Framework's workflow/handoff orchestration (or explicit fan-out) for the graph.
- Keep tools over HTTP to mock-api; no in-process fixture reads.
- Preserve DEMO mode as the deterministic offline composer (no Foundry).

## Acceptance Criteria
- [ ] Each specialist agent is independently registered and traceable in Foundry.
- [ ] The synthesizer produces the same `MorningBrief` shape as DEMO mode.
- [ ] Fan-out runs the specialists concurrently where data has no dependency.
- [ ] Full trace shows the agent graph (synthesizer → specialists → tool calls).
- [ ] DEMO mode is unaffected and offline.

## Dependencies
- 005-observability (full traceability — needed to make the agent graph observable)
- 001-morning-planning-outreach (persistent single-agent baseline)

## Notes
Builds directly on the persistent-agent model in `docs\architecture.md`. The single-agent loop
is the v1; this card is the v2 topology requested for future iterations.
