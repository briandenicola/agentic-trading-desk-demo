using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Agents.Resilience;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Models;

namespace OrchestrationApi.Agents;

/// <summary>
/// LIVE path for the "New Issue Radar" storyboard: runs a persistent Azure AI Foundry agent
/// through the Microsoft Agent Framework (<c>Microsoft.Agents.AI</c> +
/// <c>Microsoft.Agents.AI.AzureAI</c>), authenticating with <see cref="DefaultAzureCredential"/>.
/// The agent calls the Trading Desk mock endpoints as tools (<see cref="TdBriefingTools"/>) to
/// assemble the four-beat storyboard and emits the same <see cref="TdNewIssueStoryboard"/> DTO the
/// DEMO composer returns (Principle III). Construction is side-effect free; nothing on Foundry runs
/// unless <see cref="RunAsync"/> is invoked (LIVE only). On any failure — or if the agent returns a
/// storyboard with no steps — it degrades to the deterministic <see cref="TdNewIssueComposer"/>
/// re-stamped <c>LIVE</c> (FR-011), so the cockpit is never empty.
/// </summary>
public sealed class TdNewIssueRunner(
    IConfiguration config,
    TdBriefingTools tools,
    TdNewIssueComposer composer,
    ILogger<TdNewIssueRunner> logger)
{
    private const string AgentName = "trading-desk-new-issue";

    private const string AgentDescription =
        "Builds the institutional sales & trading 'New Issue Radar' storyboard — announcement, equity holdings cross-reference, recent new-debt RFQ/trade/call activity, and a prioritized outreach recommendation — by calling the trading-desk mock systems-of-record as tools.";

    public async Task<TdNewIssueStoryboard> RunAsync(
        string? issuerSecurityId, string? clientId, string? date, CancellationToken ct = default)
    {
        var equityId = string.IsNullOrWhiteSpace(issuerSecurityId) ? TdNewIssueComposer.DefaultIssuerSecurityId : issuerSecurityId;
        var focusClientId = string.IsNullOrWhiteSpace(clientId) ? TdNewIssueComposer.DefaultClientId : clientId;
        var asOf = string.IsNullOrWhiteSpace(date) ? TdNewIssueComposer.DefaultDate : date;

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
            logger.LogError(ex, "Failed to create the Foundry new-issue agent; falling back to the deterministic storyboard.");
            return await DegradeAsync(issuerSecurityId, clientId, date, $"LIVE agent unavailable: {ex.Message}", ct);
        }

        var userMessage =
            $"Build the New Issue Radar storyboard for the issuer whose equity security is '{equityId}' and the focus client '{focusClientId}' as of {asOf}. " +
            $"Call the tools to load the equity security interest (holders, news), resolve the issuer's new debt tranche via get_securities, " +
            $"load the focus client's holdings and 360-degree activity, and the desk's distribution axe on the new debt. " +
            $"Produce exactly four ordered steps (announcement, holdings, activity, outreach) and a final outreach recommendation, " +
            $"and emit ONLY the JSON object matching the td-new-issue storyboard schema. Use at most {maxHops} tool calls.";

        using var runSpan = OrchestrationTelemetry.ActivitySource.StartActivity("td_new_issue.run", ActivityKind.Internal);
        runSpan?.SetTag("wf.issuer_security_id", equityId);
        runSpan?.SetTag("wf.client_id", focusClientId);
        runSpan?.SetTag("wf.mode", "LIVE");
        runSpan?.SetTag("gen_ai.request.model", model);
        runSpan?.SetTag("gen_ai.request.max_tool_calls", maxHops);

        try
        {
            var (maxAttempts, baseDelay) = FoundryRetry.SettingsFrom(config);
            var response = await FoundryRetry.ExecuteAsync(
                c => agent.RunAsync(userMessage, cancellationToken: c),
                maxAttempts, baseDelay, logger, $"td-new-issue ({equityId}/{focusClientId})", ct);

            var usage = response.Usage;
            if (usage is not null)
            {
                runSpan?.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount);
                runSpan?.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount);
                runSpan?.SetTag("gen_ai.usage.total_tokens", usage.TotalTokenCount);
                if (usage.TotalTokenCount is long total)
                {
                    OrchestrationTelemetry.TokenUsage.Record(total, new KeyValuePair<string, object?>("wf.issuer_security_id", equityId));
                }
                logger.LogInformation(
                    "LIVE td-new-issue token usage (issuer={IssuerSecurityId}, client={ClientId}): input={Input} output={Output} total={Total}",
                    equityId, focusClientId, usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
            }

            var json = ExtractJsonObject(response.Text);
            var storyboard = MapToStoryboard(json);
            if (storyboard is null || storyboard.Steps.Count == 0)
            {
                logger.LogWarning("LIVE new-issue agent returned an empty/unparseable storyboard; reconstructing deterministically.");
                return await DegradeAsync(issuerSecurityId, clientId, date,
                    "LIVE agent returned no usable storyboard; reconstructed deterministically from the systems-of-record.", ct);
            }

            runSpan?.SetStatus(ActivityStatusCode.Ok);
            return storyboard with { Mode = "LIVE" };
        }
        catch (Exception ex)
        {
            runSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Foundry new-issue agent run failed; falling back to the deterministic storyboard.");
            return await DegradeAsync(issuerSecurityId, clientId, date, $"LIVE agent run failed: {ex.Message}", ct);
        }
    }

    // ---------------------------------------------------------------- deterministic safety net

    private async Task<TdNewIssueStoryboard> DegradeAsync(
        string? issuerSecurityId, string? clientId, string? date, string reason, CancellationToken ct)
    {
        try
        {
            var deterministic = await composer.ComposeAsync(issuerSecurityId, clientId, date, ct);
            var notes = new List<string>(deterministic.Notes ?? []) { reason };
            return deterministic with { Mode = "LIVE", Notes = notes };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Deterministic safety-net composition failed for the new-issue storyboard.");
            throw;
        }
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
            (string securityId, string since, CancellationToken ct) => InvokeToolAsync("get_security_interest", c => tools.GetSecurityInterestAsync(securityId, Blank(since), c), ct, ("security_id", securityId), ("since", since)),
            "get_security_interest", "Everything pointing at one security: inventory/axes, holders, trades, RFQs, inquiries, news, research. Start with the issuer's equity security. Pass an empty string for since to use the default window."),
        AIFunctionFactory.Create(
            (string assetClass, string sector, string issuer, string region, CancellationToken ct) => InvokeToolAsync("get_securities", c => tools.GetSecuritiesAsync(Blank(assetClass), Blank(sector), Blank(issuer), Blank(region), c), ct, ("asset_class", assetClass), ("sector", sector), ("issuer", issuer), ("region", region)),
            "get_securities", "Security master, filterable by asset class / sector / issuer / region. Use issuer to find the new debt tranche for the same issuer. Pass empty strings for unused filters."),
        AIFunctionFactory.Create(
            (string clientId, CancellationToken ct) => InvokeToolAsync("get_client", c => tools.GetClientAsync(clientId, c), ct, ("client_id", clientId)),
            "get_client", "One client's master record (name, type, region, preferred asset class)."),
        AIFunctionFactory.Create(
            (string clientId, string since, CancellationToken ct) => InvokeToolAsync("get_client_activity", c => tools.GetClientActivityAsync(clientId, Blank(since), c), ct, ("client_id", clientId), ("since", since)),
            "get_client_activity", "360-degree trailing activity for one client: holdings, trades, RFQs, inquiries, CRM. Pass an empty string for since to use the default window."),
        AIFunctionFactory.Create(
            (string clientId, CancellationToken ct) => InvokeToolAsync("get_client_holdings", c => tools.GetClientHoldingsAsync(clientId, c), ct, ("client_id", clientId)),
            "get_client_holdings", "One client's current position snapshot."),
        AIFunctionFactory.Create(
            (string clientId, string securityId, string status, string since, CancellationToken ct) => InvokeToolAsync("get_rfqs", c => tools.GetRfqsAsync(Blank(clientId), Blank(securityId), Blank(status), Blank(since), c), ct, ("client_id", clientId), ("security_id", securityId), ("status", status), ("since", since)),
            "get_rfqs", "Request-for-quote activity. Filter by client, security, response status or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (string clientId, string securityId, string direction, string since, CancellationToken ct) => InvokeToolAsync("get_trades", c => tools.GetTradesAsync(Blank(clientId), Blank(securityId), Blank(direction), Blank(since), c), ct, ("client_id", clientId), ("security_id", securityId), ("direction", direction), ("since", since)),
            "get_trades", "Executed trades. Filter by client, security, direction or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (string clientId, string urgency, string since, CancellationToken ct) => InvokeToolAsync("get_crm", c => tools.GetCrmAsync(Blank(clientId), Blank(urgency), Blank(since), c), ct, ("client_id", clientId), ("urgency", urgency), ("since", since)),
            "get_crm", "CRM call reports and follow-ups. Filter by client, urgency or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (string securityId, string desk, CancellationToken ct) => InvokeToolAsync("get_inventory", c => tools.GetInventoryAsync(Blank(securityId), Blank(desk), c), ct, ("security_id", securityId), ("desk", desk)),
            "get_inventory", "Dealer inventory / market-making axes (the desk's distribution axe on the new debt). Pass empty strings for all."),
        AIFunctionFactory.Create(
            (string securityId, string sector, string macroTheme, string since, CancellationToken ct) => InvokeToolAsync("get_news", c => tools.GetNewsAsync(Blank(securityId), Blank(sector), Blank(macroTheme), Blank(since), c), ct, ("security_id", securityId), ("sector", sector), ("macro_theme", macroTheme), ("since", since)),
            "get_news", "Market-moving news, including the new-issue announcement. Pass empty strings to get all recent news."),
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
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "td-new-issue.md");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        logger.LogWarning("Prompt file not found at {Path}; using a minimal fallback instruction.", path);
        return "You are the Institutional Sales & Trading New Issue Radar agent. Use the tools to gather the new-issue announcement, the focus client's equity holdings, their recent new-debt RFQ/trade/call activity, and the desk axe, then emit a single JSON object matching the td-new-issue storyboard schema.";
    }

    private static string ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : "{}";
    }

    private TdNewIssueStoryboard? MapToStoryboard(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TdNewIssueStoryboard>(json, TdNewIssueJson.Options);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse the LIVE agent output as a TdNewIssueStoryboard.");
            return null;
        }
    }
}
