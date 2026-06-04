# Authentication & Authorization

## Priority: P1 (Critical Path)

## Description
Add Azure Entra ID (OIDC) authentication to protect the agent API endpoints.
The cockpit frontend becomes a single-page app with MSAL.js login.

## Scope
- FastAPI dependency for JWT validation (issuer, audience, exp, nbf).
- MSAL.js integration in `frontend/` for silent token acquisition.
- Role-based access: `Trader`, `SalesHead`, `Admin`.
- `/healthz` and static assets remain unauthenticated.
- CORS tightened to known origins once auth is in place.

## Acceptance Criteria
- [ ] Unauthenticated requests to `/api/agent/*` return 401.
- [ ] Valid Azure AD token grants access; roles checked per scene.
- [ ] Frontend acquires token silently and attaches to fetch calls.
- [ ] Demo mode still works locally without auth (dev bypass via env var).
- [ ] No secrets committed; OIDC config from env vars.

## Dependencies
None — but should ship before real data connectors are exposed.

## Notes
Use `python-jose` or `PyJWT` for token validation. Consider FastAPI
`Security` dependencies for clean per-route auth.
