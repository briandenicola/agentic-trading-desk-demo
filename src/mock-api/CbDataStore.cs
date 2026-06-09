using System.Text.Json;
using MockApi.Models;

namespace MockApi;

/// <summary>
/// Loads the Commercial Banking RM fixtures (<c>Data/cb_*.json</c>) into strongly-typed,
/// in-memory lists and exposes the query helpers the RM daily-briefing flow needs
/// (book by RM, opportunities by stage / close-window / stuck, follow-ups due, active
/// complaints). All data is fictional. Loaded once at startup; this store is additive and
/// does not touch the municipal <see cref="MockDataStore"/>.
/// </summary>
public sealed class CbDataStore
{
    private readonly List<CbCustomer> _customers;
    private readonly List<CbRelationshipManager> _managers;
    private readonly List<CbOpportunity> _opportunities;
    private readonly List<CbComplaint> _complaints;
    private readonly List<CbInteraction> _interactions;

    public CbDataStore(string dataDirectory)
    {
        _customers = Load<CbCustomer>(dataDirectory, "cb_customers.json");
        _managers = Load<CbRelationshipManager>(dataDirectory, "cb_relationship_managers.json");
        _opportunities = Load<CbOpportunity>(dataDirectory, "cb_opportunities.json");
        _complaints = Load<CbComplaint>(dataDirectory, "cb_complaints.json");
        _interactions = Load<CbInteraction>(dataDirectory, "cb_interactions.json");
    }

    public bool IsReady => _customers.Count > 0 && _managers.Count > 0;

    // ---------------------------------------------------------------- Relationship managers

    public IReadOnlyList<CbRelationshipManager> Managers() => _managers;

    public CbRelationshipManager? Manager(string rmId) =>
        _managers.FirstOrDefault(m => Eq(m.RmId, rmId));

    public CbRelationshipManager? ManagerByName(string name) =>
        _managers.FirstOrDefault(m => Eq(m.Name, name));

    // ---------------------------------------------------------------- Customers

    public IEnumerable<CbCustomer> Customers(string? rm = null, string? state = null, string? sector = null, string? region = null)
    {
        IEnumerable<CbCustomer> q = _customers;
        if (!string.IsNullOrWhiteSpace(rm)) q = q.Where(c => Eq(c.RelationshipManager, rm));
        if (!string.IsNullOrWhiteSpace(state)) q = q.Where(c => Eq(c.State, state));
        if (!string.IsNullOrWhiteSpace(sector)) q = q.Where(c => Eq(c.IndustrySector, sector));
        if (!string.IsNullOrWhiteSpace(region)) q = q.Where(c => Eq(c.Region, region));
        return q;
    }

    public CbCustomer? Customer(string customerId) =>
        _customers.FirstOrDefault(c => Eq(c.CustomerId, customerId));

    // ---------------------------------------------------------------- Opportunities

    /// <summary>Open opportunities are anything not in a Closed stage (Closed Won/Lost).</summary>
    public static bool IsOpenStage(string? stage) =>
        !string.IsNullOrWhiteSpace(stage) && !stage.StartsWith("Closed", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<CbOpportunity> Opportunities(
        string? rm = null,
        string? customerId = null,
        string? stage = null,
        bool openOnly = false,
        int? closingWithinDays = null,
        double? minProbability = null,
        int? stuckMinDays = null,
        DateOnly? asOf = null)
    {
        var reference = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        IEnumerable<CbOpportunity> q = _opportunities;

        if (!string.IsNullOrWhiteSpace(rm)) q = q.Where(o => Eq(o.RelationshipManager, rm));
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(o => Eq(o.CustomerId, customerId));
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(o => Eq(o.Stage, stage));
        if (openOnly) q = q.Where(o => IsOpenStage(o.Stage));
        if (minProbability is double mp) q = q.Where(o => (o.ProbabilityPct ?? 0) >= mp);

        if (closingWithinDays is int cw)
        {
            q = q.Where(o =>
                ParseDate(o.ExpectedCloseDate) is DateOnly close &&
                close >= reference && close <= reference.AddDays(cw));
        }

        if (stuckMinDays is int sd)
        {
            // "Stuck" = still open and created more than N days before the reference date.
            q = q.Where(o =>
                IsOpenStage(o.Stage) &&
                ParseDate(o.CreatedDate) is DateOnly created &&
                (reference.DayNumber - created.DayNumber) >= sd);
        }

        return q;
    }

    public int DaysOpen(CbOpportunity o, DateOnly asOf) =>
        ParseDate(o.CreatedDate) is DateOnly created ? Math.Max(0, asOf.DayNumber - created.DayNumber) : 0;

    // ---------------------------------------------------------------- Complaints

    public static bool IsActiveComplaint(string? status) =>
        !string.IsNullOrWhiteSpace(status) && !Eq(status, "Resolved");

    public IEnumerable<CbComplaint> Complaints(
        string? rm = null,
        string? customerId = null,
        string? status = null,
        string? severity = null,
        bool activeOnly = false)
    {
        IEnumerable<CbComplaint> q = _complaints;
        if (!string.IsNullOrWhiteSpace(rm)) q = q.Where(c => Eq(c.RelationshipManager, rm));
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(c => Eq(c.CustomerId, customerId));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => Eq(c.Status, status));
        if (!string.IsNullOrWhiteSpace(severity)) q = q.Where(c => Eq(c.Severity, severity));
        if (activeOnly) q = q.Where(c => IsActiveComplaint(c.Status));
        return q;
    }

    // ---------------------------------------------------------------- Interactions

    public IEnumerable<CbInteraction> Interactions(
        string? rm = null,
        string? customerId = null,
        string? type = null,
        DateOnly? followUpDueBy = null,
        DateOnly? since = null)
    {
        IEnumerable<CbInteraction> q = _interactions;
        if (!string.IsNullOrWhiteSpace(rm)) q = q.Where(i => Eq(i.RelationshipManager, rm));
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(i => Eq(i.CustomerId, customerId));
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(i => Eq(i.Type, type));

        if (followUpDueBy is DateOnly due)
        {
            q = q.Where(i =>
                Eq(i.FollowUpRequired, "Yes") &&
                ParseDate(i.FollowUpDate) is DateOnly f && f <= due);
        }

        if (since is DateOnly s)
        {
            q = q.Where(i => ParseDate(i.Date) is DateOnly d && d >= s);
        }

        return q;
    }

    // ---------------------------------------------------------------- RM book overview (aggregate)

    /// <summary>
    /// A book-level snapshot for one RM: the manager, their customers, and the headline KPIs the
    /// daily briefing leads with (exposure, deposits, open pipeline, near-term closes, active
    /// complaints, follow-ups due). The agent uses this as the entry point, then drills in.
    /// </summary>
    public object? RmBook(string rmId, DateOnly? asOf = null)
    {
        var rm = Manager(rmId);
        if (rm is null) return null;
        var reference = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var name = rm.Name ?? "";

        var customers = Customers(rm: name).ToList();
        var openOpps = Opportunities(rm: name, openOnly: true, asOf: reference).ToList();
        var closing14 = openOpps.Where(o =>
            ParseDate(o.ExpectedCloseDate) is DateOnly c && c >= reference && c <= reference.AddDays(14)).ToList();
        var activeComplaints = Complaints(rm: name, activeOnly: true).ToList();
        var followUpsDue = Interactions(rm: name, followUpDueBy: reference).ToList();

        return new
        {
            relationshipManager = rm,
            asOf = reference.ToString("yyyy-MM-dd"),
            kpis = new
            {
                customerCount = customers.Count,
                totalExposureMm = Math.Round(customers.Sum(c => c.TotalExposureMm ?? 0), 1),
                totalDepositsMm = Math.Round(customers.Sum(c => c.DepositBalanceMm ?? 0), 1),
                openOpportunityCount = openOpps.Count,
                openPipelineAmount = openOpps.Sum(o => o.Amount ?? 0),
                closingWithin14DaysCount = closing14.Count,
                activeComplaintCount = activeComplaints.Count,
                followUpsDueCount = followUpsDue.Count,
            },
            customers,
        };
    }

    // ---------------------------------------------------------------- helpers

    private static bool Eq(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Fixtures are ISO yyyy-MM-dd; tolerate a trailing time component just in case.
        var datePart = value.Length >= 10 ? value[..10] : value;
        return DateOnly.TryParse(datePart, out var d) ? d : null;
    }

    private static List<T> Load<T>(string dir, string file)
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path)) return [];
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json, CbJson.Options) ?? [];
    }
}
