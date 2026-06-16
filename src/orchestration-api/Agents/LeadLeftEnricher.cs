using System.Globalization;
using System.Text.Json.Nodes;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents;

/// <summary>
/// Folds the desk's "lead-left" syndicate context into a <see cref="TdNewIssueStoryboard"/>.
/// Reads the new-issue deals — seeded plus any uploaded via the spreadsheet path — from the mock
/// systems-of-record over HTTP only (constitution Principle II), then, for the issuer currently in
/// focus, flags whether OUR desk runs the books on the left and surfaces the role, book status,
/// pricing date, allocation control and co-managers. Applied identically in DEMO and LIVE so both
/// modes return the same enriched shape (Principle III). Enrichment is deterministic and idempotent
/// (it recomputes from the store), so it is safe to run on either the composer or the LIVE agent
/// output. On any upstream gap it returns the storyboard unchanged.
/// </summary>
public sealed class LeadLeftEnricher(MockApiClient mockApi)
{
    public async Task<TdNewIssueStoryboard> EnrichAsync(TdNewIssueStoryboard story, CancellationToken ct = default)
    {
        List<JsonNode> deals;
        try
        {
            var node = await mockApi.GetJsonAsync("/mock/td/new-issues", ct);
            deals = (node as JsonArray)?.Where(n => n is not null).Select(n => n!).ToList() ?? [];
        }
        catch
        {
            return story; // store unavailable — leave the storyboard as-is.
        }

        if (deals.Count == 0) return story;

        var board = deals.Select(ToDeal).ToList();

        // Match the deal for the issuer in view, by issuer name or by a shared tranche security id.
        var issuerName = story.Issuer.Name;
        var trancheIds = story.Issuer.Tranches.Select(t => t.SecurityId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var match = deals.FirstOrDefault(d => SameIssuer(Str(d, "issuer"), issuerName) || TouchesTranche(d, trancheIds));

        if (match is null)
        {
            return story with { LeadLeftBoard = board };
        }

        var role = Str(match, "ourRole");
        var leadLeft = Bool(match, "leadLeft") ?? false;
        var bookStatus = Str(match, "bookStatus");
        var pricingDate = Str(match, "pricingDate");
        var allocPct = Dbl(match, "ourAllocationControlPct");
        var coManagers = StrList(match, "coManagers");
        var dealTrancheIds = StrList(match, "trancheSecurityIds")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ---- Issuer-level lead-left context + per-tranche flags --------------------------------
        var issuer = story.Issuer with
        {
            LeadLeft = leadLeft,
            SyndicateRole = role,
            BookStatus = bookStatus,
            PricingDate = pricingDate,
            OurAllocationControlPct = allocPct,
            CoManagers = coManagers.Count > 0 ? coManagers : null,
            Tranches = story.Issuer.Tranches
                .Select(t => t with { LeadLeft = leadLeft && (dealTrancheIds.Count == 0 || dealTrancheIds.Contains(t.SecurityId)) })
                .ToList(),
        };

        // ---- Announcement beat: add a syndicate metric + evidence row --------------------------
        var allocText = allocPct is double p ? $"{p:0%}" : null;
        var steps = story.Steps.Select(s =>
        {
            if (!string.Equals(s.Id, "announcement", StringComparison.OrdinalIgnoreCase) || role is null)
                return s;

            var metrics = new List<StoryboardMetric>
            {
                new() { Label = "Our book", Value = role, Tone = leadLeft ? "positive" : "accent" },
            };
            if (allocText is not null && leadLeft)
                metrics.Add(new StoryboardMetric { Label = "Allocation control", Value = allocText, Tone = "positive" });
            metrics.AddRange(s.Metrics);

            var evidence = new List<StoryboardEvidence>(s.Evidence)
            {
                new()
                {
                    Kind = "syndicate",
                    Label = $"Our syndicate role · {role}",
                    Detail = SyndicateDetail(bookStatus, pricingDate, allocText, coManagers),
                    RefId = Str(match, "dealId"),
                    Date = pricingDate,
                },
            };

            return s with { Metrics = metrics, Evidence = evidence };
        }).ToList();

        // ---- Outreach: flag the trade idea as lead-left paper + lead with the allocation -------
        var outreach = story.Outreach;
        if (leadLeft)
        {
            var tradeIdea = outreach.TradeIdea is { } ti
                ? ti with { LeadLeft = dealTrancheIds.Count == 0 || dealTrancheIds.Contains(ti.SecurityId) }
                : outreach.TradeIdea;

            var lead = allocText is not null
                ? $"We're {role?.ToLowerInvariant() ?? "lead-left"} on this deal and control ~{allocText} of allocation — I can lock in priority paper for you before books close{(pricingDate is not null ? $" ({pricingDate})" : "")}."
                : $"We're {role?.ToLowerInvariant() ?? "lead-left"} on this deal — I can lock in a priority allocation for you on the new issue.";

            var talkingPoints = new List<string> { lead };
            talkingPoints.AddRange(outreach.TalkingPoints);

            outreach = outreach with { TradeIdea = tradeIdea, TalkingPoints = talkingPoints };
        }

        return story with
        {
            Issuer = issuer,
            Steps = steps,
            Outreach = outreach,
            LeadLeftBoard = board,
        };
    }

    // ---------------------------------------------------------------- helpers

    private static LeadLeftDeal ToDeal(JsonNode d) => new()
    {
        Issuer = Str(d, "issuer") ?? "Unknown issuer",
        Sector = Str(d, "sector"),
        Role = Str(d, "ourRole"),
        LeadLeft = Bool(d, "leadLeft") ?? false,
        BookStatus = Str(d, "bookStatus"),
        PricingDate = Str(d, "pricingDate"),
        AllocationControlPct = Dbl(d, "ourAllocationControlPct"),
        TrancheSecurityIds = StrList(d, "trancheSecurityIds") is { Count: > 0 } ids ? ids : null,
        Source = Str(d, "source"),
    };

    private static string SyndicateDetail(string? bookStatus, string? pricingDate, string? allocText, IReadOnlyList<string> coManagers)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(bookStatus)) parts.Add(bookStatus!);
        if (!string.IsNullOrWhiteSpace(pricingDate)) parts.Add($"prices {pricingDate}");
        if (allocText is not null) parts.Add($"{allocText} allocation control");
        if (coManagers.Count > 0) parts.Add($"co-managers: {string.Join(", ", coManagers)}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Our desk runs the books.";
    }

    private static bool SameIssuer(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        return a.Contains(b, StringComparison.OrdinalIgnoreCase)
            || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TouchesTranche(JsonNode deal, IReadOnlySet<string> trancheIds) =>
        trancheIds.Count > 0 && StrList(deal, "trancheSecurityIds").Any(trancheIds.Contains);

    private static string? Str(JsonNode? node, string key)
    {
        try { return node?[key]?.GetValue<string>(); }
        catch { return node?[key]?.ToString(); }
    }

    private static bool? Bool(JsonNode? node, string key)
    {
        try { return node?[key]?.GetValue<bool>(); }
        catch { return bool.TryParse(node?[key]?.ToString(), out var b) ? b : null; }
    }

    private static double? Dbl(JsonNode? node, string key)
    {
        try { return node?[key]?.GetValue<double>(); }
        catch
        {
            return double.TryParse(node?[key]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
    }

    private static List<string> StrList(JsonNode? node, string key)
    {
        if (node?[key] is not JsonArray arr) return [];
        var list = new List<string>();
        foreach (var item in arr)
        {
            var s = item?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(s)) list.Add(s!);
        }
        return list;
    }
}
