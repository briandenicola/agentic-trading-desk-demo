// Agent provisioner (T020).
//
// Idempotently registers the demo's Foundry agents as versioned "prompt" agents on the
// NEW Foundry surface, using the Microsoft Agent Framework SDK (Azure.AI.Projects +
// Microsoft.Agents.AI.AzureAI) + DefaultAzureCredential. This is the same surface the
// runtime (orchestration-api AgentRunner / RmAgentRunner) connects to by name, and the
// same one the reference online-banking-demo uses (init_agents.py / ADR-005). It does NOT
// touch the classic Assistants (/assistants) API.
//
// Two kinds of agents (each role runs on its own model deployment / quota pool to avoid 429s):
//
//   RUNTIME-MANAGED (created on first scene call by the LIVE runners, *with* the real mock-api
//   tools bound) — the provisioner only DELETES any stale definition so the runtime recreates a
//   clean, tool-bearing agent on the correct deployment. It must NOT pre-register these tool-less:
//   a stored tool-less agent is reused by name and advertises no tools to the model, so it never
//   calls the systems-of-record and emits an empty briefing (the LIVE-empty regression).
//     - rm-daily-briefing : Commercial Banking RM Daily Briefing (PRIMARY)   -> FOUNDRY_MODEL
//     - morning-brief     : Municipal-sales morning brief (secondary)        -> FOUNDRY_MODEL_MORNING
//     - trading-desk-morning : Institutional Sales & Trading morning brief    -> FOUNDRY_MODEL_TRADING
//     - event-specialist  : Per-event fan-out assessment (high concurrency)  -> FOUNDRY_MODEL_SPECIALIST
//     - markets-assistant : Grounded "AI Chat" assistant (interactive)       -> FOUNDRY_MODEL_CHAT
//     - trading-desk-assistant : Grounded trading-desk "Open Chat" assistant  -> FOUNDRY_MODEL_CHAT
//
//   CONTRACT-ONLY (registered for traceability, never executed) — created tool-less on FOUNDRY_MODEL:
//     - briefing-synthesizer : Shared synthesis contract the per-scene briefing agents fulfil (R9).
//
// Guarded so it cleanly no-ops when Foundry credentials/endpoint are absent (e.g. DEMO/local)
// instead of failing — no credential is acquired in that case.

using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.AI;

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT");
var model = Environment.GetEnvironmentVariable("FOUNDRY_MODEL") ?? "gpt-5.4-mini";
// Per-role models live on separate deployments (= separate quota pools) so the fanned-out
// event-specialist and the synthesizers never contend for the same TPM/RPM (fixes 429s).
var morningModel = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_MORNING") ?? model;
var specialistModel = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_SPECIALIST") ?? model;
var chatModel = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_CHAT") ?? morningModel;
// The trading-desk synthesizer rides the same deployment as the RM briefing unless overridden.
var tradingModel = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_TRADING") ?? model;

if (string.IsNullOrWhiteSpace(endpoint))
{
    Console.WriteLine("agent-provisioner: FOUNDRY_PROJECT_ENDPOINT not set — no-op (DEMO/local). " +
                      "Set the endpoint + Azure credentials to register the agents in Foundry.");
    return 0;
}

// The runtime binds the real mock-api tools at request time. Runtime-managed agents are created
// by the runtime *with* those tools (RuntimeManaged = true → the provisioner only clears stale
// definitions); the contract-only synthesizer carries no tools (it is never executed).
var agents = new[]
{
    new AgentSpec(
        "rm-daily-briefing",
        "Produces the Commercial Banking RM daily briefing and prioritized call list by calling the mock systems-of-record as tools.",
        "rm-daily-briefing.md",
        model,
        RuntimeManaged: true),
    new AgentSpec(
        "morning-brief",
        "Synthesizes the municipal-sales morning brief by calling the mock systems-of-record as tools.",
        "morning-brief.md",
        morningModel,
        RuntimeManaged: true),
    new AgentSpec(
        "trading-desk-morning",
        "Produces the institutional sales & trading morning briefing and prioritized client call list by calling the trading-desk mock systems-of-record as tools.",
        "trading-desk-morning.md",
        tradingModel,
        RuntimeManaged: true),
    // 002 US4 — per-event multi-agent fan-out. The event-specialist is run once per current
    // event by the LIVE runners (so it is runtime-managed and tool-bearing); the
    // briefing-synthesizer is the shared synthesis contract the per-scene briefing agents fulfil
    // (registered for traceability/consolidation, R9 — never executed, so it carries no tools).
    new AgentSpec(
        "event-specialist",
        "Assesses one market/news event's portfolio impact and resolves it to typed entity selectors with a signed priority contribution.",
        "event-specialist.md",
        specialistModel,
        RuntimeManaged: true),
    new AgentSpec(
        "markets-assistant",
        "Grounded Markets-Intelligence chat assistant that answers RM questions by calling the Commercial Banking mock systems-of-record and the event feed as tools.",
        "markets-assistant.md",
        chatModel,
        RuntimeManaged: true),
    new AgentSpec(
        "trading-desk-assistant",
        "Grounded Institutional Sales & Trading chat assistant that answers coverage-salesperson questions by calling the trading-desk mock systems-of-record and the event feed as tools.",
        "trading-desk-assistant.md",
        chatModel,
        RuntimeManaged: true),
    new AgentSpec(
        "briefing-synthesizer",
        "Combines per-event specialist assessments with the systems-of-record to synthesize the final briefing DTO unchanged.",
        "briefing-synthesizer.md",
        model,
        RuntimeManaged: false),
};

try
{
    var projectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
    var noTools = Array.Empty<AITool>();

    foreach (var spec in agents)
    {
#pragma warning disable CS0618 // rc5 transitional API; native AIProjectClient.Agents APIs are not yet stable.
        // Always clear any existing definition first. This forces a tool-less or wrong-model
        // leftover to be recreated cleanly: runtime-managed agents are recreated *with* tools by
        // the LIVE runner on first use; the synthesizer is recreated tool-less just below.
        try
        {
            await projectClient.Agents.DeleteAgentAsync(spec.Name, CancellationToken.None);
            Console.WriteLine($"agent-provisioner: removed existing agent '{spec.Name}'.");
        }
        catch (Exception)
        {
            // Not found (first run) — nothing to remove.
        }

        if (spec.RuntimeManaged)
        {
            // Do NOT pre-register tool-less: the runtime creates this agent with the real
            // mock-api tools bound and on its target deployment ('{spec.Model}') on first call.
            Console.WriteLine($"agent-provisioner: '{spec.Name}' is runtime-managed — the LIVE runner creates it with tools on model '{spec.Model}' on first use.");
            continue;
        }

        var instructions = await LoadInstructionsAsync(spec.PromptFile);
        var created = await projectClient.CreateAIAgentAsync(
            name: spec.Name,
            model: spec.Model,
            instructions: instructions,
            description: spec.Description,
            tools: noTools);
        Console.WriteLine($"agent-provisioner: registered contract-only agent '{created.Name}' (model={spec.Model}) on the new Foundry surface.");
#pragma warning restore CS0618
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"agent-provisioner: registration failed — {ex.Message}");
    return 1;
}

static async Task<string> LoadInstructionsAsync(string promptFile)
{
    var path = Path.Combine(AppContext.BaseDirectory, "Prompts", promptFile);
    if (File.Exists(path))
    {
        return await File.ReadAllTextAsync(path);
    }
    Console.WriteLine($"agent-provisioner: prompt not found at {path}; using a minimal fallback instruction.");
    return "Use the provided tools to gather data and emit a single JSON object matching the agent's output schema.";
}

internal sealed record AgentSpec(string Name, string Description, string PromptFile, string Model, bool RuntimeManaged);
