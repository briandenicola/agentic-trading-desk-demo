using FluentAssertions;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Models;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Unit tests for <see cref="EventImpactResolver"/> (002 US1, T014/T015). Locks the
/// deterministic event → entity mapping (R5), the net-sum conflict resolution with all
/// drivers preserved (R6), and the no-impact / empty-store behaviour (US1 AS3, R11).
/// </summary>
public sealed class EventImpactResolverTests
{
    private static MarketEvent Event(
        string id,
        string type = "sector",
        string severity = "high",
        string? direction = "negative",
        string[]? customerIds = null,
        string[]? sectors = null,
        string[]? tickers = null,
        string[]? issuers = null) => new()
    {
        Id = id,
        Type = type,
        Headline = $"Headline {id}",
        Summary = $"Summary {id}",
        Severity = severity,
        Direction = direction,
        AffectedEntities = new AffectedEntities
        {
            CustomerIds = customerIds,
            Sectors = sectors,
            Tickers = tickers,
            Issuers = issuers,
        },
    };

    [Fact]
    public void Customer_matched_by_id_yields_one_driver()
    {
        var events = new[] { Event("evt-1", customerIds: ["CB-10005"]) };

        var linkages = EventImpactResolver.ResolveForCustomer("CB-10005", "Agriculture", events);

        linkages.Should().HaveCount(1);
        linkages[0].EventId.Should().Be("evt-1");
        linkages[0].EntityRef.Should().Be("CB-10005");
        linkages[0].Contribution.Should().Be(EventImpactResolver.SeverityHigh);
    }

    [Fact]
    public void Customer_matched_by_sector_yields_driver()
    {
        var events = new[] { Event("evt-1", sectors: ["Healthcare"]) };

        var linkages = EventImpactResolver.ResolveForCustomer("CB-10016", "Healthcare", events);

        linkages.Should().HaveCount(1);
        linkages[0].EventId.Should().Be("evt-1");
    }

    [Fact]
    public void Conflicting_events_on_one_entity_net_sum_and_both_remain_drivers()
    {
        // A negative high (+30) and a positive high (+18) on the same client.
        var events = new[]
        {
            Event("evt-neg", severity: "high", direction: "negative", customerIds: ["CB-10005"]),
            Event("evt-pos", severity: "high", direction: "positive", customerIds: ["CB-10005"]),
        };

        var linkages = EventImpactResolver.ResolveForCustomer("CB-10005", "Agriculture", events);

        linkages.Should().HaveCount(2, "both contributing events remain visible as drivers (R6)");
        linkages.Select(l => l.EventId).Should().Contain(["evt-neg", "evt-pos"]);

        var net = EventImpactResolver.NetContribution(linkages);
        net.Should().Be(30 + 18, "contributions net (sum) deterministically");
    }

    [Fact]
    public void Resolve_is_order_stable_across_input_orderings()
    {
        var a = Event("evt-a", severity: "high", customerIds: ["CB-1"]);
        var b = Event("evt-b", severity: "high", customerIds: ["CB-1"]);

        var forward = EventImpactResolver.ResolveForCustomer("CB-1", null, [a, b]);
        var reverse = EventImpactResolver.ResolveForCustomer("CB-1", null, [b, a]);

        forward.Select(l => l.EventId).Should().Equal(reverse.Select(l => l.EventId));
    }

    [Fact]
    public void Event_matching_no_entity_yields_no_drivers()
    {
        var events = new[] { Event("evt-1", customerIds: ["CB-99999"], sectors: ["Mining"]) };

        var linkages = EventImpactResolver.ResolveForCustomer("CB-10005", "Agriculture", events);

        linkages.Should().BeEmpty();
        EventImpactResolver.NetContribution(linkages).Should().Be(0);
    }

    [Fact]
    public void Empty_store_yields_no_drivers()
    {
        var linkages = EventImpactResolver.ResolveForCustomer("CB-10005", "Agriculture", []);

        linkages.Should().BeEmpty();
        EventImpactResolver.NetContribution(linkages).Should().Be(0);
    }

    [Fact]
    public void Severity_drives_magnitude_for_negative_events()
    {
        var high = EventImpactResolver.ResolveForCustomer("CB-1", null, [Event("h", severity: "high", customerIds: ["CB-1"])]);
        var med = EventImpactResolver.ResolveForCustomer("CB-1", null, [Event("m", severity: "medium", customerIds: ["CB-1"])]);
        var low = EventImpactResolver.ResolveForCustomer("CB-1", null, [Event("l", severity: "low", customerIds: ["CB-1"])]);

        high[0].Contribution.Should().BeGreaterThan(med[0].Contribution);
        med[0].Contribution.Should().BeGreaterThan(low[0].Contribution);
    }

    [Fact]
    public void Security_matched_by_ticker_issuer_or_sector()
    {
        var byTicker = EventImpactResolver.ResolveForSecurity("SEC-3003", "SEC-3003", null, null, [Event("t", tickers: ["SEC-3003"])]);
        var byIssuer = EventImpactResolver.ResolveForSecurity("Quartzite", null, "Quartzite", null, [Event("i", issuers: ["Quartzite"])]);
        var bySector = EventImpactResolver.ResolveForSecurity("SEC-1", null, null, "Technology", [Event("s", sectors: ["Technology"])]);

        byTicker.Should().HaveCount(1);
        byIssuer.Should().HaveCount(1);
        bySector.Should().HaveCount(1);
    }
}
