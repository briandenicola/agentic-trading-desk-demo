namespace OrchestrationApi.Models;

/// <summary>
/// Orchestration-side mirror of the mock-api event store shape
/// (002-reactive-event-cockpit). The orchestration layer reaches the event store
/// ONLY over HTTP via the mock API (constitution Principle II); these DTOs deserialize
/// the <c>/mock/events</c> payloads. <see cref="EventLinkage"/> is derived locally by the
/// deterministic <c>EventImpactResolver</c> (DEMO) or the specialist agents (LIVE) and is
/// surfaced additively on the existing scene DTOs. All data is fictional.
/// </summary>
public sealed record MarketEvent
{
    public required string Id { get; init; }
    public required string Type { get; init; }              // macro_rate | sector | issuer_credit | client_headline
    public required string Headline { get; init; }
    public required string Summary { get; init; }
    public string? Source { get; init; }
    public required string Severity { get; init; }          // low | medium | high
    public string? PublishedAt { get; init; }               // ISO-8601
    public string? IngestedAt { get; init; }                // ISO-8601 (server-set)
    public string? Scope { get; init; }                     // overnight | intraday
    public string? Origin { get; init; }                    // seed | admin | feed
    public string? Direction { get; init; }                 // positive | negative | neutral
    public AffectedEntities AffectedEntities { get; init; } = new();
}

/// <summary>Typed selectors mapping an event to portfolio entities across both scenes (R5).</summary>
public sealed record AffectedEntities
{
    public IReadOnlyList<string>? CustomerIds { get; init; }   // CB customers (RM scene)
    public IReadOnlyList<string>? Tickers { get; init; }       // TD securityIds (Trading scene)
    public IReadOnlyList<string>? Sectors { get; init; }       // CB + TD sectors (both scenes)
    public IReadOnlyList<string>? Issuers { get; init; }       // TD issuers (Trading scene)
}

/// <summary>
/// Derived per-item linkage naming the event(s) that changed a briefing item's rank/priority
/// (FR-007, SC-005). Not stored — recomputed each synthesis. When multiple events hit one entity,
/// each contributing linkage is listed and the <see cref="Contribution"/> values net/sum (R6).
/// </summary>
public sealed record EventLinkage
{
    public required string EventId { get; init; }
    public required string Headline { get; init; }
    public required string EntityRef { get; init; }         // customerId / ticker / issuer
    public required double Contribution { get; init; }      // signed score delta (net-able)
    public required string Rationale { get; init; }
}

/// <summary>
/// The SSE <c>briefing-update</c> envelope pushed to open cockpit briefings (FR-010/FR-011,
/// 002-reactive-event-cockpit §4). Carries a consolidated <see cref="LiveAlert"/> plus the FULL
/// re-synthesized scene DTO (reaction granularity, R7) — an <see cref="RmBriefing"/> for
/// <c>rm-briefing</c> or a <see cref="MorningBrief"/> for <c>morning-brief</c>. Identical shape in
/// DEMO and LIVE (Principle III). Validated against
/// <c>specs/002-.../contracts/live-update.schema.json</c>. All data is fictional.
/// </summary>
public sealed record LiveUpdate
{
    public required long Sequence { get; init; }            // monotonic; mirrored to SSE id: (R4)
    public required string Scene { get; init; }             // rm-briefing | morning-brief
    public required LiveAlert Alert { get; init; }
    public required object Briefing { get; init; }          // RmBriefing | MorningBrief (full DTO)
}

/// <summary>Consolidated banner content for a <see cref="LiveUpdate"/> (data-model §4a).</summary>
public sealed record LiveAlert
{
    public required string Priority { get; init; }          // info | notice | urgent (from severity)
    public required string Headline { get; init; }          // consolidated; names the new event(s)
    public required IReadOnlyList<string> EventIds { get; init; } // triggering event id(s); ≥1
    public required bool NoImpact { get; init; }            // true => new event, ranking unchanged
}

/// <summary>
/// Operator-authored payload from the <c>/admin</c> route (002 US3 / FR-014). On a successful POST
/// it becomes an intraday <see cref="MarketEvent"/> (scope=intraday, origin=admin) and flows through
/// the SAME ingestion + reactive SSE path as a real intraday event (FR-016). Validated server-side
/// (FR-015) before it reaches the event store; nothing is ingested on failure. Mirrors
/// <c>specs/002-.../contracts/admin-submission.schema.json</c>. All data is fictional.
/// </summary>
public sealed record AdminNewsSubmission
{
    public string? Headline { get; init; }
    public string? Summary { get; init; }
    public string? Source { get; init; }
    public string? Severity { get; init; }                  // low | medium | high
    public string? Type { get; init; }                      // macro_rate | sector | issuer_credit | client_headline
    public string? Direction { get; init; }                 // positive | negative | neutral
    public AffectedEntities? AffectedEntities { get; init; }
    public IReadOnlyList<string>? SceneTargeting { get; init; }
}
