using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MockApi.Tests;

/// <summary>
/// Contract tests (T013) for the reactive event store surface (<c>/mock/events</c>,
/// 002-reactive-event-cockpit): overnight seed loads, list, by-entity query, ingest
/// append, and identity dedup (R11). All data is fictional.
/// </summary>
public sealed class EventEndpointsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EventEndpointsContractTests(WebApplicationFactory<Program> factory)
        => _factory = factory;

    [Fact]
    public async Task Readyz_returns_ready_after_seed()
    {
        var client = _factory.CreateClient();
        var node = await GetJson(client, "/mock/events/readyz", HttpStatusCode.OK);
        node!["status"]!.GetValue<string>().Should().Be("ready");
    }

    [Fact]
    public async Task List_returns_seeded_overnight_events()
    {
        var client = _factory.CreateClient();
        var node = await GetJson(client, "/mock/events", HttpStatusCode.OK);
        var arr = node!.AsArray();
        arr.Count.Should().BeGreaterThanOrEqualTo(3);
        var first = arr[0]!.AsObject();
        first.Should().ContainKey("id");
        first.Should().ContainKey("type");
        first.Should().ContainKey("headline");
        first.Should().ContainKey("affectedEntities");
        // The seeded events (origin=seed) are all overnight; the store may also hold
        // intraday events ingested by sibling tests sharing this fixture's singleton store.
        var seeded = arr.Where(e => e!["origin"]!.GetValue<string>() == "seed").ToList();
        seeded.Count.Should().BeGreaterThanOrEqualTo(3);
        seeded.Should().OnlyContain(e => e!["scope"]!.GetValue<string>() == "overnight");
    }

    [Fact]
    public async Task ByEntity_filters_to_events_touching_that_sector()
    {
        var client = _factory.CreateClient();
        var node = await GetJson(client, "/mock/events/by-entity?value=Agriculture&kind=sector", HttpStatusCode.OK);
        var arr = node!.AsArray();
        arr.Count.Should().BeGreaterThan(0);
        arr.Should().OnlyContain(e =>
            e!["affectedEntities"]!["sectors"]!.AsArray()
                .Any(s => s!.GetValue<string>() == "Agriculture"));
    }

    [Fact]
    public async Task ByEntity_missing_value_returns_400()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/mock/events/by-entity");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ingest_valid_event_appends_and_server_sets_fields()
    {
        var client = _factory.CreateClient();
        var before = (await GetJson(client, "/mock/events", HttpStatusCode.OK))!.AsArray().Count;

        var submission = new
        {
            type = "client_headline",
            headline = "Unit-test ingest headline",
            summary = "Fictional event submitted by the contract test.",
            severity = "medium",
            affectedEntities = new { customerIds = new[] { "CB-10005" } }
        };

        var post = await client.PostAsJsonAsync("/mock/events", submission);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await post.Content.ReadFromJsonAsync<JsonNode>())!;
        body["added"]!.GetValue<bool>().Should().BeTrue();
        var stored = body["event"]!.AsObject();
        stored["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        stored["scope"]!.GetValue<string>().Should().Be("intraday");
        stored["origin"]!.GetValue<string>().Should().Be("admin");
        stored["ingestedAt"].Should().NotBeNull();

        var after = (await GetJson(client, "/mock/events", HttpStatusCode.OK))!.AsArray().Count;
        after.Should().Be(before + 1);
    }

    [Fact]
    public async Task Ingest_duplicate_is_deduped_and_flagged()
    {
        var client = _factory.CreateClient();
        var submission = new
        {
            type = "sector",
            headline = "Duplicate dedup headline",
            summary = "Fictional duplicate.",
            severity = "low",
            affectedEntities = new { sectors = new[] { "Technology" } }
        };

        var first = await client.PostAsJsonAsync("/mock/events", submission);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var countAfterFirst = (await GetJson(client, "/mock/events", HttpStatusCode.OK))!.AsArray().Count;

        var second = await client.PostAsJsonAsync("/mock/events", submission);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var dupBody = (await second.Content.ReadFromJsonAsync<JsonNode>())!;
        dupBody["added"]!.GetValue<bool>().Should().BeFalse();

        var countAfterSecond = (await GetJson(client, "/mock/events", HttpStatusCode.OK))!.AsArray().Count;
        countAfterSecond.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task Ingest_missing_affected_entity_returns_400()
    {
        var client = _factory.CreateClient();
        var bad = new
        {
            type = "sector",
            headline = "No entities",
            summary = "Should be rejected.",
            severity = "low"
        };
        var response = await client.PostAsJsonAsync("/mock/events", bad);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<JsonNode?> GetJson(HttpClient client, string path, HttpStatusCode expected)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(expected, $"GET {path}");
        return await response.Content.ReadFromJsonAsync<JsonNode>();
    }
}
