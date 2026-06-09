using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents.EventSynthesis;

/// <summary>
/// LIVE Foundry wiring for the per-event specialist (002 US4, FR-018, FR-021). Builds the
/// persistent <c>event-specialist</c> agent via the Microsoft Agent Framework
/// (resolve-by-name → fallback create, the same pattern the briefing runners use), exposing one
/// tool (<c>get_events_by_entity</c>) so each specialist run shows a tool-call in the trace
/// (SC-007). <see cref="AssessAsync"/> runs the agent over a single event and parses its
/// structured <see cref="EventImpactAssessment"/> JSON, degrading to a deterministic fallback if
/// the model output is unusable so the fan-out never fails on one event (FR-011).
///
/// Construction is side-effect free: no credential is acquired and nothing on Foundry runs
/// unless <see cref="CreateAgentAsync"/> / <see cref="AssessAsync"/> are invoked (LIVE only).
/// </summary>
public sealed class FoundryEventSpecialist(IConfiguration config, EventTools eventTools, ILogger<FoundryEventSpecialist> logger)
{
    public const string AgentName = "event-specialist";

    public const string AgentDescription =
        "Assesses one market/news event's portfolio impact and resolves it to typed entity selectors with a signed priority contribution.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Resolve (or self-heal) the persistent Foundry event-specialist agent.</summary>
    public async Task<AIAgent> CreateAgentAsync(CancellationToken ct = default)
    {
        var endpoint = config["FOUNDRY_PROJECT_ENDPOINT"]
            ?? throw new InvalidOperationException("LIVE mode requires FOUNDRY_PROJECT_ENDPOINT.");
        var model = config["FOUNDRY_MODEL"] ?? "gpt-5.4-mini";
        var instructions = await LoadInstructionsAsync(ct);

        var credential = new DefaultAzureCredential();
        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
        var aiTools = BuildTools();

#pragma warning disable CS0618 // rc5 transitional API; native AIProjectClient.Agents APIs are not yet stable.
        AIAgent baseAgent;
        try
        {
            baseAgent = await projectClient.GetAIAgentAsync(AgentName, aiTools, cancellationToken: ct);
            logger.LogInformation("Reusing persistent Foundry agent '{Agent}'.", AgentName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Persistent agent '{Agent}' not found; creating it (run the provisioner to persist it).", AgentName);
            baseAgent = await projectClient.CreateAIAgentAsync(
                name: AgentName,
                model: model,
                instructions: instructions,
                description: AgentDescription,
                tools: aiTools,
                cancellationToken: ct);
        }
#pragma warning restore CS0618

        var captureContent = !string.Equals(config["OTEL_CAPTURE_MESSAGE_CONTENT"], "false", StringComparison.OrdinalIgnoreCase);
        return baseAgent
            .AsBuilder()
            .UseOpenTelemetry(sourceName: OrchestrationTelemetry.SourceName, configure: c => c.EnableSensitiveData = captureContent)
            .Build();
    }

    /// <summary>Run the specialist over one event, returning its structured assessment (or a fallback).</summary>
    public async Task<EventImpactAssessment?> AssessAsync(AIAgent agent, MarketEvent e, CancellationToken ct = default)
    {
        var userMessage =
            "Assess the portfolio impact of this single event and return ONLY the JSON impact " +
            "assessment object (no prose). Resolve its affected entities into typed `kind:value` " +
            "selectors and a signed contribution.\n\nEVENT:\n" +
            JsonSerializer.Serialize(e, Json);

        try
        {
            var response = await agent.RunAsync(userMessage, cancellationToken: ct);
            var json = ExtractJsonObject(response.Text);
            var assessment = JsonSerializer.Deserialize<EventImpactAssessment>(json, RmBriefingJson.Options);
            if (assessment is null || string.IsNullOrWhiteSpace(assessment.EventId))
            {
                return Fallback(e);
            }
            return assessment;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Specialist assessment for {EventId} failed to parse; using a fallback.", e.Id);
            return Fallback(e);
        }
    }

    // ---------------------------------------------------------------- tools + helpers

    private IList<AITool> BuildTools() =>
    [
        AIFunctionFactory.Create(
            async (string value, string kind, CancellationToken ct) =>
            {
                using var span = OrchestrationTelemetry.ActivitySource.StartActivity("execute_tool get_events_by_entity", ActivityKind.Client);
                span?.SetTag("gen_ai.operation.name", "execute_tool");
                span?.SetTag("gen_ai.tool.name", "get_events_by_entity");
                var events = await eventTools.GetEventsByEntityAsync(value, string.IsNullOrWhiteSpace(kind) ? null : kind, ct);
                return JsonSerializer.Serialize(events, Json);
            },
            "get_events_by_entity",
            "Events affecting one entity (value = customerId/sector/ticker/issuer; kind = 'customer'|'sector'|'ticker'|'issuer', empty for any)."),
    ];

    private async Task<string> LoadInstructionsAsync(CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "event-specialist.md");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        logger.LogWarning("Prompt file not found at {Path}; using a minimal fallback instruction.", path);
        return "You are an Event Specialist. Assess one event and return a JSON impact assessment with typed selectors and a signed contribution.";
    }

    private static string ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : "{}";
    }

    /// <summary>Deterministic fallback (mirrors the DEMO resolver semantics) when the model output is unusable.</summary>
    private static EventImpactAssessment Fallback(MarketEvent e)
    {
        var magnitude = (e.Severity ?? "medium").ToLowerInvariant() switch
        {
            "high" => 30.0,
            "low" => 10.0,
            _ => 20.0,
        };
        var direction = (e.Direction ?? "neutral").ToLowerInvariant();
        var (contribution, lens) = direction switch
        {
            "negative" => (magnitude, "downside risk to manage"),
            "positive" => (Math.Round(magnitude * 0.6), "an engagement opportunity"),
            _ => (Math.Round(magnitude * 0.5), "a development to track"),
        };

        var selectors = new List<string>();
        var a = e.AffectedEntities;
        if (a.CustomerIds is { } cids) selectors.AddRange(cids.Select(c => $"customerId:{c}"));
        if (a.Tickers is { } tks) selectors.AddRange(tks.Select(t => $"ticker:{t}"));
        if (a.Sectors is { } secs) selectors.AddRange(secs.Select(s => $"sector:{s}"));
        if (a.Issuers is { } iss) selectors.AddRange(iss.Select(i => $"issuer:{i}"));

        return new EventImpactAssessment
        {
            EventId = e.Id,
            Headline = e.Headline,
            Severity = e.Severity,
            Direction = e.Direction,
            Selectors = selectors,
            Contribution = contribution,
            Lens = lens,
            Rationale = $"{e.Headline} — {lens} (+{contribution:0.#} priority).",
        };
    }
}
