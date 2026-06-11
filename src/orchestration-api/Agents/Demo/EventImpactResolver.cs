using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic event → portfolio-entity impact resolver (002-reactive-event-cockpit,
/// FR-008, SC-005, R5/R6). Pure and side-effect free so DEMO runs stay byte-stable
/// (SC-004): given the current events and an entity (a CB customer with its sector, or a
/// TD ticker/issuer/sector), it returns the <see cref="EventLinkage"/> drivers that touch
/// that entity, each with a signed <c>contribution</c> and a human-readable rationale.
///
/// <para>Magnitude is a function of event severity; sign follows event <c>direction</c>
/// (negative news raises call urgency; positive news is a lower-magnitude engagement
/// signal). When several events hit one entity their contributions <b>net (sum)</b> and
/// <b>every</b> contributing event remains listed as a driver (R6). Output is sorted by
/// descending |contribution| then eventId for a stable, reproducible order.</para>
/// </summary>
public static class EventImpactResolver
{
    public const int SeverityHigh = 30;
    public const int SeverityMedium = 20;
    public const int SeverityLow = 10;

    /// <summary>Resolve drivers for a CB customer (matched by customerId or industry sector).</summary>
    public static IReadOnlyList<EventLinkage> ResolveForCustomer(
        string customerId,
        string? sector,
        IReadOnlyList<MarketEvent> events)
    {
        var linkages = new List<EventLinkage>();
        foreach (var e in events)
        {
            var a = e.AffectedEntities;
            var byCustomer = a.CustomerIds is { } ids && ids.Any(id => Eq(id, customerId));
            var bySector = sector is not null && a.Sectors is { } secs && secs.Any(s => Eq(s, sector));
            if (!byCustomer && !bySector) continue;

            var matchReason = byCustomer ? "names this client" : $"hits the {sector} sector";
            linkages.Add(BuildLinkage(e, customerId, matchReason));
        }
        return Order(linkages);
    }

    /// <summary>Resolve drivers for a TD entity (matched by ticker, issuer, or sector).</summary>
    public static IReadOnlyList<EventLinkage> ResolveForSecurity(
        string entityRef,
        string? ticker,
        string? issuer,
        string? sector,
        IReadOnlyList<MarketEvent> events)
    {
        var linkages = new List<EventLinkage>();
        foreach (var e in events)
        {
            var a = e.AffectedEntities;
            var byTicker = ticker is not null && a.Tickers is { } t && t.Any(x => Eq(x, ticker));
            var byIssuer = issuer is not null && a.Issuers is { } iss && iss.Any(x => Eq(x, issuer));
            var bySector = sector is not null && a.Sectors is { } secs && secs.Any(s => Eq(s, sector));
            if (!byTicker && !byIssuer && !bySector) continue;

            var matchReason = byTicker ? "names this security"
                : byIssuer ? $"hits the {issuer} issuer"
                : $"hits the {sector} sector";
            linkages.Add(BuildLinkage(e, entityRef, matchReason));
        }
        return Order(linkages);
    }

    /// <summary>
    /// Resolve drivers for a TD client, matched by the securities/issuers/sectors the client
    /// touches (holdings, trades, RFQs, inquiries). An event links to the client when it names
    /// one of those securities (tickers), issuers, or sectors. One linkage per matching event.
    /// </summary>
    public static IReadOnlyList<EventLinkage> ResolveForClient(
        string clientId,
        IReadOnlySet<string> securityIds,
        IReadOnlySet<string> issuers,
        IReadOnlySet<string> sectors,
        IReadOnlyList<MarketEvent> events)
    {
        var linkages = new List<EventLinkage>();
        foreach (var e in events)
        {
            var a = e.AffectedEntities;
            var hitSecs = a.Tickers?.Where(securityIds.Contains).ToList() ?? new List<string>();
            var hitIssuers = a.Issuers?.Where(issuers.Contains).ToList() ?? new List<string>();
            var matchedSector = a.Sectors?.FirstOrDefault(sectors.Contains);
            var directHits = hitSecs.Count + hitIssuers.Count;
            if (directHits == 0 && matchedSector is null) continue;

            // A named security or issuer in the client's book is a direct hit; a broad
            // sector-only match is a soft read-through, so it carries far less weight. Direct
            // hits also scale with concentration — a fund touching several of the named names
            // (e.g. both Quartzite and Nimbus) reacts harder than one grazing a single name.
            // This is what lets a breaking print lift the most-exposed funds above their peers.
            string matchReason;
            double weight;
            if (directHits > 0)
            {
                var named = hitSecs.Concat(hitIssuers).First();
                matchReason = directHits > 1
                    ? $"hits {directHits} names in this client's book (incl. {named})"
                    : $"hits {named}, which this client trades";
                weight = 1.0 + 0.5 * (directHits - 1);
            }
            else
            {
                matchReason = $"hits the {matchedSector} sector this client is active in";
                weight = 0.3;
            }
            linkages.Add(BuildLinkage(e, clientId, matchReason, weight));
        }
        return Order(linkages);
    }

    /// <summary>Net (sum) of the linkage contributions — the score delta applied on top of the base score (R6).</summary>
    public static int NetContribution(IEnumerable<EventLinkage> linkages)
        => (int)Math.Round(linkages.Sum(l => l.Contribution), MidpointRounding.AwayFromZero);

    private static EventLinkage BuildLinkage(MarketEvent e, string entityRef, string matchReason, double weight = 1.0)
    {
        var magnitude = SeverityMagnitude(e.Severity) * weight;
        var direction = (e.Direction ?? "neutral").ToLowerInvariant();
        var contribution = direction switch
        {
            // Negative news drives the most urgency; positive is a softer engagement nudge.
            "negative" => magnitude,
            "positive" => Math.Round(magnitude * 0.6),
            _ => Math.Round(magnitude * 0.5),
        };

        var lens = direction switch
        {
            "negative" => "downside risk to manage",
            "positive" => "an engagement opportunity",
            _ => "a development to track",
        };

        var rationale = $"{Severity(e.Severity)} {EventKind(e.Type)} event {matchReason}: \"{e.Headline}\" — {lens} (+{contribution:0.#} priority).";

        return new EventLinkage
        {
            EventId = e.Id,
            Headline = e.Headline,
            EntityRef = entityRef,
            Contribution = contribution,
            Rationale = rationale,
        };
    }

    private static IReadOnlyList<EventLinkage> Order(List<EventLinkage> linkages) => linkages
        .OrderByDescending(l => Math.Abs(l.Contribution))
        .ThenBy(l => l.EventId, StringComparer.Ordinal)
        .ToList();

    private static int SeverityMagnitude(string? severity) => (severity ?? "medium").ToLowerInvariant() switch
    {
        "high" => SeverityHigh,
        "low" => SeverityLow,
        _ => SeverityMedium,
    };

    private static string Severity(string? severity) => (severity ?? "medium").ToLowerInvariant() switch
    {
        "high" => "High-severity",
        "low" => "Low-severity",
        _ => "Medium-severity",
    };

    private static string EventKind(string? type) => (type ?? "").ToLowerInvariant() switch
    {
        "macro_rate" => "macro/rate",
        "sector" => "sector",
        "issuer_credit" => "issuer-credit",
        "client_headline" => "client-headline",
        _ => "market",
    };

    private static bool Eq(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
