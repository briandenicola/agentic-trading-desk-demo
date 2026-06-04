# Test Suite & Coverage

## Priority: P2 (High)

## Description
Build out a proper test suite: unit tests for tools/data, integration tests
for agent endpoints, and contract tests against `openapi/tools.yaml`.

## Scope
- `tests/` directory structure: `unit/`, `integration/`, `contract/`.
- Unit tests for each function in `api/data.py` and `api/agents/tools.py`.
- Integration tests using FastAPI `TestClient` for each scene (demo mode).
- Contract tests validating responses match `openapi/tools.yaml` schemas.
- `pytest-cov` for coverage reporting; target 80%+ on `api/`.
- CI runs all tests; coverage gate in PR checks.

## Acceptance Criteria
- [ ] `pytest` discovers and runs all test types.
- [ ] Each scene has at least one integration test.
- [ ] Each tool function has at least one unit test.
- [ ] Coverage report generated and visible in CI.
- [ ] No test requires external services (all use demo/mock mode).

## Dependencies
None — can start immediately.

## Notes
Good first issue for getting comfortable with the codebase. Start with
`test_agents.py` testing the demo composers.
