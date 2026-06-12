using FluentAssertions;
using OrchestrationApi.Live;
using OrchestrationApi.Models;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Tests for <see cref="TdNewIssueLive.ApplyEvents"/> — the helper that folds injected market events
/// into the New Issue Radar storyboard so it reacts to the News Desk the same way the briefings do
/// (002 US2). Verifies driver matching (tranche ticker / issuer / sector / focus client), that a
/// matching event stamps live evidence + metrics + talking point and surfaces in <c>LiveEvents</c>,
/// and that a non-matching event leaves the storyboard untouched. All data is fictional.
/// </summary>
public sealed class TdNewIssueLiveTests
{
    private static TdNewIssueStoryboard BaseStory() => new()
    {
        Mode = "DEMO",
        AsOf = "2026-05-22",
        Title = "New Issue Radar",
        Subtitle = "Who wants our call first?",
        Issuer = new NewIssueIssuer
        {
            Name = "Prairie Green Renewables",
            Sector = "Utilities",
            Headline = "Prairie Green prices concurrent equity + senior note",
            Tranches =
            [
                new NewIssueTranche { SecurityId = "SEC-3601", SecurityName = "Prairie Green Eq", AssetClass = "Equity" },
                new NewIssueTranche { SecurityId = "SEC-3602", SecurityName = "Prairie Green 6.00% 2034", AssetClass = "Corporate Bond" },
            ],
        },
        Steps =
        [
            new TdStoryboardStep { Id = "announcement", Order = 1, Beat = "Announcement", Title = "New issue", Narration = "...", Metrics = [new StoryboardMetric { Label = "Size", Value = "$1.0bn" }], Evidence = [new StoryboardEvidence { Kind = "news", Label = "Deal announced" }] },
            new TdStoryboardStep { Id = "holdings", Order = 2, Beat = "Holdings", Title = "Who owns it", Narration = "..." },
            new TdStoryboardStep { Id = "outreach", Order = 4, Beat = "Outreach", Title = "Call now", Narration = "..." },
        ],
        Outreach = new TdOutreachRecommendation
        {
            ClientId = "CL-2015",
            ClientName = "Crestline Capital",
            Headline = "Call Crestline now",
            TalkingPoints = ["You hold the equity; we can show you the new note."],
            SuggestedAction = "Call now",
        },
    };

    private static MarketEvent Event(string id, AffectedEntities entities) => new()
    {
        Id = id,
        Type = "issuer_credit",
        Headline = $"Headline {id}",
        Summary = $"Summary {id}",
        Severity = "high",
        IngestedAt = "2026-05-22T13:30:00Z",
        AffectedEntities = entities,
    };

    [Theory]
    [InlineData("by-ticker")]
    [InlineData("by-issuer")]
    [InlineData("by-sector")]
    [InlineData("by-client")]
    public void Matching_event_is_folded_in_as_a_driver(string match)
    {
        var entities = match switch
        {
            "by-ticker" => new AffectedEntities { Tickers = ["SEC-3602"] },
            "by-issuer" => new AffectedEntities { Issuers = ["Prairie Green Renewables"] },
            "by-sector" => new AffectedEntities { Sectors = ["Utilities"] },
            _ => new AffectedEntities { CustomerIds = ["CL-2015"] },
        };
        var evt = Event("EVT-1", entities);

        var (story, drivers) = TdNewIssueLive.ApplyEvents(BaseStory(), [evt]);

        drivers.Should().ContainSingle().Which.Should().Be("EVT-1");
        story.LiveEvents.Should().NotBeNull();
        story.LiveEvents!.Select(e => e.Id).Should().ContainSingle().Which.Should().Be("EVT-1");

        var announcement = story.Steps.Single(s => s.Id == "announcement");
        announcement.Evidence.Should().Contain(e => e.Live == true && e.Label == "Headline EVT-1");
        announcement.Metrics.First().Live.Should().BeTrue();

        var outreach = story.Steps.Single(s => s.Id == "outreach");
        outreach.Metrics.First().Live.Should().BeTrue();
        story.Outreach.TalkingPoints.First().Should().Contain("Headline EVT-1");
    }

    [Fact]
    public void Non_matching_event_leaves_the_storyboard_unchanged()
    {
        var evt = Event("EVT-X", new AffectedEntities { Tickers = ["SEC-9999"], Issuers = ["Acme Co"], Sectors = ["Technology"] });

        var (story, drivers) = TdNewIssueLive.ApplyEvents(BaseStory(), [evt]);

        drivers.Should().BeEmpty();
        story.LiveEvents.Should().BeNull();
        story.Steps.Single(s => s.Id == "announcement").Evidence.Should().NotContain(e => e.Live == true);
        story.Outreach.TalkingPoints.Should().HaveCount(1);
    }

    [Fact]
    public void No_events_returns_the_storyboard_unchanged()
    {
        var (story, drivers) = TdNewIssueLive.ApplyEvents(BaseStory(), null);

        drivers.Should().BeEmpty();
        story.LiveEvents.Should().BeNull();
    }
}
