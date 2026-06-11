namespace OrchestrationApi.Agents.Demo;

/// <summary>Raised when the trading-desk dataset has no clients for a requested coverage salesperson.</summary>
public sealed class UnknownSalespersonException(string salespersonId)
    : Exception($"Could not resolve coverage salesperson '{salespersonId}'.")
{
    public string SalespersonId { get; } = salespersonId;
}
