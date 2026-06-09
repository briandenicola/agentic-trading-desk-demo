using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockApi.Models;

/// <summary>
/// Strongly-typed records for the Commercial Banking RM dataset (course correction:
/// client sample data in <c>assets/</c>). All data is fictional. Field names map to the
/// camelCase keys emitted by the spreadsheet converter into <c>Data/cb_*.json</c>.
/// </summary>
public static class CbJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed record CbCustomer
{
    public string CustomerId { get; init; } = "";
    public string? LegalName { get; init; }
    public string? Dba { get; init; }
    public string? NaicsCode { get; init; }
    public string? NaicsDescription { get; init; }
    public string? IndustrySector { get; init; }
    public string? HqCity { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
    public string? Region { get; init; }
    public double? AnnualRevenueMm { get; init; }
    public int? EmployeeCount { get; init; }
    public int? YearsInBusiness { get; init; }
    public string? CreditRating { get; init; }
    public string? RelationshipManager { get; init; }
    public string? ProductsHeld { get; init; }
    public double? TotalExposureMm { get; init; }
    public double? DepositBalanceMm { get; init; }
    public string? RiskRating { get; init; }
}

public sealed record CbRelationshipManager
{
    public string RmId { get; init; } = "";
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? OfficeCity { get; init; }
    public string? OfficeState { get; init; }
    public string? Territory { get; init; }
    public string? StatesCovered { get; init; }
    public int? CustomersInBook { get; init; }
}

public sealed record CbOpportunity
{
    public string OpportunityId { get; init; } = "";
    public string CustomerId { get; init; } = "";
    public string? CustomerName { get; init; }
    public string? OpportunityName { get; init; }
    public string? ProductType { get; init; }
    public string? Stage { get; init; }
    public double? Amount { get; init; }
    public double? ProbabilityPct { get; init; }
    public string? CreatedDate { get; init; }
    public string? ExpectedCloseDate { get; init; }
    public string? RelationshipManager { get; init; }
    public string? NextStep { get; init; }
}

public sealed record CbComplaint
{
    public string ComplaintId { get; init; } = "";
    public string CustomerId { get; init; } = "";
    public string? CustomerName { get; init; }
    public string? DateFiled { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? Severity { get; init; }
    public string? Status { get; init; }
    public string? DateResolved { get; init; }
    public int? ResolutionDays { get; init; }
    public string? RelationshipManager { get; init; }
    public string? ResolutionNotes { get; init; }
}

public sealed record CbInteraction
{
    public string InteractionId { get; init; } = "";
    public string? Date { get; init; }
    public string CustomerId { get; init; } = "";
    public string? CustomerName { get; init; }
    public string? Type { get; init; }
    public string? Channel { get; init; }
    public string? Direction { get; init; }
    public string? Subject { get; init; }
    public int? DurationMin { get; init; }
    public string? RelationshipManager { get; init; }
    public string? Summary { get; init; }
    public string? ParticipantsBank { get; init; }
    public string? ParticipantsCustomer { get; init; }
    public string? TopicsDiscussed { get; init; }
    public string? Sentiment { get; init; }
    public string? Outcome { get; init; }
    public string? ActionItems { get; init; }
    public string? FollowUpRequired { get; init; }
    public string? FollowUpDate { get; init; }
    public string? DetailedNotes { get; init; }
}
