using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;
using static OrchestrationApi.Agents.Demo.RmCallScorer;

namespace OrchestrationApi.Agents.Demo;

/// <summary>
/// Deterministic, offline DEMO composer for the Commercial Banking RM Daily Briefing —
/// the PRIMARY "Morning Planning &amp; Prioritized Outreach" scene (course correction).
/// Pulls the RM book, open pipeline, active complaints and due follow-ups from the mock
/// system-of-record API <b>over HTTP only</b> (constitution Principle II / FR-002 — never
/// reads the mock fixtures in-process), scores every customer with
/// <see cref="RmCallScorer"/>, and assembles a <see cref="RmBriefing"/> that mirrors the
/// client ground-truth sample <c>assets/rm_daily_briefing_2026-05-14.html</c>.
///
/// Output is deterministic: stable ordering and fixed text so repeated DEMO runs with the
/// same inputs are byte-identical (SC-002). No model and no credentials are used.
///
/// Resilience (FR-011): every upstream call is wrapped; if the RM cannot be resolved the
/// composer throws <see cref="UnknownRelationshipManagerException"/> (→ 400), and if other
/// data is missing it degrades to a structured brief carrying a <c>notes</c> entry.
/// </summary>
public sealed class RmBriefingComposer(MockApiClient mockApi, EventTools eventTools)
{
    public const string DefaultRmId = "RM-104";
    public const string DefaultDate = "2026-05-14";
    private const int TopCalls = 8;

    public async Task<RmBriefing> ComposeAsync(string rmId, string? date, CancellationToken ct = default)
    {
        var notes = new List<string>();
        var asOf = ParseDate(date) ?? DateOnly.Parse(DefaultDate, CultureInfo.InvariantCulture);

        var book = await GetBookAsync(rmId, asOf, notes, ct);
        if (book is null)
        {
            return BuildDegraded(rmId, asOf, notes);
        }

        var rm = book["relationshipManager"];
        var rmName = Str(rm, "name") ?? "";
        var kpis = book["kpis"];

        var opps = await TryGetArrayAsync($"/mock/cb/opportunities?rm={Encode(rmName)}&openOnly=true&asOf={asOf:yyyy-MM-dd}", notes, ct);
        var complaints = await TryGetArrayAsync($"/mock/cb/complaints?rm={Encode(rmName)}&activeOnly=true", notes, ct);
        var followUps = await TryGetArrayAsync($"/mock/cb/interactions?rm={Encode(rmName)}&followUpDueBy={asOf.AddDays(FollowUpSoonDays):yyyy-MM-dd}", notes, ct);

        var customers = (book["customers"] as JsonArray) ?? [];
        var customerById = IndexCustomers(customers);

        // Reactive overlay (002): pull the current event set over HTTP and let it nudge ranking.
        var events = await GetEventsAsync(notes, ct);

        // Group the signals by customer once.
        var complaintsByCustomer = GroupBy(complaints, "customerId");
        var oppsByCustomer = GroupBy(opps, "customerId");
        var followUpsByCustomer = GroupBy(followUps, "customerId");

        var scored = new List<ScoredCustomer>();
        foreach (var (cid, cust) in customerById)
        {
            var sig = BuildSignals(cid, asOf, complaintsByCustomer, oppsByCustomer, followUpsByCustomer);
            var baseScore = Score(sig.Escalated.Count, sig.InProgress.Count, sig.FollowUp, sig.StuckOpps.Count, sig.ClosingOpps.Count);

            var drivingEvents = EventImpactResolver.ResolveForCustomer(cid, Str(cust, "industrySector"), events);
            var eventDelta = EventImpactResolver.NetContribution(drivingEvents);

            var score = baseScore + eventDelta;
            if (score <= 0) continue;
            scored.Add(new ScoredCustomer(cid, cust, sig, score, drivingEvents));
        }

        var ranked = scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => Dbl(s.Customer, "totalExposureMm") ?? 0)
            .ThenBy(s => s.Cid, StringComparer.Ordinal)
            .Take(TopCalls)
            .ToList();

        var priorityCallList = ranked
            .Select((s, i) => BuildPriorityCall(s, rank: i + 1, asOf))
            .ToList();

        if (priorityCallList.Count == 0)
        {
            notes.Add($"No actionable signals (complaints, due follow-ups, stuck or closing pipeline) for {rmName} as of {asOf:yyyy-MM-dd}.");
        }

        var yesterdayTouchpoints = await CountYesterdayTouchpointsAsync(rmName, asOf, notes, ct);
        notes.Add("Macro snapshot is illustrative DEMO content; in LIVE mode it is sourced from current market indicators.");

        return new RmBriefing
        {
            Mode = "DEMO",
            AsOf = asOf.ToString("yyyy-MM-dd"),
            Greeting = $"Good morning, {FirstName(rmName)}",
            Rm = new RmIdentity
            {
                RmId = Str(rm, "rmId") ?? rmId,
                Name = rmName,
                Title = Str(rm, "title"),
                Territory = Str(rm, "territory"),
            },
            Portfolio = new RmPortfolio
            {
                CustomerCount = (int)(Dbl(kpis, "customerCount") ?? customerById.Count),
                TotalExposureMm = Round1(Dbl(kpis, "totalExposureMm") ?? 0),
                TotalDepositsMm = Round1(Dbl(kpis, "totalDepositsMm") ?? 0),
            },
            Kpis = new RmKpis
            {
                YesterdayTouchpoints = yesterdayTouchpoints,
                OpenPipelineCount = (int)(Dbl(kpis, "openOpportunityCount") ?? 0),
                OpenPipelineAmountMm = Round1((Dbl(kpis, "openPipelineAmount") ?? 0) / 1_000_000d),
                ClosingWithin14Days = (int)(Dbl(kpis, "closingWithin14DaysCount") ?? 0),
                ActiveComplaints = (int)(Dbl(kpis, "activeComplaintCount") ?? 0),
            },
            Reasoning = BuildReasoning(),
            PriorityCallList = priorityCallList,
            ComplaintsSnapshot = BuildComplaintsSnapshot(complaints),
            PipelineClosing = BuildPipelineClosing(opps, asOf),
            MacroSnapshot = BuildMacroSnapshot(),
            SuggestedFirstAction = BuildSuggestedFirstAction(priorityCallList),
            Notes = notes.Count > 0 ? notes : null,
            EventsConsidered = events,
        };
    }

    // ---------------------------------------------------------------- signal assembly

    private sealed record CustomerSignals(
        List<JsonNode> Escalated,
        List<JsonNode> InProgress,
        FollowUpUrgency FollowUp,
        JsonNode? FollowUpInteraction,
        List<JsonNode> StuckOpps,
        List<JsonNode> ClosingOpps);

    private sealed record ScoredCustomer(string Cid, JsonNode Customer, CustomerSignals Signals, int Score, IReadOnlyList<EventLinkage> DrivingEvents);

    private static CustomerSignals BuildSignals(
        string cid,
        DateOnly asOf,
        Dictionary<string, List<JsonNode>> complaintsByCustomer,
        Dictionary<string, List<JsonNode>> oppsByCustomer,
        Dictionary<string, List<JsonNode>> followUpsByCustomer)
    {
        var custComplaints = complaintsByCustomer.GetValueOrDefault(cid) ?? [];
        var escalated = custComplaints.Where(c => Eq(Str(c, "status"), "Escalated")).ToList();
        var inProgress = custComplaints.Where(c => !Eq(Str(c, "status"), "Escalated")).ToList();

        var custOpps = oppsByCustomer.GetValueOrDefault(cid) ?? [];
        var closingOpps = custOpps.Where(o => ClosingDays(o, asOf) is int d && d >= 0 && d <= ClosingWindowDays).ToList();
        var closingIds = closingOpps.Select(o => Str(o, "opportunityId")).ToHashSet(StringComparer.Ordinal);
        var stuckOpps = custOpps
            .Where(o => !closingIds.Contains(Str(o, "opportunityId")))
            .Where(o => DaysOpen(o, asOf) >= StuckMinDays)
            .ToList();

        // Most-urgent recent follow-up for this customer.
        var followUp = FollowUpUrgency.None;
        JsonNode? followUpNode = null;
        foreach (var i in followUpsByCustomer.GetValueOrDefault(cid) ?? [])
        {
            if (!Eq(Str(i, "followUpRequired"), "Yes")) continue;
            if (ParseDate(Str(i, "followUpDate")) is not DateOnly f) continue;
            var urgency = ClassifyFollowUp(f, asOf);
            if (urgency > followUp)
            {
                followUp = urgency;
                followUpNode = i;
            }
        }

        return new CustomerSignals(escalated, inProgress, followUp, followUpNode, stuckOpps, closingOpps);
    }

    // ---------------------------------------------------------------- card construction

    private static PriorityCall BuildPriorityCall(ScoredCustomer s, int rank, DateOnly asOf)
    {
        var c = s.Customer;
        var sig = s.Signals;

        var tags = new List<CallTag>();
        if (sig.Escalated.Count > 0) tags.Add(new CallTag { Label = "ESCALATED", Kind = "escalated" });
        if (sig.InProgress.Count > 0) tags.Add(new CallTag { Label = "IN PROGRESS", Kind = "in-progress" });
        if (sig.FollowUp != FollowUpUrgency.None) tags.Add(new CallTag { Label = FollowUpTagLabel(sig, asOf), Kind = "followup" });
        if (sig.ClosingOpps.Count > 0)
        {
            var minDays = sig.ClosingOpps.Min(o => ClosingDays(o, asOf) ?? 0);
            tags.Add(new CallTag { Label = $"CLOSING {minDays} DAYS", Kind = "closing" });
        }
        if (sig.StuckOpps.Count > 0)
        {
            var label = sig.StuckOpps.Count == 1
                ? $"STUCK {DaysOpen(sig.StuckOpps[0], asOf)}D"
                : $"{sig.StuckOpps.Count} STUCK OPPS";
            tags.Add(new CallTag { Label = label, Kind = "stuck" });
        }
        if (s.DrivingEvents.Count > 0)
        {
            var label = s.DrivingEvents.Count == 1 ? "EVENT" : $"{s.DrivingEvents.Count} EVENTS";
            tags.Add(new CallTag { Label = label, Kind = "event" });
        }

        return new PriorityCall
        {
            Rank = rank,
            Priority = PriorityBand(rank),
            CustomerId = s.Cid,
            CustomerName = Str(c, "legalName") ?? Str(c, "dba") ?? s.Cid,
            IndustrySector = Str(c, "industrySector"),
            HqCity = Str(c, "hqCity"),
            State = Str(c, "state"),
            AnnualRevenueMm = Dbl(c, "annualRevenueMm"),
            RiskRating = Str(c, "riskRating"),
            Score = s.Score,
            Tags = tags,
            Reasons = BuildReasons(sig, asOf, s.DrivingEvents),
            SuggestedAction = BuildSuggestedAction(c, sig, asOf),
            DrivingEvents = s.DrivingEvents,
        };
    }

    private static List<string> BuildReasons(CustomerSignals sig, DateOnly asOf, IReadOnlyList<EventLinkage> drivingEvents)
    {
        var reasons = new List<string>();

        foreach (var cmp in sig.Escalated.Concat(sig.InProgress))
        {
            var status = Str(cmp, "status") ?? "Open";
            reasons.Add(
                $"{status} complaint {Str(cmp, "complaintId")} — {Str(cmp, "category")}: {Str(cmp, "description")} (filed {Str(cmp, "dateFiled")}).");
        }

        if (sig.FollowUpInteraction is JsonNode fu)
        {
            var when = FollowUpPhrase(sig.FollowUp, ParseDate(Str(fu, "followUpDate")), asOf);
            reasons.Add($"Follow-up {when} from {Str(fu, "interactionId")} (\"{Str(fu, "subject")}\").");
        }

        foreach (var o in sig.ClosingOpps.OrderBy(o => ClosingDays(o, asOf) ?? int.MaxValue))
        {
            reasons.Add(
                $"{Str(o, "opportunityId")} — {Str(o, "opportunityName")} {Money(Dbl(o, "amount"))}, {Str(o, "stage")}, expected close {Str(o, "expectedCloseDate")} ({ClosingDays(o, asOf)} days).");
        }

        foreach (var o in sig.StuckOpps.OrderByDescending(o => DaysOpen(o, asOf)))
        {
            reasons.Add(
                $"{Str(o, "opportunityId")} — {Str(o, "opportunityName")} {Money(Dbl(o, "amount"))}, stuck in {Str(o, "stage")} {DaysOpen(o, asOf)} days.");
        }

        foreach (var ev in drivingEvents)
        {
            reasons.Add(ev.Rationale);
        }

        return reasons;
    }

    private static string BuildSuggestedAction(JsonNode c, CustomerSignals sig, DateOnly asOf)
    {
        var city = Str(c, "hqCity") ?? "the";
        if (sig.Escalated.Count > 0)
        {
            var push = sig.StuckOpps.Count > 0
                ? $" Use the call to also push {sig.StuckOpps.Count} stuck proposal(s) totalling {Money(sig.StuckOpps.Sum(o => Dbl(o, "amount") ?? 0))} over the line."
                : string.Empty;
            return $"Call the {city} CFO this morning. Acknowledge the open complaint(s), commit to a written resolution timeline, then re-engage on the pipeline.{push}";
        }
        if (sig.ClosingOpps.Count > 0)
        {
            var top = sig.ClosingOpps.OrderBy(o => ClosingDays(o, asOf) ?? int.MaxValue).First();
            return $"Confirm pricing and final terms on {Str(top, "opportunityName")} — {Money(Dbl(top, "amount"))} closing in {ClosingDays(top, asOf)} days is your highest-value near-term win.";
        }
        if (sig.FollowUp == FollowUpUrgency.Overdue)
        {
            return "Catch up on the overdue follow-up today before it slips further.";
        }
        if (sig.FollowUp is FollowUpUrgency.Today or FollowUpUrgency.Soon)
        {
            return "Send the follow-up note this morning and confirm next steps.";
        }
        if (sig.StuckOpps.Count > 0)
        {
            var sum = sig.StuckOpps.Sum(o => Dbl(o, "amount") ?? 0);
            return $"{Money(sum)} sitting unactioned across {sig.StuckOpps.Count} stuck deal(s) — schedule a structured proposal walk-through this week, or close the file.";
        }
        return "Reach out to keep the relationship warm.";
    }

    private static string BuildSuggestedFirstAction(IReadOnlyList<PriorityCall> calls)
    {
        if (calls.Count == 0)
        {
            return "No urgent outreach today. Use the time for proactive relationship calls and pipeline hygiene.";
        }
        var top = calls[0];
        return $"{top.SuggestedAction} This is your single biggest relationship action today — {top.CustomerName} (priority score {top.Score}).";
    }

    private static List<ReasoningStep> BuildReasoning() =>
    [
        new() { Text = "Pulled the RM book: customers, exposure, deposits and open pipeline.", Status = "done" },
        new() { Text = "Scanned active complaints, due follow-ups and stuck/closing opportunities per customer.", Status = "done" },
        new() { Text = "Scored each customer (complaints, follow-ups, pipeline urgency) into a ranked call list.", Status = "done" },
        new() { Text = "Generated a suggested action per customer and a single first action for the day.", Status = "done" },
    ];

    private static List<ComplaintSnapshot> BuildComplaintsSnapshot(JsonArray complaints) =>
        complaints
            .Where(c => c is not null)
            .Select(c => new ComplaintSnapshot
            {
                ComplaintId = Str(c, "complaintId") ?? "",
                CustomerName = Str(c, "customerName") ?? "",
                Category = Str(c, "category"),
                Severity = Str(c, "severity"),
                Status = Str(c, "status") ?? "",
                DateFiled = Str(c, "dateFiled"),
            })
            .OrderByDescending(c => string.Equals(c.Status, "Escalated", StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.DateFiled, StringComparer.Ordinal)
            .ToList();

    private static List<PipelineClose> BuildPipelineClosing(JsonArray opps, DateOnly asOf) =>
        opps
            .Where(o => o is not null && ClosingDays(o, asOf) is int d && d >= 0 && d <= ClosingWindowDays)
            .OrderBy(o => ClosingDays(o, asOf) ?? int.MaxValue)
            .Select(o => new PipelineClose
            {
                OpportunityId = Str(o, "opportunityId") ?? "",
                CustomerName = Str(o, "customerName") ?? "",
                ProductType = Str(o, "productType"),
                Stage = Str(o, "stage"),
                AmountMm = Round1((Dbl(o, "amount") ?? 0) / 1_000_000d),
                ExpectedCloseDate = Str(o, "expectedCloseDate"),
            })
            .ToList();

    // Illustrative, deterministic macro context for DEMO mode (no live web/market pull offline).
    private static List<MacroBullet> BuildMacroSnapshot() =>
    [
        new() { Headline = "10Y Treasury 4.47%", Detail = "Near YTD high after a hot PPI print — supports floating-to-fixed conversations with borrowers." },
        new() { Headline = "Curve steepening, 2s10s ~+49 bps", Detail = "Front end has richened off the inversion; revisit hedging on swap-heavy books." },
        new() { Headline = "CPI 3.8% y/y, core 2.8%", Detail = "Inflation drifting higher; expect continued cost pressure for thin-margin customers." },
        new() { Headline = "ISM Manufacturing 52.7", Detail = "Expansionary backdrop — constructive for manufacturing and equipment-finance demand." },
    ];

    // ---------------------------------------------------------------- upstream access

    private async Task<JsonNode?> GetBookAsync(string rmId, DateOnly asOf, List<string> notes, CancellationToken ct)
    {
        var path = $"/mock/cb/relationship-managers/{Encode(rmId)}/book?asOf={asOf:yyyy-MM-dd}";
        try
        {
            using var response = await mockApi.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new UnknownRelationshipManagerException(rmId);
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        }
        catch (UnknownRelationshipManagerException)
        {
            throw;
        }
        catch (Exception)
        {
            notes.Add($"Upstream data unavailable: GET {path} failed; returning a degraded briefing.");
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
                notes.Add($"Reactive overlay: {events.Count} current event(s) weighed into the call ranking.");
            }
            return events;
        }
        catch (Exception)
        {
            notes.Add("Event store unavailable: the briefing was composed from portfolio signals only.");
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

    private async Task<int> CountYesterdayTouchpointsAsync(string rmName, DateOnly asOf, List<string> notes, CancellationToken ct)
    {
        var yesterday = asOf.AddDays(-1);
        var arr = await TryGetArrayAsync($"/mock/cb/interactions?rm={Encode(rmName)}&since={yesterday:yyyy-MM-dd}", notes, ct);
        return arr.Count(i => ParseDate(Str(i, "date")) == yesterday);
    }

    private RmBriefing BuildDegraded(string rmId, DateOnly asOf, List<string> notes)
    {
        notes.Add($"Returned a degraded RM briefing for '{rmId}': the system-of-record was unavailable. Retry once upstream services recover.");
        return new RmBriefing
        {
            Mode = "DEMO",
            AsOf = asOf.ToString("yyyy-MM-dd"),
            Greeting = "Good morning",
            Rm = new RmIdentity { RmId = rmId, Name = "" },
            Portfolio = new RmPortfolio { CustomerCount = 0, TotalExposureMm = 0, TotalDepositsMm = 0 },
            Kpis = new RmKpis { YesterdayTouchpoints = 0, OpenPipelineCount = 0, OpenPipelineAmountMm = 0, ClosingWithin14Days = 0, ActiveComplaints = 0 },
            Reasoning =
            [
                new ReasoningStep { Text = "Attempted to pull the RM book and signals.", Status = "done" },
                new ReasoningStep { Text = "Upstream data unavailable — returned a degraded briefing.", Status = "pending" },
            ],
            PriorityCallList = [],
            ComplaintsSnapshot = [],
            PipelineClosing = [],
            MacroSnapshot = BuildMacroSnapshot(),
            SuggestedFirstAction = "Briefing could not be composed — retry once the system-of-record services recover.",
            Notes = notes,
        };
    }

    // ---------------------------------------------------------------- formatting helpers

    private static string FollowUpTagLabel(CustomerSignals sig, DateOnly asOf) => sig.FollowUp switch
    {
        FollowUpUrgency.Overdue => "FOLLOW-UP OVERDUE",
        FollowUpUrgency.Today => "FOLLOW-UP DUE TODAY",
        FollowUpUrgency.Soon => DaysAhead(sig.FollowUpInteraction, asOf) == 1 ? "FOLLOW-UP DUE TOMORROW" : $"FOLLOW-UP DUE IN {DaysAhead(sig.FollowUpInteraction, asOf)} DAYS",
        _ => "FOLLOW-UP",
    };

    private static string FollowUpPhrase(FollowUpUrgency urgency, DateOnly? followUp, DateOnly asOf) => urgency switch
    {
        FollowUpUrgency.Overdue => followUp is DateOnly f ? $"overdue since {f:yyyy-MM-dd} ({asOf.DayNumber - f.DayNumber} days)" : "overdue",
        FollowUpUrgency.Today => "due today",
        FollowUpUrgency.Soon => followUp is DateOnly f ? $"due {f:yyyy-MM-dd}" : "due soon",
        _ => "scheduled",
    };

    private static int DaysAhead(JsonNode? interaction, DateOnly asOf) =>
        ParseDate(Str(interaction, "followUpDate")) is DateOnly f ? Math.Max(0, f.DayNumber - asOf.DayNumber) : 0;

    private static string FirstName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "" : name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    private static string Money(double? amount)
    {
        var mm = (amount ?? 0) / 1_000_000d;
        return mm >= 100 || mm == Math.Floor(mm)
            ? $"${mm:0.#}M"
            : $"${mm:0.0}M";
    }

    private static double Round1(double v) => Math.Round(v, 1);

    // ---------------------------------------------------------------- JSON accessors

    private static Dictionary<string, JsonNode> IndexCustomers(JsonArray customers)
    {
        var map = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var c in customers)
        {
            if (c is null) continue;
            var id = Str(c, "customerId");
            if (id is not null) map[id] = c;
        }
        return map;
    }

    private static Dictionary<string, List<JsonNode>> GroupBy(JsonArray items, string key)
    {
        var map = new Dictionary<string, List<JsonNode>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null) continue;
            var k = Str(item, key);
            if (k is null) continue;
            (map.TryGetValue(k, out var list) ? list : map[k] = []).Add(item);
        }
        return map;
    }

    private static int? ClosingDays(JsonNode? o, DateOnly asOf) =>
        ParseDate(Str(o, "expectedCloseDate")) is DateOnly close ? close.DayNumber - asOf.DayNumber : null;

    private static int DaysOpen(JsonNode? o, DateOnly asOf) =>
        ParseDate(Str(o, "createdDate")) is DateOnly created ? Math.Max(0, asOf.DayNumber - created.DayNumber) : 0;

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
