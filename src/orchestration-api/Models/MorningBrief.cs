namespace OrchestrationApi.Models;

/// <summary>
/// Response DTOs for <c>POST /api/agent/morning-brief</c> (T010). The shape is
/// identical in DEMO and LIVE modes and mirrors
/// <c>contracts/morning-brief.schema.json</c> (constitution Principle III / FR-010).
/// Serialize with <see cref="MorningBriefJson.Options"/> so property names are
/// camelCase and null <c>notes</c> are omitted.
/// </summary>
public sealed record MorningBrief
{
    public required string Mode { get; init; }              // "DEMO" | "LIVE"
    public required string AsOf { get; init; }              // ISO-8601 date-time
    public required IReadOnlyList<MarketStripItem> MarketStrip { get; init; }
    public required IReadOnlyList<ReasoningStep> Reasoning { get; init; }
    public required MacroNarrative MacroNarrative { get; init; }
    public required IReadOnlyList<AffectedClient> MostAffectedClients { get; init; }
    public required IReadOnlyList<OutreachItem> Outreach { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }

    /// <summary>All current events the synthesis weighed (overnight + intraday). (002)</summary>
    public IReadOnlyList<MarketEvent> EventsConsidered { get; init; } = [];
}

public sealed record MarketStripItem
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public string? Change { get; init; }
    public required string Direction { get; init; }        // "up" | "down" | "flat"
}

public sealed record ReasoningStep
{
    public required string Text { get; init; }
    public required string Status { get; init; }           // "done" | "pending"
}

public sealed record MacroNarrative
{
    public required string Summary { get; init; }
    public required string WhyItMatters { get; init; }
    public required IReadOnlyList<string> Sources { get; init; }
}

public sealed record AffectedClient
{
    public required string Cid { get; init; }
    public required string Name { get; init; }
    public required string Tier { get; init; }
    public required string Exposure { get; init; }
    public required Concern Concern { get; init; }

    /// <summary>Per-affected-client linkage; names the driving event(s) (FR-007). (002)</summary>
    public IReadOnlyList<EventLinkage> DrivingEvents { get; init; } = [];
}

public sealed record Concern
{
    public required string Label { get; init; }
    public required string Kind { get; init; }             // "sell" | "warm" | "info"
}

public sealed record OutreachItem
{
    public required int Rank { get; init; }
    public required string Cid { get; init; }
    public required string Name { get; init; }
    public required string SuggestedTopic { get; init; }
    public required IReadOnlyList<string> TalkingPoints { get; init; }
    public required RankingRationale Rationale { get; init; }
}

public sealed record RankingRationale
{
    public required double WalletScore { get; init; }
    public required double EngagementScore { get; init; }
    public required double EventRelevanceScore { get; init; }
    public required double CompositeScore { get; init; }
    public required string Explanation { get; init; }
}
