using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchestrationApi.Models;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// User Story 2 + 4 coverage for prioritized outreach ranking, DEMO determinism,
/// LIVE/DEMO schema parity, default DEMO mode, and edge cases.
/// </summary>
public sealed class UserStory2And4MorningBriefTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const double Epsilon = 0.0001;

    private readonly WebApplicationFactory<Program> _factory;

    public UserStory2And4MorningBriefTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact] // T022 [US2]
    public async Task Demo_outreach_rationale_uses_documented_weighted_composite_and_contiguous_ranks()
    {
        var brief = await GetDemoBriefAsync();

        brief.Outreach.Should().NotBeEmpty();
        brief.Outreach.Select(o => o.Rank).Should().Equal(Enumerable.Range(1, brief.Outreach.Count));
        brief.Outreach.Select(o => o.Rationale.CompositeScore).Should().BeInDescendingOrder();

        foreach (var item in brief.Outreach)
        {
            var r = item.Rationale;
            var expected = (0.40 * r.WalletScore) + (0.30 * r.EngagementScore) + (0.30 * r.EventRelevanceScore);
            r.CompositeScore.Should().BeApproximately(expected, Epsilon, $"{item.Cid} must use the documented ranking blend");
        }
    }

    [Fact] // T023 [US2]
    public async Task Demo_outreach_talking_points_reference_event_client_and_relevant_exposure()
    {
        var brief = await GetDemoBriefAsync();
        var exposureByCid = brief.MostAffectedClients.ToDictionary(c => c.Cid, c => c.Exposure, StringComparer.Ordinal);

        foreach (var item in brief.Outreach)
        {
            item.TalkingPoints.Should().NotBeEmpty($"{item.Cid} needs actionable call prep");
            var text = string.Join(" ", item.TalkingPoints);

            text.Should().Contain(item.Name, $"talking points for {item.Cid} should be personalized");
            text.Should().MatchRegex("(?i)(fed|hike|rate|front-end|terminal)", $"talking points for {item.Cid} should reference the market event");

            var exposure = exposureByCid[item.Cid];
            if (exposure.Contains("duration", StringComparison.OrdinalIgnoreCase))
            {
                text.Should().MatchRegex("(?i)(duration|hedge|swap|CUSIP)");
            }
            else if (exposure.Contains("swap", StringComparison.OrdinalIgnoreCase))
            {
                text.Should().MatchRegex("(?i)(swap|hedge|relative value|terminal)");
            }
            else if (exposure.Contains("floating", StringComparison.OrdinalIgnoreCase))
            {
                text.Should().MatchRegex("(?i)(floating|front-end|reinvest|reset)");
            }
        }
    }

    [Fact] // T031 [US4] / SC-002
    public async Task Demo_runs_with_same_input_serialize_to_byte_identical_MorningBrief_json()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var first = await PostDemoBriefAsync(client);
        var second = await PostDemoBriefAsync(client);

        var firstJson = JsonSerializer.Serialize(first, MorningBriefJson.Options);
        var secondJson = JsonSerializer.Serialize(second, MorningBriefJson.Options);

        secondJson.Should().Be(firstJson, "DEMO mode must be deterministic and byte-identical for repeated runs");
    }

    [Fact] // T032 [US4] / SC-003
    public async Task Demo_and_representative_live_payloads_validate_against_same_morning_brief_schema()
    {
        var demo = await GetDemoBriefAsync();
        var live = RepresentativeLiveBrief();

        foreach (var brief in new[] { demo, live })
        {
            var json = JsonSerializer.Serialize(brief, MorningBriefJson.Options);
            var (isValid, errors) = MorningBriefSchemaValidator.Validate(json);
            isValid.Should().BeTrue($"{brief.Mode} payload should validate against the shared schema: {errors}");
        }
    }

    [Fact] // T033 [US4]
    public async Task Demo_mode_is_default_and_endpoint_works_without_foundry_settings()
    {
        OrchestrationApi.ModeOptions.FromConfiguration(new ConfigurationBuilder().Build()).DemoMode.Should().BeTrue();

        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FOUNDRY_PROJECT_ENDPOINT", string.Empty);
            builder.UseSetting("FOUNDRY_MODEL", string.Empty);
            builder.UseSetting("AZURE_CLIENT_ID", string.Empty);
            builder.UseSetting("AZURE_CLIENT_SECRET", string.Empty);
            builder.UseSetting("AZURE_TENANT_ID", string.Empty);
        }));

        var response = await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "fed_surprise_hike", date = "2026-06-04" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var brief = await response.Content.ReadFromJsonAsync<MorningBrief>(MorningBriefJson.Options);
        brief!.Mode.Should().Be("DEMO");
        brief.Outreach.Should().NotBeEmpty();
    }

    [Fact] // T034 [US4] empty-state edge case
    public async Task Demo_returns_empty_outreach_with_note_when_no_clients_are_materially_affected()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DEMO_MODE", "1");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<MockApiClient>(c => c.BaseAddress = new Uri("http://mock-api"))
                    .ConfigurePrimaryHttpMessageHandler(() => new NoMaterialExposureHandler());
            });
        }).CreateClient();

        var response = await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "no_material_clients", date = "2026-06-04" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var brief = await response.Content.ReadFromJsonAsync<MorningBrief>(MorningBriefJson.Options);
        brief!.MostAffectedClients.Should().BeEmpty();
        brief.Outreach.Should().BeEmpty();
        brief.Notes.Should().NotBeNullOrEmpty();
    }

    [Fact] // T034 [US4] unknown-event edge case
    public async Task Unknown_event_id_returns_structured_400_instead_of_degraded_200()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        var response = await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "unknown_event", date = "2026-06-04" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("json");

        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        body!["title"]!.GetValue<string>().Should().Contain("Unknown", "the error should be structured and actionable");
        body["detail"]!.GetValue<string>().Should().Contain("unknown_event");
    }

    private async Task<MorningBrief> GetDemoBriefAsync()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);
        return await PostDemoBriefAsync(client);
    }

    private static async Task<MorningBrief> PostDemoBriefAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "fed_surprise_hike", date = "2026-06-04" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<MorningBrief>(MorningBriefJson.Options))!;
    }

    private static MorningBrief RepresentativeLiveBrief() => MorningBriefSchemaValidatorTests.SampleBrief() with
    {
        Mode = "LIVE",
        Outreach =
        [
            new OutreachItem
            {
                Rank = 1,
                Cid = "ATLAS",
                Name = "Atlas Pension",
                SuggestedTopic = "Discuss hedging; mention new 10Y swap axes available.",
                TalkingPoints =
                [
                    "The Fed hike raises rate risk for Atlas Pension's long-duration holdings.",
                    "Use the 10Y swap axis to frame a duration hedge."
                ],
                Rationale = new RankingRationale
                {
                    WalletScore = 1.0,
                    EngagementScore = 0.8,
                    EventRelevanceScore = 1.0,
                    CompositeScore = 0.94,
                    Explanation = "Ranked #1: 0.40 wallet + 0.30 engagement + 0.30 event relevance."
                }
            }
        ]
    };

    private sealed class NoMaterialExposureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var json = path switch
            {
                "/mock/marketdata" => """
                    {"asOf":"2026-06-04T07:30:00-04:00","rates":{"ust10y":{"level":4.46,"changeBp":15}},"tone":"Risk-off"}
                    """,
                "/mock/news/no_material_clients" => """
                    {"eventId":"no_material_clients","summary":"A sector-specific headline misses the desk's held exposures.","sectors":["Healthcare"],"sources":["Fictional wire"]}
                    """,
                "/mock/marketdata/relval/no_material_clients" => """
                    {"commentary":"No material portfolio overlap was found for the event sector."}
                    """,
                "/mock/tableau/clients" => """
                    [{"id":"ATLAS","name":"Atlas Pension","tier":"Tier 1","revenueYtd":1000000,"shareOfWallet":0.4}]
                    """,
                "/mock/trading/holdings" => """
                    [{"cid":"ATLAS","cusip":"MUNI123","sector":"Education","state":"NY","exposureType":"long-duration"}]
                    """,
                "/mock/trading/axes" => "[]",
                _ => null
            };

            if (json is null)
            {
                return Task.FromResult(Json(HttpStatusCode.NotFound, "{\"error\":\"not found\"}"));
            }

            return Task.FromResult(Json(HttpStatusCode.OK, json));
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
