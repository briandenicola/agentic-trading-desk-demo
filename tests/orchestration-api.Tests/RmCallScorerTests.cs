using FluentAssertions;
using OrchestrationApi.Agents.Demo;
using Xunit;
using static OrchestrationApi.Agents.Demo.RmCallScorer;

namespace OrchestrationApi.Tests;

/// <summary>
/// Unit tests for <see cref="RmCallScorer"/>. The headline test reproduces the exact
/// ranking of the client ground-truth sample (<c>assets/rm_daily_briefing_2026-05-14.html</c>)
/// from the signal subset that briefing surfaces, locking the confirmed scoring weights.
/// </summary>
public sealed class RmCallScorerTests
{
    private static readonly DateOnly AsOf = new(2026, 5, 14);

    [Fact]
    public void Score_reproduces_sample_briefing_ranking()
    {
        // (name, escalated, inProgress, followUp, stuckOpps, closingOpps) — the signals the sample shows.
        var customers = new (string Name, int Score)[]
        {
            ("Prairie Grain",  Score(1, 1, FollowUpUrgency.None,    2, 0)), // 60+30+50      = 140
            ("Central Plains", Score(0, 0, FollowUpUrgency.Today,   0, 1)), // 45+40         = 85
            ("Heartland",      Score(0, 0, FollowUpUrgency.Soon,    1, 0)), // 40+25         = 65
            ("GL Imaging",     Score(0, 0, FollowUpUrgency.Soon,    1, 0)), // 40+25         = 65
            ("Buckeye",        Score(0, 0, FollowUpUrgency.Overdue, 0, 0)), // 50            = 50
            ("Rockfield",      Score(0, 0, FollowUpUrgency.None,    2, 0)), // 2*25          = 50
            ("Prairie Eng",    Score(0, 0, FollowUpUrgency.None,    0, 1)), // 40            = 40
            ("Lakeshore",      Score(0, 0, FollowUpUrgency.None,    1, 0)), // 25            = 25
        };

        customers.Select(c => c.Score).Should()
            .ContainInOrder(140, 85, 65, 65, 50, 50, 40, 25);

        var ranked = customers
            .Select((c, i) => (c.Name, c.Score, Index: i))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Index) // stable: preserves source order on ties (as the sample does)
            .Select(c => c.Name)
            .ToArray();

        ranked.Should().Equal(
            "Prairie Grain", "Central Plains", "Heartland", "GL Imaging",
            "Buckeye", "Rockfield", "Prairie Eng", "Lakeshore");
    }

    [Theory]
    [InlineData(0, FollowUpUrgency.Today)]    // due exactly today
    [InlineData(-6, FollowUpUrgency.Overdue)] // 6 days overdue (within window)
    [InlineData(-20, FollowUpUrgency.None)]   // stale: older than the overdue window
    [InlineData(1, FollowUpUrgency.Soon)]     // due tomorrow
    [InlineData(2, FollowUpUrgency.Soon)]     // due in 2 days
    [InlineData(5, FollowUpUrgency.None)]     // further out than the soon window
    public void ClassifyFollowUp_buckets_by_distance_from_today(int dayOffset, FollowUpUrgency expected)
    {
        ClassifyFollowUp(AsOf.AddDays(dayOffset), AsOf).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(6, 3)]
    [InlineData(7, 4)]
    [InlineData(8, 4)]
    public void PriorityBand_maps_rank_pairs_to_colour_bands(int rank, int expectedBand)
    {
        PriorityBand(rank).Should().Be(expectedBand);
    }
}
