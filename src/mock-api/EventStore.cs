using System.Text.Json;
using MockApi.Models;

namespace MockApi;

/// <summary>
/// In-memory event store for the reactive event cockpit (002). Seeds fictional
/// <c>overnight</c> events from <c>Data/events_overnight.json</c> at startup and accepts
/// runtime <c>intraday</c> ingests (Admin inject path). Non-durable by design: admin-injected
/// events are lost on restart while seeded events reload (spec Assumptions). Thread-safe for
/// concurrent reads and ingests. The orchestration layer reaches this store ONLY over HTTP
/// (constitution Principle II).
/// </summary>
public sealed class EventStore
{
    private readonly List<MarketEvent> _events = [];
    private readonly Lock _gate = new();
    private int _adminSeq;

    public EventStore(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "events_overnight.json");
        if (File.Exists(path))
        {
            var seeded = JsonSerializer.Deserialize<List<MarketEvent>>(File.ReadAllText(path), EventJson.Options) ?? [];
            foreach (var e in seeded)
            {
                _events.Add(e with
                {
                    Scope = "overnight",
                    Origin = "seed",
                    IngestedAt = e.IngestedAt ?? e.PublishedAt,
                });
            }
        }
    }

    public bool IsReady
    {
        get { lock (_gate) return _events.Count > 0; }
    }

    /// <summary>All current events, optionally filtered by scope (overnight|intraday).</summary>
    public IReadOnlyList<MarketEvent> List(string? scope = null)
    {
        lock (_gate)
        {
            IEnumerable<MarketEvent> q = _events;
            if (!string.IsNullOrWhiteSpace(scope))
                q = q.Where(e => Eq(e.Scope, scope));
            return q.ToList();
        }
    }

    /// <summary>
    /// Events affecting a given portfolio entity. <paramref name="kind"/> selects which
    /// selector to match (customer|ticker|sector|issuer); when null, matches any selector.
    /// </summary>
    public IReadOnlyList<MarketEvent> ByEntity(string value, string? kind = null)
    {
        lock (_gate)
        {
            return _events.Where(e => Matches(e.AffectedEntities, value, kind)).ToList();
        }
    }

    /// <summary>
    /// Append an intraday event (Admin inject / feed). Server-sets id (when absent),
    /// ingestedAt, scope=intraday. Deduped by normalized headline+type so a re-injected
    /// duplicate is not double-counted; returns the stored (or pre-existing) event and whether
    /// it was newly added.
    /// </summary>
    public (MarketEvent Event, bool Added) Ingest(MarketEvent incoming)
    {
        lock (_gate)
        {
            var existing = _events.FirstOrDefault(e =>
                Eq(e.Headline, incoming.Headline) && Eq(e.Type, incoming.Type));
            if (existing is not null)
                return (existing, false);

            var id = string.IsNullOrWhiteSpace(incoming.Id)
                ? $"evt-{DateTime.UtcNow:yyyyMMdd}-a{++_adminSeq:000}"
                : incoming.Id;

            var stored = incoming with
            {
                Id = id,
                Scope = "intraday",
                Origin = string.IsNullOrWhiteSpace(incoming.Origin) || Eq(incoming.Origin, "seed") ? "admin" : incoming.Origin,
                IngestedAt = DateTime.UtcNow.ToString("O"),
                PublishedAt = incoming.PublishedAt ?? DateTime.UtcNow.ToString("O"),
            };
            _events.Add(stored);
            return (stored, true);
        }
    }

    private static bool Matches(AffectedEntities a, string value, string? kind)
    {
        bool In(IReadOnlyList<string>? list) => list is not null && list.Any(v => Eq(v, value));
        return kind?.ToLowerInvariant() switch
        {
            "customer" or "customerid" => In(a.CustomerIds),
            "ticker" or "security" => In(a.Tickers),
            "sector" => In(a.Sectors),
            "issuer" => In(a.Issuers),
            _ => In(a.CustomerIds) || In(a.Tickers) || In(a.Sectors) || In(a.Issuers),
        };
    }

    private static bool Eq(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
