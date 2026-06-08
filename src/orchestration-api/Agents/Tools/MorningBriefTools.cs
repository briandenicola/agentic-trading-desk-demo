using System.Text.Json;

namespace OrchestrationApi.Agents.Tools;

/// <summary>
/// LIVE tool functions (T018) wrapping the mock system-of-record endpoints
/// (<c>openapi/tools.yaml</c>) for the Foundry agent. Each method calls the mock API
/// over HTTP via <see cref="MockApiClient"/> (Principle II / FR-002), returns a JSON
/// string the model can consume, and <b>never throws</b> — failures come back as a
/// structured <c>{"error": ...}</c> object so the tool-calling loop can degrade
/// gracefully (FR-011) instead of crashing.
///
/// These are surfaced to the Agent Framework as callable tools by
/// <see cref="OrchestrationApi.Agents.AgentRunner"/>.
/// </summary>
public sealed class MorningBriefTools(MockApiClient mockApi)
{
    /// <summary>MMD/UST levels, equity futures, credit spreads, tone.</summary>
    public Task<string> GetMarketDataAsync(CancellationToken ct = default)
        => GetAsync("/mock/marketdata", ct);

    /// <summary>Resolve a news event into headline/summary/entities/sectors/states.</summary>
    public Task<string> GetNewsAsync(string eventId, CancellationToken ct = default)
        => GetAsync($"/mock/news/{Encode(eventId)}", ct);

    /// <summary>Relative-value / curve context for a market event.</summary>
    public Task<string> GetRelativeValueAsync(string eventId, CancellationToken ct = default)
        => GetAsync($"/mock/marketdata/relval/{Encode(eventId)}", ct);

    /// <summary>All clients with revenue, rankings, share of wallet.</summary>
    public Task<string> GetClientValueAllAsync(CancellationToken ct = default)
        => GetAsync("/mock/tableau/clients", ct);

    /// <summary>One client's revenue, rankings, share of wallet.</summary>
    public Task<string> GetClientValueAsync(string cid, CancellationToken ct = default)
        => GetAsync($"/mock/tableau/clients/{Encode(cid)}", ct);

    /// <summary>Coverage + recent engagement footprint for a client.</summary>
    public Task<string> GetEngagementAsync(string cid, CancellationToken ct = default)
        => GetAsync($"/mock/dynamics/clients/{Encode(cid)}/engagement", ct);

    /// <summary>Live axes / IOIs from the trading book.</summary>
    public Task<string> GetAxesAsync(CancellationToken ct = default)
        => GetAsync("/mock/trading/axes", ct);

    /// <summary>Find clients holding a cusip / state / sector.</summary>
    public Task<string> SearchHoldingsAsync(string? cusip = null, string? state = null, string? sector = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(cusip)) query.Add($"cusip={Encode(cusip)}");
        if (!string.IsNullOrWhiteSpace(state)) query.Add($"state={Encode(state)}");
        if (!string.IsNullOrWhiteSpace(sector)) query.Add($"sector={Encode(sector)}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return GetAsync($"/mock/trading/holdings{qs}", ct);
    }

    /// <summary>Today's new-issue calendar.</summary>
    public Task<string> GetNewIssuesAsync(CancellationToken ct = default)
        => GetAsync("/mock/calendar/newissues", ct);

    /// <summary>Competitive benchmarking for a client.</summary>
    public Task<string> GetCoalitionAsync(string cid, CancellationToken ct = default)
        => GetAsync($"/mock/coalition/{Encode(cid)}", ct);

    /// <summary>Firm rank within a deal sector.</summary>
    public Task<string> GetCoalitionSectorAsync(string sector, CancellationToken ct = default)
        => GetAsync($"/mock/coalition/sector/{Encode(sector)}", ct);

    // ---------------------------------------------------------------- internals

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            using var response = await mockApi.GetAsync(path, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return Error($"GET {path} returned {(int)response.StatusCode}", body);
            }
            return string.IsNullOrWhiteSpace(body) ? "null" : body;
        }
        catch (Exception ex)
        {
            return Error($"GET {path} failed", ex.Message);
        }
    }

    private static string Error(string message, string? detail) =>
        JsonSerializer.Serialize(new { error = message, detail });

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
