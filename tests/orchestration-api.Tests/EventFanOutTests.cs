using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrchestrationApi.Agents;
using OrchestrationApi.Agents.EventSynthesis;
using OrchestrationApi.Models;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// User Story 4 (T040) tests for the per-event multi-agent fan-out (<see cref="EventFanOut"/>).
///
/// These assert the deterministic, Foundry-free contract: the fan-out emits a
/// <c>briefing_synthesis.fanout</c> parent span with one <c>event_specialist.assess</c> child per
/// event, each nesting its tool-call span — the synthesizer → specialist → tool-call trace graph
/// (SC-007). It also verifies bounded concurrency (<c>EVENT_FANOUT_MAX_CONCURRENCY</c>) and that
/// assessments are collected and ordered by |contribution| (the synthesizer fold order). The LIVE
/// Foundry leg is exercised separately; here the specialist is a fake so the orchestration logic is
/// verifiable offline. All data is fictional.
/// </summary>
public sealed class EventFanOutTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];

    public EventFanOutTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == "AgenticTradersDesk.Orchestration",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                lock (_activities) _activities.Add(a);
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private static EventFanOut CreateFanOut(int? maxConcurrency = null)
    {
        var settings = new Dictionary<string, string?>();
        if (maxConcurrency is { } mc) settings["EVENT_FANOUT_MAX_CONCURRENCY"] = mc.ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new EventFanOut(config, NullLogger<EventFanOut>.Instance);
    }

    private static MarketEvent Event(string id, string sector) => new()
    {
        Id = id,
        Type = "sector",
        Headline = $"Headline {id}",
        Summary = $"Summary {id}",
        Severity = "high",
        Direction = "negative",
        AffectedEntities = new AffectedEntities { Sectors = [sector] },
    };

    [Fact] // T040 [US4]
    public async Task Fan_out_emits_a_synthesizer_specialist_toolcall_trace_graph()
    {
        var fanOut = CreateFanOut();
        var events = new[] { Event("evt-a", "Agriculture"), Event("evt-b", "Manufacturing") };

        var assessments = await fanOut.AssessAllAsync("rm-briefing", events, async (e, ct) =>
        {
            // Each specialist makes one tool call — assert it nests under the specialist span.
            using (var tool = OrchestrationTelemetry.ActivitySource.StartActivity("execute_tool get_events_by_entity"))
            {
                tool?.SetTag("gen_ai.tool.name", "get_events_by_entity");
            }
            await Task.Yield();
            return new EventImpactAssessment
            {
                EventId = e.Id,
                Headline = e.Headline,
                Selectors = ["sector:Agriculture"],
                Contribution = e.Id == "evt-a" ? 30 : 10,
            };
        });

        assessments.Should().HaveCount(2);

        var parent = _activities.Single(a => a.OperationName == "briefing_synthesis.fanout");
        parent.GetTagItem("atd.fanout.event_count").Should().Be(2);
        parent.GetTagItem("atd.fanout.assessment_count").Should().Be(2);

        var specialists = _activities.Where(a => a.OperationName.StartsWith("event_specialist.assess")).ToList();
        specialists.Should().HaveCount(2);
        specialists.Should().OnlyContain(s => s.ParentSpanId == parent.SpanId, "each specialist runs under the fan-out");

        var toolCalls = _activities.Where(a => a.OperationName.StartsWith("execute_tool")).ToList();
        toolCalls.Should().HaveCount(2);
        toolCalls.Should().OnlyContain(t => specialists.Any(s => s.SpanId == t.ParentSpanId),
            "each tool-call nests under a specialist (synthesizer → specialist → tool-call)");
    }

    [Fact] // T040 [US4]
    public async Task Assessments_are_ordered_by_absolute_contribution()
    {
        var fanOut = CreateFanOut();
        var events = new[]
        {
            Event("evt-small", "Agriculture"),
            Event("evt-big", "Manufacturing"),
            Event("evt-mid", "Energy"),
        };

        var weights = new Dictionary<string, double>
        {
            ["evt-small"] = 5,
            ["evt-big"] = 40,
            ["evt-mid"] = -20,
        };

        var assessments = await fanOut.AssessAllAsync("rm-briefing", events, (e, ct) =>
            Task.FromResult<EventImpactAssessment?>(new EventImpactAssessment
            {
                EventId = e.Id,
                Headline = e.Headline,
                Contribution = weights[e.Id],
            }));

        assessments.Select(a => a.EventId).Should().ContainInOrder("evt-big", "evt-mid", "evt-small");
    }

    [Fact] // T040 [US4]
    public async Task Fan_out_respects_the_max_concurrency_bound()
    {
        var fanOut = CreateFanOut(maxConcurrency: 2);
        var events = Enumerable.Range(0, 6).Select(i => Event($"evt-{i}", "Agriculture")).ToArray();

        var current = 0;
        var peak = 0;
        var sync = new object();

        await fanOut.AssessAllAsync("rm-briefing", events, async (e, ct) =>
        {
            lock (sync) { current++; peak = Math.Max(peak, current); }
            await Task.Delay(20, ct);
            lock (sync) { current--; }
            return new EventImpactAssessment { EventId = e.Id, Headline = e.Headline, Contribution = 1 };
        });

        peak.Should().BeLessThanOrEqualTo(2, "EVENT_FANOUT_MAX_CONCURRENCY caps in-flight specialist runs");
    }

    [Fact] // T040 [US4]
    public async Task A_failing_specialist_is_skipped_not_fatal()
    {
        var fanOut = CreateFanOut();
        var events = new[] { Event("evt-ok", "Agriculture"), Event("evt-bad", "Manufacturing") };

        var assessments = await fanOut.AssessAllAsync("rm-briefing", events, (e, ct) =>
            e.Id == "evt-bad"
                ? throw new InvalidOperationException("specialist blew up")
                : Task.FromResult<EventImpactAssessment?>(new EventImpactAssessment
                {
                    EventId = e.Id,
                    Headline = e.Headline,
                    Contribution = 30,
                }));

        assessments.Should().ContainSingle().Which.EventId.Should().Be("evt-ok");
    }

    public void Dispose() => _listener.Dispose();
}
