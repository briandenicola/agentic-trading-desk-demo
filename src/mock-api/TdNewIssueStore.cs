using System.Text.Json;
using MockApi.Models;

namespace MockApi;

/// <summary>
/// In-memory store of primary new-issue deals and OUR desk's syndicate role on each
/// (lead-left bookrunner, joint bookrunner, co-manager). Seeds fictional deals from
/// <c>Data/td_new_issues.json</c> at startup and accepts runtime uploads (the New Issue
/// Radar "upload a spreadsheet of possible lead-left deals" path). Non-durable by design:
/// uploaded deals are lost on restart while seeded deals reload. Thread-safe for concurrent
/// reads and ingests. The orchestration layer reaches this store ONLY over HTTP
/// (constitution Principle II). All data is fictional.
/// </summary>
public sealed class TdNewIssueStore
{
    private readonly List<TdNewIssue> _deals = [];
    private readonly Lock _gate = new();
    private int _uploadSeq;

    public TdNewIssueStore(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "td_new_issues.json");
        if (File.Exists(path))
        {
            var seeded = JsonSerializer.Deserialize<List<TdNewIssue>>(File.ReadAllText(path), CbJson.Options) ?? [];
            foreach (var d in seeded)
            {
                _deals.Add(d with { Source = string.IsNullOrWhiteSpace(d.Source) ? "seed" : d.Source });
            }
        }
    }

    public bool IsReady
    {
        get { lock (_gate) return _deals.Count > 0; }
    }

    /// <summary>
    /// All known deals, optionally filtered to a given issuer (case-insensitive contains) or to
    /// deals whose tranches include a given security id.
    /// </summary>
    public IReadOnlyList<TdNewIssue> List(string? issuer = null, string? securityId = null)
    {
        lock (_gate)
        {
            IEnumerable<TdNewIssue> q = _deals;
            if (!string.IsNullOrWhiteSpace(issuer))
                q = q.Where(d => Contains(d.Issuer, issuer));
            if (!string.IsNullOrWhiteSpace(securityId))
                q = q.Where(d => d.TrancheSecurityIds is not null && d.TrancheSecurityIds.Any(s => Eq(s, securityId)));
            return q.ToList();
        }
    }

    /// <summary>
    /// Ingest uploaded deals (from the parsed spreadsheet). Each is server-stamped
    /// <c>source=upload</c> and given a deal id when absent. A deal replaces an existing one with
    /// the same id or issuer (so re-uploading or overriding the seeded deal is idempotent).
    /// Returns how many were added vs. updated and the full current list.
    /// </summary>
    public (int Added, int Updated, IReadOnlyList<TdNewIssue> Deals) Ingest(IEnumerable<TdNewIssue> incoming)
    {
        lock (_gate)
        {
            int added = 0, updated = 0;
            foreach (var raw in incoming)
            {
                if (raw is null || string.IsNullOrWhiteSpace(raw.Issuer)) continue;

                var dealId = string.IsNullOrWhiteSpace(raw.DealId)
                    ? $"NI-up{++_uploadSeq:000}"
                    : raw.DealId;
                var deal = raw with { DealId = dealId, Source = "upload" };

                var idx = _deals.FindIndex(d => Eq(d.DealId, dealId) || Eq(d.Issuer, deal.Issuer));
                if (idx >= 0) { _deals[idx] = deal; updated++; }
                else { _deals.Add(deal); added++; }
            }
            return (added, updated, _deals.ToList());
        }
    }

    private static bool Eq(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? a, string? b) =>
        a is not null && b is not null && a.Contains(b, StringComparison.OrdinalIgnoreCase);
}
