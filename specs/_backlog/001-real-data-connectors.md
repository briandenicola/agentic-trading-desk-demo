# Replace Mock APIs with Real Data Connectors

## Priority: P1 (Critical Path)

## Status: Deferred — kept in backlog, not planned for the current iteration (per 2026-06-09 direction). Demo stays on fictional mock data.

## Description
Replace the fixture-based mock APIs in `api/data.py` with real connectors to
Tableau, Dynamics 365, the trading book, new-issue calendar, MMD/relative-value
feed, and Coalition.

## Scope
- Keep `api/data.py` function signatures intact (they are the seam).
- Each connector gets its own module under `api/connectors/`.
- Configuration via env vars (connection strings, API keys).
- Graceful fallback: if a real connector is unavailable, fall back to mock data.
- Add `CONNECTOR_MODE=mock|live` env var to toggle per-source.

## Acceptance Criteria
- [ ] At least one connector (e.g., Tableau) reads from a real source.
- [ ] All existing tests still pass in mock mode.
- [ ] New integration tests exercise the real connector (skipped in CI if no creds).
- [ ] `.env.example` updated with new required vars.
- [ ] No change to agent tools or frontend.

## Dependencies
None — this can start immediately.

## Notes
Start with whichever system of record is easiest to access (likely Dynamics or
the trading book). The architecture ensures zero changes to `api/agents/tools.py`.
