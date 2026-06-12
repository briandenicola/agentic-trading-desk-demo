# Trading Desk "Open Chat" — grounded institutional-desk chat assistant

## Priority: P2 (High)

## Status: Delivered & deployed to Sweden (2026-06-13). DEMO end-to-end + LIVE Foundry path + UI built, tested, and LIVE-smoke verified.

## Description
Bring the Commercial Banking RM "Open Chat" affordance to the **Trading Desk** scenes. The desk's call
cards (and the New Issue outreach card) now expose a seeded **Open Chat** button that launches the same
floating chat dock, but grounded in the **trading-desk** systems-of-record (`/mock/td/*`) for the
coverage salesperson (persona **Theo Wexler**) rather than the RM book. The assistant answers
desk-relevant questions — who to call and why, a client profile/recent activity, a security's interest
(holders/trades/RFQs/news), inventory axes, and current intraday events. It is agent/data-backed (LIVE
Foundry reasons over the trading-desk HTTP tools; DEMO composes the identical reply shape — Principle
III). All data is fictional.

## Scope (delivered)
- **Routing**: `POST /api/chat` branches on `ChatRequest.SalespersonId` — when set, it routes to the
  trading-desk path; otherwise it keeps the existing Commercial Banking RM path. Both return the
  mode-blind `ChatReply` shape.
- **DEMO** (`Agents\Demo\TdChatResponder.cs`): deterministic, HTTP-only intent router grounded in
  `/mock/td/*` (who-to-call / client profile `CL-xxxx` / security interest `SEC-xxxxx` / axes / events /
  help). Reuses `TdBriefingComposer` for the top calls and `EventTools` for events.
- **LIVE** (`Agents\TdChatAgentRunner.cs`, agent `trading-desk-assistant`,
  `Prompts\trading-desk-assistant.md`): Foundry agent binding the 12 `TdBriefingTools`; degrades to
  `TdChatResponder` on failure/empty output. Uses `FOUNDRY_MODEL_CHAT`.
- **Provisioning**: `agent-provisioner` registers `trading-desk-assistant` (runtime-managed).
- **UI** (`scenes\TradeDesk\tdChatConfig.ts`): the chat dock is generalized via a `ChatDockConfig`
  (`ChatDockProvider`/`ChatOverlay`), default = RM (backward-compatible). The three Trading Desk scenes
  (`/desk`, `/desk/morning-brief`, `/desk/new-issue`) wrap their content in
  `<ChatDockProvider config={tdChatConfig}>`; each `TdCallCard` and the New Issue outreach card render a
  seeded **Open Chat** button. `client.ts` adds `sendDeskChat(messages, salespersonId?)`.

## Acceptance Criteria
- [x] `dotnet build WF-Garage.sln` clean; trading path of `POST /api/chat` (with
      `{ salespersonId: "Theo Wexler", … }`) returns grounded `ChatReply` in DEMO. New tests in
      `tests\orchestration-api.Tests\TdChatTests.cs` (4) pass.
- [x] `npm --prefix src\ui-app run build` clean; `npm --prefix src\ui-app test` green (incl. a new
      `TradeDeskScene.test.tsx` case asserting the seeded Open Chat posts `salespersonId` to `/chat`).
- [x] DEMO and LIVE return the identical `ChatReply` shape (Principle III); CB chat path unchanged
      (`sendChat` and its tests untouched).
- [x] Deployed to Sweden (orchestration + ui-app + mock-api @e9ab5a6, rev r2606121130) and LIVE-smoke
      verified: `POST /api/chat {salespersonId:"Theo Wexler", …}` → `mode=LIVE`, grounded trading-desk
      answer (CL-/SEC- IDs + current Fed event). `trading-desk-assistant` auto-created on first LIVE call
      (GetAIAgent→catch→Create). CB chat regression: default path still routes to `markets-assistant`.

## Non-Goals
- No new mock-api endpoints — reuses the existing `/mock/td/*` aggregates.
- No change to the Commercial Banking RM chat (default dock config / `markets-assistant`).
- No real market-data vendors; all data fictional.

## Dependencies
- Builds on the Trading Desk scene (`td-briefing`), `TdBriefingTools`/`TdBriefingComposer`, the
  Commercial Banking AI Chat (`markets-assistant`, the chat dock pattern), and the mint-v4
  command-center shell (backlog 012).

## Notes
Reference: client request (2026-06-13) — mirror the CB RM "Open Chat" hero affordance on the trading
desk. Persona/coverage salesperson Theo Wexler. Cast names stay fictional (constitution).
