# New Issue Radar — guided new-issue outreach storyboard

## Priority: P2 (High)

## Status: Delivered — requested by client 2026-06-12 (Teams storyboard). DEMO end-to-end + LIVE Foundry path + UI walkthrough built; pending Sweden deploy verification.

## Description
A guided, step-by-step storyboard for the Institutional Sales & Trading desk that dramatizes the
desk's core edge: connecting a **new-issue announcement** to an existing client who is already
exposed. A primary issuer (**Prairie Green Renewables**) announces a **concurrent debt + equity
issue**; the desk spots that an existing asset-manager client (**Crestline Capital**, CL-2015) both
**holds ~$1.0bn of the issuer's equity** and has been **actively trading the new senior note**
(electronic RFQs + calls), and is prompted to **call them now** with a concrete allocation. The
storyboard is agent/data-backed (LIVE Foundry reasons over the trading-desk HTTP tools; DEMO composes
the identical shape — Principle III) and rendered as a beat-by-beat walkthrough. All data is fictional.

## Scope (delivered)
- **Data** (`src\mock-api\Data\td_*.json`): Prairie Green issuer + equity `SEC-3601` + new note
  `SEC-3602`; Crestline holding `HLD-7901`; RFQs `RFQ-5901..5905`; trades `TRD-8901..8903`; calls
  `CRM-9901/9902`; announcement `NEWS-1901`; desk distribution axe `INV-4901`. No new mock-api
  endpoints — reuses `securities/{id}/interest` + `clients/{id}/activity` aggregates.
- **DTO** (`Models\TdNewIssueStoryboard.cs`): ordered four beats (`announcement` → `holdings` →
  `activity` → `outreach`), each `metrics[]` + `evidence[]`, plus a final `outreach` recommendation
  (talking points, `TradeIdea`, suggested action, draft message). `TdNewIssueRequest` (issuer security,
  client, date).
- **DEMO** (`Agents\Demo\TdNewIssueComposer.cs`): deterministic, HTTP-only composer; derives every
  figure from the tools; falls back to the largest equity holder if the requested client doesn't hold it.
- **LIVE** (`Agents\TdNewIssueRunner.cs`, agent `trading-desk-new-issue`, `Prompts\td-new-issue.md`):
  Foundry agent reusing `TdBriefingTools`; degrades to the DEMO composer on failure/empty output.
- **Endpoint**: `POST /api/agent/td-new-issue` (DEMO→composer, LIVE→runner), serialized with
  `TdNewIssueJson.Options`.
- **UI** (`scenes\NewIssue\TdNewIssueScene.tsx` + `useTdNewIssue.ts`): guided walkthrough in
  `CommandCenterShell` at `/desk/new-issue` (issuer header, clickable beat rail, active beat with
  metrics + evidence, Back/Next, outreach card on the final beat); `client.ts` types + `runTdNewIssue`
  (120s timeout); sidebar nav entry + landing featured chip; Vitest test.
- **iteration-2 landing**: deliberate terminal/command-center restyle of `LandingScene.tsx`.

## Acceptance Criteria
- [x] `dotnet build WF-Garage.sln` clean; `POST /api/agent/td-new-issue` returns the 4-beat
      storyboard + outreach in DEMO (figures data-derived: $998.8mm holding, 5 RFQs, $69.6mm traded).
- [x] `npm --prefix src\ui-app run build` clean; `npm --prefix src\ui-app test` green (22/22).
- [x] DEMO and LIVE return the identical `TdNewIssueStoryboard` shape (Principle III).
- [ ] Deployed to Sweden (mock-api + orchestration + ui-app) and LIVE-smoke verified.

## Non-Goals
- No real market-data vendors; all data fictional.
- No reassignment of Crestline's coverage — framed as a desk-level scenario (activity salesperson
  Theo Wexler).

## Dependencies
- Builds on the Trading Desk scene (`td-briefing`), `TdBriefingTools`, and the mint-v4
  command-center shell (backlog 012).

## Notes
Reference: client Teams storyboard (2026-06-12). Cast names stay fictional (constitution).
