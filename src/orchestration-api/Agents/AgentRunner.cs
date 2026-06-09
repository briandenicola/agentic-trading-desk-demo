using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents;

/// <summary>
/// LIVE path (T019): runs the Morning Brief agent on Azure AI Foundry through the
/// Microsoft Agent Framework (<c>Microsoft.Agents.AI</c> + <c>Microsoft.Agents.AI.AzureAI</c>),
/// authenticating with <see cref="DefaultAzureCredential"/>. The agent calls the mock
/// system-of-record APIs as tools (<see cref="MorningBriefTools"/>) in a loop capped at
/// <c>MAX_TOOL_HOPS</c>, then its JSON output is mapped to the same
/// <see cref="MorningBrief"/> DTO the DEMO composer returns (Principle III / FR-010).
///
/// IMPORTANT: nothing here runs — and no credential is acquired — unless
/// <see cref="RunAsync"/> is invoked, which only happens in LIVE mode (the DEMO mode
/// switch never reaches this class). Construction is side-effect free.
///
/// The single Foundry-specific construction step is isolated in
/// <see cref="CreateFoundryAgentAsync"/> so a prerelease (rc5) API change cannot affect
/// the offline DEMO path.
/// </summary>
public sealed class AgentRunner(IConfiguration config, MorningBriefTools tools, ILogger<AgentRunner> logger)
{
    /// <summary>
    /// Name of the single persistent Foundry agent. The agent-provisioner registers it once
    /// (idempotently); the runtime reuses it by name on every request instead of creating a new
    /// agent per call, so agents do not accumulate and all runs are traceable under one agent.
    /// </summary>
    private const string AgentName = "morning-brief";

    /// <summary>Description stored on the Foundry agent definition (visible in the portal).</summary>
    private const string AgentDescription =
        "Synthesizes the municipal-sales morning brief by calling the mock systems-of-record as tools.";

    public async Task<MorningBrief> RunAsync(string eventId, string? date, CancellationToken ct = default)
    {
        var endpoint = config["FOUNDRY_PROJECT_ENDPOINT"]
            ?? throw new InvalidOperationException("LIVE mode requires FOUNDRY_PROJECT_ENDPOINT.");
        var model = config["FOUNDRY_MODEL"] ?? "gpt-5.4-mini";
        var maxHops = int.TryParse(config["MAX_TOOL_HOPS"], out var h) && h > 0 ? h : 8;

        var instructions = await LoadInstructionsAsync(ct);

        AIAgent agent;
        try
        {
            agent = await CreateFoundryAgentAsync(endpoint, model, instructions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the Foundry agent; returning a degraded brief.");
            return Degraded(eventId, $"LIVE agent unavailable: {ex.Message}");
        }

        var userMessage =
            $"Produce the morning brief for event '{eventId}' on {date ?? "the current trading day"}. " +
            $"Call the tools to gather market data, news, clients, holdings and engagement, then emit ONLY the JSON object. " +
            $"Use at most {maxHops} tool calls.";

        using var runSpan = OrchestrationTelemetry.ActivitySource.StartActivity("morning_brief.run", ActivityKind.Internal);
        runSpan?.SetTag("wf.event_id", eventId);
        runSpan?.SetTag("wf.mode", "LIVE");
        runSpan?.SetTag("wf.trading_day", date ?? "current");
        runSpan?.SetTag("gen_ai.request.model", model);
        runSpan?.SetTag("gen_ai.request.max_tool_calls", maxHops);

        try
        {
            var response = await agent.RunAsync(userMessage, cancellationToken: ct);

            var usage = response.Usage;
            if (usage is not null)
            {
                runSpan?.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount);
                runSpan?.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount);
                runSpan?.SetTag("gen_ai.usage.total_tokens", usage.TotalTokenCount);
                if (usage.TotalTokenCount is long total)
                {
                    OrchestrationTelemetry.TokenUsage.Record(total, new KeyValuePair<string, object?>("wf.event_id", eventId));
                }
                logger.LogInformation(
                    "LIVE morning-brief token usage (event={EventId}): input={Input} output={Output} total={Total}",
                    eventId, usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
            }

            var json = ExtractJsonObject(response.Text);
            var brief = MapToBrief(json, eventId);
            runSpan?.SetStatus(ActivityStatusCode.Ok);
            return brief;
        }
        catch (Exception ex)
        {
            runSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Foundry agent run failed; returning a degraded brief.");
            return Degraded(eventId, $"LIVE agent run failed: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- Foundry wiring (isolated)

    /// <summary>
    /// Resolve a Foundry-backed <see cref="AIAgent"/> for the morning brief. The agent is
    /// <b>persistent</b> in Foundry (registered by <c>agent-provisioner</c> as a versioned "prompt"
    /// agent on the new Foundry surface); this method retrieves it <b>by name</b> via the Agent
    /// Framework SDK (<see cref="Azure.AI.Projects.AzureAIProjectChatClientExtensions.GetAIAgentAsync"/>),
    /// which returns a local <see cref="AIAgent"/> proxy over the latest version with the mock-api
    /// tools attached for client-side execution. If the agent has not been provisioned yet, it falls
    /// back to creating it (a new version under the same name) so LIVE mode still works.
    ///
    /// This deliberately uses ONLY the new-Foundry surface (the same one the reference
    /// online-banking-demo uses); it does not touch the classic Assistants (<c>/assistants</c>) API.
    /// It is the only method that touches the prerelease Azure AI Foundry surface, isolated so a
    /// signature change in the rc5 package cannot affect the offline DEMO path.
    /// </summary>
    private async Task<AIAgent> CreateFoundryAgentAsync(string endpoint, string model, string instructions, CancellationToken ct)
    {
        // DefaultAzureCredential is constructed lazily here — never in DEMO mode (FR-008).
        var credential = new DefaultAzureCredential();
        var projectClient = new AIProjectClient(new Uri(endpoint), credential);

        var aiTools = BuildTools();

#pragma warning disable CS0618 // rc5 transitional API; native AIProjectClient.Agents APIs are not yet stable. Tracked for LIVE follow-up.
        AIAgent baseAgent;
        try
        {
            // Reuse the persistent agent the provisioner registered, resolved by NAME (latest version).
            baseAgent = await projectClient.GetAIAgentAsync(AgentName, aiTools, cancellationToken: ct);
            logger.LogInformation("Reusing persistent Foundry agent '{Agent}'.", AgentName);
        }
        catch (Exception ex)
        {
            // Self-healing: the provisioner has not run yet (or the agent was deleted). Create the
            // prompt agent on the new Foundry surface so subsequent runs reuse it by name.
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

        // Wrap the agent with Agent Framework OpenTelemetry so each run emits GenAI spans
        // (model, tool calls, token usage) under OrchestrationTelemetry.SourceName (backlog 005).
        // EnableSensitiveData captures prompts/responses — safe here because all data is fictional;
        // gated by OTEL_CAPTURE_MESSAGE_CONTENT (default on) so it can be disabled if needed.
        var captureContent = !string.Equals(config["OTEL_CAPTURE_MESSAGE_CONTENT"], "false", StringComparison.OrdinalIgnoreCase);
        return baseAgent
            .AsBuilder()
            .UseOpenTelemetry(sourceName: OrchestrationTelemetry.SourceName, configure: c => c.EnableSensitiveData = captureContent)
            .Build();
    }

    /// <summary>Expose the mock-api tool functions to the agent (typed HttpClient under the hood).
    /// Each call is wrapped in <see cref="InvokeToolAsync"/> so it emits a child span (tool name,
    /// args, duration, result bytes) under the run span for full tool-call traceability (backlog 005).</summary>
    private IList<AITool> BuildTools() =>
    [
        AIFunctionFactory.Create(
            (CancellationToken ct) => InvokeToolAsync("get_market_data", tools.GetMarketDataAsync, ct),
            "get_market_data", "MMD/UST levels, equity futures, credit spreads, tone."),
        AIFunctionFactory.Create(
            (string eventId, CancellationToken ct) => InvokeToolAsync("get_news", c => tools.GetNewsAsync(eventId, c), ct, ("event_id", eventId)),
            "get_news", "Resolve a news event id into headline, summary, entities, sectors, states, sources."),
        AIFunctionFactory.Create(
            (string eventId, CancellationToken ct) => InvokeToolAsync("get_relative_value", c => tools.GetRelativeValueAsync(eventId, c), ct, ("event_id", eventId)),
            "get_relative_value", "Relative-value / curve context for a market event id."),
        AIFunctionFactory.Create(
            (CancellationToken ct) => InvokeToolAsync("get_client_value_all", tools.GetClientValueAllAsync, ct),
            "get_client_value_all", "All clients with revenue, rankings, share of wallet."),
        AIFunctionFactory.Create(
            (string cid, CancellationToken ct) => InvokeToolAsync("get_engagement", c => tools.GetEngagementAsync(cid, c), ct, ("cid", cid)),
            "get_engagement", "Recent engagement footprint for a client id."),
        AIFunctionFactory.Create(
            (string cusip, string state, string sector, CancellationToken ct) =>
                InvokeToolAsync("search_holdings", c => SearchHoldingsAdapter(cusip, state, sector, c), ct,
                    ("cusip", cusip), ("state", state), ("sector", sector)),
            "search_holdings", "Find clients holding a cusip / state / sector (pass empty strings to skip a filter)."),
        AIFunctionFactory.Create(
            (CancellationToken ct) => InvokeToolAsync("get_axes", tools.GetAxesAsync, ct),
            "get_axes", "Live axes / IOIs from the trading book."),
        AIFunctionFactory.Create(
            (string scope, CancellationToken ct) => InvokeToolAsync("list_events", c => tools.GetCurrentEventsAsync(scope, c), ct, ("scope", scope)),
            "list_events", "Current market/news events to weigh into client linkage. Pass an empty string for scope to get all (overnight + intraday)."),
        AIFunctionFactory.Create(
            (string value, string kind, CancellationToken ct) => InvokeToolAsync("get_events_by_entity", c => tools.GetEventsByEntityAsync(value, kind, c), ct, ("value", value), ("kind", kind)),
            "get_events_by_entity", "Events affecting one entity (value = ticker/sector/issuer; kind = 'ticker'|'sector'|'issuer', empty for any)."),
    ];

    /// <summary>
    /// Run a single tool call inside a child span so every tool invocation is traceable: it
    /// records the tool name, any string arguments, the wall-clock duration (also on the
    /// <c>wf.tool.duration</c> histogram), and the response size. Tools never throw — the catch
    /// is defensive so an unexpected failure still marks the span and propagates.
    /// </summary>
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

    private Task<string> SearchHoldingsAdapter(string cusip, string state, string sector, CancellationToken ct) =>
        tools.SearchHoldingsAsync(
            string.IsNullOrWhiteSpace(cusip) ? null : cusip,
            string.IsNullOrWhiteSpace(state) ? null : state,
            string.IsNullOrWhiteSpace(sector) ? null : sector,
            ct);

    // ---------------------------------------------------------------- prompt + mapping

    private async Task<string> LoadInstructionsAsync(CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "morning-brief.md");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        logger.LogWarning("Prompt file not found at {Path}; using a minimal fallback instruction.", path);
        return "You are the Morning Brief agent. Use the tools to gather data and emit a single JSON object matching the morning-brief schema.";
    }

    private static string ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : "{}";
    }

    private MorningBrief MapToBrief(string json, string eventId)
    {
        try
        {
            var brief = JsonSerializer.Deserialize<MorningBrief>(json, MorningBriefJson.Options);
            if (brief is null)
            {
                return Degraded(eventId, "LIVE agent returned no parseable brief.");
            }
            // Force the mode marker and normalize the LIVE ranking envelope regardless of model ordering.
            return NormalizeLiveBrief(brief);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse the LIVE agent output as a MorningBrief.");
            return Degraded(eventId, "LIVE agent output did not match the morning-brief schema.");
        }
    }

    private static MorningBrief NormalizeLiveBrief(MorningBrief brief)
    {
        var outreach = brief.Outreach
            .Select(o =>
            {
                var rationale = o.Rationale;
                var composite = OutreachRanker.CompositeScore(
                    rationale.WalletScore,
                    rationale.EngagementScore,
                    rationale.EventRelevanceScore);

                return o with
                {
                    Rationale = rationale with { CompositeScore = composite }
                };
            })
            .OrderByDescending(o => o.Rationale.CompositeScore)
            .ThenBy(o => o.Cid, StringComparer.Ordinal)
            .Select((o, i) => o with { Rank = i + 1 })
            .ToList();

        return brief with { Mode = "LIVE", Outreach = outreach };
    }

    private static MorningBrief Degraded(string eventId, string note) => new()
    {
        Mode = "LIVE",
        AsOf = DateTimeOffset.UtcNow.ToString("o"),
        MarketStrip = [],
        Reasoning =
        [
            new ReasoningStep { Text = "Attempted to run the Foundry morning-brief agent.", Status = "done" },
            new ReasoningStep { Text = "LIVE path unavailable — returned a degraded brief.", Status = "pending" },
        ],
        MacroNarrative = new MacroNarrative
        {
            Summary = $"The LIVE morning brief for '{eventId}' could not be produced.",
            WhyItMatters = "Verify Foundry connectivity/credentials or fall back to DEMO mode.",
            Sources = [],
        },
        MostAffectedClients = [],
        Outreach = [],
        Notes = [note],
    };
}
