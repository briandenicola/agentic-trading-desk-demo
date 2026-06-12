using System.Diagnostics;
using System.Text;
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
/// LIVE path for the grounded Institutional Sales &amp; Trading assistant (the trading-desk
/// "Open Chat" surface): a persistent Azure AI Foundry chat agent (<c>trading-desk-assistant</c>)
/// run through the Microsoft Agent Framework with the trading-desk mock-api tools bound at request
/// time, so it answers natural-language questions grounded in the systems-of-record and the current
/// event feed (Principle II / FR-002). Authenticates with <see cref="DefaultAzureCredential"/>.
///
/// Construction is side-effect free: no credential is acquired and nothing on Foundry runs unless
/// <see cref="RunAsync"/> is invoked (LIVE only). Any failure (no endpoint, agent-create or run
/// error, empty model output) degrades gracefully to the deterministic <see cref="TdChatResponder"/>
/// re-stamped <c>LIVE</c> so the chat is never dead (FR-011 / Principle III).
/// </summary>
public sealed class TdChatAgentRunner(
    IConfiguration config,
    TdBriefingTools tools,
    TdChatResponder responder,
    ILogger<TdChatAgentRunner> logger)
{
    private const string AgentName = "trading-desk-assistant";

    private const string AgentDescription =
        "Grounded Institutional Sales & Trading chat assistant that answers coverage-salesperson questions by calling the trading-desk mock systems-of-record and the event feed as tools.";

    public async Task<ChatReply> RunAsync(ChatRequest request, CancellationToken ct = default)
    {
        var endpoint = config["FOUNDRY_PROJECT_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // LIVE requested but no Foundry endpoint — answer deterministically (no credential).
            return await responder.RespondAsync(request, ct);
        }

        // Chat is interactive + low-volume; default it onto the chat deployment (a separate quota
        // pool from the trading synthesizer) to avoid 429 contention.
        var model = config["FOUNDRY_MODEL_CHAT"] ?? config["FOUNDRY_MODEL_MORNING"] ?? config["FOUNDRY_MODEL"] ?? "gpt-4o-mini";
        var instructions = await LoadInstructionsAsync(ct);

        AIAgent agent;
        try
        {
            agent = await CreateFoundryAgentAsync(endpoint, model, instructions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the Foundry trading-desk chat agent; falling back to the deterministic responder.");
            return AsLive(await responder.RespondAsync(request, ct));
        }

        var prompt = BuildPrompt(request);

        using var span = OrchestrationTelemetry.ActivitySource.StartActivity("td.chat.run", ActivityKind.Internal);
        span?.SetTag("atd.mode", "LIVE");
        span?.SetTag("gen_ai.request.model", model);

        try
        {
            var (maxAttempts, baseDelay) = FoundryRetry.SettingsFrom(config);
            var response = await FoundryRetry.ExecuteAsync(
                c => agent.RunAsync(prompt, cancellationToken: c),
                maxAttempts, baseDelay, logger, "trading-desk-assistant chat", ct);

            var usage = response.Usage;
            if (usage?.TotalTokenCount is long total)
            {
                span?.SetTag("gen_ai.usage.total_tokens", total);
                OrchestrationTelemetry.TokenUsage.Record(total, new KeyValuePair<string, object?>("atd.surface", "td-chat"));
            }

            var text = response.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                // The model returned nothing usable — deterministic safety net.
                return AsLive(await responder.RespondAsync(request, ct));
            }

            span?.SetStatus(ActivityStatusCode.Ok);
            return new ChatReply { Mode = "LIVE", Message = text };
        }
        catch (Exception ex)
        {
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Foundry trading-desk chat run failed; falling back to the deterministic responder.");
            return AsLive(await responder.RespondAsync(request, ct));
        }
    }

    // ---------------------------------------------------------------- prompt assembly

    /// <summary>
    /// Render the (stateless) conversation into a single grounded prompt: optional salesperson
    /// context, the recent transcript, and the latest question, with an instruction to ground every
    /// fact via tools. The agent's stored instructions carry the system persona.
    /// </summary>
    private static string BuildPrompt(ChatRequest request)
    {
        var turns = request.Messages.TakeLast(12).ToList();
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(request.SalespersonId))
        {
            sb.AppendLine($"Context: the coverage salesperson is '{request.SalespersonId}'. Resolve their book when relevant.");
            sb.AppendLine();
        }

        if (turns.Count > 1)
        {
            sb.AppendLine("Conversation so far:");
            foreach (var t in turns.SkipLast(1))
            {
                var who = t.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User";
                sb.AppendLine($"{who}: {t.Content}");
            }
            sb.AppendLine();
        }

        var last = turns.LastOrDefault(t => t.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content
                   ?? turns.LastOrDefault()?.Content ?? "";
        sb.AppendLine($"User question: {last}");
        sb.AppendLine();
        sb.AppendLine("Answer the user's question. Call the tools to ground every fact (clients, securities, trades, RFQs, inquiries, axes, events) — never invent data. Be concise and desk-ready; cite ids where helpful.");
        return sb.ToString();
    }

    private static ChatReply AsLive(ChatReply reply) => reply with { Mode = "LIVE" };

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
            logger.LogWarning(ex, "Persistent agent '{Agent}' not found; creating it with tools.", AgentName);
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
            (string salesperson, CancellationToken ct) => tools.GetClientsAsync(Blank(salesperson), ct: ct),
            "get_clients", "The coverage salesperson's book of institutional clients (id, name, type, region, preferred asset class). Pass an empty string for an unfiltered list. Start here."),
        AIFunctionFactory.Create(
            (string clientId, string since, CancellationToken ct) => tools.GetClientActivityAsync(clientId, Blank(since), ct),
            "get_client_activity", "360-degree trailing activity for one client: holdings, trades, RFQs, inquiries, CRM. Pass an empty string for since to use the default window."),
        AIFunctionFactory.Create(
            (string clientId, CancellationToken ct) => tools.GetClientHoldingsAsync(clientId, ct),
            "get_client_holdings", "One client's current position snapshot."),
        AIFunctionFactory.Create(
            (string securityId, string since, CancellationToken ct) => tools.GetSecurityInterestAsync(securityId, Blank(since), ct),
            "get_security_interest", "Everything pointing at one security: inventory/axes, holders, trades, RFQs, inquiries, news, research. Pass an empty string for since to use the default window."),
        AIFunctionFactory.Create(
            (string securityId, string desk, CancellationToken ct) => tools.GetInventoryAsync(Blank(securityId), Blank(desk), ct),
            "get_inventory", "Dealer inventory / market-making axes the desk wants to work. Pass empty strings for all."),
        AIFunctionFactory.Create(
            (string securityId, string sector, string macroTheme, string since, CancellationToken ct) => tools.GetNewsAsync(Blank(securityId), Blank(sector), Blank(macroTheme), Blank(since), ct),
            "get_news", "Market-moving news. Pass empty strings to get all overnight + recent news."),
        AIFunctionFactory.Create(
            (string securityId, string sector, string ratingAction, CancellationToken ct) => tools.GetResearchAsync(Blank(securityId), Blank(sector), Blank(ratingAction), ct),
            "get_research", "Published desk research notes. Pass empty strings to get all."),
        AIFunctionFactory.Create(
            (string clientId, string securityId, string status, string since, CancellationToken ct) => tools.GetRfqsAsync(Blank(clientId), Blank(securityId), Blank(status), Blank(since), ct),
            "get_rfqs", "Request-for-quote activity. Filter by client, security, response status or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (string clientId, string securityId, string sentiment, string since, CancellationToken ct) => tools.GetInquiriesAsync(Blank(clientId), Blank(securityId), Blank(sentiment), Blank(since), ct),
            "get_inquiries", "Less-structured client inquiries / colour. Filter by client, security, sentiment or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (string clientId, string urgency, string since, CancellationToken ct) => tools.GetCrmAsync(Blank(clientId), Blank(urgency), Blank(since), ct),
            "get_crm", "CRM call reports and follow-ups. Filter by client, urgency or since (empty strings = all)."),
        AIFunctionFactory.Create(
            (CancellationToken ct) => tools.GetNarrativeThemesAsync(ct),
            "get_narrative_themes", "The embedded cross-dataset storylines (macro themes) for narrative context."),
        AIFunctionFactory.Create(
            (string scope, CancellationToken ct) => tools.GetCurrentEventsAsync(Blank(scope), ct),
            "list_events", "Current market/admin events (overnight + intraday). Pass an empty string for all."),
    ];

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task<string> LoadInstructionsAsync(CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "trading-desk-assistant.md");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        logger.LogWarning("Prompt file not found at {Path}; using a minimal fallback instruction.", path);
        return "You are a grounded Trading Desk assistant for an institutional sales & trading coverage salesperson. Use the tools to answer questions about the client book, our axes, the securities in play and the current events. Never invent data. Be concise.";
    }
}
