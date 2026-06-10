using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.Tools;

/// <summary>
/// HTTP tool wrappers over the mock-api event store (<c>/mock/events</c>,
/// 002-reactive-event-cockpit). The orchestration layer reaches the event store ONLY
/// through this HTTP seam (constitution Principle II / FR-004) — it never reads the
/// mock fixtures in-process. Each method degrades gracefully (returns empty / null on a
/// non-success response) so a transient mock-api hiccup never throws the composer.
/// All data is fictional.
/// </summary>
public sealed class EventTools(MockApiClient mockApi)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>List all current events (optionally filter by <paramref name="scope"/>: overnight | intraday).</summary>
    public async Task<IReadOnlyList<MarketEvent>> ListEventsAsync(string? scope = null, CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(scope) ? "/mock/events" : $"/mock/events?scope={Uri.EscapeDataString(scope)}";
        return await GetListAsync(path, ct);
    }

    /// <summary>Events affecting one portfolio entity (kind: customer | ticker | sector | issuer; default any).</summary>
    public async Task<IReadOnlyList<MarketEvent>> GetEventsByEntityAsync(string value, string? kind = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var path = $"/mock/events/by-entity?value={Uri.EscapeDataString(value)}";
        if (!string.IsNullOrWhiteSpace(kind)) path += $"&kind={Uri.EscapeDataString(kind)}";
        return await GetListAsync(path, ct);
    }

    /// <summary>Ingest an intraday event. Returns the stored event and whether it was newly added (vs deduped).</summary>
    public async Task<EventIngestResult> IngestEventAsync(MarketEvent submission, CancellationToken ct = default)
    {
        using var response = await mockApi.PostJsonAsync("/mock/events", submission, ct);
        if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
        {
            return new EventIngestResult(null, false, false);
        }

        var payload = await response.Content.ReadFromJsonAsync<IngestEnvelope>(Json, ct);
        return new EventIngestResult(payload?.Event, payload?.Added ?? false, true);
    }

    private async Task<IReadOnlyList<MarketEvent>> GetListAsync(string path, CancellationToken ct)
    {
        using var response = await mockApi.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode) return [];
        var events = await response.Content.ReadFromJsonAsync<List<MarketEvent>>(Json, ct);
        return events ?? [];
    }

    private sealed record IngestEnvelope(MarketEvent? Event, bool Added);
}

/// <summary>Outcome of an ingest: the stored event, whether it was newly added, and whether the call succeeded.</summary>
public sealed record EventIngestResult(MarketEvent? Event, bool Added, bool Succeeded);
