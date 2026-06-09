namespace OrchestrationApi.Models;

/// <summary>
/// Request body for <c>POST /api/agent/rm-briefing</c>. Mirrors the morning-brief
/// request envelope: an optional <c>payload</c> with the relationship manager to brief
/// (<c>rmId</c>, defaults to the demo persona RM-104 / Marcus Johnson) and the briefing
/// <c>date</c> (defaults to the ground-truth sample date 2026-05-14). Both modes accept
/// the same shape.
/// </summary>
public sealed record RmBriefingRequest
{
    public RmBriefingPayload? Payload { get; init; }
}

public sealed record RmBriefingPayload
{
    /// <summary>Relationship manager id to brief (defaults to <c>RM-104</c>).</summary>
    public string? RmId { get; init; }

    /// <summary>Briefing day (defaults to <c>2026-05-14</c>, the sample date).</summary>
    public string? Date { get; init; }
}
