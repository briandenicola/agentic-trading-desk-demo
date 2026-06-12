using System.Text.Json;
using FluentAssertions;
using OrchestrationApi.Agents;
using OrchestrationApi.Models;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Regression tests for the LIVE trading-desk synthesizer output handling (checkpoint 032).
///
/// The deployed LIVE agent produced a large, prose-wrapped response whose embedded briefing JSON
/// (a) was not a clean first-<c>{</c>-to-last-<c>}</c> slice and (b) routinely omitted one or more
/// <c>required</c> nested fields. The old code grabbed a corrupt span and/or threw a
/// <see cref="JsonException"/> on the first missing <c>required</c> member, discarding the ENTIRE
/// briefing and silently falling back to the deterministic composer — which is why editing the
/// Foundry prompt never changed the on-screen output. These tests lock in the fix: the model's
/// actual call list survives.
/// </summary>
public sealed class TdLiveParsingTests
{
    [Fact]
    public void ExtractJsonObject_picks_briefing_object_from_prose_and_reasoning()
    {
        var raw =
            "Here is my reasoning. I called get_clients and scored the book.\n" +
            "Some tool returned {\"clientId\":\"CL-1\",\"note\":\"ignore me\"} earlier.\n" +
            "Final answer:\n" +
            "{\"mode\":\"LIVE\",\"priorityCallList\":[{\"clientId\":\"CL-2006\"}]}\n" +
            "Let me know if you need anything else.";

        var json = TdAgentRunner.ExtractJsonObject(raw);

        json.Should().Contain("\"priorityCallList\"");
        json.Should().StartWith("{\"mode\"");
        // Must be valid, self-contained JSON (not a corrupt first-{-to-last-} span).
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("priorityCallList").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void ExtractJsonObject_unwraps_markdown_fence()
    {
        var raw = "```json\n{\"mode\":\"LIVE\",\"priorityCallList\":[]}\n```";

        var json = TdAgentRunner.ExtractJsonObject(raw);

        Action parse = () => JsonDocument.Parse(json);
        parse.Should().NotThrow();
        json.Should().Contain("\"priorityCallList\"");
    }

    [Fact]
    public void Deserialize_tolerates_missing_required_nested_fields()
    {
        // A realistic small-model briefing that OMITS several `required` members: the call has no
        // rationale/whyNow/talkingPoints/tradeIdeas/suggestedAction, the market strip item has no
        // direction, and several top-level required lists are absent. The old strict contract threw
        // here and nuked everything; now the call list must survive.
        const string partial = """
        {
          "mode": "LIVE",
          "asOf": "2026-05-22",
          "greeting": "Good morning, Theo",
          "salesperson": { "name": "Theo Wexler" },
          "marketStrip": [ { "label": "10Y", "value": "4.47%" } ],
          "priorityCallList": [
            { "clientId": "CL-2006", "clientName": "Tradewinds Partners", "score": 88 }
          ]
        }
        """;

        TdBriefing? brief = null;
        Action act = () => brief = JsonSerializer.Deserialize<TdBriefing>(partial, TdBriefingJson.Options);

        act.Should().NotThrow();
        brief.Should().NotBeNull();
        brief!.PriorityCallList.Should().HaveCount(1);
        brief.PriorityCallList[0].ClientName.Should().Be("Tradewinds Partners");
        brief.PriorityCallList[0].Score.Should().Be(88);
        // Omitted required lists default to empty (not null) so downstream code is null-safe.
        brief.MacroThemes.Should().NotBeNull();
        brief.PriorityCallList[0].TalkingPoints.Should().NotBeNull();
    }
}
