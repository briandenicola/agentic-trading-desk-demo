using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// DEMO tests for the lead-left enrichment of the New Issue Radar storyboard
/// (<c>POST /api/agent/td-new-issue</c>) and the spreadsheet-upload proxy
/// (<c>POST/GET /api/td/new-issues</c>). Run against the real mock-api hosted in-memory
/// (<see cref="MockApiBackedFactory"/>) so the full composer → enricher → mock-api seam is
/// exercised over HTTP (Principle II). All data is fictional.
/// </summary>
public sealed class TdNewIssueLeadLeftTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TdNewIssueLeadLeftTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Demo_storyboard_flags_the_issuer_lead_left_and_carries_the_board()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var response = await client.PostAsJsonAsync("/api/agent/td-new-issue", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        body["mode"]!.GetValue<string>().Should().Be("DEMO");

        var issuer = body["issuer"]!.AsObject();
        issuer["leadLeft"]!.GetValue<bool>().Should().BeTrue();
        issuer["syndicateRole"]!.GetValue<string>().Should().Contain("Lead-Left");
        issuer["ourAllocationControlPct"]!.GetValue<double>().Should().BeApproximately(0.45, 0.001);
        issuer["coManagers"]!.AsArray().Count.Should().BeGreaterThan(0);

        // Every tranche that belongs to the deal is flagged lead-left.
        issuer["tranches"]!.AsArray().Should().OnlyContain(t => t!["leadLeft"]!.GetValue<bool>());

        // The lead-left board accompanies the storyboard.
        var board = body["leadLeftBoard"]!.AsArray();
        board.Should().Contain(d => d!["issuer"]!.GetValue<string>() == "Prairie Green Renewables");

        // The announcement beat gains a syndicate evidence row + "Our book" metric.
        var announcement = body["steps"]!.AsArray()
            .First(s => s!["id"]!.GetValue<string>() == "announcement")!.AsObject();
        announcement["evidence"]!.AsArray()
            .Should().Contain(e => e!["kind"]!.GetValue<string>() == "syndicate");
        announcement["metrics"]!.AsArray()
            .Should().Contain(m => m!["label"]!.GetValue<string>() == "Our book");

        // The outreach trade idea is flagged as lead-left allocation paper.
        body["outreach"]!["tradeIdea"]!["leadLeft"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Upload_proxy_ingests_deals_and_lists_them()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var upload = new[]
        {
            new
            {
                issuer = "Cascade Hydro Partners",
                ourRole = "Lead-Left Bookrunner",
                leadLeft = true,
                pricingDate = "2026-06-02",
                ourAllocationControlPct = 0.4,
                trancheSecurityIds = new[] { "SEC-9001" },
            },
        };

        var post = await client.PostAsJsonAsync("/api/td/new-issues", upload);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = (await post.Content.ReadFromJsonAsync<JsonNode>())!;
        result["added"]!.GetValue<int>().Should().Be(1);

        var list = await client.GetFromJsonAsync<JsonNode>("/api/td/new-issues");
        list!.AsArray().Should().Contain(d => d!["issuer"]!.GetValue<string>() == "Cascade Hydro Partners");
    }

    [Fact]
    public async Task Upload_proxy_rejects_an_empty_payload()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var response = await client.PostAsJsonAsync("/api/td/new-issues", Array.Empty<object>());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
