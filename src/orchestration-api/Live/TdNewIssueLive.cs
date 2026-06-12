using OrchestrationApi.Models;

namespace OrchestrationApi.Live;

/// <summary>
/// Folds injected/live market events (002-reactive-event-cockpit, US2) into a
/// <see cref="TdNewIssueStoryboard"/> so the New Issue Radar reacts to the News Desk the same way
/// the briefings do. An event is a <b>driver</b> when its affected entities touch the storyboard's
/// issuer, either tranche security, sector, or the focus client. Driver events are surfaced as a
/// "live" evidence row on the announcement beat, a live metric on both the announcement and outreach
/// beats, a leading outreach talking point, and the storyboard's <see cref="TdNewIssueStoryboard.LiveEvents"/>
/// list. Non-matching events are ignored. Pure and deterministic; runs identically over DEMO and
/// LIVE output (Principle III). All data is fictional.
/// </summary>
public static class TdNewIssueLive
{
    /// <summary>
    /// Returns the storyboard with any matching live events folded in, plus the set of driver event
    /// ids (used by the SSE hub to decide whether an update is impactful). When no event matches, the
    /// storyboard is returned unchanged with an empty driver set.
    /// </summary>
    public static (TdNewIssueStoryboard storyboard, HashSet<string> driverEventIds) ApplyEvents(
        TdNewIssueStoryboard story, IReadOnlyList<MarketEvent>? events)
    {
        var empty = new HashSet<string>(StringComparer.Ordinal);
        if (events is null || events.Count == 0) return (story, empty);

        var trancheIds = story.Issuer.Tranches
            .Select(t => t.SecurityId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issuerName = story.Issuer.Name;
        var sector = story.Issuer.Sector;
        var clientId = story.Outreach.ClientId;

        bool Matches(MarketEvent e)
        {
            var ae = e.AffectedEntities;
            if (ae is null) return false;
            if (ae.Tickers is not null && ae.Tickers.Any(t => trancheIds.Contains(t))) return true;
            if (ae.Issuers is not null && !string.IsNullOrWhiteSpace(issuerName) &&
                ae.Issuers.Any(i => string.Equals(i, issuerName, StringComparison.OrdinalIgnoreCase))) return true;
            if (ae.Sectors is not null && !string.IsNullOrWhiteSpace(sector) &&
                ae.Sectors.Any(s => string.Equals(s, sector, StringComparison.OrdinalIgnoreCase))) return true;
            if (ae.CustomerIds is not null && !string.IsNullOrWhiteSpace(clientId) &&
                ae.CustomerIds.Any(c => string.Equals(c, clientId, StringComparison.OrdinalIgnoreCase))) return true;
            return false;
        }

        var drivers = events
            .Where(Matches)
            .OrderByDescending(e => e.IngestedAt ?? e.PublishedAt ?? string.Empty, StringComparer.Ordinal)
            .ToList();
        if (drivers.Count == 0) return (story, empty);

        var driverIds = drivers
            .Select(e => e.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var lead = drivers[0];
        var more = drivers.Count > 1 ? $" (+{drivers.Count - 1} more)" : string.Empty;
        var liveMetric = new StoryboardMetric
        {
            Label = "Live update",
            Value = Truncate(lead.Headline, 48),
            Sub = more.Length > 0 ? more.Trim() : null,
            Tone = "warning",
            Live = true
        };

        var liveEvidence = drivers.Select(e => new StoryboardEvidence
        {
            Kind = "news",
            Label = e.Headline,
            Detail = e.Summary,
            RefId = string.IsNullOrWhiteSpace(e.Id) ? null : e.Id,
            Date = e.IngestedAt ?? e.PublishedAt,
            Live = true
        }).ToList();

        var steps = story.Steps.Select(s =>
        {
            if (string.Equals(s.Id, "announcement", StringComparison.OrdinalIgnoreCase))
            {
                return s with
                {
                    Metrics = Prepend(liveMetric, s.Metrics),
                    Evidence = liveEvidence.Concat(s.Evidence).ToList()
                };
            }
            if (string.Equals(s.Id, "outreach", StringComparison.OrdinalIgnoreCase))
            {
                return s with { Metrics = Prepend(liveMetric, s.Metrics) };
            }
            return s;
        }).ToList();

        var talkingPoint = $"Just crossed — {lead.Headline}{more}. Lead the call with it: it moves the {issuerName} deal you're already anchored in.";
        var outreach = story.Outreach with
        {
            TalkingPoints = new[] { talkingPoint }.Concat(story.Outreach.TalkingPoints).ToList()
        };

        var stamped = story with
        {
            Steps = steps,
            Outreach = outreach,
            LiveEvents = drivers
        };
        return (stamped, driverIds);
    }

    private static IReadOnlyList<StoryboardMetric> Prepend(StoryboardMetric metric, IReadOnlyList<StoryboardMetric> rest)
        => new[] { metric }.Concat(rest).ToList();

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..(max - 1)] + "…";
}
