// Agent provisioner (T020).
//
// Idempotently registers the demo's Foundry agents as versioned "prompt" agents on the
// NEW Foundry surface, using the Microsoft Agent Framework SDK (Azure.AI.Projects +
// Microsoft.Agents.AI.AzureAI) + DefaultAzureCredential. This is the same surface the
// runtime (orchestration-api AgentRunner / RmAgentRunner) connects to by name, and the
// same one the reference online-banking-demo uses (init_agents.py / ADR-005). It does NOT
// touch the classic Assistants (/assistants) API.
//
// Registers (each on its own model deployment / quota pool to avoid 429 throttling):
//   - rm-daily-briefing    : Commercial Banking RM Daily Briefing (PRIMARY)   -> FOUNDRY_MODEL
//   - morning-brief        : Municipal-sales morning brief (secondary)        -> FOUNDRY_MODEL_MORNING
//   - event-specialist     : Per-event fan-out assessment (high concurrency)  -> FOUNDRY_MODEL_SPECIALIST
//   - briefing-synthesizer : Shared synthesis contract                        -> FOUNDRY_MODEL
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

if (string.IsNullOrWhiteSpace(endpoint))
{
    Console.WriteLine("agent-provisioner: FOUNDRY_PROJECT_ENDPOINT not set — no-op (DEMO/local). " +
                      "Set the endpoint + Azure credentials to register the agents in Foundry.");
    return 0;
}

// The runtime binds the real mock-api tools at request time; the stored definitions carry no
// tools (matches the reference init_agents.py PromptAgentDefinition pattern).
var agents = new[]
{
    new AgentSpec(
        "rm-daily-briefing",
        "Produces the Commercial Banking RM daily briefing and prioritized call list by calling the mock systems-of-record as tools.",
        "rm-daily-briefing.md",
        model),
    new AgentSpec(
        "morning-brief",
        "Synthesizes the municipal-sales morning brief by calling the mock systems-of-record as tools.",
        "morning-brief.md",
        morningModel),
    // 002 US4 — per-event multi-agent fan-out. The event-specialist is run once per current
    // event by the LIVE runners; the briefing-synthesizer is the shared synthesis contract the
    // per-scene briefing agents fulfil (registered for traceability/consolidation, R9).
    new AgentSpec(
        "event-specialist",
        "Assesses one market/news event's portfolio impact and resolves it to typed entity selectors with a signed priority contribution.",
        "event-specialist.md",
        specialistModel),
    new AgentSpec(
        "briefing-synthesizer",
        "Combines per-event specialist assessments with the systems-of-record to synthesize the final briefing DTO unchanged.",
        "briefing-synthesizer.md",
        model),
};

try
{
    var projectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
    var noTools = Array.Empty<AITool>();

    foreach (var spec in agents)
    {
        var instructions = await LoadInstructionsAsync(spec.PromptFile);

#pragma warning disable CS0618 // rc5 transitional API; native AIProjectClient.Agents APIs are not yet stable.
        // The model is authoritative: a stored agent binds its deployment at run time, and the
        // runtime reuses the agent by name. So we delete any existing definition and recreate it
        // on the desired deployment — otherwise a model change (e.g. moving the specialist to its
        // own quota pool) would never take effect for an already-registered agent.
        try
        {
            await projectClient.Agents.DeleteAgentAsync(spec.Name, CancellationToken.None);
            Console.WriteLine($"agent-provisioner: removed existing agent '{spec.Name}' before recreating it on model '{spec.Model}'.");
        }
        catch (Exception)
        {
            // Not found (first run) — nothing to remove.
        }

        var created = await projectClient.CreateAIAgentAsync(
            name: spec.Name,
            model: spec.Model,
            instructions: instructions,
            description: spec.Description,
            tools: noTools);
        Console.WriteLine($"agent-provisioner: registered agent '{created.Name}' (model={spec.Model}) on the new Foundry surface.");
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

internal sealed record AgentSpec(string Name, string Description, string PromptFile, string Model);
