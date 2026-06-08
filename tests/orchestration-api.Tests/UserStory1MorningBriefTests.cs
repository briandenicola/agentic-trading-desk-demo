using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// User Story 1 (MVP) tests for <c>POST /api/agent/morning-brief</c>.
///
/// Written now per TDD, but SKIPPED until the scene endpoint + DEMO composer land
/// in Phase 3 (T015-T016, and the tool functions in T018). Skipping keeps the suite
/// GREEN while the contract is captured up-front. Remove the Skip when Phase 3 ships.
/// </summary>
public sealed class UserStory1MorningBriefTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PendingReason =
        "Pending Phase 3: POST /api/agent/morning-brief is implemented in T015 (DEMO composer) + T016 (endpoint).";

    private readonly WebApplicationFactory<Program> _factory;

    public UserStory1MorningBriefTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact(Skip = PendingReason)] // T013 [US1]
    public async Task MorningBrief_demo_returns_200_schema_valid_and_populated()
    {
        var client = _factory.WithWebHostBuilder(b =>
            b.UseSetting("DEMO_MODE", "1")).CreateClient();

        var response = await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "fed_surprise_hike", date = "2026-06-04" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        var (isValid, errors) = MorningBriefSchemaValidator.Validate(body);
        isValid.Should().BeTrue(errors);

        body!["mode"]!.GetValue<string>().Should().Be("DEMO");
        body["macroNarrative"]!["summary"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        body["mostAffectedClients"]!.AsArray().Count.Should().BeGreaterThan(0);
        body["outreach"]!.AsArray().Count.Should().BeGreaterThan(0);
    }

    [Fact(Skip = PendingReason)] // T014 [US1] — FR-011 tool-error degradation
    public async Task MorningBrief_degrades_with_notes_when_mock_api_returns_5xx()
    {
        var client = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("DEMO_MODE", "1");
            b.ConfigureTestServices(services =>
            {
                services.AddHttpClient<MockApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new AlwaysFailHandler());
            });
        }).CreateClient();

        var response = await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "fed_surprise_hike", date = "2026-06-04" } });

        // Degrades gracefully: structured JSON (never HTML), with a notes entry.
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        body!["notes"].Should().NotBeNull();
        body["notes"]!.AsArray().Count.Should().BeGreaterThan(0);
    }

    /// <summary>Test handler that simulates a mock-api 5xx for the degradation test.</summary>
    private sealed class AlwaysFailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"mock upstream failure\"}", System.Text.Encoding.UTF8, "application/json")
            });
    }
}
