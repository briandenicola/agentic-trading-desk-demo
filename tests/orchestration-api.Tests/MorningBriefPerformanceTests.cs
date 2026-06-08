using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// T047 — DEMO-mode performance check (SC-001). The morning brief must return in
/// well under 10 seconds end-to-end. Runs against the real mock-api hosted in-memory
/// so the timing reflects the full composer → mock-api HTTP path.
/// </summary>
public sealed class MorningBriefPerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MorningBriefPerformanceTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact] // T047
    public async Task MorningBrief_demo_returns_within_10_seconds()
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);

        // Warm up the in-memory hosts so JIT/host startup is not attributed to the SLA.
        await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "fed_surprise_hike", date = "2026-06-04" } });

        var stopwatch = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/api/agent/morning-brief",
            new { payload = new { eventId = "fed_surprise_hike", date = "2026-06-04" } });
        stopwatch.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonNode>();
        body!["outreach"]!.AsArray().Count.Should().BeGreaterThan(0);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            $"the DEMO morning brief must render in < 10s (SC-001); took {stopwatch.ElapsedMilliseconds}ms");
    }
}
