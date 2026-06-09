# Demo UX Enhancements (Art of the Possible)

## Priority: P2 (High)

## Status: Selected for NEXT iteration — order 3 of 6 (per 2026-06-09 direction). Rescoped from "production frontend hardening" to demo-grade UX. The React 19 + Vite + TypeScript + MUI SPA already exists in `src\ui-app\`; this card is the demo polish + the chat surface that unblocks 009.

## Description
This is an **art-of-the-possible demo**, not a hardened production site. The cockpit SPA
(`src\ui-app\`) already renders the morning brief. This card covers the demo-grade UX that makes
the experience compelling and **unblocks multi-turn (009)** — primarily a follow-up/chat surface
and visible agent reasoning — without auth or production hardening.

> Note: the original card targeted a Python-era `frontend/` rebuild with four scenes and MSAL
> hardening. That is superseded — the SPA exists (ADR-0002), the demo is the single morning-brief
> scene, and authentication (002) is intentionally deferred.

## Scope
- **Follow-up chat panel**: type a follow-up question, see the running turn history (the UI surface
  multi-turn memory needs — see 009).
- **Agent reasoning / trace affordance**: surface the `reasoning` + tool-call flow already returned
  in the brief (and, once 005 lands, a link/IDs to the Foundry run / App Insights trace).
- **Loading & error states**: graceful spinners and a friendly error panel (the global JSON error
  shape already exists server-side).
- **Light visual polish** for a demo on desktop (1280px+).

## Acceptance Criteria
- [ ] `npm --prefix src\ui-app run build` produces a deployable bundle (unchanged from today).
- [ ] A follow-up can be typed and the turn history is visible in the UI.
- [ ] The agent's reasoning / tool-call flow is viewable from the brief.
- [ ] Loading and error states render gracefully (no raw stack traces, no HTML error pages).
- [ ] DEMO mode renders identically without any Foundry/auth credentials.

## Non-Goals (explicitly out of scope for the demo)
- Entra ID / MSAL login (deferred to 002).
- Production hardening, full WCAG 2.1 AA conformance, multi-device responsive matrix.

## Dependencies
- None hard. Pairs with 005-observability (to deep-link traces) and is the UI surface for 009.

## Notes
SPA lives in `src\ui-app\` (React 19, Vite, TypeScript, MUI v9; tests via Vitest/RTL). Keep the
frontend mode-blind (LIVE vs DEMO) per constitution Principle II.
