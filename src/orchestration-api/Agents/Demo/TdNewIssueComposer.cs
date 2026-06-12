using System.Globalization;
using System.Text.Json.Nodes;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic, offline DEMO composer for the Institutional Sales &amp; Trading
/// "New Issue Radar" storyboard (<c>POST /api/agent/td-new-issue</c>). Given an issuer's
/// equity security and a focus client, it pulls — <b>over HTTP only</b> (constitution
/// Principle II / FR-002, never reading fixtures in-process) — the security interest
/// (holders, trades, RFQs, news, inventory), the issuer's debt tranche, and the client's
/// activity, then assembles a four-beat <see cref="TdNewIssueStoryboard"/>:
/// announcement → equity holdings cross-reference → recent new-debt RFQ/trade/call activity →
/// a concrete outreach recommendation. All data is fictional.
///
/// Output is deterministic (stable ordering, figures derived from the data) so repeated DEMO
/// runs are byte-identical. No model and no credentials are used. On an upstream gap it
/// degrades to a structured storyboard carrying <c>notes</c> (FR-011).
/// </summary>
public sealed class TdNewIssueComposer(MockApiClient mockApi)
{
    public const string DefaultIssuerSecurityId = "SEC-3601";   // Prairie Green Renewables (equity)
    public const string DefaultClientId = "CL-2015";            // Crestline Capital
    public const string DefaultDate = "2026-05-22";
    private const int WindowDays = 60;

    public async Task<TdNewIssueStoryboard> ComposeAsync(
        string? issuerSecurityId, string? clientId, string? date, CancellationToken ct = default)
    {
        var notes = new List<string>();
        var equityId = string.IsNullOrWhiteSpace(issuerSecurityId) ? DefaultIssuerSecurityId : issuerSecurityId;
        var focusClientId = string.IsNullOrWhiteSpace(clientId) ? DefaultClientId : clientId;
        var asOf = ParseDate(date) ?? DateOnly.Parse(DefaultDate, CultureInfo.InvariantCulture);
        var since = asOf.AddDays(-WindowDays);

        var equityInterest = await GetJsonAsync($"/mock/td/securities/{Encode(equityId)}/interest?since={since:yyyy-MM-dd}", notes, ct);
        var equity = equityInterest?["security"];
        if (equity is null)
        {
            return BuildDegraded(equityId, focusClientId, asOf, notes);
        }

        var issuerName = Str(equity, "issuer") ?? Str(equity, "securityName") ?? "the issuer";
        var sector = Str(equity, "sector");

        // Resolve the issuer's debt tranche (the new senior note).
        var issuerSecurities = await GetArrayAsync($"/mock/td/securities?issuer={Encode(issuerName)}", notes, ct);
        var debt = issuerSecurities.FirstOrDefault(s => string.Equals(Str(s, "assetClass"), "Corporate Bond", StringComparison.OrdinalIgnoreCase));
        var debtId = Str(debt, "securityId");
        var debtInterest = debtId is null ? null : await GetJsonAsync($"/mock/td/securities/{Encode(debtId)}/interest?since={since:yyyy-MM-dd}", notes, ct);

        // Focus client master + 360° activity.
        var client = await GetJsonAsync($"/mock/td/clients/{Encode(focusClientId)}", notes, ct);
        var clientName = Str(client, "clientName") ?? focusClientId;
        var clientType = Str(client, "clientType");
        var activity = await GetJsonAsync($"/mock/td/clients/{Encode(focusClientId)}/activity?since={since:yyyy-MM-dd}", notes, ct);

        // ---- Step 1: the announcement (latest news on the equity) --------------
        var newsItems = Arr(equityInterest, "news").Where(n => n is not null).Select(n => n!).ToList();
        var announcement = newsItems
            .OrderByDescending(n => Str(n, "publishTimestamp"), StringComparer.Ordinal)
            .FirstOrDefault();

        var tranches = new List<NewIssueTranche>
        {
            new()
            {
                SecurityId = equityId,
                SecurityName = Str(equity, "securityName") ?? equityId,
                AssetClass = Str(equity, "assetClass") ?? "Equity",
                Detail = "Primary equity",
                ReferencePrice = Dbl(equity, "referencePrice"),
            },
        };
        if (debt is not null && debtId is not null)
        {
            tranches.Add(new NewIssueTranche
            {
                SecurityId = debtId,
                SecurityName = Str(debt, "securityName") ?? debtId,
                AssetClass = Str(debt, "assetClass") ?? "Corporate Bond",
                Detail = TrancheDetail(debt),
                ReferencePrice = Dbl(debt, "referencePrice"),
            });
        }

        var issuer = new NewIssueIssuer
        {
            Name = issuerName,
            Sector = sector,
            Headline = Str(announcement, "headline") ?? $"{issuerName} announces a new issue",
            Summary = Str(announcement, "summary"),
            AnnouncedAt = Str(announcement, "publishTimestamp"),
            Tranches = tranches,
        };

        var announcementStep = new TdStoryboardStep
        {
            Id = "announcement",
            Order = 1,
            Beat = "New issue announced",
            Title = $"{issuerName} is bringing debt and equity",
            Narration =
                $"Overnight, {issuerName} announced a concurrent new issue — a primary equity offering and a new senior note. " +
                "That is the trigger: a name where we may already have a client with skin in the game.",
            Metrics =
            [
                new StoryboardMetric { Label = "Issue type", Value = "Debt + Equity", Tone = "accent" },
                new StoryboardMetric { Label = "Tranches", Value = tranches.Count.ToString(CultureInfo.InvariantCulture) },
                new StoryboardMetric { Label = "Sector", Value = sector ?? "—" },
            ],
            Evidence = BuildAnnouncementEvidence(announcement, tranches),
        };

        // ---- Step 2: equity holdings cross-reference --------------------------
        var holders = Arr(equityInterest, "holders").Where(h => h is not null).Select(h => h!).ToList();
        var holding = holders.FirstOrDefault(h => string.Equals(Str(h, "clientId"), focusClientId, StringComparison.OrdinalIgnoreCase))
                      ?? holders.OrderByDescending(h => Dbl(h, "marketValue") ?? 0).FirstOrDefault();
        if (holding is not null && !string.Equals(Str(holding, "clientId"), focusClientId, StringComparison.OrdinalIgnoreCase))
        {
            // Fall back to the largest holder if the requested client doesn't hold it.
            focusClientId = Str(holding, "clientId") ?? focusClientId;
            client = await GetJsonAsync($"/mock/td/clients/{Encode(focusClientId)}", notes, ct);
            clientName = Str(client, "clientName") ?? focusClientId;
            clientType = Str(client, "clientType");
            activity = await GetJsonAsync($"/mock/td/clients/{Encode(focusClientId)}/activity?since={since:yyyy-MM-dd}", notes, ct);
        }

        var holdingMv = Dbl(holding, "marketValue");
        var holdingQty = Lng(holding, "quantity");
        var holdingWeight = Dbl(holding, "weightPct");

        var holdingsStep = new TdStoryboardStep
        {
            Id = "holdings",
            Order = 2,
            Beat = "Holdings cross-reference",
            Title = $"{clientName} already owns the equity",
            Narration =
                $"{clientName} holds {Money(holdingMv)} of {issuerName} equity" +
                (holdingWeight is not null ? $" — about {holdingWeight:0.0}% of their book" : "") +
                ". They have a direct stake in how this deal prices, which is exactly why they are the first call.",
            Metrics =
            [
                new StoryboardMetric { Label = "Equity position", Value = Money(holdingMv), Tone = "positive" },
                new StoryboardMetric { Label = "Shares", Value = holdingQty is not null ? Mm(holdingQty.Value) + " sh" : "—" },
                new StoryboardMetric { Label = "Portfolio weight", Value = holdingWeight is not null ? $"{holdingWeight:0.0}%" : "—" },
            ],
            Evidence = holding is null ? [] :
            [
                new StoryboardEvidence
                {
                    Kind = "holding",
                    Label = $"{clientName} · {issuerName} equity",
                    Detail = $"{(holdingQty is not null ? Mm(holdingQty.Value) + " shares · " : "")}{Money(holdingMv)}",
                    RefId = Str(holding, "holdingId"),
                    Date = Str(holding, "asOfDate"),
                    SecurityId = equityId,
                },
            ],
        };

        // ---- Step 3: recent new-debt RFQ / trade / call activity --------------
        var debtRfqs = Arr(debtInterest, "rfqs")
            .Where(r => r is not null && string.Equals(Str(r, "clientId"), focusClientId, StringComparison.OrdinalIgnoreCase))
            .Select(r => r!)
            .OrderBy(r => Str(r, "rfqDate"), StringComparer.Ordinal)
            .ToList();
        var debtTrades = Arr(debtInterest, "trades")
            .Where(t => t is not null && string.Equals(Str(t, "clientId"), focusClientId, StringComparison.OrdinalIgnoreCase))
            .Select(t => t!)
            .OrderBy(t => Str(t, "tradeDate"), StringComparer.Ordinal)
            .ToList();
        var calls = Arr(activity, "crm")
            .Where(c => c is not null)
            .Select(c => c!)
            .Where(c => MentionsIssuer(c, issuerName))
            .OrderBy(c => Str(c, "entryDate"), StringComparer.Ordinal)
            .ToList();

        var tradedNotional = debtTrades.Sum(t => Dbl(t, "notional") ?? 0);
        var lastTouch = calls.Select(c => Str(c, "entryDate"))
            .Concat(debtRfqs.Select(r => Str(r, "rfqDate")))
            .Where(d => d is not null)
            .OrderByDescending(d => d, StringComparer.Ordinal)
            .FirstOrDefault();
        var debtName = Str(debt, "securityName") ?? "the new senior note";

        var activityStep = new TdStoryboardStep
        {
            Id = "activity",
            Order = 3,
            Beat = "Recent flow & conversations",
            Title = "They've been trading the credit and calling us",
            Narration =
                $"Over the last month {clientName} has worked {debtRfqs.Count} electronic RFQs in {debtName} and lifted " +
                $"{Money(tradedNotional)} of it from the desk — and they've called us about the name. This is a live, " +
                "engaged account, not a cold outreach.",
            Metrics =
            [
                new StoryboardMetric { Label = "Electronic RFQs", Value = debtRfqs.Count.ToString(CultureInfo.InvariantCulture), Tone = "accent" },
                new StoryboardMetric { Label = "Traded (30d)", Value = Money(tradedNotional), Tone = "positive" },
                new StoryboardMetric { Label = "Last contact", Value = lastTouch ?? "—" },
            ],
            Evidence = BuildActivityEvidence(debtRfqs, debtTrades, calls, debtId),
        };

        // ---- Step 4: the outreach recommendation ------------------------------
        var axe = Arr(debtInterest, "inventory").Where(i => i is not null).Select(i => i!).FirstOrDefault();
        var axeSize = Lng(axe, "inventorySize");
        var axeOffer = Dbl(axe, "offerPrice");
        var lastCallAction = calls.LastOrDefault() is { } lc ? Str(lc, "followUpAction") : null;

        var talkingPoints = new List<string>
        {
            $"You're long {Money(holdingMv)} of {issuerName} equity — the concurrent equity raise is near-term dilutive, but it de-risks the balance sheet behind the credit you've been buying.",
            $"You've worked {debtRfqs.Count} RFQs and lifted {Money(tradedNotional)} of {debtName} from us this month; we want you anchored in the new senior note, not chasing it in the grey market.",
        };
        if (axe is not null)
        {
            talkingPoints.Add($"Desk is showing {(axeSize is not null ? Mm(axeSize.Value) : "")} to sell{(axeOffer is not null ? $" around {axeOffer:0.00}" : "")} — I can indicate a priority allocation for you on the new paper.");
        }

        var outreach = new TdOutreachRecommendation
        {
            ClientId = focusClientId,
            ClientName = clientName,
            ClientType = clientType,
            Headline = $"Call {clientName} now — anchor them in the {issuerName} new issue",
            TalkingPoints = talkingPoints,
            TradeIdea = debt is null ? null : new TradeIdea
            {
                SecurityId = debtId!,
                SecurityName = debtName,
                Side = "Buy",
                Rationale = "Consistent electronic buyer of the credit with a large equity stake — prioritise their allocation on the new senior note.",
                Level = axeOffer is not null ? $"~{axeOffer:0.00} (desk axe{(axeSize is not null ? $" {Mm(axeSize.Value)}" : "")})" : "indicative on the new issue",
            },
            SuggestedAction = lastCallAction is not null
                ? $"{lastCallAction}. Lead with the new-issue allocation, anchored to their equity position and this month's RFQ flow."
                : $"Call {clientName} immediately with a priority allocation on {debtName}, anchored to their equity position and recent RFQ flow.",
            DraftMessage =
                $"Hi — {issuerName} is bringing the debt and equity deal you flagged. Given your position and the RFQs we've worked this month, " +
                $"we want you anchored in the new senior note. Can talk allocation now — let me know a good time.",
        };

        var outreachStep = new TdStoryboardStep
        {
            Id = "outreach",
            Order = 4,
            Beat = "Prioritized outreach",
            Title = outreach.Headline,
            Narration =
                $"Put it together: a client who owns the equity, has been actively trading the new credit, and asked us to call on " +
                "announcement. That is the first call of the morning — with a concrete allocation in hand.",
            Metrics =
            [
                new StoryboardMetric { Label = "Priority", Value = "P1", Tone = "warning" },
                new StoryboardMetric { Label = "Why now", Value = "Holds equity + trading the debt", Tone = "accent" },
                new StoryboardMetric { Label = "Desk axe", Value = axe is not null && axeSize is not null ? Mm(axeSize.Value) + " to sell" : "—" },
            ],
            Evidence = axe is null ? [] :
            [
                new StoryboardEvidence
                {
                    Kind = "axe",
                    Label = $"Distribution axe · {debtName}",
                    Detail = $"{(axeSize is not null ? Mm(axeSize.Value) + " to sell" : "")}{(axeOffer is not null ? $" @ {axeOffer:0.00}" : "")} · {Str(axe, "desk")}",
                    RefId = Str(axe, "inventoryId"),
                    Date = Str(axe, "asOfDate"),
                    SecurityId = debtId,
                },
            ],
        };

        return new TdNewIssueStoryboard
        {
            Mode = "DEMO",
            AsOf = asOf.ToString("yyyy-MM-dd"),
            Title = "New Issue Radar",
            Subtitle = $"{issuerName} · debt + equity · who do we call first?",
            Issuer = issuer,
            Steps = [announcementStep, holdingsStep, activityStep, outreachStep],
            Outreach = outreach,
            Notes = notes.Count > 0 ? notes : null,
        };
    }

    // ---------------------------------------------------------------- evidence builders

    private static IReadOnlyList<StoryboardEvidence> BuildAnnouncementEvidence(JsonNode? news, IReadOnlyList<NewIssueTranche> tranches)
    {
        var list = new List<StoryboardEvidence>();
        if (news is not null)
        {
            list.Add(new StoryboardEvidence
            {
                Kind = "news",
                Label = Str(news, "headline") ?? "New issue announcement",
                Detail = Str(news, "summary"),
                RefId = Str(news, "newsId"),
                Date = Str(news, "publishTimestamp"),
                SecurityId = Str(news, "relatedSecurityId"),
            });
        }
        foreach (var t in tranches)
        {
            list.Add(new StoryboardEvidence
            {
                Kind = t.AssetClass.Contains("Bond", StringComparison.OrdinalIgnoreCase) ? "trade" : "holding",
                Label = $"{t.AssetClass} · {t.SecurityName}",
                Detail = t.Detail,
                RefId = t.SecurityId,
                SecurityId = t.SecurityId,
            });
        }
        return list;
    }

    private static IReadOnlyList<StoryboardEvidence> BuildActivityEvidence(
        IReadOnlyList<JsonNode> rfqs, IReadOnlyList<JsonNode> trades, IReadOnlyList<JsonNode> calls, string? debtId)
    {
        var list = new List<StoryboardEvidence>();
        foreach (var c in calls)
        {
            list.Add(new StoryboardEvidence
            {
                Kind = "crm",
                Label = $"{Str(c, "meetingType")} · {Str(c, "topic")}",
                Detail = Str(c, "callReportText"),
                RefId = Str(c, "crmId"),
                Date = Str(c, "entryDate"),
            });
        }
        foreach (var t in trades)
        {
            list.Add(new StoryboardEvidence
            {
                Kind = "trade",
                Label = $"Bought {Mm(Lng(t, "quantity") ?? 0)} · {Str(t, "executionChannel")}",
                Detail = $"{Money(Dbl(t, "notional"))} @ {Dbl(t, "price"):0.00}",
                RefId = Str(t, "tradeId"),
                Date = Str(t, "tradeDate"),
                SecurityId = debtId,
            });
        }
        foreach (var r in rfqs)
        {
            list.Add(new StoryboardEvidence
            {
                Kind = "rfq",
                Label = $"RFQ {Str(r, "direction")} {Mm(Lng(r, "quantity") ?? 0)}",
                Detail = $"{Str(r, "responseStatus")} · trader {Str(r, "traderName")}",
                RefId = Str(r, "rfqId"),
                Date = Str(r, "rfqDate"),
                SecurityId = debtId,
            });
        }
        return list;
    }

    private TdNewIssueStoryboard BuildDegraded(string equityId, string clientId, DateOnly asOf, List<string> notes)
    {
        notes.Add($"Returned a degraded storyboard: could not resolve the new-issue security '{equityId}'. Retry once upstream services recover.");
        return new TdNewIssueStoryboard
        {
            Mode = "DEMO",
            AsOf = asOf.ToString("yyyy-MM-dd"),
            Title = "New Issue Radar",
            Subtitle = "New issue data unavailable",
            Issuer = new NewIssueIssuer { Name = "Unknown issuer", Headline = "New issue data unavailable", Tranches = [] },
            Steps = [],
            Outreach = new TdOutreachRecommendation
            {
                ClientId = clientId,
                ClientName = clientId,
                Headline = "Outreach unavailable",
                TalkingPoints = [],
                SuggestedAction = "Retry once the trading-desk system-of-record is reachable.",
            },
            Notes = notes,
        };
    }

    // ---------------------------------------------------------------- http + json helpers

    private async Task<JsonNode?> GetJsonAsync(string path, List<string> notes, CancellationToken ct)
    {
        try
        {
            return await mockApi.GetJsonAsync(path, ct);
        }
        catch (Exception)
        {
            notes.Add($"Upstream data unavailable: GET {path} failed; the storyboard was composed without it.");
            return null;
        }
    }

    private async Task<List<JsonNode>> GetArrayAsync(string path, List<string> notes, CancellationToken ct)
    {
        var node = await GetJsonAsync(path, notes, ct);
        return (node as JsonArray)?.Where(n => n is not null).Select(n => n!).ToList() ?? [];
    }

    private static string TrancheDetail(JsonNode? bond)
    {
        var parts = new List<string>();
        var maturity = Str(bond, "maturityDate");
        var rating = Str(bond, "rating");
        if (!string.IsNullOrWhiteSpace(maturity)) parts.Add($"matures {maturity}");
        if (!string.IsNullOrWhiteSpace(rating)) parts.Add(rating!);
        return parts.Count > 0 ? string.Join(" · ", parts) : "New senior note";
    }

    private static bool MentionsIssuer(JsonNode crm, string issuerName)
    {
        var token = issuerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? issuerName;
        return Contains(Str(crm, "topic"), token) || Contains(Str(crm, "callReportText"), token);
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private static string Money(double? value)
    {
        if (value is null || value == 0) return "—";
        var v = value.Value;
        if (Math.Abs(v) >= 1_000_000_000) return $"${v / 1_000_000_000:0.00}bn";
        if (Math.Abs(v) >= 1_000_000) return $"${v / 1_000_000:0.0}mm";
        if (Math.Abs(v) >= 1_000) return $"${v / 1_000:0.0}k";
        return $"${v:0}";
    }

    private static string Mm(double qty)
    {
        if (Math.Abs(qty) >= 1_000_000) return $"{qty / 1_000_000:0.#}mm";
        if (Math.Abs(qty) >= 1_000) return $"{qty / 1_000:0.#}k";
        return qty.ToString("0", CultureInfo.InvariantCulture);
    }

    private static JsonArray Arr(JsonNode? node, string key) => node?[key] as JsonArray ?? [];

    private static string? Str(JsonNode? node, string key)
    {
        try { return node?[key]?.GetValue<string>(); }
        catch { return node?[key]?.ToString(); }
    }

    private static double? Dbl(JsonNode? node, string key)
    {
        try { return node?[key]?.GetValue<double>(); }
        catch
        {
            return double.TryParse(node?[key]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
    }

    private static long? Lng(JsonNode? node, string key)
    {
        try { return node?[key]?.GetValue<long>(); }
        catch
        {
            return long.TryParse(node?[key]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var l) ? l : null;
        }
    }

    private static bool Contains(string? a, string? b) =>
        a is not null && b is not null && a.Contains(b, StringComparison.OrdinalIgnoreCase);
}
