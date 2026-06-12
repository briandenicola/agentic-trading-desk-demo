using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Models;

namespace OrchestrationApi.Live;

/// <summary>
/// Folds injected/live market events onto a <b>base</b> <see cref="TdBriefing"/> deterministically so
/// the Trading Desk call list re-ranks in ~1-2s on a News Desk inject — instead of re-running the full
/// Foundry agent (~50-60s) on every SSE push. The base briefing is produced once (DEMO composer or LIVE
/// agent) and reflects the events present at synthesis time (<paramref name="baseEventIds"/>); this
/// overlay applies a deterministic <see cref="EventImpactResolver"/> delta for <b>only</b> the events
/// that arrived afterwards (so the agent's own folding is never double-counted), re-ranks by adjusted
/// score, re-bands priority, merges the new drivers into each affected call, and refreshes
/// <see cref="TdBriefing.LiveEvents"/>.
///
/// <para>This is the same deterministic event re-rank step the DEMO composer already runs inline
/// (<c>EventImpactResolver.ResolveForClient</c> → net contribution → re-sort), so LIVE and DEMO react
/// identically (Principle III). Pure and side-effect free. All data is fictional.</para>
/// </summary>
public static class TdBriefingLive
{
    /// <summary>
    /// Returns the base briefing with post-base events folded in, plus the set of driver event ids
    /// (used by the SSE hub to decide whether an update is impactful). When nothing new matches, the
    /// briefing is returned unchanged with an empty driver set.
    /// </summary>
    public static (TdBriefing briefing, HashSet<string> driverEventIds) ApplyEvents(
        TdBriefing baseBriefing,
        IReadOnlyList<MarketEvent>? allEvents,
        IReadOnlySet<string> baseEventIds,
        IReadOnlyDictionary<string, ClientEntitySet> entitySets)
    {
        var empty = new HashSet<string>(StringComparer.Ordinal);
        if (allEvents is null || allEvents.Count == 0) return (baseBriefing, empty);

        // The base briefing already reflects the events present when it was synthesized — re-applying
        // them would double-count — so only fold events that arrived AFTER the base was built.
        var newEvents = allEvents.Where(e => !baseEventIds.Contains(e.Id)).ToList();
        if (newEvents.Count == 0) return (baseBriefing, empty);

        var driverIds = new HashSet<string>(StringComparer.Ordinal);

        var rescored = baseBriefing.PriorityCallList.Select(call =>
        {
            IReadOnlyList<EventLinkage> drivers = Array.Empty<EventLinkage>();
            if (entitySets.TryGetValue(call.ClientId, out var es))
            {
                drivers = EventImpactResolver.ResolveForClient(
                    call.ClientId, es.Securities, es.Issuers, es.Sectors, newEvents);
            }

            if (drivers.Count == 0) return (call, score: call.Score, newDrivers: drivers);

            var delta = EventImpactResolver.NetContribution(drivers);
            foreach (var d in drivers) driverIds.Add(d.EventId);
            return (call, score: call.Score + delta, newDrivers: drivers);
        }).ToList();

        if (driverIds.Count == 0) return (baseBriefing, empty);

        // Re-rank by adjusted score; preserve the agent's original order on ties (stable re-rank).
        var ranked = rescored
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.call.Rank)
            .ToList();

        var newCalls = ranked.Select((x, i) =>
        {
            var rank = i + 1;
            var mergedDrivers = x.newDrivers.Count > 0
                ? x.newDrivers.Concat(x.call.DrivingEvents).ToList()
                : x.call.DrivingEvents;
            return x.call with
            {
                Rank = rank,
                Priority = PriorityByRank(rank),
                Score = x.score,
                DrivingEvents = mergedDrivers,
            };
        }).ToList();

        var liveEvents = MergeLiveEvents(baseBriefing.LiveEvents, newEvents.Where(e => driverIds.Contains(e.Id)));

        var updated = baseBriefing with
        {
            PriorityCallList = newCalls,
            LiveEvents = liveEvents,
        };
        return (updated, driverIds);
    }

    // Same banding the DEMO composer uses (TdBriefingComposer.PriorityByRank): top pair P1, next P2, …
    private static int PriorityByRank(int rank) => rank switch
    {
        <= 2 => 1,
        <= 4 => 2,
        <= 6 => 3,
        _ => 4,
    };

    private static IReadOnlyList<MarketEvent> MergeLiveEvents(
        IReadOnlyList<MarketEvent> existing, IEnumerable<MarketEvent> incoming)
    {
        var seen = new HashSet<string>(existing.Select(e => e.Id), StringComparer.Ordinal);
        var merged = existing.ToList();
        foreach (var e in incoming)
        {
            if (seen.Add(e.Id)) merged.Add(e);
        }
        return merged;
    }
}
