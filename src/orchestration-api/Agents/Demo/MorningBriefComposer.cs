using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic, offline DEMO composer (T015) for the morning brief. Pulls market
/// data, news, relative value, clients, holdings, engagement and axes from the mock
/// system-of-record API <b>over HTTP only</b> (constitution Principle II / FR-002 —
/// never reads the mock fixtures in-process) and assembles a <see cref="MorningBrief"/>.
///
/// Output is deterministic: stable ordering and fixed text so repeated DEMO runs with
/// the same inputs are byte-identical (SC-002). No model and no credentials are used.
///
/// Resilience (FR-011): every upstream call is wrapped; if essential data is missing
/// the composer returns a degraded-but-structured brief carrying a <c>notes</c> entry
/// rather than throwing.
/// </summary>
public sealed class MorningBriefComposer(MockApiClient mockApi)
{
    // How many flagged clients to surface (matches the storyboard's curated set).
    private const int TopAffected = 3;

    public async Task<MorningBrief> ComposeAsync(string eventId, string? date, CancellationToken ct = default)
    {
        var notes = new List<string>();

        var market = await TryGetAsync("/mock/marketdata", notes, ct);
        var news = await TryGetNewsAsync(eventId, notes, ct);
        var relval = await TryGetAsync($"/mock/marketdata/relval/{eventId}", notes, ct);
        var clientsNode = await TryGetAsync("/mock/tableau/clients", notes, ct);
        var holdingsNode = await TryGetAsync("/mock/trading/holdings", notes, ct);
        var axesNode = await TryGetAsync("/mock/trading/axes", notes, ct);

        // Essential inputs for a populated brief. If any is missing, degrade gracefully.
        if (market is null || news is null || clientsNode is not JsonArray clients || holdingsNode is not JsonArray holdings)
        {
            return BuildDegraded(eventId, notes);
        }

        var asOf = market["asOf"]?.GetValue<string>() ?? $"{date ?? "2026-06-04"}T07:30:00-04:00";

        var marketStrip = BuildMarketStrip(market);
        var reasoning = BuildReasoning();
        var macroNarrative = BuildMacroNarrative(news, relval);

        // Index clients and their dominant exposure (from holdings) for reuse.
        var clientIndex = IndexClients(clients);
        var exposureByCid = DominantExposureByClient(holdings);

        var eventSectors = (news["sectors"] as JsonArray)?
            .Select(s => s!.GetValue<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Affected = clients holding instruments in an event sector. Ordered by wallet (revenue) desc.
        var affected = exposureByCid
            .Where(kv => clientIndex.ContainsKey(kv.Key))
            .Where(kv => eventSectors.Count == 0 || eventSectors.Contains(kv.Value.Sector))
            .Select(kv => new { Cid = kv.Key, Exposure = kv.Value, Client = clientIndex[kv.Key] })
            .OrderByDescending(x => x.Client.RevenueYtd)
            .ThenBy(x => x.Cid, StringComparer.Ordinal)
            .Take(TopAffected)
            .ToList();

        if (affected.Count == 0)
        {
            notes.Add($"No clients held material exposure to '{eventId}' sectors; outreach list is empty.");
            return new MorningBrief
            {
                Mode = "DEMO",
                AsOf = asOf,
                MarketStrip = marketStrip,
                Reasoning = reasoning,
                MacroNarrative = macroNarrative,
                MostAffectedClients = [],
                Outreach = [],
                Notes = notes,
            };
        }

        var mostAffected = affected
            .Select(x => new AffectedClient
            {
                Cid = x.Cid,
                Name = x.Client.Name,
                Tier = x.Client.Tier,
                Exposure = ExposureLabel(x.Exposure.ExposureType),
                Concern = ConcernFor(x.Exposure.ExposureType),
            })
            .ToList();

        var rankerInputs = new List<OutreachRanker.RankingInput>(affected.Count);
        foreach (var a in affected)
        {
            var engagement = await TryGetAsync($"/mock/dynamics/clients/{a.Cid}/engagement", notes, ct);
            rankerInputs.Add(new OutreachRanker.RankingInput(
                a.Cid,
                a.Client.Name,
                a.Client.RevenueYtd,
                a.Exposure.ExposureType,
                a.Exposure.Cusip,
                engagement));
        }

        var axes = axesNode as JsonArray ?? [];
        var outreach = OutreachRanker.Rank(rankerInputs, axes);

        return new MorningBrief
        {
            Mode = "DEMO",
            AsOf = asOf,
            MarketStrip = marketStrip,
            Reasoning = reasoning,
            MacroNarrative = macroNarrative,
            MostAffectedClients = mostAffected,
            Outreach = outreach,
            Notes = notes.Count > 0 ? notes : null,
        };
    }

    // ---------------------------------------------------------------- helpers

    private async Task<JsonNode?> TryGetAsync(string path, List<string> notes, CancellationToken ct)
    {
        try
        {
            return await mockApi.GetJsonAsync(path, ct);
        }
        catch (Exception)
        {
            notes.Add($"Upstream data unavailable: GET {path} failed; the brief was composed without it.");
            return null;
        }
    }

    private async Task<JsonNode?> TryGetNewsAsync(string eventId, List<string> notes, CancellationToken ct)
    {
        var path = $"/mock/news/{eventId}";
        try
        {
            using var response = await mockApi.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new UnknownMorningBriefEventException(eventId);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        }
        catch (UnknownMorningBriefEventException)
        {
            throw;
        }
        catch (Exception)
        {
            notes.Add($"Upstream data unavailable: GET {path} failed; the brief was composed without it.");
            return null;
        }
    }

    private static List<MarketStripItem> BuildMarketStrip(JsonNode market)
    {
        var items = new List<MarketStripItem>();
        var rates = market["rates"];
        if (rates?["ust10y"] is JsonNode u10)
        {
            var bp = u10["changeBp"]?.GetValue<int>() ?? 0;
            items.Add(new MarketStripItem { Label = "10y UST", Value = $"{u10["level"]!.GetValue<double>():0.00}%", Change = FormatBp(bp), Direction = DirFromBp(bp) });
        }
        if (rates?["ust2y"] is JsonNode u2)
        {
            var bp = u2["changeBp"]?.GetValue<int>() ?? 0;
            items.Add(new MarketStripItem { Label = "2y UST", Value = $"{u2["level"]!.GetValue<double>():0.00}%", Change = FormatBp(bp), Direction = DirFromBp(bp) });
        }
        if (market["equities"]?["spFuturesPct"] is JsonNode sp)
        {
            var pct = sp.GetValue<double>();
            items.Add(new MarketStripItem { Label = "S&P fut", Value = $"{pct:0.0}%", Direction = pct > 0 ? "up" : pct < 0 ? "down" : "flat" });
        }
        if (market["credit"]?["igChangeBp"] is JsonNode ig)
        {
            var bp = ig.GetValue<int>();
            items.Add(new MarketStripItem { Label = "IG credit", Value = $"{(bp >= 0 ? "+" : "")}{bp}bp wider", Direction = DirFromBp(bp) });
        }
        if (market["vix"] is JsonNode vix)
        {
            var chg = vix["change"]?.GetValue<double>() ?? 0;
            items.Add(new MarketStripItem { Label = "VIX", Value = $"{vix["level"]!.GetValue<double>():0.0}", Change = $"{(chg >= 0 ? "+" : "")}{chg:0.0}", Direction = chg > 0 ? "up" : chg < 0 ? "down" : "flat" });
        }
        if (market["tone"] is JsonNode tone)
        {
            items.Add(new MarketStripItem { Label = "Tone", Value = tone.GetValue<string>(), Direction = "flat" });
        }
        return items;
    }

    private static List<ReasoningStep> BuildReasoning() =>
    [
        new() { Text = "Pulled overnight macro events + market reaction (rates, equities, credit).", Status = "done" },
        new() { Text = "Scored client portfolios for rate sensitivity (duration, swap books).", Status = "done" },
        new() { Text = "Blended wallet, recent engagement & strategic importance into an outreach rank.", Status = "done" },
        new() { Text = "Generated personalized talking points per priority client.", Status = "done" },
    ];

    private static MacroNarrative BuildMacroNarrative(JsonNode news, JsonNode? relval)
    {
        var summary = news["summary"]?.GetValue<string>()
            ?? news["headline"]?.GetValue<string>()
            ?? "Overnight market event.";

        var whyItMatters = relval?["commentary"]?.GetValue<string>()
            ?? "The move repriced the front end hardest (curve flatter), pressuring long-duration holders and anyone carrying unhedged floating-rate exposure.";

        var sources = (news["sources"] as JsonArray)?
            .Select(s => s!.GetValue<string>())
            .ToList() ?? [];

        return new MacroNarrative { Summary = summary, WhyItMatters = whyItMatters, Sources = sources };
    }

    private static Dictionary<string, ClientRow> IndexClients(JsonArray clients)
    {
        var map = new Dictionary<string, ClientRow>(StringComparer.Ordinal);
        foreach (var c in clients)
        {
            if (c is null) continue;
            var id = c["id"]?.GetValue<string>();
            if (id is null) continue;
            map[id] = new ClientRow(
                id,
                c["name"]?.GetValue<string>() ?? id,
                c["tier"]?.GetValue<string>() ?? "—",
                c["revenueYtd"]?.GetValue<double>() ?? 0,
                c["shareOfWallet"]?.GetValue<double>() ?? 0);
        }
        return map;
    }

    private static Dictionary<string, ExposureRow> DominantExposureByClient(JsonArray holdings)
    {
        // First holding (in source order) per client determines its dominant exposure — deterministic.
        var map = new Dictionary<string, ExposureRow>(StringComparer.Ordinal);
        foreach (var h in holdings)
        {
            if (h is null) continue;
            var cid = h["cid"]?.GetValue<string>();
            if (cid is null || map.ContainsKey(cid)) continue;
            map[cid] = new ExposureRow(
                h["exposureType"]?.GetValue<string>() ?? "unknown",
                h["sector"]?.GetValue<string>() ?? "",
                h["cusip"]?.GetValue<string>() ?? "");
        }
        return map;
    }

    private static string ExposureLabel(string exposureType) => exposureType switch
    {
        "long-duration" => "Heavy long-duration bonds",
        "swap-book" => "Interest-rate swaps book",
        "floating-rate" => "Floating-rate notes",
        _ => "Rate-sensitive exposure",
    };

    private static Concern ConcernFor(string exposureType) => exposureType switch
    {
        "long-duration" => new Concern { Label = "Price drop", Kind = "sell" },
        "swap-book" => new Concern { Label = "Hedge adjust", Kind = "warm" },
        "floating-rate" => new Concern { Label = "Reinvest", Kind = "info" },
        _ => new Concern { Label = "Review", Kind = "info" },
    };

    private MorningBrief BuildDegraded(string eventId, List<string> notes)
    {
        notes.Add($"Returned a degraded morning brief for '{eventId}': one or more system-of-record sources were unavailable. Retry once upstream services recover.");
        return new MorningBrief
        {
            Mode = "DEMO",
            AsOf = DateTimeOffset.UtcNow.ToString("o"),
            MarketStrip = [],
            Reasoning =
            [
                new ReasoningStep { Text = "Attempted to pull overnight macro + client data.", Status = "done" },
                new ReasoningStep { Text = "Upstream data unavailable — returned a degraded brief.", Status = "pending" },
            ],
            MacroNarrative = new MacroNarrative
            {
                Summary = "A morning brief could not be fully composed because upstream market/news data was unavailable.",
                WhyItMatters = "No client outreach was generated; the system-of-record services should be retried.",
                Sources = [],
            },
            MostAffectedClients = [],
            Outreach = [],
            Notes = notes,
        };
    }

    private static string FormatBp(int bp) => $"{(bp >= 0 ? "+" : "")}{bp}bp";
    private static string DirFromBp(int bp) => bp > 0 ? "up" : bp < 0 ? "down" : "flat";

    private readonly record struct ClientRow(string Id, string Name, string Tier, double RevenueYtd, double ShareOfWallet);
    private readonly record struct ExposureRow(string ExposureType, string Sector, string Cusip);
}
