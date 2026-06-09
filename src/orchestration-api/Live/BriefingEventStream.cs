using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using OrchestrationApi;
using OrchestrationApi.Agents;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;

namespace OrchestrationApi.Live;

/// <summary>
/// Server-Sent Events hub for reactive briefing updates (002-reactive-event-cockpit, US2 /
/// FR-010…FR-013). A background poller (<see cref="EventStreamPollingService"/>) calls
/// <see cref="PollAndBroadcastAsync"/> on an interval; when new events appear in the mock-api
/// event store (reached ONLY over HTTP via <see cref="EventTools"/>, Principle II) it coalesces a
/// burst (research R3), re-synthesizes the affected scene DTO per distinct (scene, persona) group,
/// and pushes a <see cref="LiveUpdate"/> frame to each subscriber. Every push is a full snapshot
/// (reaction granularity, R7) so reconnects reconcile without missed-delta divergence (R4).
/// All data is fictional.
/// </summary>
public sealed class BriefingEventStream
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ModeOptions _mode;
    private readonly ILogger<BriefingEventStream> _logger;
    private readonly TimeSpan _coalesceWindow;

    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private HashSet<string> _knownEventIds = new(StringComparer.Ordinal);
    private bool _seeded;
    private long _sequence;

    /// <summary>
    /// Test seam: when set, replaces the HTTP event fetch with a supplied source so the
    /// new-event-detection and coalescing logic can be exercised deterministically (T024).
    /// Production leaves this null and reads through <see cref="EventTools"/>.
    /// </summary>
    internal Func<CancellationToken, Task<IReadOnlyList<MarketEvent>>>? EventSourceOverride { get; set; }

    public BriefingEventStream(
        IServiceScopeFactory scopes,
        ModeOptions mode,
        IConfiguration configuration,
        ILogger<BriefingEventStream> logger)
    {
        _scopes = scopes;
        _mode = mode;
        _logger = logger;
        var windowMs = int.TryParse(configuration["SSE_COALESCE_WINDOW_MS"], out var ms) ? ms : 750;
        _coalesceWindow = TimeSpan.FromMilliseconds(Math.Max(0, windowMs));
    }

    public long CurrentSequence => Interlocked.Read(ref _sequence);

    /// <summary>A single SSE subscriber: a scene/persona scope plus its outbound frame channel.</summary>
    public sealed class Subscriber
    {
        public required Guid Id { get; init; }
        public required string Scene { get; init; }
        public string? Persona { get; init; }
        public Channel<string> Frames { get; } = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    public Subscriber Subscribe(string scene, string? persona)
    {
        var sub = new Subscriber { Id = Guid.NewGuid(), Scene = scene, Persona = persona };
        _subscribers[sub.Id] = sub;
        return sub;
    }

    public void Unsubscribe(Guid id) => _subscribers.TryRemove(id, out _);

    /// <summary>
    /// One poll cycle: list current events, diff against the known set, coalesce a burst over the
    /// debounce window, then broadcast a single consolidated update per (scene, persona) group.
    /// Idempotent and serialized by <see cref="_pollLock"/>. The first call seeds the known set so
    /// pre-existing overnight events never trigger a startup alert storm.
    /// </summary>
    public async Task PollAndBroadcastAsync(CancellationToken ct = default)
    {
        await _pollLock.WaitAsync(ct);
        try
        {
            var current = await FetchEventsAsync(ct);
            var currentIds = current.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

            if (!_seeded)
            {
                _knownEventIds = currentIds;
                _seeded = true;
                return;
            }

            var newEvents = current.Where(e => !_knownEventIds.Contains(e.Id)).ToList();
            if (newEvents.Count == 0) return;

            // Coalesce: pause for the debounce window then re-read to fold in burst stragglers (R3).
            if (_coalesceWindow > TimeSpan.Zero)
            {
                await Task.Delay(_coalesceWindow, ct);
                current = await FetchEventsAsync(ct);
                currentIds = current.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
                newEvents = current.Where(e => !_knownEventIds.Contains(e.Id)).ToList();
            }

            _knownEventIds = currentIds;
            if (newEvents.Count == 0) return;

            await BroadcastAsync(newEvents, ct);
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private async Task<IReadOnlyList<MarketEvent>> FetchEventsAsync(CancellationToken ct)
    {
        if (EventSourceOverride is not null) return await EventSourceOverride(ct);
        using var scope = _scopes.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<EventTools>();
        return await tools.ListEventsAsync(ct: ct);
    }

    private async Task BroadcastAsync(IReadOnlyList<MarketEvent> newEvents, CancellationToken ct)
    {
        var groups = _subscribers.Values
            .GroupBy(s => (s.Scene, s.Persona))
            .ToList();

        foreach (var group in groups)
        {
            var (scene, persona) = group.Key;
            object briefing;
            HashSet<string> driverIds;
            try
            {
                using var scope = _scopes.CreateScope();
                (briefing, driverIds) = await SynthesizeAsync(scope.ServiceProvider, scene, persona, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SSE re-synthesis failed for scene {Scene} persona {Persona}", scene, persona);
                continue;
            }

            var newIds = newEvents.Select(e => e.Id).ToList();
            var noImpact = !newEvents.Any(e => driverIds.Contains(e.Id));
            var seq = Interlocked.Increment(ref _sequence);
            var update = new LiveUpdate
            {
                Sequence = seq,
                Scene = scene,
                Alert = new LiveAlert
                {
                    Priority = MapPriority(newEvents),
                    Headline = BuildHeadline(newEvents, noImpact),
                    EventIds = newIds,
                    NoImpact = noImpact
                },
                Briefing = briefing
            };

            var frame = FormatFrame("briefing-update", seq, update);
            foreach (var sub in group)
            {
                sub.Frames.Writer.TryWrite(frame);
            }
        }
    }

    /// <summary>Build the one-time snapshot frame sent on (re)connect (snapshot-on-reconnect, R4).</summary>
    public async Task<string?> BuildSnapshotFrameAsync(string scene, string? persona, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var current = await FetchEventsAsync(ct);
        var (briefing, _) = await SynthesizeAsync(scope.ServiceProvider, scene, persona, ct);
        var ids = current.Select(e => e.Id).ToList();
        if (ids.Count == 0) return null; // nothing to anchor an alert to; the `ready` frame suffices

        var seq = CurrentSequence;
        var update = new LiveUpdate
        {
            Sequence = seq,
            Scene = scene,
            Alert = new LiveAlert
            {
                Priority = MapPriority(current),
                Headline = $"Live briefing connected — {ids.Count} event(s) in view.",
                EventIds = ids,
                NoImpact = true
            },
            Briefing = briefing
        };
        return FormatFrame("briefing-update", seq, update);
    }

    public string FormatReadyFrame(string scene)
    {
        var payload = JsonSerializer.Serialize(new { sequence = CurrentSequence, scene }, RmBriefingJson.Options);
        return $"event: ready\ndata: {payload}\n\n";
    }

    public static string FormatHeartbeatFrame() =>
        $"event: heartbeat\ndata: {{\"ts\":\"{DateTimeOffset.UtcNow:O}\"}}\n\n";

    private async Task<(object briefing, HashSet<string> driverEventIds)> SynthesizeAsync(
        IServiceProvider sp, string scene, string? persona, CancellationToken ct)
    {
        if (scene == "rm-briefing")
        {
            var rmId = string.IsNullOrWhiteSpace(persona) ? RmBriefingComposer.DefaultRmId : persona!;
            var brief = _mode.DemoMode
                ? await sp.GetRequiredService<RmBriefingComposer>().ComposeAsync(rmId, null, ct)
                : await sp.GetRequiredService<RmAgentRunner>().RunAsync(rmId, null, ct);
            var ids = brief.PriorityCallList
                .SelectMany(c => c.DrivingEvents)
                .Select(d => d.EventId)
                .ToHashSet(StringComparer.Ordinal);
            return (brief, ids);
        }
        else
        {
            var brief = _mode.DemoMode
                ? await sp.GetRequiredService<MorningBriefComposer>().ComposeAsync("fed_surprise_hike", null, ct)
                : await sp.GetRequiredService<AgentRunner>().RunAsync("fed_surprise_hike", null, ct);
            var ids = brief.MostAffectedClients
                .SelectMany(c => c.DrivingEvents)
                .Select(d => d.EventId)
                .ToHashSet(StringComparer.Ordinal);
            return (brief, ids);
        }
    }

    private static string MapPriority(IReadOnlyList<MarketEvent> events)
    {
        if (events.Any(e => string.Equals(e.Severity, "high", StringComparison.OrdinalIgnoreCase))) return "urgent";
        if (events.Any(e => string.Equals(e.Severity, "medium", StringComparison.OrdinalIgnoreCase))) return "notice";
        return "info";
    }

    private static string BuildHeadline(IReadOnlyList<MarketEvent> events, bool noImpact)
    {
        var lead = events[0].Headline;
        var more = events.Count > 1 ? $" (+{events.Count - 1} more)" : string.Empty;
        return noImpact
            ? $"New event — no portfolio impact: {lead}{more}"
            : $"{lead}{more}";
    }

    private static string FormatFrame(string eventType, long sequence, LiveUpdate update)
    {
        var data = JsonSerializer.Serialize(update, RmBriefingJson.Options);
        var sb = new StringBuilder();
        sb.Append("id: ").Append(sequence).Append('\n');
        sb.Append("event: ").Append(eventType).Append('\n');
        sb.Append("data: ").Append(data).Append('\n');
        sb.Append('\n');
        return sb.ToString();
    }
}
