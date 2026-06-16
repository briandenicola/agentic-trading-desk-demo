using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MockApi.Tests;

/// <summary>
/// Contract tests for the new-issue / lead-left surface (<c>/mock/td/new-issues</c>): the seeded
/// lead-left deal loads, the list filters by issuer, and the spreadsheet-upload ingest appends and
/// overrides by issuer (non-durable). All data is fictional.
/// </summary>
public sealed class TdNewIssueEndpointsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TdNewIssueEndpointsContractTests(WebApplicationFactory<Program> factory)
        => _factory = factory;

    [Fact]
    public async Task List_returns_the_seeded_lead_left_deal()
    {
        var client = _factory.CreateClient();
        var node = await GetJson(client, "/mock/td/new-issues", HttpStatusCode.OK);
        var arr = node!.AsArray();
        arr.Count.Should().BeGreaterThanOrEqualTo(1);

        var prairie = arr.First(d => d!["issuer"]!.GetValue<string>() == "Prairie Green Renewables")!.AsObject();
        prairie["leadLeft"]!.GetValue<bool>().Should().BeTrue();
        prairie["ourRole"]!.GetValue<string>().Should().Contain("Lead-Left");
        prairie["source"]!.GetValue<string>().Should().Be("seed");
        prairie["trancheSecurityIds"]!.AsArray().Select(x => x!.GetValue<string>())
            .Should().Contain(new[] { "SEC-3601", "SEC-3602" });
    }

    [Fact]
    public async Task List_filters_by_security_id()
    {
        var client = _factory.CreateClient();
        var node = await GetJson(client, "/mock/td/new-issues?securityId=SEC-3602", HttpStatusCode.OK);
        node!.AsArray().Should().OnlyContain(d =>
            d!["trancheSecurityIds"]!.AsArray().Any(s => s!.GetValue<string>() == "SEC-3602"));
    }

    [Fact]
    public async Task Upload_appends_a_new_issuer_and_stamps_source_upload()
    {
        var client = _factory.CreateClient();
        var before = (await GetJson(client, "/mock/td/new-issues", HttpStatusCode.OK))!.AsArray().Count;

        var upload = new[]
        {
            new
            {
                issuer = "Cascade Hydro Partners",
                ourRole = "Lead-Left Bookrunner",
                leadLeft = true,
                bookStatus = "Books open",
                pricingDate = "2026-06-02",
                ourAllocationControlPct = 0.4,
                trancheSecurityIds = new[] { "SEC-9001" },
            },
        };

        var post = await client.PostAsJsonAsync("/mock/td/new-issues", upload);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await post.Content.ReadFromJsonAsync<JsonNode>())!;
        body["added"]!.GetValue<int>().Should().Be(1);

        var after = (await GetJson(client, "/mock/td/new-issues?issuer=Cascade", HttpStatusCode.OK))!.AsArray();
        after.Should().HaveCount(1);
        after[0]!["source"]!.GetValue<string>().Should().Be("upload");
        after[0]!["dealId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();

        var total = (await GetJson(client, "/mock/td/new-issues", HttpStatusCode.OK))!.AsArray().Count;
        total.Should().Be(before + 1);
    }

    [Fact]
    public async Task Upload_with_empty_body_returns_400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/mock/td/new-issues", Array.Empty<object>());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<JsonNode?> GetJson(HttpClient client, string path, HttpStatusCode expected)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(expected, $"GET {path}");
        return await response.Content.ReadFromJsonAsync<JsonNode>();
    }
}
