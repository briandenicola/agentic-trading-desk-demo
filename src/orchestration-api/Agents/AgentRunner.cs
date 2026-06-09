using System.Text.Json;
using Azure.AI.Agents.Persistent;
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

        try
        {
            var response = await agent.RunAsync(userMessage, cancellationToken: ct);
            var json = ExtractJsonObject(response.Text);
            return MapToBrief(json, eventId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Foundry agent run failed; returning a degraded brief.");
            return Degraded(eventId, $"LIVE agent run failed: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- Foundry wiring (isolated)

    /// <summary>
    /// Resolve a Foundry-backed <see cref="AIAgent"/> for the morning brief. The agent itself is
    /// <b>persistent</b> in Foundry (registered by <c>agent-provisioner</c>); this method looks it up
    /// by name and returns a local <see cref="AIAgent"/> proxy with the mock-api tools attached for
    /// client-side execution. If the persistent agent is not found (e.g. provisioner has not run yet),
    /// it falls back to creating one so LIVE mode still works.
    ///
    /// This is the only method that touches the prerelease Azure AI Foundry surface; it is isolated so
    /// a signature change in the rc5 package cannot affect the offline DEMO path.
    /// </summary>
    private async Task<AIAgent> CreateFoundryAgentAsync(string endpoint, string model, string instructions, CancellationToken ct)
    {
        // DefaultAzureCredential is constructed lazily here — never in DEMO mode (FR-008).
        var credential = new DefaultAzureCredential();
        var projectClient = new AIProjectClient(new Uri(endpoint), credential);

        var aiTools = BuildTools();

        // Prefer reusing the persistent agent the provisioner registered.
        var existingId = await TryFindPersistentAgentIdAsync(endpoint, credential, ct);

#pragma warning disable CS0618 // rc5 transitional API; native AIProjectClient.Agents APIs are not yet stable. Tracked for LIVE follow-up.
        if (existingId is not null)
        {
            logger.LogInformation("Reusing persistent Foundry agent '{Agent}' (id={Id}).", AgentName, existingId);
            return await projectClient.GetAIAgentAsync(existingId, aiTools, cancellationToken: ct);
        }

        logger.LogWarning("Persistent agent '{Agent}' not found; creating it (run the provisioner to persist it).", AgentName);
        return await projectClient.CreateAIAgentAsync(
            model: model,
            name: AgentName,
            instructions: instructions,
            tools: aiTools,
            cancellationToken: ct);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Look up the id of the persistent <see cref="AgentName"/> agent in Foundry. Returns
    /// <see langword="null"/> (never throws) if enumeration fails or no match exists, so the
    /// caller can fall back to creating an agent.
    /// </summary>
    private async Task<string?> TryFindPersistentAgentIdAsync(string endpoint, DefaultAzureCredential credential, CancellationToken ct)
    {
        try
        {
            var admin = new PersistentAgentsAdministrationClient(new Uri(endpoint), credential);
            await foreach (var agent in admin.GetAgentsAsync(cancellationToken: ct))
            {
                if (string.Equals(agent.Name, AgentName, StringComparison.Ordinal))
                {
                    return agent.Id;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not enumerate persistent agents; falling back to create.");
        }

        return null;
    }

    /// <summary>Expose the mock-api tool functions to the agent (typed HttpClient under the hood).</summary>
    private IList<AITool> BuildTools() =>
    [
        AIFunctionFactory.Create((Func<CancellationToken, Task<string>>)tools.GetMarketDataAsync,
            "get_market_data", "MMD/UST levels, equity futures, credit spreads, tone."),
        AIFunctionFactory.Create((Func<string, CancellationToken, Task<string>>)tools.GetNewsAsync,
            "get_news", "Resolve a news event id into headline, summary, entities, sectors, states, sources."),
        AIFunctionFactory.Create((Func<string, CancellationToken, Task<string>>)tools.GetRelativeValueAsync,
            "get_relative_value", "Relative-value / curve context for a market event id."),
        AIFunctionFactory.Create((Func<CancellationToken, Task<string>>)tools.GetClientValueAllAsync,
            "get_client_value_all", "All clients with revenue, rankings, share of wallet."),
        AIFunctionFactory.Create((Func<string, CancellationToken, Task<string>>)tools.GetEngagementAsync,
            "get_engagement", "Recent engagement footprint for a client id."),
        AIFunctionFactory.Create((Func<string, string, string, CancellationToken, Task<string>>)SearchHoldingsAdapter,
            "search_holdings", "Find clients holding a cusip / state / sector (pass empty strings to skip a filter)."),
        AIFunctionFactory.Create((Func<CancellationToken, Task<string>>)tools.GetAxesAsync,
            "get_axes", "Live axes / IOIs from the trading book."),
    ];

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
