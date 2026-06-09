using System.Text.Json;

namespace OrchestrationApi.Agents.Tools;

/// <summary>
/// LIVE tool functions wrapping the Commercial Banking RM mock endpoints
/// (<c>/mock/cb/*</c>) for the Foundry RM Daily Briefing agent. Each method calls the
/// mock API over HTTP via <see cref="MockApiClient"/> (Principle II / FR-002), returns a
/// JSON string the model can consume, and <b>never throws</b> — failures come back as a
/// structured <c>{"error": ...}</c> object so the tool-calling loop degrades gracefully
/// (FR-011). Surfaced to the Agent Framework by
/// <see cref="OrchestrationApi.Agents.RmAgentRunner"/>.
/// </summary>
public sealed class RmBriefingTools(MockApiClient mockApi)
{
    /// <summary>Book-level snapshot for one RM: manager, customers, and headline KPIs.</summary>
    public Task<string> GetRmBookAsync(string rmId, string? asOf = null, CancellationToken ct = default)
        => GetAsync($"/mock/cb/relationship-managers/{Encode(rmId)}/book{AsOfQuery(asOf)}", ct);

    /// <summary>Open opportunities for an RM (optionally near-close / stuck filters via the API).</summary>
    public Task<string> GetOpenOpportunitiesAsync(string rm, string? asOf = null, CancellationToken ct = default)
        => GetAsync($"/mock/cb/opportunities?rm={Encode(rm)}&openOnly=true{(asOf is null ? "" : $"&asOf={Encode(asOf)}")}", ct);

    /// <summary>Active (unresolved) complaints for an RM.</summary>
    public Task<string> GetActiveComplaintsAsync(string rm, CancellationToken ct = default)
        => GetAsync($"/mock/cb/complaints?rm={Encode(rm)}&activeOnly=true", ct);

    /// <summary>Interactions with a follow-up due on or before a date (overdue + upcoming).</summary>
    public Task<string> GetDueFollowUpsAsync(string rm, string followUpDueBy, CancellationToken ct = default)
        => GetAsync($"/mock/cb/interactions?rm={Encode(rm)}&followUpDueBy={Encode(followUpDueBy)}", ct);

    /// <summary>One customer's full profile.</summary>
    public Task<string> GetCustomerAsync(string customerId, CancellationToken ct = default)
        => GetAsync($"/mock/cb/customers/{Encode(customerId)}", ct);

    /// <summary>One customer's opportunities.</summary>
    public Task<string> GetCustomerOpportunitiesAsync(string customerId, CancellationToken ct = default)
        => GetAsync($"/mock/cb/customers/{Encode(customerId)}/opportunities", ct);

    /// <summary>One customer's interactions (call log + follow-ups).</summary>
    public Task<string> GetCustomerInteractionsAsync(string customerId, CancellationToken ct = default)
        => GetAsync($"/mock/cb/customers/{Encode(customerId)}/interactions", ct);

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

    private static string AsOfQuery(string? asOf) => string.IsNullOrWhiteSpace(asOf) ? "" : $"?asOf={Encode(asOf)}";

    private static string Error(string message, string? detail) =>
        JsonSerializer.Serialize(new { error = message, detail });

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
