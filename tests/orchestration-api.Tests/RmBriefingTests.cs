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
/// Tests for the PRIMARY scene <c>POST /api/agent/rm-briefing</c> (Commercial Banking RM
/// Daily Briefing). The happy path runs against the real mock-api hosted in-memory
/// (<see cref="MockApiBackedFactory"/>) so the full composer → CB mock-api seam is
/// exercised over HTTP (Principle II / FR-002); other tests cover determinism (SC-002),
/// tool-error degradation (FR-011) and unknown-RM validation.
/// </summary>
public sealed class RmBriefingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RmBriefingTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static object DefaultPayload => new { payload = new { rmId = "RM-104", date = "2026-05-14" } };

    [Fact]
    public async Task RmBriefing_demo_returns_200_with_marcus_book_and_prairie_grain_first()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var response = await client.PostAsJsonAsync("/api/agent/rm-briefing", DefaultPayload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        body!["mode"]!.GetValue<string>().Should().Be("DEMO");
        body["greeting"]!.GetValue<string>().Should().Be("Good morning, Marcus");
        body["rm"]!["rmId"]!.GetValue<string>().Should().Be("RM-104");

        // Book KPIs match the ground-truth briefing for Marcus Johnson (RM-104).
        body["portfolio"]!["customerCount"]!.GetValue<int>().Should().Be(14);
        body["portfolio"]!["totalExposureMm"]!.GetValue<double>().Should().Be(819.0);
        body["kpis"]!["closingWithin14Days"]!.GetValue<int>().Should().Be(2);
        body["kpis"]!["activeComplaints"]!.GetValue<int>().Should().Be(2);

        // Prairie Grain is the unambiguous #1 (escalated + in-progress complaint + stuck proposals).
        var calls = body["priorityCallList"]!.AsArray();
        calls.Count.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(8);
        calls[0]!["customerId"]!.GetValue<string>().Should().Be("CB-10036");
        calls[0]!["rank"]!.GetValue<int>().Should().Be(1);
        calls[0]!["priority"]!.GetValue<int>().Should().Be(1);
        calls[0]!["tags"]!.AsArray().Select(t => t!["kind"]!.GetValue<string>())
            .Should().Contain("escalated").And.Contain("in-progress");

        // Ranks are contiguous from 1 and ordered by descending score.
        var ranks = calls.Select(c => c!["rank"]!.GetValue<int>()).ToArray();
        ranks.Should().Equal(Enumerable.Range(1, calls.Count).ToArray());
        var scores = calls.Select(c => c!["score"]!.GetValue<int>()).ToArray();
        scores.Should().BeInDescendingOrder();

        // Complaints snapshot carries the two active Prairie Grain complaints (escalated first).
        var complaints = body["complaintsSnapshot"]!.AsArray();
        complaints.Select(c => c!["complaintId"]!.GetValue<string>())
            .Should().Contain("CMP-9018").And.Contain("CMP-9017");
        complaints[0]!["status"]!.GetValue<string>().Should().Be("Escalated");

        // Pipeline closing in 14 days are the two near-term opportunities, sorted by close date.
        body["pipelineClosing"]!.AsArray().Select(o => o!["opportunityId"]!.GetValue<string>())
            .Should().Equal("OPP-20056", "OPP-20031");

        body["suggestedFirstAction"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    [Fact] // SC-002 determinism
    public async Task RmBriefing_demo_is_byte_identical_across_runs()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var first = await (await client.PostAsJsonAsync("/api/agent/rm-briefing", DefaultPayload)).Content.ReadAsStringAsync();
        var second = await (await client.PostAsJsonAsync("/api/agent/rm-briefing", DefaultPayload)).Content.ReadAsStringAsync();

        second.Should().Be(first);
    }

    [Fact] // FR-011 tool-error degradation
    public async Task RmBriefing_degrades_with_notes_when_mock_api_returns_5xx()
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

        var response = await client.PostAsJsonAsync("/api/agent/rm-briefing", DefaultPayload);

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        body!["mode"]!.GetValue<string>().Should().Be("DEMO");
        body["priorityCallList"]!.AsArray().Count.Should().Be(0);
        body["notes"].Should().NotBeNull();
        body["notes"]!.AsArray().Count.Should().BeGreaterThan(0);
    }

    [Fact] // unknown RM → 400 problem+json
    public async Task RmBriefing_returns_400_for_unknown_relationship_manager()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var response = await client.PostAsJsonAsync("/api/agent/rm-briefing",
            new { payload = new { rmId = "RM-999", date = "2026-05-14" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        body!["rmId"]!.GetValue<string>().Should().Be("RM-999");
    }

    private sealed class AlwaysFailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"mock upstream failure\"}", System.Text.Encoding.UTF8, "application/json")
            });
    }
}
