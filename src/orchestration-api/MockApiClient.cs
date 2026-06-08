using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OrchestrationApi;

/// <summary>
/// Typed HTTP client for the mock system-of-record API (T011). The orchestration
/// API reaches source data ONLY through this HTTP seam — never by reading the
/// mock-api fixtures in-process (constitution Principle II / FR-002).
/// </summary>
public sealed class MockApiClient(HttpClient http)
{
    /// <summary>GET a JSON resource from the mock API, returning the parsed node.</summary>
    public async Task<JsonNode?> GetJsonAsync(string path, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonNode>(ct);
    }

    /// <summary>Raw GET — lets callers inspect non-success status (e.g. tool-error degradation).</summary>
    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct = default)
        => http.GetAsync(path, ct);
}
