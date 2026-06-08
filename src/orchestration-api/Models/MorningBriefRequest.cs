namespace OrchestrationApi.Models;

/// <summary>
/// Request body for <c>POST /api/agent/morning-brief</c> (T016). Mirrors
/// <c>contracts/agent-api.yaml</c>: an optional <c>payload</c> with an optional
/// <c>eventId</c> and <c>date</c>. Both modes accept the same shape; missing values
/// fall back to the demo Fed-hike scenario.
/// </summary>
public sealed record MorningBriefRequest
{
    public MorningBriefPayload? Payload { get; init; }
}

public sealed record MorningBriefPayload
{
    /// <summary>Market event to brief on (defaults to <c>fed_surprise_hike</c>).</summary>
    public string? EventId { get; init; }

    /// <summary>Trading day (defaults to the market-data <c>asOf</c> date).</summary>
    public string? Date { get; init; }
}
