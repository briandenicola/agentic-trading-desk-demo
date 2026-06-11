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
/// LIVE path for the Institutional Sales &amp; Trading morning briefing: runs a persistent Azure
/// AI Foundry agent through the Microsoft Agent Framework
/// (<c>Microsoft.Agents.AI</c> + <c>Microsoft.Agents.AI.AzureAI</c>), authenticating with
/// <see cref="DefaultAzureCredential"/>. The agent calls the Trading Desk mock endpoints as tools
/// (<see cref="TdBriefingTools"/>) and emits the same <see cref="TdBriefing"/> DTO the DEMO
/// composer returns (Principle III / FR-010).
///
/// In LIVE mode the agent acts as the <b>briefing synthesizer</b>: before it runs,
/// <see cref="EventFanOut"/> fans out one <see cref="FoundryEventSpecialist"/> assessment per
/// current event (concurrently, traceably), and those assessments are fed into the synthesizer so
/// the prioritized call ranking reflects every live event (the reactive re-rank the desk sees).
///
/// Mirrors <see cref="RmAgentRunner"/> but stays a separate class so the scenes are decoupled.
/// Construction is side-effect free: no credential is acquired and nothing on Foundry runs unless
/// <see cref="RunAsync"/> is invoked (only in LIVE mode). The single Foundry-specific step is
/// isolated in <see cref="CreateFoundryAgentAsync"/> so a prerelease (rc5) API change cannot
/// affect the offline DEMO path.
/// </summary>
public sealed class TdAgentRunner(
    IConfiguration config,
    TdBriefingTools tools,
    TdBriefingComposer composer,
    EventTools eventTools,
    EventFanOut fanOut,
    FoundryEventSpecialist specialist,
    ILogger<TdAgentRunner> logger)
{
    private const string AgentName = "trading-desk-morning";

    private const string AgentDescription =
        "Produces the institutional sales & trading morning briefing and prioritized client call list by calling the trading-desk mock systems-of-record as tools.";

    public async Task<TdBriefing> RunAsync(string salespersonId, string? date, CancellationToken ct = default)
    {
        var endpoint = config["FOUNDRY_PROJECT_ENDPOINT"]
            ?? throw new InvalidOperationException("LIVE mode requires FOUNDRY_PROJECT_ENDPOINT.");
        var model = config["FOUNDRY_MODEL"] ?? "gpt-5.4-mini";
        var maxHops = int.TryParse(config["MAX_TOOL_HOPS"], out var h) && h > 0 ? h : 16;

        var instructions = await LoadInstructionsAsync(ct);

        AIAgent agent;
        try
        {
            agent = await CreateFoundryAgentAsync(endpoint, model, instructions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the Foundry trading-desk agent; falling back to the deterministic briefing.");
            return await EnsurePopulatedAsync(Degraded(salespersonId, date, $"LIVE agent unavailable: {ex.Message}"), salespersonId, date, [], ct);
        }

        var userMessage =
            $"Produce the institutional sales & trading morning briefing for coverage salesperson '{salespersonId}' as of {date ?? TdBriefingComposer.DefaultDate}. " +
            $"Call the tools to load the book, the desk axes, overnight news and research, and each client's activity, " +
            $"score and rank each client, and emit ONLY the JSON object matching the td-briefing schema. " +
            $"Use at most {maxHops} tool calls.";

        using var runSpan = OrchestrationTelemetry.ActivitySource.StartActivity("td_briefing.run", ActivityKind.Internal);
        runSpan?.SetTag("wf.salesperson_id", salespersonId);
        runSpan?.SetTag("wf.mode", "LIVE");
        runSpan?.SetTag("wf.briefing_day", date ?? TdBriefingComposer.DefaultDate);
        runSpan?.SetTag("gen_ai.request.model", model);
        runSpan?.SetTag("gen_ai.request.max_tool_calls", maxHops);

        // Hoisted so the catch block can hand the authoritative event set to the safety net.
        IReadOnlyList<MarketEvent> events = [];
        try
        {
            string synthMessage;
            (synthMessage, events) = await ApplyEventFanOutAsync(userMessage, runSpan, ct);
            var (maxAttempts, baseDelay) = FoundryRetry.SettingsFrom(config);
            var response = await FoundryRetry.ExecuteAsync(
                c => agent.RunAsync(synthMessage, cancellationToken: c),
                maxAttempts, baseDelay, logger, $"td-briefing synthesizer ({salespersonId})", ct);

            var usage = response.Usage;
            if (usage is not null)
            {
                runSpan?.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount);
                runSpan?.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount);
                runSpan?.SetTag("gen_ai.usage.total_tokens", usage.TotalTokenCount);
                if (usage.TotalTokenCount is long total)
                {
                    OrchestrationTelemetry.TokenUsage.Record(total, new KeyValuePair<string, object?>("wf.salesperson_id", salespersonId));
                }
                logger.LogInformation(
                    "LIVE td-briefing token usage (salesperson={SalespersonId}): input={Input} output={Output} total={Total}",
                    salespersonId, usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
            }

            var json = ExtractJsonObject(response.Text);
            // LiveEvents is sourced from the authoritative event store (the same list the fan-out
            // fetched), not the model output, so the LIVE DTO always carries the events it weighed
            // — matching the DEMO composer (Principle III).
            var brief = MapToBriefing(json, salespersonId, date) with { LiveEvents = events };
            // Safety net: if the synthesizer dropped the prioritized call list, reconstruct it
            // deterministically so the cockpit is never empty (re-stamped LIVE, same JSON shape).
            brief = await EnsurePopulatedAsync(brief, salespersonId, date, events, ct);
            runSpan?.SetStatus(ActivityStatusCode.Ok);
            return brief;
        }
        catch (Exception ex)
        {
            runSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Foundry trading-desk agent run failed; falling back to the deterministic briefing.");
            return await EnsurePopulatedAsync(Degraded(salespersonId, date, $"LIVE agent run failed: {ex.Message}"), salespersonId, date, events, ct);
        }
    }

    // ---------------------------------------------------------------- deterministic safety net

    /// <summary>
    /// Deterministic safety net (FR-011 / Principle III): the LIVE synthesizer occasionally returns
    /// no prioritized calls (small-model variance, amplified by a large event fan-out blob). Rather
    /// than surface an empty briefing, reconstruct the prioritized call list from the same
    /// systems-of-record with <see cref="TdBriefingComposer"/> and re-stamp it <c>LIVE</c> so the
    /// JSON shape is unchanged and the cockpit always shows a correct, populated briefing. If the
    /// briefing already has calls, it is returned untouched.
    /// </summary>
    private async Task<TdBriefing> EnsurePopulatedAsync(
        TdBriefing brief, string salespersonId, string? date, IReadOnlyList<MarketEvent> events, CancellationToken ct)
    {
        if (brief.PriorityCallList.Count > 0)
        {
            return brief;
        }

        try
        {
            var deterministic = await composer.ComposeAsync(salespersonId, date, ct);
            if (deterministic.PriorityCallList.Count == 0)
            {
                return brief; // genuinely nothing to surface — keep the agent's briefing.
            }

            var notes = new List<string>(deterministic.Notes ?? [])
            {
                "LIVE synthesizer returned no prioritized calls; the briefing was reconstructed deterministically from the systems-of-record (graceful degrade).",
            };
            return deterministic with
            {
                Mode = "LIVE",
                LiveEvents = events.Count > 0 ? events : deterministic.LiveEvents,
                Notes = notes,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Deterministic safety-net composition failed; returning the LIVE agent's briefing unchanged.");
            return brief;
        }
    }

    // ---------------------------------------------------------------- event fan-out

    /// <summary>
    /// LIVE synthesizer pre-step: list the current events, fan out one specialist assessment per
    /// event (concurrent + traceable), and append the assessments to the synthesizer's user message
    /// so the client ranking reflects every event. Failures degrade to the un-augmented message
    /// (FR-011) — the briefing is still produced.
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
                    "td-briefing", events, (e, c) => specialist.AssessAsync(specialistAgent, e, c), ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Event fan-out failed; synthesizing the trading-desk briefing without per-event assessments.");
        }

        runSpan?.SetTag("wf.fanout.assessment_count", assessments.Count);
        if (assessments.Count == 0)
        {
            return (userMessage, events);
        }

        return (userMessage +
            "\n\nPER-EVENT IMPACT ASSESSMENTS (from specialist agents — fold each contribution into " +
            "the affected clients' scores, re-rank, and list every contributing event as a driver in " +
            "drivingEvents):\n" +
            JsonSerializer.Serialize(assessments, TdBriefingJson.Options), events);
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
            (string salesperson, CancellationToken ct) => InvokeToolAsync("get_clients", c => tools.GetClientsAsync(Blank(salesperson), ct: c), ct, ("salesperson", salesperson)),
            "get_clients", "The coverage salesperson's book of institutional clients (id, name, type, region, preferred asset class). Pass an empty string for an unfiltered list. Start here."),
        AIFunctionFactory.Create(
            (string clientId, string since, CancellationToken ct) => InvokeToolAsync("get_client_activity", c => tools.GetClientActivityAsync(clientId, Blank(since), c), ct, ("client_id", clientId), ("since", since)),
            "get_client_activity", "360-degree trailing activity for one client: holdings, trades, RFQs, inquiries, CRM. Pass an empty string for since to use the default window."),
        AIFunctionFactory.Create(
            (string clientId, CancellationToken ct) => InvokeToolAsync("get_client_holdings", c => tools.GetClientHoldingsAsync(clientId, c), ct, ("client_id", clientId)),
            "get_client_holdings", "One client's current position snapshot."),
        AIFunctionFactory.Create(
            (string securityId, string since, CancellationToken ct) => InvokeToolAsync("get_security_interest", c => tools.GetSecurityInterestAsync(securityId, Blank(since), c), ct, ("security_id", securityId), ("since", since)),
            "get_security_interest", "Everything pointing at one security: inventory/axes, holders, trades, RFQs, inquiries, news, research. Pass an empty string for since to use the default window."),
        AIFunctionFactory.Create(
            (string securityId, string desk, CancellationToken ct) => InvokeToolAsync("get_inventory", c => tools.GetInventoryAsync(Blank(securityId), Blank(desk), c), ct, ("security_id", securityId), ("desk", desk)),
            "get_inventory", "Dealer inventory / market-making axes the desk wants to work. Pass empty strings for all."),
        AIFunctionFactory.Create(
            (string securityId, string sector, string macroTheme, string since, CancellationToken ct) => InvokeToolAsync("get_news", c => tools.GetNewsAsync(Blank(securityId), Blank(sector), Blank(macroTheme), Blank(since), c), ct, ("security_id", securityId), ("sector", sector), ("macro_theme", macroTheme), ("since", since)),
            "get_news", "Market-moving news. Pass empty strings to get all overnight + recent news."),
        AIFunctionFactory.Create(
            (string securityId, string sector, string ratingAction, CancellationToken ct) => InvokeToolAsync("get_research", c => tools.GetResearchAsync(Blank(securityId), Blank(sector), Blank(ratingAction), c), ct, ("security_id", securityId), ("sector", sector), ("rating_action", ratingAction)),
            "get_research", "Published desk research notes. Pass empty strings to get all."),
        AIFunctionFactory.Create(
            (string clientId, string securityId, string status, string since, CancellationToken ct) => InvokeToolAsync("get_rfqs", c => tools.GetRfqsAsync(Blank(clientId), Blank(securityId), Blank(status), Blank(since), c), ct, ("client_id", clientId), ("security_id", securityId), ("status", status), ("since", since)),
            "get_rfqs", "Request-for-quote activity. Filter by client, security, response status or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (string clientId, string securityId, string sentiment, string since, CancellationToken ct) => InvokeToolAsync("get_inquiries", c => tools.GetInquiriesAsync(Blank(clientId), Blank(securityId), Blank(sentiment), Blank(since), c), ct, ("client_id", clientId), ("security_id", securityId), ("sentiment", sentiment), ("since", since)),
            "get_inquiries", "Less-structured client inquiries / colour. Filter by client, security, sentiment or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (string clientId, string urgency, string since, CancellationToken ct) => InvokeToolAsync("get_crm", c => tools.GetCrmAsync(Blank(clientId), Blank(urgency), Blank(since), c), ct, ("client_id", clientId), ("urgency", urgency), ("since", since)),
            "get_crm", "CRM call reports and follow-ups. Filter by client, urgency or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (CancellationToken ct) => InvokeToolAsync("get_narrative_themes", c => tools.GetNarrativeThemesAsync(c), ct),
            "get_narrative_themes", "The embedded cross-dataset storylines (macro themes) for narrative context."),
        AIFunctionFactory.Create(
            (string scope, CancellationToken ct) => InvokeToolAsync("list_events", c => tools.GetCurrentEventsAsync(Blank(scope), c), ct, ("scope", scope)),
            "list_events", "Current market/admin events (overnight + intraday) to weigh into the ranking. Pass an empty string for all."),
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
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "trading-desk-morning.md");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        logger.LogWarning("Prompt file not found at {Path}; using a minimal fallback instruction.", path);
        return "You are the Institutional Sales & Trading morning-planning agent. Use the tools to gather the book, desk axes, news and research, score each client, and emit a single JSON object matching the td-briefing schema.";
    }

    private static string ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : "{}";
    }

    private TdBriefing MapToBriefing(string json, string salespersonId, string? date)
    {
        try
        {
            var brief = JsonSerializer.Deserialize<TdBriefing>(json, TdBriefingJson.Options);
            if (brief is null)
            {
                return Degraded(salespersonId, date, "LIVE agent returned no parseable briefing.");
            }
            return NormalizeLiveBriefing(brief);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse the LIVE agent output as a TdBriefing.");
            return Degraded(salespersonId, date, "LIVE agent output did not match the td-briefing schema.");
        }
    }

    // Force the mode marker and re-derive rank + priority band from score so the ordering is
    // consistent regardless of how the model ordered the list.
    private static TdBriefing NormalizeLiveBriefing(TdBriefing brief)
    {
        var calls = brief.PriorityCallList
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.ClientId, StringComparer.Ordinal)
            .Select((c, i) => c with { Rank = i + 1, Priority = PriorityByRank(i + 1) })
            .ToList();

        return brief with { Mode = "LIVE", PriorityCallList = calls };
    }

    private static int PriorityByRank(int rank) => rank switch
    {
        <= 2 => 1,
        <= 4 => 2,
        <= 6 => 3,
        _ => 4,
    };

    private static TdBriefing Degraded(string salespersonId, string? date, string note) => new()
    {
        Mode = "LIVE",
        AsOf = date ?? TdBriefingComposer.DefaultDate,
        Greeting = "Good morning",
        Salesperson = new SalespersonIdentity { SalespersonId = salespersonId, Name = salespersonId, ClientCount = 0 },
        MarketStrip = [],
        MacroThemes = [],
        Reasoning =
        [
            new ReasoningStep { Text = "Attempted to run the Foundry trading-desk agent.", Status = "done" },
            new ReasoningStep { Text = "LIVE path unavailable — returned a degraded briefing.", Status = "pending" },
        ],
        PriorityCallList = [],
        InventoryAxes = [],
        SuggestedFirstAction = "Verify Foundry connectivity/credentials or fall back to DEMO mode.",
        Notes = [note],
    };
}
