using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockApi.Models;

/// <summary>
/// Fictional market/news event records for the reactive event cockpit
/// (002-reactive-event-cockpit). Seeded overnight from <c>Data/events_overnight.json</c>
/// and appended to at runtime via the Admin inject path. All data is fictional.
/// Serialized camelCase, omitting null fields, like the other mock datasets.
/// </summary>
public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>A single fictional event in the store.</summary>
public sealed record MarketEvent
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";              // macro_rate | sector | issuer_credit | client_headline
    public string Headline { get; init; } = "";
    public string Summary { get; init; } = "";
    public string? Source { get; init; }
    public string Severity { get; init; } = "medium";    // low | medium | high
    public string? PublishedAt { get; init; }            // ISO-8601
    public string? IngestedAt { get; init; }             // ISO-8601 (server-set)
    public string Scope { get; init; } = "overnight";    // overnight | intraday
    public string Origin { get; init; } = "seed";        // seed | admin | feed
    public string? Direction { get; init; }              // positive | negative | neutral
    public AffectedEntities AffectedEntities { get; init; } = new();
}

/// <summary>Typed selectors mapping an event to portfolio entities across both scenes.</summary>
public sealed record AffectedEntities
{
    public IReadOnlyList<string>? CustomerIds { get; init; }   // CB customers (RM scene)
    public IReadOnlyList<string>? Tickers { get; init; }       // TD securityIds (Trading scene)
    public IReadOnlyList<string>? Sectors { get; init; }       // CB + TD sectors (both scenes)
    public IReadOnlyList<string>? Issuers { get; init; }       // TD issuers (Trading scene)

    public bool HasAny =>
        (CustomerIds is { Count: > 0 }) ||
        (Tickers is { Count: > 0 }) ||
        (Sectors is { Count: > 0 }) ||
        (Issuers is { Count: > 0 });
}
