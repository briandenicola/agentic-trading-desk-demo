# Observability & Structured Logging

## Priority: P2 (High)

## Description
Add structured logging, OpenTelemetry tracing, and a correlation ID system
so agent interactions are traceable end-to-end.

## Scope
- Structured JSON logging (replace default uvicorn logger).
- Correlation ID middleware (generate on request, propagate to tool calls).
- OpenTelemetry traces on `/api/agent/{scene}` and each tool HTTP call.
- Log the tool-calling sequence in LIVE mode (which tools, latency, tokens).
- Azure Monitor / Application Insights exporter for production.
- Never log secrets or PII.

## Acceptance Criteria
- [ ] Every log line is structured JSON with timestamp, level, correlation_id.
- [ ] Agent requests show full tool-call chain in traces.
- [ ] LIVE mode logs token usage per request.
- [ ] No secrets or PII in logs (verified by test or grep).
- [ ] Works locally (console exporter) and in Azure (App Insights).

## Dependencies
- 003-container-deployment (for Azure Monitor integration)

## Notes
Use `opentelemetry-instrumentation-fastapi` and `opentelemetry-instrumentation-httpx`.
