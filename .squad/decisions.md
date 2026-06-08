# Project Decisions Ledger

> Append-only log of binding project decisions. Never rewrite existing entries.
> See constitution §0 for authority ranking.

## Decisions

| # | Date | Decision | Rationale | Author |
|---|------|----------|-----------|--------|
| 1 | 2026-06-04 | Adopt Spec Kit + Squad framework from briandenicola/template | Establish structured AI-driven delivery pipeline for making the project production-ready | Lead |
| 2 | 2026-06-04 | Use ruff for all Python linting/formatting | Single tool replaces black + isort + flake8; faster, consistent | Lead |
| 3 | 2026-06-04 | Target Azure Container Apps for deployment | Aligns with Azure OpenAI dependency; serverless scaling for demo/prod | Lead |
| 4 | 2026-06-08 | Hire Squad crew for Demo 1 (`001-morning-planning-outreach`): Rusty (Lead/Architect, Reviewer), Livingston (Backend .NET/Agent Framework), Linus (Frontend React 19/MUI v9), Basher (Platform/Terraform/CI-CD), Yen (QA/Test, Reviewer) + Scribe + Ralph. Cast from one universe; charters persisted under `.squad\agents\` (require_charter=true honored). | Stand up a specialist team mapped 1:1 to the plan.md `src\` layout so T001–T047 have clear single-owner accountability and reviewer gates per §17/§21. | Squad (Coordinator) for Brian Denicola |
| 5 | 2026-06-08 | Kick off C#/.NET 10 implementation of Demo 1 per constitution §0 Hierarchy of Authority and ADR-0002 (supersedes the stale Python/FastAPI `copilot-instructions.md`, flagged for T043). MVP slice = Phases 1→2→3 (T001–T021) + T047; DEMO path before LIVE within US1; all data over HTTP via mock-api (Principle II); LIVE/DEMO JSON-shape parity (Principle III). This run produced team + Squad artifacts + delegation plan only — NO production code. | Constitution v0.2.1 + ADR-0002 mandate the C#/.NET 10 + Microsoft Agent Framework + Foundry + ACA stack; sequencing the MVP first makes the morning brief demoable offline before LIVE/Foundry and deployment work. | Squad (Coordinator) for Brian Denicola |
| 6 | 2026-06-08 | T001 package-version substitutions (build-green): FluentAssertions pinned **7.2.2** (v8 went paid/commercial — Xceed license); **JsonSchema.Net 9.2.1** added as the draft-2020-12 contract validator; **Microsoft.Agents.AI.AzureAI 1.0.0-rc5** (no stable release; LIVE/Foundry path only, unreferenced until Phase 3); `global.json` pins 10.0.100 with `rollForward: latestFeature` (installed SDK is 10.0.204); **Vite 7.3.5 + plugin-react 5.2.0** instead of Vite 8 (Vite 8/rolldown emits a relative index.html error under the repo's Windows junction path). All other versions match plan.md. | Keep T001–T014 build/test GREEN against currently-available stable packages while preserving plan intent; substitutions are license/availability/toolchain-driven, not design changes. | Squad (Coordinator) for Brian Denicola |
