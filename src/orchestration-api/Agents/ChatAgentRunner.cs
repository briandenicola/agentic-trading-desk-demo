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
/// LIVE path for the grounded Markets-Intelligence assistant (the "AI Chat" surface): a persistent
/// Azure AI Foundry chat agent (<c>markets-assistant</c>) run through the Microsoft Agent Framework
/// with the same Commercial Banking mock-api tools the RM briefing uses bound at request time, so
/// the assistant answers natural-language questions grounded in the systems-of-record and the
/// current event feed (Principle II / FR-002). Authenticates with <see cref="DefaultAzureCredential"/>.
///
/// Construction is side-effect free: no credential is acquired and nothing on Foundry runs unless
/// <see cref="RunAsync"/> is invoked (LIVE only). Any failure (no endpoint, agent-create or run
/// error, empty model output) degrades gracefully to the deterministic <see cref="ChatResponder"/>
/// re-stamped <c>LIVE</c> so the chat is never dead (FR-011 / Principle III).
/// </summary>
public sealed class ChatAgentRunner(
    IConfiguration config,
    RmBriefingTools tools,
    ChatResponder responder,
    ILogger<ChatAgentRunner> logger)
{
    private const string AgentName = "markets-assistant";

    private const string AgentDescription =
        "Grounded Markets-Intelligence chat assistant that answers RM questions by calling the Commercial Banking mock systems-of-record and the event feed as tools.";

    public async Task<ChatReply> RunAsync(ChatRequest request, CancellationToken ct = default)
    {
        var endpoint = config["FOUNDRY_PROJECT_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // LIVE requested but no Foundry endpoint — answer deterministically (no credential).
            return await responder.RespondAsync(request, ct);
        }

        // Chat is interactive + low-volume; default it onto the morning-brief deployment
        // (gpt-4o-mini, a separate quota pool from the RM synthesizer) to avoid 429 contention.
        var model = config["FOUNDRY_MODEL_CHAT"] ?? config["FOUNDRY_MODEL_MORNING"] ?? config["FOUNDRY_MODEL"] ?? "gpt-4o-mini";
        var instructions = await LoadInstructionsAsync(ct);

        AIAgent agent;
        try
        {
            agent = await CreateFoundryAgentAsync(endpoint, model, instructions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the Foundry chat agent; falling back to the deterministic responder.");
            return AsLive(await responder.RespondAsync(request, ct));
        }

        var prompt = BuildPrompt(request);

        using var span = OrchestrationTelemetry.ActivitySource.StartActivity("chat.run", ActivityKind.Internal);
        span?.SetTag("wf.mode", "LIVE");
        span?.SetTag("gen_ai.request.model", model);

        try
        {
            var (maxAttempts, baseDelay) = FoundryRetry.SettingsFrom(config);
            var response = await FoundryRetry.ExecuteAsync(
                c => agent.RunAsync(prompt, cancellationToken: c),
                maxAttempts, baseDelay, logger, "markets-assistant chat", ct);

            var usage = response.Usage;
            if (usage?.TotalTokenCount is long total)
            {
                span?.SetTag("gen_ai.usage.total_tokens", total);
                OrchestrationTelemetry.TokenUsage.Record(total, new KeyValuePair<string, object?>("wf.surface", "chat"));
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
            logger.LogError(ex, "Foundry chat run failed; falling back to the deterministic responder.");
            return AsLive(await responder.RespondAsync(request, ct));
        }
    }

    // ---------------------------------------------------------------- prompt assembly

    /// <summary>
    /// Render the (stateless) conversation into a single grounded prompt: optional RM context, the
    /// recent transcript, and the latest user question, with an instruction to ground every fact via
    /// tools. The agent's stored instructions carry the system persona.
    /// </summary>
    private static string BuildPrompt(ChatRequest request)
    {
        var turns = request.Messages.TakeLast(12).ToList();
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(request.RmId))
        {
            sb.AppendLine($"Context: the relationship manager is '{request.RmId}'. Resolve their book when relevant.");
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
        sb.AppendLine("Answer the user's question. Call the tools to ground every fact (customers, opportunities, complaints, follow-ups, events) — never invent data. Be concise and desk-ready; cite ids where helpful.");
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
            (string rmId, string asOf, CancellationToken ct) => tools.GetRmBookAsync(rmId, Blank(asOf), ct),
            "get_rm_book", "Book-level snapshot for an RM id (manager, customers, headline KPIs). Pass an empty string for as_of to use today."),
        AIFunctionFactory.Create(
            (string rm, string asOf, CancellationToken ct) => tools.GetOpenOpportunitiesAsync(rm, Blank(asOf), ct),
            "get_open_opportunities", "Open opportunities for an RM (by manager name). Pass an empty string for as_of to use today."),
        AIFunctionFactory.Create(
            (string rm, CancellationToken ct) => tools.GetActiveComplaintsAsync(rm, ct),
            "get_active_complaints", "Active (unresolved) complaints for an RM (by manager name)."),
        AIFunctionFactory.Create(
            (string rm, string followUpDueBy, CancellationToken ct) => tools.GetDueFollowUpsAsync(rm, followUpDueBy, ct),
            "get_due_followups", "Interactions with a follow-up due on or before a yyyy-MM-dd date (overdue + upcoming) for an RM."),
        AIFunctionFactory.Create(
            (string customerId, CancellationToken ct) => tools.GetCustomerAsync(customerId, ct),
            "get_customer", "One customer's full profile by id (e.g. CB-10036)."),
        AIFunctionFactory.Create(
            (string customerId, CancellationToken ct) => tools.GetCustomerOpportunitiesAsync(customerId, ct),
            "get_customer_opportunities", "One customer's opportunities by id."),
        AIFunctionFactory.Create(
            (string customerId, CancellationToken ct) => tools.GetCustomerInteractionsAsync(customerId, ct),
            "get_customer_interactions", "One customer's interactions (call log + follow-ups) by id."),
        AIFunctionFactory.Create(
            (string scope, CancellationToken ct) => tools.GetCurrentEventsAsync(Blank(scope), ct),
            "list_events", "Current market/news events. Pass an empty string for scope to get all (overnight + intraday)."),
        AIFunctionFactory.Create(
            (string value, string kind, CancellationToken ct) => tools.GetEventsByEntityAsync(value, Blank(kind), ct),
            "get_events_by_entity", "Events affecting one entity (value = customerId or sector; kind = 'customer' or 'sector', empty for any)."),
    ];

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task<string> LoadInstructionsAsync(CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "markets-assistant.md");
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        logger.LogWarning("Prompt file not found at {Path}; using a minimal fallback instruction.", path);
        return "You are a grounded Markets-Intelligence assistant for a Commercial Banking RM. Use the tools to answer questions about the RM's book and the current events. Never invent data. Be concise.";
    }
}
