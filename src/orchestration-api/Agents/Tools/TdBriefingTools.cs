using System.Text.Json;

namespace OrchestrationApi.Agents.Tools;

/// <summary>
/// LIVE tool functions wrapping the Trading Desk mock endpoints (<c>/mock/td/*</c>) for the
/// Foundry institutional-sales morning-briefing agent. Each method calls the mock API over
/// HTTP via <see cref="MockApiClient"/> (Principle II / FR-002), returns a JSON string the
/// model can consume, and <b>never throws</b> — failures come back as a structured
/// <c>{"error": ...}</c> object so the tool-calling loop degrades gracefully (FR-011).
/// Surfaced to the Agent Framework by <see cref="OrchestrationApi.Agents.TdAgentRunner"/>.
/// </summary>
public sealed class TdBriefingTools(MockApiClient mockApi)
{
    /// <summary>Institutional clients, optionally filtered by coverage salesperson / type / region / asset class.</summary>
    public Task<string> GetClientsAsync(string? salesperson = null, string? type = null, string? region = null, string? assetClass = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/clients{Query(("salesperson", salesperson), ("type", type), ("region", region), ("assetClass", assetClass))}", ct);

    /// <summary>One client's master record.</summary>
    public Task<string> GetClientAsync(string clientId, CancellationToken ct = default)
        => GetAsync($"/mock/td/clients/{Encode(clientId)}", ct);

    /// <summary>360° activity for one client over a trailing window: holdings, trades, RFQs, inquiries, CRM.</summary>
    public Task<string> GetClientActivityAsync(string clientId, string? since = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/clients/{Encode(clientId)}/activity{Query(("since", since))}", ct);

    /// <summary>One client's current position snapshot.</summary>
    public Task<string> GetClientHoldingsAsync(string clientId, CancellationToken ct = default)
        => GetAsync($"/mock/td/clients/{Encode(clientId)}/holdings", ct);

    /// <summary>Security master, optionally filtered by asset class / sector / issuer / region.</summary>
    public Task<string> GetSecuritiesAsync(string? assetClass = null, string? sector = null, string? issuer = null, string? region = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/securities{Query(("assetClass", assetClass), ("sector", sector), ("issuer", issuer), ("region", region))}", ct);

    /// <summary>One security's master record.</summary>
    public Task<string> GetSecurityAsync(string securityId, CancellationToken ct = default)
        => GetAsync($"/mock/td/securities/{Encode(securityId)}", ct);

    /// <summary>Everything pointing at one security: inventory/axes, holders, trades, RFQs, inquiries, news, research.</summary>
    public Task<string> GetSecurityInterestAsync(string securityId, string? since = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/securities/{Encode(securityId)}/interest{Query(("since", since))}", ct);

    /// <summary>Executed trades, filterable by client / security / direction / since.</summary>
    public Task<string> GetTradesAsync(string? clientId = null, string? securityId = null, string? direction = null, string? since = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/trades{Query(("clientId", clientId), ("securityId", securityId), ("direction", direction), ("since", since))}", ct);

    /// <summary>Request-for-quote activity, filterable by client / security / response status / since.</summary>
    public Task<string> GetRfqsAsync(string? clientId = null, string? securityId = null, string? status = null, string? since = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/rfqs{Query(("clientId", clientId), ("securityId", securityId), ("status", status), ("since", since))}", ct);

    /// <summary>CRM call reports, filterable by client / urgency / since.</summary>
    public Task<string> GetCrmAsync(string? clientId = null, string? urgency = null, string? since = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/crm{Query(("clientId", clientId), ("urgency", urgency), ("since", since))}", ct);

    /// <summary>Dealer inventory / market-making axes, filterable by security / desk.</summary>
    public Task<string> GetInventoryAsync(string? securityId = null, string? desk = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/inventory{Query(("securityId", securityId), ("desk", desk))}", ct);

    /// <summary>Less-structured client inquiries, filterable by client / security / sentiment / since.</summary>
    public Task<string> GetInquiriesAsync(string? clientId = null, string? securityId = null, string? sentiment = null, string? since = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/inquiries{Query(("clientId", clientId), ("securityId", securityId), ("sentiment", sentiment), ("since", since))}", ct);

    /// <summary>Market-moving news, filterable by security / sector / macro theme / since.</summary>
    public Task<string> GetNewsAsync(string? securityId = null, string? sector = null, string? macroTheme = null, string? since = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/news{Query(("securityId", securityId), ("sector", sector), ("macroTheme", macroTheme), ("since", since))}", ct);

    /// <summary>Published desk research, filterable by security / sector / rating action.</summary>
    public Task<string> GetResearchAsync(string? securityId = null, string? sector = null, string? ratingAction = null, CancellationToken ct = default)
        => GetAsync($"/mock/td/research{Query(("securityId", securityId), ("sector", sector), ("ratingAction", ratingAction))}", ct);

    /// <summary>The embedded cross-dataset storylines (macro themes) for narrative context.</summary>
    public Task<string> GetNarrativeThemesAsync(CancellationToken ct = default)
        => GetAsync("/mock/td/narrative-themes", ct);

    /// <summary>Current market/admin events (overnight + intraday) for reactive ranking.</summary>
    public Task<string> GetCurrentEventsAsync(string? scope = null, CancellationToken ct = default)
        => GetAsync(string.IsNullOrWhiteSpace(scope) ? "/mock/events" : $"/mock/events?scope={Encode(scope)}", ct);

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

    private static string Query(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Encode(p.Value!)}")
            .ToArray();
        return parts.Length == 0 ? "" : "?" + string.Join("&", parts);
    }

    private static string Error(string message, string? detail) =>
        JsonSerializer.Serialize(new { error = message, detail });

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
