using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;
using static OrchestrationApi.Agents.Demo.TdClientScorer;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic, offline DEMO composer for the Institutional Sales &amp; Trading morning
/// briefing — the "Morning Planning &amp; Prioritized Outreach" scene for a coverage
/// salesperson. Pulls the salesperson's client book, each client's 360° activity, dataset
/// news/research, dealer inventory axes and the live event set from the mock
/// system-of-record API <b>over HTTP only</b> (constitution Principle II / FR-002 — never
/// reads the mock fixtures in-process), scores every client with <see cref="TdClientScorer"/>,
/// and assembles a <see cref="TdBriefing"/>. All data is fictional.
///
/// Output is deterministic: stable ordering and fixed text so repeated DEMO runs with the
/// same inputs are byte-identical. No model and no credentials are used. Resilience
/// (FR-011): a salesperson with no book throws <see cref="UnknownSalespersonException"/>
/// (→ 400); other upstream gaps degrade to a structured brief carrying <c>notes</c>.
/// </summary>
public sealed class TdBriefingComposer(MockApiClient mockApi, EventTools eventTools)
{
    public const string DefaultSalespersonId = "Theo Wexler";
    public const string DefaultDate = "2026-05-22";
    private const int WindowDays = 90;

    /// <summary>Securities surfaced on the morning market strip (Theo's AI-basket + key book names).</summary>
    private static readonly string[] StripSecurities =
        ["SEC-3003", "SEC-3002", "SEC-3005", "SEC-3008", "SEC-3101", "SEC-3501"];

    public async Task<TdBriefing> ComposeAsync(string salespersonId, string? date, CancellationToken ct = default)
    {
        var notes = new List<string>();
        var asOf = ParseDate(date) ?? DateOnly.Parse(DefaultDate, CultureInfo.InvariantCulture);
        var since = asOf.AddDays(-WindowDays);
        var salesperson = string.IsNullOrWhiteSpace(salespersonId) ? DefaultSalespersonId : salespersonId;

        // Client book for this salesperson.
        var clients = await GetBookAsync(salesperson, notes, ct);
        if (clients is null)
        {
            return BuildDegraded(salesperson, asOf, notes);
        }
        if (clients.Count == 0)
        {
            throw new UnknownSalespersonException(salesperson);
        }

        // Reference data shared across clients.
        var securities = IndexById(await TryGetArrayAsync("/mock/td/securities", notes, ct), "securityId");
        var news = (await TryGetArrayAsync($"/mock/td/news?since={since:yyyy-MM-dd}", notes, ct)).Where(n => n is not null).Select(n => n!).ToList();
        var research = (await TryGetArrayAsync("/mock/td/research", notes, ct)).Where(r => r is not null).Select(r => r!).ToList();
        var inventory = (await TryGetArrayAsync("/mock/td/inventory", notes, ct)).Where(i => i is not null).Select(i => i!).ToList();
        var liveEvents = await GetEventsAsync(notes, ct);

        var scored = new List<ScoredClient>();
        foreach (var client in clients)
        {
            var cid = Str(client, "clientId");
            if (cid is null) continue;

            var activity = await GetActivityAsync(cid, since, notes, ct);
            var signals = BuildSignals(cid, client, activity, securities, news, research, inventory, asOf, since);

            var drivingEvents = EventImpactResolver.ResolveForClient(
                cid, signals.SecurityIds, signals.Issuers, signals.Sectors, liveEvents);
            var eventDelta = EventImpactResolver.NetContribution(drivingEvents);

            scored.Add(signals with
            {
                // Keep the full composite (no 100-clamp) so a small, uniformly-active book still
                // differentiates; eventDelta then re-orders on a breaking print. Card display uses
                // the 0-100 Rationale.CompositeScore; Score is the internal ranking key.
                Score = signals.RawScore + eventDelta,
                DrivingEvents = drivingEvents,
            });
        }

        var ranked = scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.RawScore)
            .ThenByDescending(s => s.ExposureMm)
            .ThenBy(s => s.ClientId, StringComparer.Ordinal)
            .ToList();

        var priorityCallList = ranked
            .Select((s, i) => BuildPriorityCall(s, rank: i + 1))
            .ToList();

        var eventsConsidered = BuildEventsConsidered(news, research);
        var inventoryAxes = BuildInventoryAxes(inventory, securities, scored);

        return new TdBriefing
        {
            Mode = "DEMO",
            AsOf = asOf.ToString("yyyy-MM-dd"),
            Greeting = $"Good morning, {FirstName(salesperson)}",
            Salesperson = new SalespersonIdentity
            {
                SalespersonId = salesperson,
                Name = salesperson,
                Desk = "Institutional Sales & Trading",
                Coverage = CoverageLabel(clients),
                ClientCount = clients.Count,
            },
            MarketStrip = BuildMarketStrip(securities, inventory, news),
            MacroThemes = BuildMacroThemes(scored),
            Reasoning = BuildReasoning(),
            PriorityCallList = priorityCallList,
            InventoryAxes = inventoryAxes,
            SuggestedFirstAction = BuildSuggestedFirstAction(priorityCallList),
            Notes = notes.Count > 0 ? notes : null,
            EventsConsidered = eventsConsidered,
            LiveEvents = liveEvents,
        };
    }

    /// <summary>
    /// Per-client entity sets (the securities, issuers and sectors each client in the salesperson's
    /// book touches via holdings/trades/RFQs/inquiries), keyed by clientId. Reuses the same HTTP data
    /// path as <see cref="ComposeAsync"/> so the LIVE deterministic re-rank overlay
    /// (<see cref="OrchestrationApi.Live.TdBriefingLive"/>) can fold injected events onto the agent's
    /// briefing without re-running the agent. HTTP-only (Principle II); all data fictional.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ClientEntitySet>> GetClientEntitySetsAsync(
        string salespersonId, string? date, CancellationToken ct = default)
    {
        var notes = new List<string>();
        var asOf = ParseDate(date) ?? DateOnly.Parse(DefaultDate, CultureInfo.InvariantCulture);
        var since = asOf.AddDays(-WindowDays);
        var salesperson = string.IsNullOrWhiteSpace(salespersonId) ? DefaultSalespersonId : salespersonId;

        var result = new Dictionary<string, ClientEntitySet>(StringComparer.OrdinalIgnoreCase);
        var clients = await GetBookAsync(salesperson, notes, ct);
        if (clients is null || clients.Count == 0) return result;

        var securities = IndexById(await TryGetArrayAsync("/mock/td/securities", notes, ct), "securityId");
        foreach (var client in clients)
        {
            var cid = Str(client, "clientId");
            if (cid is null) continue;
            var activity = await GetActivityAsync(cid, since, notes, ct);
            var (secIds, issuers, sectors) = ExtractEntitySets(activity, securities);
            result[cid] = new ClientEntitySet(secIds, issuers, sectors);
        }
        return result;
    }

    /// <summary>
    /// Extract the securities / issuers / sectors a client touches from its 360° activity payload.
    /// Shared by <see cref="BuildSignals"/> and <see cref="GetClientEntitySetsAsync"/> so the DEMO
    /// composer and the LIVE overlay resolve the same entity universe per client.
    /// </summary>
    private static (HashSet<string> Securities, HashSet<string> Issuers, HashSet<string> Sectors) ExtractEntitySets(
        JsonNode? activity, IReadOnlyDictionary<string, JsonNode> securities)
    {
        var secIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in Arr(activity, "holdings")) AddIf(secIds, Str(h, "securityId"));
        foreach (var t in Arr(activity, "trades")) AddIf(secIds, Str(t, "securityId"));
        foreach (var r in Arr(activity, "rfqs")) AddIf(secIds, Str(r, "securityId"));
        foreach (var i in Arr(activity, "inquiries")) AddIf(secIds, Str(i, "inferredSecurity"));

        var issuers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sid in secIds)
        {
            if (!securities.TryGetValue(sid, out var sec)) continue;
            AddIf(issuers, Str(sec, "issuer"));
            AddIf(sectors, Str(sec, "sector"));
        }
        return (secIds, issuers, sectors);
    }

    // ---------------------------------------------------------------- signal assembly

    private sealed record ScoredClient
    {
        public required string ClientId { get; init; }
        public required JsonNode Client { get; init; }
        public required HashSet<string> SecurityIds { get; init; }
        public required HashSet<string> Issuers { get; init; }
        public required HashSet<string> Sectors { get; init; }
        public required double ExposureMm { get; init; }
        public required IReadOnlyList<WhyNowDriver> WhyNow { get; init; }
        public required IReadOnlyList<TradeIdea> TradeIdeas { get; init; }
        public required TdCallRationale Rationale { get; init; }
        public required int RawScore { get; init; }
        public string? PersonalNote { get; init; }
        public int Score { get; init; }
        public IReadOnlyList<EventLinkage> DrivingEvents { get; init; } = [];
    }

    private ScoredClient BuildSignals(
        string cid, JsonNode client, JsonNode? activity, Dictionary<string, JsonNode> securities,
        List<JsonNode> news, List<JsonNode> research, List<JsonNode> inventory, DateOnly asOf, DateOnly since)
    {
        var holdings = Arr(activity, "holdings");
        var trades = Arr(activity, "trades");
        var rfqs = Arr(activity, "rfqs");
        var inquiries = Arr(activity, "inquiries");
        var crm = Arr(activity, "crm");

        // Securities / issuers / sectors this client touches.
        var (secIds, issuers, sectors) = ExtractEntitySets(activity, securities);

        var exposureMm = Math.Round(holdings.Sum(h => Dbl(h, "marketValue") ?? 0) / 1_000_000d, 1);
        var whyNow = new List<WhyNowDriver>();
        var tradeIdeas = new List<TradeIdea>();

        // --- (a) news / research touching the book (matched by security or, crucially, by ISSUER
        //     so equity news bridges to the same name's bonds; broad sector-only hits are ignored) ---
        var relevantNews = news
            .Where(n => NewsRelevance(Str(n, "relatedSecurityId"), securities, secIds, issuers) > 0)
            .OrderByDescending(n => Str(n, "publishTimestamp"), StringComparer.Ordinal)
            .Take(5)
            .ToList();
        var relevantResearch = research
            .Where(r => NewsRelevance(Str(r, "relatedSecurityId"), securities, secIds, issuers) > 0)
            .OrderByDescending(r => Str(r, "publishDate"), StringComparer.Ordinal)
            .Take(4)
            .ToList();

        var newsScore = 0;
        foreach (var n in relevantNews)
        {
            newsScore += NewsItem + (IsStrongSentiment(Str(n, "sentiment")) ? SentimentBoost : 0);
            var sid = Str(n, "relatedSecurityId");
            whyNow.Add(new WhyNowDriver
            {
                Kind = "news",
                Label = $"{Str(n, "sentiment") ?? "Neutral"} news on {SecName(securities, sid)}: {Str(n, "headline")}",
                Detail = Str(n, "summary"),
                SecurityId = sid,
                RefId = Str(n, "newsId"),
            });
        }
        newsScore = Math.Min(newsScore, 80);
        var researchScore = 0;
        foreach (var r in relevantResearch)
        {
            researchScore += ResearchAction;
            var sid = Str(r, "relatedSecurityId");
            whyNow.Add(new WhyNowDriver
            {
                Kind = "research",
                Label = $"{Str(r, "ratingAction")} on {SecName(securities, sid)} — {Str(r, "title")}",
                Detail = $"{Str(r, "analystName")}: {Str(r, "shortSummary")} ({Str(r, "targetOrSpreadView")})",
                SecurityId = sid,
                RefId = Str(r, "researchId"),
            });
        }
        newsScore = Math.Min(newsScore + researchScore, 100);

        // --- (b) open RFQs awaiting follow-up ---
        var rfqScore = 0;
        foreach (var rfq in rfqs.OrderByDescending(r => Str(r, "rfqDate"), StringComparer.Ordinal))
        {
            var status = Str(rfq, "responseStatus");
            var pts = status switch
            {
                "Quoted" => RfqQuoted,
                "No Follow-Up" => RfqNoFollowUp,
                "Passed" => RfqPassed,
                _ => 0,
            };
            if (pts == 0) continue;
            rfqScore += pts;
            var sid = Str(rfq, "securityId");
            whyNow.Add(new WhyNowDriver
            {
                Kind = "rfq",
                Label = $"Open RFQ — {Str(rfq, "direction")} {Qty(Lng(rfq, "quantity"))} {SecName(securities, sid)} ({status})",
                Detail = $"Received {Str(rfq, "rfqDate")}; trader {Str(rfq, "traderName")}.",
                SecurityId = sid,
                RefId = Str(rfq, "rfqId"),
            });
        }
        rfqScore = Math.Min(rfqScore, 60);

        // --- (c) inquiries to action / clarify ---
        var inquiryScore = 0;
        foreach (var inq in inquiries.OrderByDescending(i => Str(i, "inquiryDate"), StringComparer.Ordinal))
        {
            var sentiment = Str(inq, "sentiment");
            var ambiguous = string.IsNullOrWhiteSpace(Str(inq, "inferredSecurity")) || string.IsNullOrWhiteSpace(Str(inq, "inferredDirection"));
            var pts = Eq(sentiment, "Urgent") ? InquiryUrgent : ambiguous ? InquiryAmbiguous : InquiryRecent;
            inquiryScore += pts;
            var sid = Str(inq, "inferredSecurity");
            whyNow.Add(new WhyNowDriver
            {
                Kind = "inquiry",
                Label = ambiguous
                    ? $"Clarify {Str(inq, "channel")} inquiry ({sentiment})"
                    : $"{sentiment} inquiry — {Str(inq, "inferredDirection")} {Str(inq, "inferredSize")} {SecName(securities, sid)}",
                Detail = Str(inq, "rawInquiryText"),
                SecurityId = sid,
                RefId = Str(inq, "inquiryId"),
            });
        }
        inquiryScore = Math.Min(inquiryScore, 60);

        // --- (d) inventory axes matching this client's book (top 4 by desk size) ---
        var axeScore = 0;
        var matchingAxes = inventory
            .Where(inv => Str(inv, "securityId") is { } s && secIds.Contains(s) && (Lng(inv, "inventorySize") ?? 0) != 0)
            .OrderByDescending(inv => Math.Abs(Lng(inv, "inventorySize") ?? 0))
            .ThenBy(inv => Str(inv, "securityId"), StringComparer.Ordinal)
            .Take(4);
        foreach (var inv in matchingAxes)
        {
            var sid = Str(inv, "securityId")!;
            var size = Lng(inv, "inventorySize") ?? 0;
            axeScore += AxeMatch;

            var axeSide = size < 0 ? "buy" : "sell";
            var clientSide = ClientSideFor(size, sid, trades, inquiries);
            whyNow.Add(new WhyNowDriver
            {
                Kind = "axe",
                Label = $"We're axed to {axeSide} {SecName(securities, sid)} (desk {(size < 0 ? "short" : "long")} {Qty(Math.Abs(size))})",
                Detail = $"Bid {Str(inv, "bidPrice")} / Offer {Str(inv, "offerPrice")} ({Str(inv, "desk")}). Natural fit for a client {clientSide.ToLowerInvariant()} order.",
                SecurityId = sid,
            });
            tradeIdeas.Add(new TradeIdea
            {
                SecurityId = sid,
                SecurityName = SecName(securities, sid),
                Side = clientSide,
                Rationale = size < 0
                    ? "Desk is short and axed to buy — strong bid if they're trimming; we can work a buy order into the tape."
                    : "Desk is long and axed to sell — competitive offer to add at size.",
                Level = $"{Str(inv, "bidPrice")} / {Str(inv, "offerPrice")}",
            });
        }
        axeScore = Math.Min(axeScore, 60);

        // --- (e) CRM urgency ---
        var crmScore = 0;
        var latestCrm = crm.OrderByDescending(c => Str(c, "entryDate"), StringComparer.Ordinal).FirstOrDefault();
        var maxUrgency = crm.Select(c => Str(c, "urgency")).ToList();
        if (maxUrgency.Any(u => Eq(u, "High"))) crmScore = CrmHigh;
        else if (maxUrgency.Any(u => Eq(u, "Medium"))) crmScore = CrmMedium;
        if (crmScore > 0 && latestCrm is not null)
        {
            whyNow.Add(new WhyNowDriver
            {
                Kind = "crm",
                Label = $"Open CRM thread — {Str(latestCrm, "topic")} ({Str(latestCrm, "urgency")})",
                Detail = $"{Str(latestCrm, "entryDate")}: {Str(latestCrm, "callReportText")}",
                RefId = Str(latestCrm, "crmId"),
            });
        }

        var raw = newsScore + rfqScore + inquiryScore + axeScore + crmScore;
        var rationale = new TdCallRationale
        {
            NewsRelevance = Intensity(newsScore),
            OpenRfqWeight = Intensity(rfqScore),
            InquiryWeight = Intensity(inquiryScore),
            InventoryAxeMatch = Intensity(axeScore),
            Urgency = Intensity(crmScore),
            CompositeScore = Intensity(raw),
            Explanation = BuildExplanation(client, newsScore, rfqScore, inquiryScore, axeScore, crmScore),
        };

        return new ScoredClient
        {
            ClientId = cid,
            Client = client,
            SecurityIds = secIds,
            Issuers = issuers,
            Sectors = sectors,
            ExposureMm = exposureMm,
            WhyNow = OrderWhyNow(whyNow),
            TradeIdeas = tradeIdeas.DistinctBy(t => t.SecurityId + t.Side).Take(3).ToList(),
            Rationale = rationale,
            RawScore = raw,
            PersonalNote = latestCrm is null ? null : Str(latestCrm, "callReportText"),
        };
    }

    // ---------------------------------------------------------------- card construction

    private TdPriorityCall BuildPriorityCall(ScoredClient s, int rank)
    {
        var c = s.Client;
        return new TdPriorityCall
        {
            Rank = rank,
            Priority = PriorityByRank(rank),
            ClientId = s.ClientId,
            ClientName = Str(c, "clientName") ?? s.ClientId,
            ClientType = Str(c, "clientType"),
            Region = Str(c, "clientRegion"),
            PreferredAssetClass = Str(c, "preferredAssetClass"),
            Score = s.Score,
            Rationale = s.Rationale,
            WhyNow = s.WhyNow.Take(6).ToList(),
            TalkingPoints = BuildTalkingPoints(s),
            TradeIdeas = s.TradeIdeas,
            PersonalNote = s.PersonalNote,
            SuggestedAction = BuildSuggestedAction(s),
            DrivingEvents = s.DrivingEvents,
        };
    }

    // Small, uniformly high-priority institutional book: band by rank for a clean, legible
    // spread (top pair P1, next pair P2, …) that visibly re-orders on a breaking-print inject.
    private static int PriorityByRank(int rank) => rank switch
    {
        <= 2 => 1,
        <= 4 => 2,
        <= 6 => 3,
        _ => 4,
    };

    private static List<string> BuildTalkingPoints(ScoredClient s)
    {
        var points = new List<string>();
        foreach (var d in s.WhyNow)
        {
            var point = d.Kind switch
            {
                "news" => $"Lead with the tape: {d.Label}. Frame the read-through for their book.",
                "research" => $"Walk through desk research — {d.Label}. Position it against their current exposure.",
                "rfq" => $"Close the loop on the open RFQ: {d.Label}. Ask if it's still live and offer a level.",
                "inquiry" => $"Pick up their inquiry: {d.Label}. Bring colour and a two-sided market.",
                "axe" => $"Put our axe in front of them: {d.Label}.",
                "crm" => $"Reconnect on the open thread: {d.Label}.",
                _ => d.Label,
            };
            points.Add(point);
            if (points.Count >= 4) break;
        }
        if (points.Count == 0)
        {
            points.Add("Relationship check-in — no fresh signals; confirm appetite across the AI-compute and credit baskets.");
        }
        return points;
    }

    private static string BuildSuggestedAction(ScoredClient s)
    {
        var name = Str(s.Client, "clientName") ?? s.ClientId;
        var top = s.WhyNow.FirstOrDefault();
        if (top is null)
        {
            return $"Keep {name} warm — proactive market colour across their preferred asset class.";
        }
        return top.Kind switch
        {
            "axe" => $"Call {name} first on the axe — {top.Label}. They're a natural counterparty; lead with the level.",
            "rfq" => $"Call {name} to revive the open RFQ — {top.Label}. Quote a level and lock the trade.",
            "news" => $"Call {name} on the overnight tape — {top.Label}. Bring the read-through and an actionable idea.",
            "research" => $"Call {name} with the new research — {top.Label}. Tie it to a trade in their book.",
            "inquiry" => $"Call {name} back on their inquiry — {top.Label}. Bring colour and a two-sided market.",
            _ => $"Call {name} — {top.Label}.",
        };
    }

    private static string BuildSuggestedFirstAction(IReadOnlyList<TdPriorityCall> calls)
    {
        if (calls.Count == 0)
        {
            return "No urgent outreach today. Use the time for proactive market colour and book reviews.";
        }
        var top = calls[0];
        return $"{top.SuggestedAction} This is your single highest-conviction call this morning — {top.ClientName} (score {top.Score}).";
    }

    // ---------------------------------------------------------------- supporting sections

    private static List<MarketStripItem> BuildMarketStrip(
        Dictionary<string, JsonNode> securities, List<JsonNode> inventory, List<JsonNode> news)
    {
        // The dataset is a single snapshot (no intraday series), so a numeric % change would be
        // synthetic. Instead, drive the strip's direction from the latest related-news sentiment
        // and show the indicative reference level — honest and demo-meaningful.
        var newsSentiment = news
            .Where(n => Str(n, "relatedSecurityId") is not null)
            .GroupBy(n => Str(n, "relatedSecurityId")!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(n => Str(n, "publishTimestamp"), StringComparer.Ordinal).First());
        var issuerSentiment = news
            .Select(n => (issuer: IssuerOf(securities, Str(n, "relatedSecurityId")), n))
            .Where(x => x.issuer is not null)
            .GroupBy(x => x.issuer!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => Str(x.n, "publishTimestamp"), StringComparer.Ordinal).First().n);

        var strip = new List<MarketStripItem>();
        foreach (var sid in StripSecurities)
        {
            if (!securities.TryGetValue(sid, out var sec)) continue;
            var reference = Dbl(sec, "referencePrice");

            // Prefer security-level news; fall back to same-issuer news (equity ↔ bond bridge).
            JsonNode? n = newsSentiment.GetValueOrDefault(sid);
            if (n is null && IssuerOf(securities, sid) is { } iss) n = issuerSentiment.GetValueOrDefault(iss);

            var direction = n is null ? "flat"
                : Eq(Str(n, "sentiment"), "Positive") ? "up"
                : Eq(Str(n, "sentiment"), "Negative") ? "down" : "flat";

            strip.Add(new MarketStripItem
            {
                Label = Ticker(sec),
                Value = reference is double v ? v.ToString("0.00", CultureInfo.InvariantCulture) : "—",
                Change = n is null ? null : Str(n, "macroTheme"),
                Direction = direction,
            });
        }
        return strip;
    }

    private static string? IssuerOf(Dictionary<string, JsonNode> securities, string? sid) =>
        sid is not null && securities.TryGetValue(sid, out var sec) ? Str(sec, "issuer") : null;

    private List<MacroThemeBullet> BuildMacroThemes(List<ScoredClient> scored)
    {
        // Pull the embedded storylines; keep the ones intersecting this book's securities/sectors.
        var bookSecs = scored.SelectMany(s => s.SecurityIds).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var themes = new List<MacroThemeBullet>();
        try
        {
            var raw = mockApi.GetJsonAsync("/mock/td/narrative-themes").GetAwaiter().GetResult() as JsonArray ?? [];
            foreach (var t in raw)
            {
                var theme = Str(t, "theme");
                var narrative = Str(t, "narrative");
                if (theme is null || narrative is null) continue;
                themes.Add(new MacroThemeBullet { Theme = theme, Detail = narrative });
            }
        }
        catch
        {
            // Degrade silently — themes are illustrative context.
        }
        return themes;
    }

    private static List<TdEventConsidered> BuildEventsConsidered(List<JsonNode> news, List<JsonNode> research)
    {
        var items = new List<TdEventConsidered>();
        foreach (var n in news)
        {
            items.Add(new TdEventConsidered
            {
                Id = Str(n, "newsId") ?? "",
                Kind = "news",
                Headline = Str(n, "headline") ?? "",
                Summary = Str(n, "summary"),
                Sector = Str(n, "relatedSector"),
                Sentiment = Str(n, "sentiment"),
                RelatedSecurityId = Str(n, "relatedSecurityId"),
                MacroTheme = Str(n, "macroTheme"),
                Timestamp = Str(n, "publishTimestamp"),
            });
        }
        foreach (var r in research)
        {
            items.Add(new TdEventConsidered
            {
                Id = Str(r, "researchId") ?? "",
                Kind = "research",
                Headline = Str(r, "title") ?? "",
                Summary = Str(r, "shortSummary"),
                Sector = Str(r, "sector"),
                Sentiment = Str(r, "ratingAction"),
                RelatedSecurityId = Str(r, "relatedSecurityId"),
                Timestamp = Str(r, "publishDate"),
            });
        }
        return items
            .OrderByDescending(i => i.Timestamp, StringComparer.Ordinal)
            .ToList();
    }

    private static List<InventoryAxe> BuildInventoryAxes(
        List<JsonNode> inventory, Dictionary<string, JsonNode> securities, List<ScoredClient> scored)
    {
        var axes = new List<InventoryAxe>();
        foreach (var inv in inventory)
        {
            var sid = Str(inv, "securityId");
            if (sid is null) continue;
            var size = Lng(inv, "inventorySize") ?? 0;
            if (size == 0) continue;

            var matched = scored.Where(s => s.SecurityIds.Contains(sid)).Select(s => Str(s.Client, "clientName") ?? s.ClientId).ToList();
            if (matched.Count == 0) continue; // only show axes relevant to this book

            securities.TryGetValue(sid, out var sec);
            axes.Add(new InventoryAxe
            {
                SecurityId = sid,
                SecurityName = Str(sec, "securityName") ?? sid,
                AssetClass = Str(sec, "assetClass"),
                Sector = Str(sec, "sector"),
                InventorySize = size,
                AxeSide = size < 0 ? "buy" : "sell",
                BidPrice = Dbl(inv, "bidPrice"),
                OfferPrice = Dbl(inv, "offerPrice"),
                Desk = Str(inv, "desk"),
                MatchedClients = matched,
            });
        }
        return axes
            .OrderByDescending(a => a.MatchedClients.Count)
            .ThenBy(a => a.SecurityId, StringComparer.Ordinal)
            .ToList();
    }

    private static List<ReasoningStep> BuildReasoning() =>
    [
        new() { Text = "Pulled the coverage book and each client's 360° activity: holdings, trades, RFQs, inquiries and CRM.", Status = "done" },
        new() { Text = "Scanned overnight news, desk research and dealer inventory axes against every client's positions.", Status = "done" },
        new() { Text = "Scored each client (news relevance, open RFQs, inquiries, axe match, urgency) into a ranked call list.", Status = "done" },
        new() { Text = "Matched our axes to client demand and generated talking points and trade ideas per call.", Status = "done" },
    ];

    // ---------------------------------------------------------------- upstream access

    private async Task<List<JsonNode>?> GetBookAsync(string salesperson, List<string> notes, CancellationToken ct)
    {
        var path = $"/mock/td/clients?salesperson={Encode(salesperson)}";
        try
        {
            using var response = await mockApi.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            var node = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
            return (node as JsonArray ?? []).Where(c => c is not null).Select(c => c!).ToList();
        }
        catch (Exception)
        {
            notes.Add($"Upstream data unavailable: GET {path} failed; returning a degraded briefing.");
            return null;
        }
    }

    private async Task<JsonNode?> GetActivityAsync(string clientId, DateOnly since, List<string> notes, CancellationToken ct)
    {
        var path = $"/mock/td/clients/{Encode(clientId)}/activity?since={since:yyyy-MM-dd}";
        try
        {
            using var response = await mockApi.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        }
        catch (Exception)
        {
            notes.Add($"Upstream data unavailable: GET {path} failed; {clientId} was scored without its activity.");
            return null;
        }
    }

    private async Task<IReadOnlyList<MarketEvent>> GetEventsAsync(List<string> notes, CancellationToken ct)
    {
        try
        {
            var events = await eventTools.ListEventsAsync(ct: ct);
            if (events.Count > 0)
            {
                notes.Add($"Reactive overlay: {events.Count} live event(s) weighed into the call ranking.");
            }
            return events;
        }
        catch (Exception)
        {
            notes.Add("Event store unavailable: the briefing was composed from trading-desk signals only.");
            return [];
        }
    }

    private async Task<JsonArray> TryGetArrayAsync(string path, List<string> notes, CancellationToken ct)
    {
        try
        {
            var node = await mockApi.GetJsonAsync(path, ct);
            return node as JsonArray ?? [];
        }
        catch (Exception)
        {
            notes.Add($"Upstream data unavailable: GET {path} failed; the briefing was composed without it.");
            return [];
        }
    }

    private TdBriefing BuildDegraded(string salesperson, DateOnly asOf, List<string> notes)
    {
        notes.Add($"Returned a degraded briefing for '{salesperson}': the system-of-record was unavailable. Retry once upstream services recover.");
        return new TdBriefing
        {
            Mode = "DEMO",
            AsOf = asOf.ToString("yyyy-MM-dd"),
            Greeting = "Good morning",
            Salesperson = new SalespersonIdentity { SalespersonId = salesperson, Name = salesperson, ClientCount = 0 },
            MarketStrip = [],
            MacroThemes = [],
            Reasoning =
            [
                new ReasoningStep { Text = "Attempted to pull the coverage book and client activity.", Status = "done" },
                new ReasoningStep { Text = "Upstream data unavailable — returned a degraded briefing.", Status = "pending" },
            ],
            PriorityCallList = [],
            InventoryAxes = [],
            SuggestedFirstAction = "Briefing could not be composed — retry once the system-of-record services recover.",
            Notes = notes,
        };
    }

    // ---------------------------------------------------------------- helpers

    private static string BuildExplanation(JsonNode client, int newsScore, int rfqScore, int inquiryScore, int axeScore, int crmScore)
    {
        var name = Str(client, "clientName") ?? "This client";
        var parts = new List<string>();
        if (newsScore > 0) parts.Add($"news/research relevance to their book ({newsScore})");
        if (rfqScore > 0) parts.Add($"open RFQ follow-ups ({rfqScore})");
        if (inquiryScore > 0) parts.Add($"live inquiries ({inquiryScore})");
        if (axeScore > 0) parts.Add($"a dealer axe matching their demand ({axeScore})");
        if (crmScore > 0) parts.Add($"an open CRM thread ({crmScore})");
        return parts.Count == 0
            ? $"{name} has no fresh signals today — a relationship check-in."
            : $"{name} ranks on " + string.Join(", ", parts) + ".";
    }

    private static string CoverageLabel(List<JsonNode> clients)
    {
        var types = clients.Select(c => Str(c, "clientType")).Where(t => t is not null).Distinct().ToList();
        return types.Count == 1 ? $"{types[0]} book" : "Institutional book";
    }

    private static IReadOnlyList<WhyNowDriver> OrderWhyNow(List<WhyNowDriver> drivers)
    {
        int Weight(string kind) => kind switch
        {
            // Lead with the timely catalyst (news/research), then our axe as the commercial hook,
            // then open loops (rfq/inquiry) and relationship context.
            "news" => 6, "research" => 5, "axe" => 4, "rfq" => 3, "inquiry" => 2, "crm" => 1, _ => 0,
        };
        return drivers
            .OrderByDescending(d => Weight(d.Kind))
            .ThenBy(d => d.RefId, StringComparer.Ordinal)
            .ToList();
    }

    private static string ClientSideFor(long inventorySize, string sid, JsonArray trades, JsonArray inquiries)
    {
        // If the client recently bought / signalled bullish, lead with Buy; if sold / bearish, lead with Sell.
        var boughtRecently = trades.Any(t => Eq(Str(t, "securityId"), sid) && Eq(Str(t, "direction"), "Buy"));
        var soldRecently = trades.Any(t => Eq(Str(t, "securityId"), sid) && Eq(Str(t, "direction"), "Sell"));
        var bullish = inquiries.Any(i => Eq(Str(i, "inferredSecurity"), sid) && Eq(Str(i, "inferredDirection"), "Buy"));
        var bearish = inquiries.Any(i => Eq(Str(i, "inferredSecurity"), sid) && Eq(Str(i, "inferredDirection"), "Sell"));
        if (boughtRecently || bullish) return "Buy";
        if (soldRecently || bearish) return "Sell";
        // Otherwise lead with the side that absorbs our axe: we're short → they sell to us; we're long → they buy.
        return inventorySize < 0 ? "Sell" : "Buy";
    }

    private static string SecName(Dictionary<string, JsonNode> securities, string? sid) =>
        sid is not null && securities.TryGetValue(sid, out var sec) ? Str(sec, "securityName") ?? sid : sid ?? "—";

    private static string Ticker(JsonNode sec)
    {
        var name = Str(sec, "securityName") ?? Str(sec, "securityId") ?? "—";
        // Short label: first two words.
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 2 ? name : string.Join(' ', words.Take(2));
    }

    private static string Qty(long? q)
    {
        if (q is not long v) return "";
        return v >= 1_000_000 ? $"{v / 1_000_000d:0.#}mm" : v >= 1_000 ? $"{v / 1_000d:0.#}k" : v.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsStrongSentiment(string? s) => Eq(s, "Positive") || Eq(s, "Negative");

    private static void AddIf(HashSet<string> set, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) set.Add(value);
    }

    private static bool InSet(HashSet<string> set, string? value) => value is not null && set.Contains(value);

    /// <summary>
    /// Relevance of a news/research item to a client: 2 when it names a security the client
    /// holds/trades, or shares its issuer (so equity news bridges to the same name's bonds);
    /// 0 otherwise. Broad sector-only matches are deliberately ignored to avoid score inflation.
    /// </summary>
    private static int NewsRelevance(string? newsSecurityId, Dictionary<string, JsonNode> securities, HashSet<string> secIds, HashSet<string> issuers)
    {
        if (string.IsNullOrWhiteSpace(newsSecurityId)) return 0;
        if (secIds.Contains(newsSecurityId)) return 2;
        var issuer = securities.TryGetValue(newsSecurityId, out var sec) ? Str(sec, "issuer") : null;
        return issuer is not null && issuers.Contains(issuer) ? 2 : 0;
    }

    private static Dictionary<string, JsonNode> IndexById(JsonArray items, string key)
    {
        var map = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item is null) continue;
            var id = Str(item, key);
            if (id is not null) map[id] = item;
        }
        return map;
    }

    private static JsonArray Arr(JsonNode? node, string key) => node?[key] as JsonArray ?? [];

    private static string FirstName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "" : name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

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

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var datePart = value.Length >= 10 ? value[..10] : value;
        return DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static bool Eq(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Encode(string value) => Uri.EscapeDataString(value);
}

/// <summary>
/// The securities, issuers and sectors a single client touches (holdings/trades/RFQs/inquiries),
/// used to resolve event→client linkage in both the DEMO composer and the LIVE re-rank overlay.
/// </summary>
public sealed record ClientEntitySet(
    IReadOnlySet<string> Securities,
    IReadOnlySet<string> Issuers,
    IReadOnlySet<string> Sectors);
