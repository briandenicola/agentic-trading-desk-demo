using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrchestrationApi.Models;

/// <summary>
/// Response DTO for <c>POST /api/agent/td-new-issue</c> — the Institutional Sales &amp; Trading
/// "New Issue Radar" storyboard. A coverage salesperson is walked through a guided narrative:
/// a primary issuer announces a concurrent debt + equity issue, the desk spots that an existing
/// client both <b>holds the equity</b> and has been <b>actively trading the new debt</b> (electronic
/// RFQs + calls), and the desk is prompted to call that client now with a concrete allocation.
///
/// The shape is identical in DEMO and LIVE modes (constitution Principle III) and is grounded
/// entirely in the fictional trading-desk dataset (<c>/mock/td/*</c>). Each <see cref="TdStoryboardStep"/>
/// is a beat the UI reveals one at a time. Serialize with <see cref="TdNewIssueJson.Options"/>.
/// </summary>
public sealed record TdNewIssueStoryboard
{
    public required string Mode { get; init; }                       // "DEMO" | "LIVE"
    public required string AsOf { get; init; }                       // ISO-8601 date
    public required string Title { get; init; }                      // "New Issue Radar"
    public required string Subtitle { get; init; }
    public required NewIssueIssuer Issuer { get; init; }
    public required IReadOnlyList<TdStoryboardStep> Steps { get; init; }
    public required TdOutreachRecommendation Outreach { get; init; }
    /// <summary>
    /// The "lead-left board": every primary new-issue deal the desk is tracking (seeded plus any
    /// uploaded via the spreadsheet path), with OUR syndicate role on each. Lets the cockpit show
    /// which deals we run the books on, independent of the issuer currently in focus. Null/empty
    /// when no deals are loaded.
    /// </summary>
    public IReadOnlyList<LeadLeftDeal>? LeadLeftBoard { get; init; }
    /// <summary>
    /// Injected/live market events (002 US2) that touch this issuer, its tranches, sector, or the
    /// focus client — folded in by the reactive SSE path so the storyboard reacts to the News Desk
    /// the same way the briefings do. Null/empty when nothing live is in view.
    /// </summary>
    public IReadOnlyList<MarketEvent>? LiveEvents { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }
}

/// <summary>The primary issuer and the tranches of the new issue (equity + new debt).</summary>
public sealed record NewIssueIssuer
{
    public required string Name { get; init; }                       // "Prairie Green Renewables"
    public string? Sector { get; init; }
    public required string Headline { get; init; }                   // announcement headline
    public string? Summary { get; init; }
    public string? AnnouncedAt { get; init; }                        // timestamp of the announcement
    public required IReadOnlyList<NewIssueTranche> Tranches { get; init; }

    /// <summary>True when OUR desk is the lead-left bookrunner on this deal.</summary>
    public bool? LeadLeft { get; init; }
    /// <summary>OUR syndicate role, e.g. "Lead-Left Bookrunner", "Joint Bookrunner", "Co-Manager".</summary>
    public string? SyndicateRole { get; init; }
    public string? BookStatus { get; init; }                         // "Books open" | "Priced" | …
    public string? PricingDate { get; init; }                        // expected pricing date
    /// <summary>Share of allocation our desk controls as lead-left (0..1 fraction).</summary>
    public double? OurAllocationControlPct { get; init; }
    public IReadOnlyList<string>? CoManagers { get; init; }
}

/// <summary>One leg of the new issue (the primary equity or the new senior note).</summary>
public sealed record NewIssueTranche
{
    public required string SecurityId { get; init; }
    public required string SecurityName { get; init; }
    public required string AssetClass { get; init; }                 // "Equity" | "Corporate Bond"
    public string? Detail { get; init; }                             // "6.00% 2034 · BBB-" / "Primary equity"
    public double? ReferencePrice { get; init; }
    /// <summary>True when this tranche is part of a deal we run lead-left.</summary>
    public bool? LeadLeft { get; init; }
}

/// <summary>
/// A primary new-issue deal the desk is tracking, with OUR syndicate role. Seeded from the mock
/// systems-of-record and augmentable via the spreadsheet-upload path. Backs the lead-left board.
/// </summary>
public sealed record LeadLeftDeal
{
    public string? DealId { get; init; }                             // stable id (e.g. NI-3601 / NI-up001)
    public required string Issuer { get; init; }
    public string? Sector { get; init; }
    public string? Role { get; init; }                               // our syndicate role
    public bool LeadLeft { get; init; }
    public string? BookStatus { get; init; }
    public string? PricingDate { get; init; }
    public double? AllocationControlPct { get; init; }
    public IReadOnlyList<string>? TrancheSecurityIds { get; init; }
    public string? Source { get; init; }                             // "seed" | "upload"
}

/// <summary>A single beat of the storyboard, revealed one step at a time in the UI.</summary>
public sealed record TdStoryboardStep
{
    public required string Id { get; init; }                         // announcement|holdings|activity|outreach
    public required int Order { get; init; }
    public required string Beat { get; init; }                       // short label ("Holdings cross-reference")
    public required string Title { get; init; }
    public required string Narration { get; init; }                  // plain-language narration for this beat
    public IReadOnlyList<StoryboardMetric> Metrics { get; init; } = [];
    public IReadOnlyList<StoryboardEvidence> Evidence { get; init; } = [];
}

/// <summary>A headline figure rendered as a chip/stat on a step.</summary>
public sealed record StoryboardMetric
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public string? Sub { get; init; }
    public string? Tone { get; init; }                               // neutral|positive|warning|accent
    public bool? Live { get; init; }                                 // true when injected by a live event
}

/// <summary>A supporting record from a system-of-record (the "receipts" behind a beat).</summary>
public sealed record StoryboardEvidence
{
    public required string Kind { get; init; }                       // news|holding|rfq|trade|crm|inquiry|axe|syndicate
    public required string Label { get; init; }
    public string? Detail { get; init; }
    public string? RefId { get; init; }                              // source record id (RFQ-XXXX, TRD-XXXX, ...)
    public string? Date { get; init; }
    public string? SecurityId { get; init; }
    public bool? Live { get; init; }                                 // true when this is an injected live event
}

/// <summary>The concluding "call them now" recommendation with talking points and a trade idea.</summary>
public sealed record TdOutreachRecommendation
{
    public required string ClientId { get; init; }
    public required string ClientName { get; init; }
    public string? ClientType { get; init; }
    public required string Headline { get; init; }                   // "Call Crestline Capital now"
    public required IReadOnlyList<string> TalkingPoints { get; init; }
    public TradeIdea? TradeIdea { get; init; }                       // reuses TdBriefing.TradeIdea
    public required string SuggestedAction { get; init; }
    public string? DraftMessage { get; init; }                       // ready-to-send note
}

/// <summary>Serialization options for the New Issue storyboard (camelCase, omit null).</summary>
public static class TdNewIssueJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
