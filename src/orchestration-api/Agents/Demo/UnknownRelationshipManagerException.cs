namespace OrchestrationApi.Agents.Demo;

/// <summary>Raised when the mock CB system cannot resolve a requested relationship-manager id.</summary>
public sealed class UnknownRelationshipManagerException(string rmId)
    : Exception($"Could not resolve relationship manager '{rmId}'.")
{
    public string RmId { get; } = rmId;
}
