namespace MockApi.Models;

/// <summary>
/// Strongly-typed records for the Trading Desk / Capital Markets dataset
/// (<c>trading_desk_dataset.xlsx</c> → <c>Data/td_*.json</c>). All data is fictional.
/// This real dataset replaces the synthetic municipal fixtures in the existing cockpit.
/// JSON keys are camelCase (see <see cref="CbJson.Options"/>, reused by the store).
/// </summary>
public sealed record TdClient
{
    public string ClientId { get; init; } = "";
    public string? ClientName { get; init; }
    public string? ClientType { get; init; }
    public string? ClientRegion { get; init; }
    public string? CoverageSalesperson { get; init; }
    public string? PreferredAssetClass { get; init; }
    public string? RiskStyle { get; init; }
}

public sealed record TdSecurity
{
    public string SecurityId { get; init; } = "";
    public string? SecurityName { get; init; }
    public string? AssetClass { get; init; }
    public string? Sector { get; init; }
    public string? Issuer { get; init; }
    public string? Currency { get; init; }
    public string? Region { get; init; }
    public string? Rating { get; init; }
    public string? MaturityDate { get; init; }
    public double? ReferencePrice { get; init; }
}

public sealed record TdTrade
{
    public string TradeId { get; init; } = "";
    public string? TradeDate { get; init; }
    public string ClientId { get; init; } = "";
    public string SecurityId { get; init; } = "";
    public string? Direction { get; init; }
    public long? Quantity { get; init; }
    public double? Price { get; init; }
    public double? Notional { get; init; }
    public string? TraderName { get; init; }
    public string? SalespersonName { get; init; }
    public string? ExecutionChannel { get; init; }
}

public sealed record TdMarketPrint
{
    public string PrintId { get; init; } = "";
    public string? Timestamp { get; init; }
    public string SecurityId { get; init; } = "";
    public string? AssetClass { get; init; }
    public string? MarketSide { get; init; }
    public double? PrintPrice { get; init; }
    public long? PrintSize { get; init; }
    public string? Venue { get; init; }
}

public sealed record TdRfq
{
    public string RfqId { get; init; } = "";
    public string? RfqDate { get; init; }
    public string ClientId { get; init; } = "";
    public string SecurityId { get; init; } = "";
    public string? Direction { get; init; }
    public long? Quantity { get; init; }
    public string? ResponseStatus { get; init; }
    public string? SalespersonName { get; init; }
    public string? TraderName { get; init; }
}

public sealed record TdCrm
{
    public string CrmId { get; init; } = "";
    public string? EntryDate { get; init; }
    public string ClientId { get; init; } = "";
    public string? CoverageSalesperson { get; init; }
    public string? MeetingType { get; init; }
    public string? Topic { get; init; }
    public string? CallReportText { get; init; }
    public string? ClientInterest { get; init; }
    public string? FollowUpAction { get; init; }
    public string? Urgency { get; init; }
}

public sealed record TdHolding
{
    public string HoldingId { get; init; } = "";
    public string? AsOfDate { get; init; }
    public string ClientId { get; init; } = "";
    public string SecurityId { get; init; } = "";
    public long? Quantity { get; init; }
    public double? MarketValue { get; init; }
    public double? WeightPct { get; init; }
}

public sealed record TdInventory
{
    public string InventoryId { get; init; } = "";
    public string? AsOfDate { get; init; }
    public string SecurityId { get; init; } = "";
    public long? InventorySize { get; init; }
    public double? BidPrice { get; init; }
    public double? OfferPrice { get; init; }
    public string? TraderName { get; init; }
    public string? Desk { get; init; }
}

public sealed record TdInquiry
{
    public string InquiryId { get; init; } = "";
    public string? InquiryDate { get; init; }
    public string ClientId { get; init; } = "";
    public string? Channel { get; init; }
    public string? RawInquiryText { get; init; }
    public string? InferredSecurity { get; init; }
    public string? InferredDirection { get; init; }
    public string? InferredSize { get; init; }
    public string? Sentiment { get; init; }
}

public sealed record TdNews
{
    public string NewsId { get; init; } = "";
    public string? PublishTimestamp { get; init; }
    public string? Headline { get; init; }
    public string? Summary { get; init; }
    public string? RelatedSecurityId { get; init; }
    public string? RelatedSector { get; init; }
    public string? Sentiment { get; init; }
    public string? MacroTheme { get; init; }
}

public sealed record TdResearch
{
    public string ResearchId { get; init; } = "";
    public string? PublishDate { get; init; }
    public string? Title { get; init; }
    public string? AnalystName { get; init; }
    public string? Sector { get; init; }
    public string? RatingAction { get; init; }
    public string? TargetOrSpreadView { get; init; }
    public string? ShortSummary { get; init; }
    public string? RelatedSecurityId { get; init; }
}

public sealed record TdNarrativeTheme
{
    public string? Theme { get; init; }
    public string? Narrative { get; init; }
}
