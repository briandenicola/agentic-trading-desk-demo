using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Agents.EventSynthesis;
using OrchestrationApi.Agents.Resilience;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents;

/// <summary>
/// LIVE path for the Commercial Banking RM Daily Briefing: runs a persistent Azure AI
/// Foundry agent through the Microsoft Agent Framework
/// (<c>Microsoft.Agents.AI</c> + <c>Microsoft.Agents.AI.AzureAI</c>), authenticating with
/// <see cref="DefaultAzureCredential"/>. The agent calls the CB mock endpoints as tools
/// (<see cref="RmBriefingTools"/>) and emits the same <see cref="RmBriefing"/> DTO the DEMO
/// composer returns (Principle III / FR-010).
///
/// In LIVE mode the agent acts as the <b>briefing synthesizer</b> (002 US4): before it runs,
/// <see cref="EventFanOut"/> fans out one <see cref="FoundryEventSpecialist"/> assessment per
/// current event (concurrently, traceably), and those assessments are fed into the synthesizer
/// so the call ranking reflects every event (FR-018, SC-007).
///
/// Mirrors <see cref="AgentRunner"/> (the municipal morning brief) but is a separate class
/// so the two scenes stay decoupled. Construction is side-effect free: no credential is
/// acquired and nothing on Foundry runs unless <see cref="RunAsync"/> is invoked (only in
/// LIVE mode). The single Foundry-specific step is isolated in
/// <see cref="CreateFoundryAgentAsync"/> so a prerelease (rc5) API change cannot affect the
/// offline DEMO path.
/// </summary>
public sealed class RmAgentRunner(
    IConfiguration config,
    RmBriefingTools tools,
    EventTools eventTools,
    EventFanOut fanOut,
    FoundryEventSpecialist specialist,
    ILogger<RmAgentRunner> logger)
{
    private const string AgentName = "rm-daily-briefing";

    private const string AgentDescription =
        "Produces the Commercial Banking RM daily briefing and prioritized call list by calling the mock systems-of-record as tools.";

    public async Task<RmBriefing> RunAsync(string rmId, string? date, CancellationToken ct = default)
    {
        var endpoint = config["FOUNDRY_PROJECT_ENDPOINT"]
            ?? throw new InvalidOperationException("LIVE mode requires FOUNDRY_PROJECT_ENDPOINT.");
        var model = config["FOUNDRY_MODEL"] ?? "gpt-5.4-mini";
        var maxHops = int.TryParse(config["MAX_TOOL_HOPS"], out var h) && h > 0 ? h : 12;

        var instructions = await LoadInstructionsAsync(ct);

        AIAgent agent;
        try
        {
            agent = await CreateFoundryAgentAsync(endpoint, model, instructions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the Foundry RM-briefing agent; returning a degraded briefing.");
            return Degraded(rmId, date, $"LIVE agent unavailable: {ex.Message}");
        }

        var userMessage =
            $"Produce the RM daily briefing for relationship manager '{rmId}' as of {date ?? RmBriefingComposer.DefaultDate}. " +
            $"Call the tools to load the RM book, open opportunities, active complaints and due follow-ups, " +
            $"score each customer, and emit ONLY the JSON object matching the rm-briefing schema. " +
            $"Use at most {maxHops} tool calls.";

        using var runSpan = OrchestrationTelemetry.ActivitySource.StartActivity("rm_briefing.run", ActivityKind.Internal);
        runSpan?.SetTag("wf.rm_id", rmId);
        runSpan?.SetTag("wf.mode", "LIVE");
        runSpan?.SetTag("wf.briefing_day", date ?? RmBriefingComposer.DefaultDate);
        runSpan?.SetTag("gen_ai.request.model", model);
        runSpan?.SetTag("gen_ai.request.max_tool_calls", maxHops);

        try
        {
            var (synthMessage, events) = await ApplyEventFanOutAsync(userMessage, runSpan, ct);
            var (maxAttempts, baseDelay) = FoundryRetry.SettingsFrom(config);
            var response = await FoundryRetry.ExecuteAsync(
                c => agent.RunAsync(synthMessage, cancellationToken: c),
                maxAttempts, baseDelay, logger, $"rm-briefing synthesizer ({rmId})", ct);

            var usage = response.Usage;
            if (usage is not null)
            {
                runSpan?.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount);
                runSpan?.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount);
                runSpan?.SetTag("gen_ai.usage.total_tokens", usage.TotalTokenCount);
                if (usage.TotalTokenCount is long total)
                {
                    OrchestrationTelemetry.TokenUsage.Record(total, new KeyValuePair<string, object?>("wf.rm_id", rmId));
                }
                logger.LogInformation(
                    "LIVE rm-briefing token usage (rm={RmId}): input={Input} output={Output} total={Total}",
                    rmId, usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
            }

            var json = ExtractJsonObject(response.Text);
            // EventsConsidered is sourced from the authoritative event store (the same list the
            // fan-out fetched), not the model output, so the LIVE DTO carries the events it weighed
            // even when the synthesizer omits them — matching the DEMO composer (FR-018, Principle III).
            var brief = MapToBriefing(json, rmId, date) with { EventsConsidered = events };
            runSpan?.SetStatus(ActivityStatusCode.Ok);
            return brief;
        }
        catch (Exception ex)
        {
            runSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Foundry RM-briefing agent run failed; returning a degraded briefing.");
            return Degraded(rmId, date, $"LIVE agent run failed: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- event fan-out (US4)

    /// <summary>
    /// LIVE synthesizer pre-step (002 US4): list the current events, fan out one specialist
    /// assessment per event (concurrent + traceable), and append the assessments to the
    /// synthesizer's user message so the call ranking reflects every event. Failures degrade to
    /// the un-augmented message (FR-011) — the briefing is still produced.
    /// </summary>
    private async Task<(string Message, IReadOnlyList<MarketEvent> Events)> ApplyEventFanOutAsync(string userMessage, Activity? runSpan, CancellationToken ct)
    {
        IReadOnlyList<MarketEvent> events = [];
        IReadOnlyList<EventImpactAssessment> assessments = [];
        try
        {
            events = await eventTools.ListEventsAsync(null, ct);
            if (events.Count > 0)
            {
                var specialistAgent = await specialist.CreateAgentAsync(ct);
                assessments = await fanOut.AssessAllAsync(
                    "rm-briefing", events, (e, c) => specialist.AssessAsync(specialistAgent, e, c), ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Event fan-out failed; synthesizing the RM briefing without per-event assessments.");
        }

        runSpan?.SetTag("wf.fanout.assessment_count", assessments.Count);
        if (assessments.Count == 0)
        {
            return (userMessage, events);
        }

        return (userMessage +
            "\n\nPER-EVENT IMPACT ASSESSMENTS (from specialist agents — fold each contribution into " +
            "the affected customers' scores, re-rank, and list every contributing event as a driver):\n" +
            JsonSerializer.Serialize(assessments, RmBriefingJson.Options), events);
    }

    // ---------------------------------------------------------------- Foundry wiring (isolated)

    private async Task<AIAgent> CreateFoundryAgentAsync(string endpoint, string model, string instructions, CancellationToken ct)
    {
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

    private IList<AITool> BuildTools() =>
    [
        AIFunctionFactory.Create(
            (string rmId, string asOf, CancellationToken ct) => InvokeToolAsync("get_rm_book", c => tools.GetRmBookAsync(rmId, Blank(asOf), c), ct, ("rm_id", rmId), ("as_of", asOf)),
            "get_rm_book", "Book-level snapshot for an RM id (manager, customers, headline KPIs). Pass an empty string for as_of to use today."),
        AIFunctionFactory.Create(
            (string rm, string asOf, CancellationToken ct) => InvokeToolAsync("get_open_opportunities", c => tools.GetOpenOpportunitiesAsync(rm, Blank(asOf), c), ct, ("rm", rm), ("as_of", asOf)),
            "get_open_opportunities", "Open opportunities for an RM (by manager name). Pass an empty string for as_of to use today."),
        AIFunctionFactory.Create(
            (string rm, CancellationToken ct) => InvokeToolAsync("get_active_complaints", c => tools.GetActiveComplaintsAsync(rm, c), ct, ("rm", rm)),
            "get_active_complaints", "Active (unresolved) complaints for an RM (by manager name)."),
        AIFunctionFactory.Create(
            (string rm, string followUpDueBy, CancellationToken ct) => InvokeToolAsync("get_due_followups", c => tools.GetDueFollowUpsAsync(rm, followUpDueBy, c), ct, ("rm", rm), ("follow_up_due_by", followUpDueBy)),
            "get_due_followups", "Interactions with a follow-up due on or before a yyyy-MM-dd date (overdue + upcoming) for an RM."),
        AIFunctionFactory.Create(
            (string customerId, CancellationToken ct) => InvokeToolAsync("get_customer", c => tools.GetCustomerAsync(customerId, c), ct, ("customer_id", customerId)),
            "get_customer", "One customer's full profile by id."),
        AIFunctionFactory.Create(
            (string customerId, CancellationToken ct) => InvokeToolAsync("get_customer_opportunities", c => tools.GetCustomerOpportunitiesAsync(customerId, c), ct, ("customer_id", customerId)),
            "get_customer_opportunities", "One customer's opportunities by id."),
        AIFunctionFactory.Create(
            (string customerId, CancellationToken ct) => InvokeToolAsync("get_customer_interactions", c => tools.GetCustomerInteractionsAsync(customerId, c), ct, ("customer_id", customerId)),
            "get_customer_interactions", "One customer's interactions (call log + follow-ups) by id."),
        AIFunctionFactory.Create(
            (string scope, CancellationToken ct) => InvokeToolAsync("list_events", c => tools.GetCurrentEventsAsync(Blank(scope), c), ct, ("scope", scope)),
            "list_events", "Current market/news events to weigh into the call ranking. Pass an empty string for scope to get all (overnight + intraday)."),
        AIFunctionFactory.Create(
            (string value, string kind, CancellationToken ct) => InvokeToolAsync("get_events_by_entity", c => tools.GetEventsByEntityAsync(value, Blank(kind), c), ct, ("value", value), ("kind", kind)),
            "get_events_by_entity", "Events affecting one entity (value = customerId or sector; kind = 'customer' or 'sector', empty for any)."),
    ];

    private async Task<string> InvokeToolAsync(
        string toolName,
        Func<CancellationToken, Task<string>> call,
        CancellationToken ct,
        params (string Key, string? Value)[] args)
    {
        using var span = OrchestrationTelemetry.ActivitySource.StartActivity($"execute_tool {toolName}", ActivityKind.Client);
        span?.SetTag("gen_ai.operation.name", "execute_tool");
        span?.SetTag("gen_ai.tool.name", toolName);
        foreach (var (key, value) in args)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                span?.SetTag($"gen_ai.tool.arg.{key}", value);
            }
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call(ct);
            sw.Stop();
            span?.SetTag("wf.tool.result_bytes", result?.Length ?? 0);
            span?.SetTag("wf.tool.duration_ms", sw.Elapsed.TotalMilliseconds);
            OrchestrationTelemetry.ToolDuration.Record(
                sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("gen_ai.tool.name", toolName));
            return result ?? string.Empty;
        }
        catch (Exception ex)
        {
            sw.Stop();
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // ---------------------------------------------------------------- prompt + mapping

    private async Task<string> LoadInstructionsAsync(CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "rm-daily-briefing.md");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        logger.LogWarning("Prompt file not found at {Path}; using a minimal fallback instruction.", path);
        return "You are the RM Daily Briefing agent. Use the tools to gather the RM book and signals, score each customer, and emit a single JSON object matching the rm-briefing schema.";
    }

    private static string ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : "{}";
    }

    private RmBriefing MapToBriefing(string json, string rmId, string? date)
    {
        try
        {
            var brief = JsonSerializer.Deserialize<RmBriefing>(json, RmBriefingJson.Options);
            if (brief is null)
            {
                return Degraded(rmId, date, "LIVE agent returned no parseable briefing.");
            }
            return NormalizeLiveBriefing(brief);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse the LIVE agent output as an RmBriefing.");
            return Degraded(rmId, date, "LIVE agent output did not match the rm-briefing schema.");
        }
    }

    // Force the mode marker and re-derive rank + priority band from score so the ordering
    // is consistent regardless of how the model ordered the list.
    private static RmBriefing NormalizeLiveBriefing(RmBriefing brief)
    {
        var calls = brief.PriorityCallList
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.CustomerId, StringComparer.Ordinal)
            .Select((c, i) => c with { Rank = i + 1, Priority = RmCallScorer.PriorityBand(i + 1) })
            .ToList();

        return brief with { Mode = "LIVE", PriorityCallList = calls };
    }

    private static RmBriefing Degraded(string rmId, string? date, string note) => new()
    {
        Mode = "LIVE",
        AsOf = date ?? RmBriefingComposer.DefaultDate,
        Greeting = "Good morning",
        Rm = new RmIdentity { RmId = rmId, Name = "" },
        Portfolio = new RmPortfolio { CustomerCount = 0, TotalExposureMm = 0, TotalDepositsMm = 0 },
        Kpis = new RmKpis { YesterdayTouchpoints = 0, OpenPipelineCount = 0, OpenPipelineAmountMm = 0, ClosingWithin14Days = 0, ActiveComplaints = 0 },
        Reasoning =
        [
            new ReasoningStep { Text = "Attempted to run the Foundry RM-briefing agent.", Status = "done" },
            new ReasoningStep { Text = "LIVE path unavailable — returned a degraded briefing.", Status = "pending" },
        ],
        PriorityCallList = [],
        ComplaintsSnapshot = [],
        PipelineClosing = [],
        MacroSnapshot = [],
        SuggestedFirstAction = "Verify Foundry connectivity/credentials or fall back to DEMO mode.",
        Notes = [note],
    };
}
