// Agent provisioner (T020).
//
// Idempotently registers the demo's Foundry agents as versioned "prompt" agents on the
// NEW Foundry surface, using the Microsoft Agent Framework SDK (Azure.AI.Projects +
// Microsoft.Agents.AI.AzureAI) + DefaultAzureCredential. This is the same surface the
// runtime (orchestration-api AgentRunner / RmAgentRunner) connects to by name, and the
// same one the reference online-banking-demo uses (init_agents.py / ADR-005). It does NOT
// touch the classic Assistants (/assistants) API.
//
// Registers:
//   - rm-daily-briefing : Commercial Banking RM Daily Briefing (the PRIMARY scene).
//   - morning-brief     : Municipal-sales morning brief (secondary scene).
//
// Guarded so it cleanly no-ops when Foundry credentials/endpoint are absent (e.g. DEMO/local)
// instead of failing — no credential is acquired in that case.

using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.AI;

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT");
var model = Environment.GetEnvironmentVariable("FOUNDRY_MODEL") ?? "gpt-5.4-mini";

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
        "rm-daily-briefing.md"),
    new AgentSpec(
        "morning-brief",
        "Synthesizes the municipal-sales morning brief by calling the mock systems-of-record as tools.",
        "morning-brief.md"),
};

try
{
    var projectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
    var noTools = Array.Empty<AITool>();

    foreach (var spec in agents)
    {
        var instructions = await LoadInstructionsAsync(spec.PromptFile);

#pragma warning disable CS0618 // rc5 transitional API; native AIProjectClient.Agents APIs are not yet stable.
        // Idempotency: if a named agent already exists, leave it; otherwise create it (new Foundry).
        try
        {
            await projectClient.GetAIAgentAsync(spec.Name, noTools);
            Console.WriteLine($"agent-provisioner: agent '{spec.Name}' already registered; leaving the existing version in place.");
        }
        catch (Exception)
        {
            var created = await projectClient.CreateAIAgentAsync(
                name: spec.Name,
                model: model,
                instructions: instructions,
                description: spec.Description,
                tools: noTools);
            Console.WriteLine($"agent-provisioner: registered agent '{created.Name}' (model={model}) on the new Foundry surface.");
        }
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

internal sealed record AgentSpec(string Name, string Description, string PromptFile);
