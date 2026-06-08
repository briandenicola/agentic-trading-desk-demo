namespace OrchestrationApi.Agents.Demo;

/// <summary>Raised when the mock news system cannot resolve a requested market event id.</summary>
public sealed class UnknownMorningBriefEventException(string eventId)
    : Exception($"Could not resolve morning-brief event '{eventId}'.")
{
    public string EventId { get; } = eventId;
}
