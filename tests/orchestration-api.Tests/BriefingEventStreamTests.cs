extern alias MockApiHost;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrchestrationApi.Live;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// User Story 2 (T024) tests for the reactive SSE hub (<see cref="BriefingEventStream"/>).
///
/// Each test hosts the real mock-api in-memory and rewires the orchestration-api's
/// <c>MockApiClient</c> to call it over HTTP (constitution Principle II / FR-002), then drives
/// <see cref="BriefingEventStream.PollAndBroadcastAsync"/> deterministically (the background
/// <c>EventStreamPollingService</c> is removed so cycles are explicit, and the coalesce window is
/// zeroed). It asserts: an intraday ingest triggers a re-synthesized full DTO push; a burst yields
/// ONE consolidated update; and reconnect produces a fresh snapshot frame. All data is fictional.
/// </summary>
public sealed class BriefingEventStreamTests : IDisposable
{
    private readonly WebApplicationFactory<MockApiHost::Program> _mockApi = new();
    private readonly WebApplicationFactory<Program> _orchestration;
    private readonly HttpClient _mockApiClient;

    public BriefingEventStreamTests()
    {
        var handler = _mockApi.Server.CreateHandler();
        _mockApiClient = _mockApi.CreateClient();

        _orchestration = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DEMO_MODE", "1");
            builder.UseSetting("SSE_COALESCE_WINDOW_MS", "0"); // deterministic single-cycle coalescing
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<MockApiClient>(c => c.BaseAddress = new Uri("http://mock-api"))
                    .ConfigurePrimaryHttpMessageHandler(() => handler);

                // Drive poll cycles by hand — remove the background poller for determinism.
                var poller = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType == typeof(EventStreamPollingService));
                if (poller is not null) services.Remove(poller);
            });
        });
    }

    [Fact] // T024 [US2]
    public async Task Intraday_ingest_triggers_a_resynthesized_full_dto_push()
    {
        var stream = _orchestration.Services.GetRequiredService<BriefingEventStream>();
        var sub = stream.Subscribe("rm-briefing", "RM-104");

        await stream.PollAndBroadcastAsync();           // seed: known set = the 5 overnight events
        var newId = await IngestEventAsync("Intraday grain shock", "CB-10036", "Agriculture");
        await stream.PollAndBroadcastAsync();           // detect + broadcast

        sub.Frames.Reader.TryRead(out var frame).Should().BeTrue("the new event should push one update");
        frame.Should().Contain("event: briefing-update");

        var update = ParseUpdate(frame!);
        update.GetProperty("scene").GetString().Should().Be("rm-briefing");
        update.GetProperty("alert").GetProperty("eventIds").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain(newId);
        update.TryGetProperty("briefing", out var briefing).Should().BeTrue();
        briefing.GetProperty("priorityCallList").GetArrayLength().Should().BeGreaterThan(0);

        sub.Frames.Reader.TryRead(out _).Should().BeFalse("exactly one update for one ingest");
    }

    [Fact] // T024 [US2]
    public async Task A_burst_within_the_coalesce_window_yields_one_consolidated_update()
    {
        var stream = _orchestration.Services.GetRequiredService<BriefingEventStream>();
        var sub = stream.Subscribe("rm-briefing", "RM-104");

        await stream.PollAndBroadcastAsync();           // seed
        var idA = await IngestEventAsync("Burst event A", "CB-10036", "Agriculture");
        var idB = await IngestEventAsync("Burst event B", "CB-10035", "Agriculture");
        await stream.PollAndBroadcastAsync();           // one cycle sees both as new

        sub.Frames.Reader.TryRead(out var frame).Should().BeTrue();
        var eventIds = ParseUpdate(frame!).GetProperty("alert").GetProperty("eventIds")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        eventIds.Should().Contain(idA).And.Contain(idB);

        sub.Frames.Reader.TryRead(out _).Should().BeFalse("a burst must coalesce into ONE update");
    }

    [Fact] // T024 [US2]
    public async Task Reconnect_returns_a_fresh_full_snapshot_frame()
    {
        var stream = _orchestration.Services.GetRequiredService<BriefingEventStream>();

        var frame = await stream.BuildSnapshotFrameAsync("morning-brief", null, default);

        frame.Should().NotBeNull("seeded events anchor a snapshot-on-reconnect (R4)");
        frame!.Should().Contain("event: briefing-update");
        var update = ParseUpdate(frame);
        update.GetProperty("scene").GetString().Should().Be("morning-brief");
        update.GetProperty("alert").GetProperty("eventIds").GetArrayLength().Should().BeGreaterThan(0);
        update.TryGetProperty("briefing", out _).Should().BeTrue();
    }

    [Fact] // reset re-seed: a mock-api restart re-seeds the known set so reused ids are re-detected
    public async Task A_store_reset_reseeds_so_a_reused_id_is_detected_again()
    {
        var stream = _orchestration.Services.GetRequiredService<BriefingEventStream>();
        var sub = stream.Subscribe("rm-briefing", "RM-104");

        // Drive the poller off a controllable event source (the orchestration-api MarketEvent type).
        IReadOnlyList<OrchestrationApi.Models.MarketEvent> live = new[] { OrchEv("evt-x-a001"), OrchEv("evt-x-a002") };
        stream.EventSourceOverride = _ => Task.FromResult(live);

        await stream.PollAndBroadcastAsync();           // seed: known = {a001, a002}
        sub.Frames.Reader.TryRead(out _).Should().BeFalse("seeding never alerts");

        // Mock-api reset: intraday events dropped back to the overnight baseline (a002 vanished).
        live = new[] { OrchEv("evt-x-a001") };
        await stream.PollAndBroadcastAsync();           // shrink detected → re-seed known = {a001}, no alert
        sub.Frames.Reader.TryRead(out _).Should().BeFalse("a reset re-seeds silently, it must not alert");

        // Post-reset inject reuses id a002 (admin sequence restarted): must be detected as new.
        live = new[] { OrchEv("evt-x-a001"), OrchEv("evt-x-a002") };
        await stream.PollAndBroadcastAsync();

        sub.Frames.Reader.TryRead(out var frame).Should().BeTrue(
            "after a reset the reused id a002 must be treated as a fresh event and broadcast");
        ParseUpdate(frame!).GetProperty("alert").GetProperty("eventIds")
            .EnumerateArray().Select(e => e.GetString()).Should().Contain("evt-x-a002");
    }

    private static OrchestrationApi.Models.MarketEvent OrchEv(string id) => new()
    {
        Id = id,
        Type = "sector",
        Headline = $"Fictional {id}",
        Summary = "Fictional event for reset re-seed coverage.",
        Severity = "high",
        Direction = "negative",
        AffectedEntities = new OrchestrationApi.Models.AffectedEntities { Sectors = new[] { "Agriculture" } }
    };

    private async Task<string> IngestEventAsync(string headline, string customerId, string sector)
    {
        var submission = new
        {
            type = "sector",
            headline,
            summary = $"Fictional intraday signal: {headline}.",
            severity = "high",
            direction = "negative",
            affectedEntities = new { customerIds = new[] { customerId }, sectors = new[] { sector } }
        };

        var response = await _mockApiClient.PostAsJsonAsync("/mock/events", submission);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("event").GetProperty("id").GetString()!;
    }

    private static JsonElement ParseUpdate(string frame)
    {
        // Extract the SSE `data:` line payload.
        var dataLine = frame.Split('\n').First(l => l.StartsWith("data: ", StringComparison.Ordinal));
        var json = dataLine["data: ".Length..];
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    public void Dispose()
    {
        _mockApiClient.Dispose();
        _orchestration.Dispose();
        _mockApi.Dispose();
    }
}
