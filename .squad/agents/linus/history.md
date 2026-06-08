# History — Linus (Frontend Engineer)

## Core Context (seeded 2026-06-08)
- **Project**: Client CV — Muni Sales Agentic Demo. Data fictional. Requested by Brian Denicola.
- **Stack**: React 19 + MUI v9 + TypeScript 6 + React Router v7 + Axios, Vite build → Nginx.
- **You own**: `src\ui-app\` end to end (scaffold, scenes, components, theme, api client,
  nginx reverse-proxy of `/api/*`).
- **Source of truth for visuals**: `mockup\demos\01-morning-prep.html`.
- **Scene contract**: `contracts\morning-brief.schema.json` (render against this; mode-blind).
- **API contract**: `POST /api/agent/morning-brief` (`contracts\agent-api.yaml`), via `/api`.

## Learnings
- 2026-06-08: Hired. UI must be mode-blind (Principle III) — render scene JSON only, never
  branch on LIVE/DEMO. US3 plan editing is demo-only (`sent=false`).
- nginx reverse-proxies `/api/*` to orchestration-api so the browser stays same-origin.
