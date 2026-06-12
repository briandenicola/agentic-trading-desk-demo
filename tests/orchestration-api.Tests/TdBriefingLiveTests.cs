using FluentAssertions;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Live;
using OrchestrationApi.Models;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Tests for <see cref="TdBriefingLive.ApplyEvents"/> — the deterministic overlay that re-ranks the
/// LIVE Trading Desk briefing on a News Desk inject without re-running the Foundry agent. Verifies
/// that a post-base event lifts the affected client's score and re-ranks/re-bands the call list,
/// that events already reflected in the base are NOT double-counted, and that non-matching / empty
/// event sets leave the briefing untouched. All data is fictional.
/// </summary>
public sealed class TdBriefingLiveTests
{
    private static TdPriorityCall Call(string clientId, int rank, int score) => new()
    {
        Rank = rank,
        Priority = rank <= 2 ? 1 : rank <= 4 ? 2 : rank <= 6 ? 3 : 4,
        ClientId = clientId,
        ClientName = clientId,
        Score = score,
        Rationale = new TdCallRationale
        {
            NewsRelevance = 0,
            OpenRfqWeight = 0,
            InquiryWeight = 0,
            InventoryAxeMatch = 0,
            Urgency = 0,
            CompositeScore = score,
            Explanation = "base",
        },
        WhyNow = [],
        TalkingPoints = [],
        TradeIdeas = [],
        SuggestedAction = "Call",
    };

    private static TdBriefing BaseBriefing(params TdPriorityCall[] calls) => new()
    {
        Mode = "LIVE",
        AsOf = "2026-05-22",
        Greeting = "Good morning, Theo",
        Salesperson = new SalespersonIdentity { SalespersonId = "Theo Wexler", Name = "Theo Wexler", ClientCount = calls.Length },
        MarketStrip = [],
        MacroThemes = [],
        Reasoning = [],
        PriorityCallList = calls,
        InventoryAxes = [],
        SuggestedFirstAction = "Start with #1",
    };

    private static MarketEvent Event(string id, string severity, string direction, AffectedEntities entities) => new()
    {
        Id = id,
        Type = "issuer_credit",
        Headline = $"Headline {id}",
        Summary = $"Summary {id}",
        Severity = severity,
        Direction = direction,
        IngestedAt = "2026-05-22T13:30:00Z",
        AffectedEntities = entities,
    };

    private static Dictionary<string, ClientEntitySet> EntitySets() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["CL-2015"] = new ClientEntitySet(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SEC-3602" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Prairie Green Renewables" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Utilities" }),
        ["CL-3000"] = new ClientEntitySet(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SEC-9999" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Technology" }),
    };

    [Fact]
    public void Post_base_event_lifts_the_affected_client_and_re_ranks()
    {
        // Base: CL-3000 (#1, 85) ahead of CL-2015 (#2, 80).
        var baseBrief = BaseBriefing(Call("CL-3000", 1, 85), Call("CL-2015", 2, 80));
        var evt = Event("EVT-1", "high", "negative", new AffectedEntities { Tickers = ["SEC-3602"] });
        var baseIds = new HashSet<string>(StringComparer.Ordinal); // base built with no events

        var (brief, drivers) = TdBriefingLive.ApplyEvents(baseBrief, [evt], baseIds, EntitySets());

        drivers.Should().Contain("EVT-1");
        // CL-2015 gains +30 (high/negative direct hit) → 110 > 85, so it now leads.
        var top = brief.PriorityCallList[0];
        top.ClientId.Should().Be("CL-2015");
        top.Rank.Should().Be(1);
        top.Priority.Should().Be(1);
        top.Score.Should().Be(110);
        top.DrivingEvents.Should().Contain(d => d.EventId == "EVT-1");

        brief.PriorityCallList[1].ClientId.Should().Be("CL-3000");
        brief.PriorityCallList[1].Rank.Should().Be(2);
        brief.LiveEvents.Select(e => e.Id).Should().Contain("EVT-1");
    }

    [Fact]
    public void Events_already_in_the_base_are_not_double_counted()
    {
        var baseBrief = BaseBriefing(Call("CL-3000", 1, 85), Call("CL-2015", 2, 80));
        var evt = Event("EVT-1", "high", "negative", new AffectedEntities { Tickers = ["SEC-3602"] });
        var baseIds = new HashSet<string>(StringComparer.Ordinal) { "EVT-1" }; // already reflected in base

        var (brief, drivers) = TdBriefingLive.ApplyEvents(baseBrief, [evt], baseIds, EntitySets());

        drivers.Should().BeEmpty();
        brief.PriorityCallList[0].ClientId.Should().Be("CL-3000");
        brief.PriorityCallList[0].Score.Should().Be(85);
    }

    [Fact]
    public void Non_matching_event_leaves_the_briefing_unchanged()
    {
        var baseBrief = BaseBriefing(Call("CL-3000", 1, 85), Call("CL-2015", 2, 80));
        var evt = Event("EVT-Z", "high", "negative", new AffectedEntities { Tickers = ["SEC-0000"], Sectors = ["Energy"] });

        var (brief, drivers) = TdBriefingLive.ApplyEvents(
            baseBrief, [evt], new HashSet<string>(StringComparer.Ordinal), EntitySets());

        drivers.Should().BeEmpty();
        brief.PriorityCallList[0].ClientId.Should().Be("CL-3000");
    }

    [Fact]
    public void No_events_returns_the_briefing_unchanged()
    {
        var baseBrief = BaseBriefing(Call("CL-3000", 1, 85), Call("CL-2015", 2, 80));

        var (brief, drivers) = TdBriefingLive.ApplyEvents(
            baseBrief, null, new HashSet<string>(StringComparer.Ordinal), EntitySets());

        drivers.Should().BeEmpty();
        brief.Should().BeSameAs(baseBrief);
    }
}
