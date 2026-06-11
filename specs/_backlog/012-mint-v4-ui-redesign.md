# M.INT v4 UI Redesign (visual alignment to `assets\mint-v4.html`)

## Priority: P2 (High)

## Status: Backlog — requested by client 2026-06-11. Visual/UX alignment only; no change to the agent flows, DTOs, or LIVE/DEMO parity.

## Description
The client would like the React UI (`src\ui-app\`) to look similar to the design mockup at
`assets\mint-v4.html`. This is a **look-and-feel / theming** pass over the existing scenes —
primarily the landing page, the Trading Desk (`/desk`, `/desk/morning-brief`) and the Commercial
Banking RM workspace — to match the layout, palette, typography, and component styling of the v4
mockup. The data, routes, agent calls, SSE re-rank, and the human-in-the-loop call plan stay as-is.

## Scope
- Reconcile the M.INT theme (`src\ui-app\src\theme\theme.ts`) palette/typography/spacing with the
  mint-v4 mockup.
- Re-style the shared shell (header/nav, hero, cards, market strip, call cards) to the v4 layout.
- Align the Trading Desk scenes (`scenes\TradeDesk`) and landing chooser to the v4 composition.
- Keep every scene **mode-blind** (Principle III) and render the same DTOs unchanged.

## Acceptance Criteria
- [ ] `npm --prefix src\ui-app run build` produces a deployable bundle.
- [ ] `npm --prefix src\ui-app test` stays green (update snapshots/queries only as needed).
- [ ] Landing, `/desk`, `/desk/morning-brief`, and `/cb` visually align with `assets\mint-v4.html`.
- [ ] No change to API contracts, DTO shapes, SSE behavior, or DEMO/LIVE parity.

## Non-Goals
- No new scenes, routes, or agent behavior.
- No backend/orchestration or mock-api changes.

## Dependencies
- None. Pure frontend; pairs with the existing Trading Desk scenes.

## Notes
Reference mockup: `assets\mint-v4.html` (do not commit `assets/`). SPA: React 19 + Vite + TS + MUI v9,
tests via Vitest/RTL.
