# Multi-Turn Conversation & Memory

## Priority: P3 (Medium)

## Status: Selected for NEXT iteration — order 4 of 6 (per 2026-06-09 direction). Unblocked: uses anonymous session ids (no auth), and the chat surface comes from the rescoped 006. 002-authentication is NO LONGER a dependency.

## Description
Extend agents from single-shot request/response to multi-turn conversations
with context memory, enabling follow-up questions and drill-downs.

## Scope
- Session management: conversation history per **anonymous session id** (cookie/header/GUID — no login required for the demo).
- Redis or Cosmos DB for session state persistence.
- Modified runner to accept message history (not just single payload).
- Frontend chat-style UI per scene (type follow-up, see history).
- Token budget management (truncate/summarize old messages).
- Session timeout and cleanup.

## Acceptance Criteria
- [ ] User can ask a follow-up and agent has context from prior turns.
- [ ] Sessions expire after configurable TTL.
- [ ] Token usage stays within model limits (automatic summarization).
- [ ] DEMO mode supports scripted multi-turn sequences.
- [ ] No PII persisted beyond session TTL.

## Dependencies
- 006-demo-ux (the follow-up chat surface). NOTE: 002-authentication is intentionally NOT a
  dependency — the demo uses anonymous session ids.

## Notes
Start simple: in-memory dict for dev, Redis for prod. Cosmos DB only
if we need cross-region persistence.
