# Multi-Turn Conversation & Memory

## Priority: P3 (Medium)

## Status: BLOCKED for the current iteration — depends on 002-authentication (need user identity for sessions) and 006-production-frontend (chat UI), both kept in backlog. Revisit once those land.

## Description
Extend agents from single-shot request/response to multi-turn conversations
with context memory, enabling follow-up questions and drill-downs.

## Scope
- Session management: conversation history per user session.
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
- 002-authentication (need user identity for sessions)
- 006-production-frontend (chat UI)

## Notes
Start simple: in-memory dict for dev, Redis for prod. Cosmos DB only
if we need cross-region persistence.
