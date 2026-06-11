namespace OrchestrationApi.Models;

/// <summary>
/// Request body for <c>POST /api/agent/td-briefing</c>. Mirrors the other scene envelopes:
/// an optional <c>payload</c> naming the coverage salesperson to brief
/// (<c>salespersonId</c>, defaults to the demo persona Theo Wexler) and the briefing
/// <c>date</c> (defaults to the dataset snapshot 2026-05-22). Both modes accept the same shape.
/// </summary>
public sealed record TdBriefingRequest
{
    public TdBriefingPayload? Payload { get; init; }
}

public sealed record TdBriefingPayload
{
    /// <summary>Coverage salesperson to brief (defaults to the demo persona, Theo Wexler).</summary>
    public string? SalespersonId { get; init; }

    /// <summary>Briefing day (defaults to <c>2026-05-22</c>, the dataset snapshot date).</summary>
    public string? Date { get; init; }
}
