using System.Diagnostics;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.EventSynthesis;

/// <summary>
/// Per-event multi-agent fan-out orchestrator (002 US4, FR-018/FR-019, SC-007, R8). Given the
/// current events, it runs one specialist assessment per event <b>concurrently</b> — bounded by
/// <c>EVENT_FANOUT_MAX_CONCURRENCY</c> — and collects their <see cref="EventImpactAssessment"/>
/// outputs for the briefing synthesizer to fold in.
///
/// <para>It is scene-agnostic and Foundry-agnostic: the caller supplies a delegate that assesses
/// one event (the LIVE runners pass <see cref="FoundryEventSpecialist"/>; tests pass a fake), so
/// the concurrency + tracing behaviour is verifiable without Foundry. Each run is wrapped in a
/// child span under a parent <c>briefing_synthesis.fanout</c> span, giving the
/// synthesizer → specialist → tool-call trace graph (SC-007). A failing specialist degrades to a
/// skip (logged) rather than failing the whole briefing (FR-011).</para>
/// </summary>
public sealed class EventFanOut(IConfiguration config, ILogger<EventFanOut> logger)
{
    /// <summary>Max specialist runs in flight at once (default 4, floor 1).</summary>
    public int MaxConcurrency { get; } =
        int.TryParse(config["EVENT_FANOUT_MAX_CONCURRENCY"], out var c) && c > 0 ? c : 4;

    public async Task<IReadOnlyList<EventImpactAssessment>> AssessAllAsync(
        string scene,
        IReadOnlyList<MarketEvent> events,
        Func<MarketEvent, CancellationToken, Task<EventImpactAssessment?>> assessOne,
        CancellationToken ct = default)
    {
        if (events.Count == 0)
        {
            return [];
        }

        using var parent = OrchestrationTelemetry.ActivitySource.StartActivity(
            "briefing_synthesis.fanout", ActivityKind.Internal);
        parent?.SetTag("wf.scene", scene);
        parent?.SetTag("wf.fanout.event_count", events.Count);
        parent?.SetTag("wf.fanout.max_concurrency", MaxConcurrency);

        using var gate = new SemaphoreSlim(MaxConcurrency);

        var tasks = events.Select(async e =>
        {
            await gate.WaitAsync(ct);
            try
            {
                using var span = OrchestrationTelemetry.ActivitySource.StartActivity(
                    $"event_specialist.assess {e.Id}", ActivityKind.Internal);
                span?.SetTag("wf.event_id", e.Id);
                span?.SetTag("wf.event_severity", e.Severity);
                span?.SetTag("wf.event_type", e.Type);
                try
                {
                    var assessment = await assessOne(e, ct);
                    span?.SetStatus(ActivityStatusCode.Ok);
                    return assessment;
                }
                catch (Exception ex)
                {
                    span?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    logger.LogWarning(ex, "Event specialist failed for {EventId}; skipping it.", e.Id);
                    return null;
                }
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        var assessments = results
            .Where(a => a is not null)
            .Select(a => a!)
            .OrderByDescending(a => Math.Abs(a.Contribution))
            .ThenBy(a => a.EventId, StringComparer.Ordinal)
            .ToList();

        parent?.SetTag("wf.fanout.assessment_count", assessments.Count);
        return assessments;
    }
}
