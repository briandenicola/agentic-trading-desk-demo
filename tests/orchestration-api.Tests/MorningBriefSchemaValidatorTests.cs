using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using OrchestrationApi.Models;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Exercises the schema-validation helper (T012): a well-formed MorningBrief DTO
/// serializes to a schema-valid payload, and malformed payloads are rejected.
/// </summary>
public sealed class MorningBriefSchemaValidatorTests
{
    [Fact]
    public void Valid_MorningBrief_DTO_passes_schema()
    {
        var brief = SampleBrief();
        var json = JsonSerializer.Serialize(brief, MorningBriefJson.Options);

        var (isValid, errors) = MorningBriefSchemaValidator.Validate(json);

        isValid.Should().BeTrue(errors);
    }

    [Fact]
    public void Payload_missing_required_field_fails_schema()
    {
        var json = JsonSerializer.Serialize(SampleBrief(), MorningBriefJson.Options);
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("macroNarrative"); // required by the schema

        var (isValid, _) = MorningBriefSchemaValidator.Validate(node);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void Payload_with_invalid_direction_enum_fails_schema()
    {
        var json = JsonSerializer.Serialize(SampleBrief(), MorningBriefJson.Options);
        var node = JsonNode.Parse(json)!.AsObject();
        node["marketStrip"]!.AsArray()[0]!["direction"] = "sideways"; // not in {up,down,flat}

        var (isValid, _) = MorningBriefSchemaValidator.Validate(node);

        isValid.Should().BeFalse();
    }

    internal static MorningBrief SampleBrief() => new()
    {
        Mode = "DEMO",
        AsOf = "2026-06-04T07:30:00-04:00",
        MarketStrip =
        [
            new MarketStripItem { Label = "10y UST", Value = "4.46%", Change = "+15bp", Direction = "up" },
            new MarketStripItem { Label = "S&P fut", Value = "-1.1%", Direction = "down" }
        ],
        Reasoning =
        [
            new ReasoningStep { Text = "Pulled overnight macro events + market reaction.", Status = "done" }
        ],
        MacroNarrative = new MacroNarrative
        {
            Summary = "The Fed delivered a surprise 25bp hike.",
            WhyItMatters = "The move repriced the front end hardest.",
            Sources = ["Fed statement", "Overnight rates feed"]
        },
        MostAffectedClients =
        [
            new AffectedClient
            {
                Cid = "ATLAS", Name = "Atlas Pension", Tier = "Tier 1",
                Exposure = "Heavy long-duration bonds",
                Concern = new Concern { Label = "Price drop", Kind = "sell" }
            }
        ],
        Outreach =
        [
            new OutreachItem
            {
                Rank = 1, Cid = "ATLAS", Name = "Atlas Pension",
                SuggestedTopic = "Discuss hedging; mention new 10Y swap axes available.",
                TalkingPoints = ["Lead with the duration impact from the Fed hike.", "Offer a 10Y swap to hedge price risk."],
                Rationale = new RankingRationale
                {
                    WalletScore = 0.34, EngagementScore = 0.80, EventRelevanceScore = 0.95,
                    CompositeScore = 0.66, Explanation = "High wallet + recent engagement + direct rate exposure."
                }
            }
        ]
    };
}
