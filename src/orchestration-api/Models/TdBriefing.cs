namespace OrchestrationApi.Models;

/// <summary>
/// Response DTOs for <c>POST /api/agent/td-briefing</c> — the Institutional Sales &amp;
/// Trading "Morning Planning &amp; Prioritized Outreach" scene for a coverage salesperson.
/// The shape is identical in DEMO and LIVE modes (constitution Principle III) and is
/// grounded entirely in the fictional trading-desk dataset (<c>/mock/td/*</c>). Serialize
/// with <see cref="TdBriefingJson.Options"/> (camelCase, omit null <c>notes</c>).
/// </summary>
public sealed record TdBriefing
{
    public required string Mode { get; init; }                   // "DEMO" | "LIVE"
    public required string AsOf { get; init; }                   // ISO-8601 date
    public required string Greeting { get; init; }               // "Good morning, Theo"
    public required SalespersonIdentity Salesperson { get; init; }
    public required IReadOnlyList<MarketStripItem> MarketStrip { get; init; }
    public required IReadOnlyList<MacroThemeBullet> MacroThemes { get; init; }
    public required IReadOnlyList<ReasoningStep> Reasoning { get; init; }
    public required IReadOnlyList<TdPriorityCall> PriorityCallList { get; init; }
    public required IReadOnlyList<InventoryAxe> InventoryAxes { get; init; }
    public required string SuggestedFirstAction { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }

    /// <summary>
    /// True when this briefing was produced by the deterministic safety net instead of the LIVE
    /// Foundry agent (agent unavailable, run failed, or returned no parseable call list). The JSON
    /// shape is unchanged (Principle III) — this flag lets the cockpit show an honest "degraded"
    /// banner rather than silently passing the fallback off as a normal LIVE result. Always false
    /// for a genuine DEMO/LIVE response.
    /// </summary>
    public bool Degraded { get; init; }

    /// <summary>Human-readable reason the briefing degraded (mirrors the leading <c>notes</c> entry). Null unless <see cref="Degraded"/>.</summary>
    public string? DegradedReason { get; init; }

    /// <summary>All news/research the synthesis weighed (overnight + intraday). Empty ⇒ none processed.</summary>
    public IReadOnlyList<TdEventConsidered> EventsConsidered { get; init; } = [];

    /// <summary>Live market/admin events (overnight + intraday) reflected in this synthesis (reactive re-rank).</summary>
    public IReadOnlyList<MarketEvent> LiveEvents { get; init; } = [];
}

public sealed record SalespersonIdentity
{
    public required string SalespersonId { get; init; }          // synthetic id, e.g. "SP-Theo"
    public required string Name { get; init; }                   // "Theo Wexler"
    public string? Desk { get; init; }                           // "Institutional Sales"
    public string? Coverage { get; init; }                       // "Hedge Fund book"
    public required int ClientCount { get; init; }
}

/// <summary>One quote/level on the morning market strip (reuses <c>MarketStripItem</c> from MorningBrief).</summary>
public sealed record MacroThemeBullet
{
    public required string Theme { get; init; }                  // "AI / Datacenter Boom"
    public required string Detail { get; init; }                 // condensed narrative
}

/// <summary>A news item or research note weighed by the synthesis (deduped overnight + intraday).</summary>
public sealed record TdEventConsidered
{
    public required string Id { get; init; }                     // NEWS-1002 | RES-2002
    public required string Kind { get; init; }                   // news|research
    public required string Headline { get; init; }
    public string? Summary { get; init; }
    public string? Sector { get; init; }
    public string? Sentiment { get; init; }                      // Positive|Negative|Mixed|Neutral
    public string? RelatedSecurityId { get; init; }
    public string? MacroTheme { get; init; }
    public string? Timestamp { get; init; }
}

/// <summary>A dealer axe / inventory line the desk wants to work, matched to client demand.</summary>
public sealed record InventoryAxe
{
    public required string SecurityId { get; init; }
    public required string SecurityName { get; init; }
    public string? AssetClass { get; init; }
    public string? Sector { get; init; }
    public required long InventorySize { get; init; }            // +long / -short
    public required string AxeSide { get; init; }                // buy|sell|two-way (the side we want to do)
    public double? BidPrice { get; init; }
    public double? OfferPrice { get; init; }
    public string? Desk { get; init; }
    /// <summary>Clients in this salesperson's book who could absorb / supply the axe.</summary>
    public IReadOnlyList<string> MatchedClients { get; init; } = [];
}

/// <summary>One ranked client in the prioritized outreach plan.</summary>
public sealed record TdPriorityCall
{
    public required int Rank { get; init; }
    public required int Priority { get; init; }                  // 1..4 UI band (red→green)
    public required string ClientId { get; init; }
    public required string ClientName { get; init; }
    public string? ClientType { get; init; }                     // Hedge Fund | Asset Manager | ...
    public string? Region { get; init; }
    public string? PreferredAssetClass { get; init; }
    public required int Score { get; init; }
    public required TdCallRationale Rationale { get; init; }
    public required IReadOnlyList<WhyNowDriver> WhyNow { get; init; }
    public required IReadOnlyList<string> TalkingPoints { get; init; }
    public required IReadOnlyList<TradeIdea> TradeIdeas { get; init; }
    public string? PersonalNote { get; init; }
    public required string SuggestedAction { get; init; }

    /// <summary>Per-call linkage; names the live event(s) that changed this call's rank/priority. Empty if un-affected.</summary>
    public IReadOnlyList<EventLinkage> DrivingEvents { get; init; } = [];
}

/// <summary>Component scores (0..100) and a plain-language explanation behind a client's composite rank.</summary>
public sealed record TdCallRationale
{
    public required int NewsRelevance { get; init; }
    public required int OpenRfqWeight { get; init; }
    public required int InquiryWeight { get; init; }
    public required int InventoryAxeMatch { get; init; }
    public required int Urgency { get; init; }
    public required int CompositeScore { get; init; }
    public required string Explanation { get; init; }
}

/// <summary>A concrete reason to call this client now, traced to a system-of-record record.</summary>
public sealed record WhyNowDriver
{
    public required string Kind { get; init; }                   // news|research|rfq|inquiry|holding|axe|crm
    public required string Label { get; init; }                  // short headline
    public string? Detail { get; init; }                         // supporting text
    public string? SecurityId { get; init; }
    public string? RefId { get; init; }                          // source record id (RFQ-XXXX, INQ-XXXX, ...)
}

/// <summary>A specific axe / trade idea to put in front of the client.</summary>
public sealed record TradeIdea
{
    public required string SecurityId { get; init; }
    public required string SecurityName { get; init; }
    public required string Side { get; init; }                   // client-side: Buy|Sell
    public string? Rationale { get; init; }
    public string? Level { get; init; }                          // indicative level / size
}
