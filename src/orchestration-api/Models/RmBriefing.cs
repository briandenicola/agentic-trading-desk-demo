namespace OrchestrationApi.Models;

/// <summary>
/// Response DTOs for <c>POST /api/agent/rm-briefing</c> — the Commercial Banking RM
/// Daily Briefing (course correction: the PRIMARY "Morning Planning &amp; Prioritized
/// Outreach" scene). The shape is identical in DEMO and LIVE modes (constitution
/// Principle III) and mirrors the client ground-truth sample
/// <c>assets/rm_daily_briefing_2026-05-14.html</c>. All data is fictional. Serialize
/// with <see cref="RmBriefingJson.Options"/> (camelCase, omit null <c>notes</c>).
/// </summary>
public sealed record RmBriefing
{
    public required string Mode { get; init; }                  // "DEMO" | "LIVE"
    public required string AsOf { get; init; }                  // ISO-8601 date
    public required string Greeting { get; init; }              // "Good morning, Marcus"
    public required RmIdentity Rm { get; init; }
    public required RmPortfolio Portfolio { get; init; }
    public required RmKpis Kpis { get; init; }
    public required IReadOnlyList<ReasoningStep> Reasoning { get; init; }
    public required IReadOnlyList<PriorityCall> PriorityCallList { get; init; }
    public required IReadOnlyList<ComplaintSnapshot> ComplaintsSnapshot { get; init; }
    public required IReadOnlyList<PipelineClose> PipelineClosing { get; init; }
    public required IReadOnlyList<MacroBullet> MacroSnapshot { get; init; }
    public required string SuggestedFirstAction { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }

    /// <summary>All current events the synthesis weighed (overnight + intraday). Empty ⇒ no events processed. (002)</summary>
    public IReadOnlyList<MarketEvent> EventsConsidered { get; init; } = [];
}

public sealed record RmIdentity
{
    public required string RmId { get; init; }
    public required string Name { get; init; }
    public string? Title { get; init; }
    public string? Territory { get; init; }
}

public sealed record RmPortfolio
{
    public required int CustomerCount { get; init; }
    public required double TotalExposureMm { get; init; }
    public required double TotalDepositsMm { get; init; }
}

public sealed record RmKpis
{
    public required int YesterdayTouchpoints { get; init; }
    public required int OpenPipelineCount { get; init; }
    public required double OpenPipelineAmountMm { get; init; }
    public required int ClosingWithin14Days { get; init; }
    public required int ActiveComplaints { get; init; }
}

/// <summary>One ranked customer in the priority call list (mirrors the briefing's cards).</summary>
public sealed record PriorityCall
{
    public required int Rank { get; init; }
    public required int Priority { get; init; }                 // 1..4 UI band (red→green)
    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public string? IndustrySector { get; init; }
    public string? HqCity { get; init; }
    public string? State { get; init; }
    public double? AnnualRevenueMm { get; init; }
    public string? RiskRating { get; init; }
    public required int Score { get; init; }
    public required IReadOnlyList<CallTag> Tags { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public required string SuggestedAction { get; init; }

    /// <summary>Per-call linkage; names the event(s) that changed this call's rank/priority (FR-007, SC-005). Empty for un-affected calls. (002)</summary>
    public IReadOnlyList<EventLinkage> DrivingEvents { get; init; } = [];
}

public sealed record CallTag
{
    public required string Label { get; init; }
    public required string Kind { get; init; }                  // escalated|in-progress|closing|stuck|followup
}

public sealed record ComplaintSnapshot
{
    public required string ComplaintId { get; init; }
    public required string CustomerName { get; init; }
    public string? Category { get; init; }
    public string? Severity { get; init; }
    public required string Status { get; init; }
    public string? DateFiled { get; init; }
}

public sealed record PipelineClose
{
    public required string OpportunityId { get; init; }
    public required string CustomerName { get; init; }
    public string? ProductType { get; init; }
    public string? Stage { get; init; }
    public required double AmountMm { get; init; }
    public string? ExpectedCloseDate { get; init; }
}

public sealed record MacroBullet
{
    public required string Headline { get; init; }
    public required string Detail { get; init; }
}
