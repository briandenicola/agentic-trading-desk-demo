using System.Text.Json.Nodes;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic US2 outreach ranker. Given fetched mock-api data from the
/// composer, computes the documented wallet / engagement / event-relevance
/// blend and produces ordered, axes/holdings-aware <see cref="OutreachItem"/>s.
/// </summary>
public static class OutreachRanker
{
    public const double WalletWeight = 0.40;
    public const double EngagementWeight = 0.30;
    public const double EventRelevanceWeight = 0.30;

    public static IReadOnlyList<OutreachItem> Rank(IReadOnlyList<RankingInput> affected, JsonArray axes)
    {
        if (affected.Count == 0)
        {
            return [];
        }

        var maxRevenue = affected.Max(c => c.RevenueYtd);
        var engagementTotals = affected.ToDictionary(c => c.Cid, c => Last30dTouches(c.Engagement), StringComparer.Ordinal);
        var maxEngagement = Math.Max(1, engagementTotals.Values.DefaultIfEmpty(0).Max());

        var scored = affected.Select(x =>
        {
            var wallet = Round(maxRevenue <= 0 ? 0 : x.RevenueYtd / maxRevenue);
            var engagement = Round((double)engagementTotals[x.Cid] / maxEngagement);
            var eventRelevance = Round(RateSensitivity(x.ExposureType));
            var composite = CompositeScore(wallet, engagement, eventRelevance);
            return new
            {
                x.Cid,
                x.Name,
                x.RevenueYtd,
                x.ExposureType,
                x.Cusip,
                Wallet = wallet,
                Engagement = engagement,
                EventRelevance = eventRelevance,
                Composite = composite,
            };
        })
        .OrderByDescending(s => s.Composite)
        .ThenByDescending(s => s.RevenueYtd)
        .ThenBy(s => s.Cid, StringComparer.Ordinal)
        .ToList();

        var outreach = new List<OutreachItem>(scored.Count);
        for (var i = 0; i < scored.Count; i++)
        {
            var s = scored[i];
            var exposure = new ExposureRow(s.ExposureType, s.Cusip);
            outreach.Add(new OutreachItem
            {
                Rank = i + 1,
                Cid = s.Cid,
                Name = s.Name,
                SuggestedTopic = SuggestedTopic(s.ExposureType),
                TalkingPoints = TalkingPoints(s.Name, exposure, axes),
                Rationale = new RankingRationale
                {
                    WalletScore = s.Wallet,
                    EngagementScore = s.Engagement,
                    EventRelevanceScore = s.EventRelevance,
                    CompositeScore = s.Composite,
                    Explanation = Explanation(i + 1, s.Wallet, s.Engagement, s.EventRelevance, s.Composite, s.ExposureType),
                },
            });
        }

        return outreach;
    }

    public static double CompositeScore(double wallet, double engagement, double eventRelevance) =>
        Round(WalletWeight * wallet + EngagementWeight * engagement + EventRelevanceWeight * eventRelevance);

    private static double RateSensitivity(string exposureType) => exposureType switch
    {
        "long-duration" => 1.00,
        "swap-book" => 0.80,
        "floating-rate" => 0.60,
        _ => 0.50,
    };

    private static int Last30dTouches(JsonNode? engagement)
    {
        var d = engagement?["last30d"];
        if (d is null) return 0;
        return (d["meetings"]?.GetValue<int>() ?? 0)
             + (d["calls"]?.GetValue<int>() ?? 0)
             + (d["emails"]?.GetValue<int>() ?? 0);
    }

    private static string SuggestedTopic(string exposureType) => exposureType switch
    {
        "long-duration" => "Discuss hedging; mention new 10Y swap axes available.",
        "swap-book" => "Review swap hedges vs higher terminal rate.",
        "floating-rate" => "Reinvestment ideas at higher front-end yields.",
        _ => "Review portfolio positioning vs the rate move.",
    };

    private static List<string> TalkingPoints(string name, ExposureRow exposure, JsonArray axes)
    {
        switch (exposure.ExposureType)
        {
            case "long-duration":
                var hedgeAxis = FindAxis(axes, "duration") ?? FindAxis(axes, "hedge");
                return
                [
                    $"The surprise Fed hike repriced the front end hardest — {name}'s long-duration positions ({exposure.Cusip}) face the most price pressure.",
                    hedgeAxis is { } ha
                        ? $"We're showing a {ha.Instrument} axis ({ha.Side}, ${ha.SizeMm:0}mm) that can hedge the duration risk."
                        : "We can offer a 10Y UST swap to hedge the duration risk.",
                ];
            case "swap-book":
                return
                [
                    $"With the Fed signaling a higher terminal rate, {name}'s interest-rate swap book ({exposure.Cusip}) sensitivity is worth reviewing.",
                    "Relative value favors receiving the belly on the flattening — relevant to the existing swap positions.",
                ];
            case "floating-rate":
                var frontAxis = FindAxis(axes, "front-end");
                return
                [
                    $"Floating-rate coupons reset higher after the hike — {name}'s {exposure.Cusip} position benefits at the next reset.",
                    frontAxis is { } fa
                        ? $"Higher front-end yields open reinvestment ideas; the {fa.Instrument} is {fa.Side} here."
                        : "Higher front-end yields open reinvestment ideas at the short end.",
                ];
            default:
                return [$"Review {name}'s positioning against the overnight rate move."];
        }
    }

    private static string Explanation(int rank, double wallet, double engagement, double eventRelevance, double composite, string exposureType)
    {
        var driver = exposureType switch
        {
            "long-duration" => "direct long-duration price exposure",
            "swap-book" => "swap-book sensitivity to the higher terminal rate",
            "floating-rate" => "floating-rate reset / reinvestment angle",
            _ => "rate-sensitive exposure",
        };
        return $"Ranked #{rank}: composite {WalletWeight:0.00}·wallet({wallet:0.00}) + {EngagementWeight:0.00}·engagement({engagement:0.00}) + {EventRelevanceWeight:0.00}·event({eventRelevance:0.00}) = {composite:0.00}, driven by {driver}.";
    }

    private static AxisRow? FindAxis(JsonArray axes, string tag)
    {
        foreach (var a in axes)
        {
            if (a is null) continue;
            var tags = a["relevanceTags"] as JsonArray;
            if (tags is null) continue;
            if (tags.Any(t => string.Equals(t?.GetValue<string>(), tag, StringComparison.OrdinalIgnoreCase)))
            {
                return new AxisRow(
                    a["instrument"]?.GetValue<string>() ?? "axis",
                    a["side"]?.GetValue<string>() ?? "",
                    (a["size"]?.GetValue<long>() ?? 0) / 1_000_000.0);
            }
        }
        return null;
    }

    private static double Round(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    public readonly record struct RankingInput(string Cid, string Name, double RevenueYtd, string ExposureType, string Cusip, JsonNode? Engagement);
    private readonly record struct ExposureRow(string ExposureType, string Cusip);
    private readonly record struct AxisRow(string Instrument, string Side, double SizeMm);
}
