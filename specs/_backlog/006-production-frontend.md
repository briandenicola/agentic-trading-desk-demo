# Production Frontend (React/Vite)

## Priority: P2 (High)

## Description
Replace the static HTML/JS cockpit with a proper React + Vite SPA that
supports routing, state management, and component reuse.

## Scope
- Vite + React + TypeScript scaffold in `frontend/`.
- Scene components (one per agent: Prioritization, Exposure, Allocation, Client360).
- Shared layout: sidebar nav, header with user info, main content area.
- API client layer with proper error handling and loading states.
- MSAL.js integration (ref backlog 002).
- Responsive design for desktop (1280px+) and tablet.

## Acceptance Criteria
- [ ] `npm run build` produces a deployable bundle.
- [ ] All four scenes render agent responses identically to current HTML demo.
- [ ] Loading/error states handled gracefully.
- [ ] Auth flow integrated (login, token refresh, logout).
- [ ] Accessible (WCAG 2.1 AA basics: focus management, labels, contrast).

## Dependencies
- 002-authentication (for MSAL integration)

## Notes
Consider a design system (Fluent UI or Radix) for consistency with Microsoft
tooling. Keep the existing `frontend/index.html` as a fallback for demo mode.
