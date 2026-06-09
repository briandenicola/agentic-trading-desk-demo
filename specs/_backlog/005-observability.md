# Observability — Full Agent & Tool-Call Traceability

## Priority: P2 (High)

## Status: Selected for NEXT iteration — order 1 of 6 (per 2026-06-09 direction). Partially delivered (baseline wired); full agent traceability outstanding. Unblocks 011.

## Description
Make every morning-brief request traceable end-to-end — from the UI call, through the Foundry
agent's reasoning, each tool call, and the mock-api data fetch — in Application Insights, and keep
the Foundry portal run view as the agent-native trace.

> Note: this card was originally written for a Python/uvicorn/FastAPI stack. The project pivoted to
> C#/.NET 10 (ADR-0002); the scope below reflects the C# implementation in `src\shared\Observability`,
> `src\orchestration-api`, and `infra\`.

## Delivered baseline
- Serilog structured JSON logging with `service.name` + correlation id enrichment.
- Correlation-id middleware (`X-Correlation-ID`), generated per request and pushed to `LogContext`.
- OpenTelemetry tracing + metrics: `AddAspNetCoreInstrumentation` + `AddHttpClientInstrumentation`
  (tool calls appear as outbound HTTP dependency spans).
- Global JSON exception handler (never HTML), preserving the correlation id (FR-011).
- Persistent single agent (`001`) so Foundry portal run history is consolidated under one agent.

## Outstanding scope (the actual gaps)
- **Traces export nowhere in Azure today.** The OTLP exporter only attaches when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set; the container apps inject only
  `APPLICATIONINSIGHTS_CONNECTION_STRING`. Wire the **Azure Monitor OpenTelemetry exporter**
  (`Azure.Monitor.OpenTelemetry.AspNetCore`, already pinned) keyed off that connection string.
- **No GenAI spans.** Register the Microsoft Agent Framework `ActivitySource` so LLM-level
  `gen_ai.*` spans (model, tool choice, token usage) are captured.
- **No explicit tool-call span.** Emit a span per tool invocation (tool name, arguments, duration,
  result size) and a single parent **agent-run span** correlating UI → agent → tool → mock-api.
- **Token usage** per LIVE request logged/recorded as a metric.
- **Message-content capture** enabled (demo data is fictional, so prompt/response capture is safe)
  to make the reasoning fully inspectable.

## Acceptance Criteria
- [ ] Every log line is structured JSON with timestamp, level, correlation id.
- [ ] A LIVE request shows the full tool-call chain as one correlated trace in App Insights.
- [ ] LIVE mode records token usage per request.
- [ ] GenAI spans show model + tool decisions.
- [ ] No secrets or PII in logs (verified by test or grep) — all data is fictional.
- [ ] Works locally (console exporter) and in Azure (App Insights via Azure Monitor exporter).

## Dependencies
- 003-container-deployment (Azure Monitor wiring) — realized by `001-morning-planning-outreach`.

## Notes
Implementation surface: `src\shared\Observability\ObservabilityExtensions.cs` (exporter +
ActivitySource registration), `src\orchestration-api\Agents\AgentRunner.cs` (agent-run + tool-call
spans), and `infra\containerapps.tf` (inject `APPLICATIONINSIGHTS_CONNECTION_STRING`, already
present). See `docs\architecture.md` for the current/target trace model.
