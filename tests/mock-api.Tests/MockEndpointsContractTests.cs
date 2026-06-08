using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MockApi.Tests;

/// <summary>
/// Contract tests (T009) asserting every operation in <c>openapi/tools.yaml</c>
/// returns the expected status + JSON shape, served from the fictional fixtures.
/// </summary>
public sealed class MockEndpointsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MockEndpointsContractTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetAllClients_returns_200_array_with_value_fields()
    {
        var node = await GetJson("/mock/tableau/clients", HttpStatusCode.OK);
        var arr = node!.AsArray();
        arr.Count.Should().BeGreaterThan(0);
        var first = arr[0]!.AsObject();
        first.Should().ContainKey("id");
        first.Should().ContainKey("name");
        first.Should().ContainKey("revenueYtd");
        first.Should().ContainKey("shareOfWallet");
        first.Should().ContainKey("rankings");
    }

    [Theory]
    [InlineData("ATLAS")]
    [InlineData("BROOK")]
    [InlineData("CEDAR")]
    public async Task GetClient_known_returns_200(string cid)
    {
        var node = await GetJson($"/mock/tableau/clients/{cid}", HttpStatusCode.OK);
        node!["id"]!.GetValue<string>().Should().Be(cid);
    }

    [Fact]
    public async Task GetClient_unknown_returns_404()
        => await GetJson("/mock/tableau/clients/NOPE", HttpStatusCode.NotFound);

    [Fact]
    public async Task GetEngagement_returns_200_with_coverage_and_windows()
    {
        var node = await GetJson("/mock/dynamics/clients/ATLAS/engagement", HttpStatusCode.OK);
        node!["cid"]!.GetValue<string>().Should().Be("ATLAS");
        node["coverage"].Should().NotBeNull();
        node["last30d"].Should().NotBeNull();
        node["last180d"].Should().NotBeNull();
    }

    [Fact]
    public async Task GetAxes_returns_200_array_with_axis_fields()
    {
        var node = await GetJson("/mock/trading/axes", HttpStatusCode.OK);
        var first = node!.AsArray()[0]!.AsObject();
        first.Should().ContainKey("id");
        first.Should().ContainKey("instrument");
        first.Should().ContainKey("side");
        first.Should().ContainKey("relevanceTags");
    }

    [Fact]
    public async Task GetHoldings_unfiltered_returns_200_array()
    {
        var node = await GetJson("/mock/trading/holdings", HttpStatusCode.OK);
        node!.AsArray().Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetHoldings_filtered_by_sector_returns_only_matches()
    {
        var node = await GetJson("/mock/trading/holdings?sector=Rates", HttpStatusCode.OK);
        var arr = node!.AsArray();
        arr.Count.Should().BeGreaterThan(0);
        arr.Should().OnlyContain(h => h!["sector"]!.GetValue<string>() == "Rates");
    }

    [Fact]
    public async Task GetNewIssues_returns_200_array()
    {
        var node = await GetJson("/mock/calendar/newissues", HttpStatusCode.OK);
        var first = node!.AsArray()[0]!.AsObject();
        first.Should().ContainKey("issuer");
        first.Should().ContainKey("sector");
    }

    [Fact]
    public async Task GetMarketData_returns_200_with_rates_and_tone()
    {
        var node = await GetJson("/mock/marketdata", HttpStatusCode.OK);
        node!["rates"].Should().NotBeNull();
        node["tone"]!.GetValue<string>().Should().Be("Risk-off");
        node["asOf"].Should().NotBeNull();
    }

    [Fact]
    public async Task GetRelVal_known_event_returns_200()
    {
        var node = await GetJson("/mock/marketdata/relval/fed_surprise_hike", HttpStatusCode.OK);
        node!["eventId"]!.GetValue<string>().Should().Be("fed_surprise_hike");
    }

    [Fact]
    public async Task GetRelVal_unknown_event_returns_404()
        => await GetJson("/mock/marketdata/relval/unknown_event", HttpStatusCode.NotFound);

    [Fact]
    public async Task GetNews_known_event_returns_200_with_resolved_dimensions()
    {
        var node = await GetJson("/mock/news/fed_surprise_hike", HttpStatusCode.OK);
        node!["headline"].Should().NotBeNull();
        node["sectors"]!.AsArray().Count.Should().BeGreaterThan(0);
        node["states"]!.AsArray().Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetNews_unknown_event_returns_404()
        => await GetJson("/mock/news/unknown_event", HttpStatusCode.NotFound);

    [Fact]
    public async Task GetCoalition_byClient_returns_200_with_rank()
    {
        var node = await GetJson("/mock/coalition/ATLAS", HttpStatusCode.OK);
        node!["cid"]!.GetValue<string>().Should().Be("ATLAS");
        node["rank"].Should().NotBeNull();
        node["capture"].Should().NotBeNull();
    }

    [Fact]
    public async Task GetCoalition_bySector_returns_200_with_rank()
    {
        var node = await GetJson("/mock/coalition/sector/Municipals", HttpStatusCode.OK);
        node!["sector"]!.GetValue<string>().Should().Be("Municipals");
        node["rank"].Should().NotBeNull();
    }

    [Fact]
    public async Task GetCoalition_unknownClient_returns_404()
        => await GetJson("/mock/coalition/NOPE", HttpStatusCode.NotFound);

    [Fact]
    public async Task Healthz_returns_200()
        => await GetJson("/healthz", HttpStatusCode.OK);

    [Fact]
    public async Task Readyz_returns_200_when_data_loaded()
    {
        var node = await GetJson("/readyz", HttpStatusCode.OK);
        node!["status"]!.GetValue<string>().Should().Be("ready");
    }

    private async Task<JsonNode?> GetJson(string path, HttpStatusCode expected)
    {
        var response = await _client.GetAsync(path);
        response.StatusCode.Should().Be(expected, $"GET {path}");
        if (response.Content.Headers.ContentType is not null)
        {
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
        }
        return await response.Content.ReadFromJsonAsync<JsonNode>();
    }
}
