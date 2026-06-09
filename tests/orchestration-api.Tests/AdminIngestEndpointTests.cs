extern alias MockApiHost;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// User Story 3 (T033) tests for the admin news-injection proxy (<c>POST/GET /api/events</c>).
///
/// The browser reaches the mock-api event store ONLY through the orchestration proxy
/// (constitution Principle II). These tests host the real mock-api in-memory, rewire the
/// orchestration-api's <c>MockApiClient</c> to call it over HTTP, then assert: a complete
/// submission is ingested with <c>origin=admin</c> / <c>scope=intraday</c> (FR-016) and then
/// appears in the event list, while an incomplete submission is rejected with 400 and ingests
/// nothing. All data is fictional.
/// </summary>
public sealed class AdminIngestEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<MockApiHost::Program> _mockApi = new();
    private readonly HttpClient _client;

    public AdminIngestEndpointTests()
    {
        var handler = _mockApi.Server.CreateHandler();
        _client = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DEMO_MODE", "1");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<MockApiClient>(c => c.BaseAddress = new Uri("http://mock-api"))
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        }).CreateClient();
    }

    [Fact] // T033 [US3]
    public async Task A_complete_submission_is_ingested_as_admin_intraday_and_then_listed()
    {
        var submission = new
        {
            type = "sector",
            headline = "Admin: agricultural credit stress",
            summary = "Fictional operator-injected signal for the RM book.",
            severity = "high",
            direction = "negative",
            affectedEntities = new { customerIds = new[] { "CB-10036" }, sectors = new[] { "Agriculture" } }
        };

        var response = await _client.PostAsJsonAsync("/api/events", submission);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stored = doc.RootElement;
        var id = stored.GetProperty("id").GetString();
        id.Should().NotBeNullOrWhiteSpace();
        stored.GetProperty("origin").GetString().Should().Be("admin");
        stored.GetProperty("scope").GetString().Should().Be("intraday");

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/events?scope=intraday");
        list.EnumerateArray().Select(e => e.GetProperty("id").GetString())
            .Should().Contain(id);
    }

    [Fact] // T033 [US3]
    public async Task An_incomplete_submission_is_rejected_with_400_and_ingests_nothing()
    {
        var before = await CountIntradayAsync();

        var bad = new
        {
            type = "sector",
            headline = "   ",                 // blank headline
            summary = "",                     // blank summary
            severity = "high",
            affectedEntities = new { }        // no selectors
        };

        var response = await _client.PostAsJsonAsync("/api/events", bad);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = await CountIntradayAsync();
        after.Should().Be(before, "a rejected submission must not reach the store");
    }

    private async Task<int> CountIntradayAsync()
    {
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/events?scope=intraday");
        return list.GetArrayLength();
    }

    public void Dispose()
    {
        _client.Dispose();
        _mockApi.Dispose();
    }
}
