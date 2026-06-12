namespace OrchestrationApi.Models;

/// <summary>
/// Request body for <c>POST /api/agent/td-new-issue</c>. Optional <c>payload</c> can pin the
/// primary issuer's equity security and the focus client; both default to the demo cast
/// (Prairie Green Renewables <c>SEC-3601</c> / Crestline Capital <c>CL-2015</c>) and the dataset
/// snapshot date <c>2026-05-22</c>. Both modes accept the same shape.
/// </summary>
public sealed record TdNewIssueRequest
{
    public TdNewIssuePayload? Payload { get; init; }
}

public sealed record TdNewIssuePayload
{
    /// <summary>Equity security of the issuer announcing the new issue (defaults to SEC-3601).</summary>
    public string? IssuerSecurityId { get; init; }

    /// <summary>Client to spotlight for outreach (defaults to CL-2015, Crestline Capital).</summary>
    public string? ClientId { get; init; }

    /// <summary>As-of day (defaults to <c>2026-05-22</c>, the dataset snapshot date).</summary>
    public string? Date { get; init; }
}
