// Agent provisioner (T020).
//
// Idempotently registers the "morning-brief" agent in Azure AI Foundry as a versioned
// "prompt" agent on the NEW Foundry surface, using the Microsoft Agent Framework SDK
// (Azure.AI.Projects + Microsoft.Agents.AI.AzureAI) + DefaultAzureCredential. This is the
// same surface the runtime (orchestration-api AgentRunner) connects to by name, and the
// same one the reference online-banking-demo uses (init_agents.py / ADR-005). It does NOT
// touch the classic Assistants (/assistants) API.
//
// Guarded so it cleanly no-ops when Foundry credentials/endpoint are absent (e.g. DEMO/local)
// instead of failing — no credential is acquired in that case.

using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.AI;

const string AgentName = "morning-brief";
const string AgentDescription =
    "Synthesizes the municipal-sales morning brief by calling the mock systems-of-record as tools.";

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT");
var model = Environment.GetEnvironmentVariable("FOUNDRY_MODEL") ?? "gpt-5.4-mini";

if (string.IsNullOrWhiteSpace(endpoint))
{
    Console.WriteLine("agent-provisioner: FOUNDRY_PROJECT_ENDPOINT not set — no-op (DEMO/local). " +
                      "Set the endpoint + Azure credentials to register the agent in Foundry.");
    return 0;
}

var instructions = await LoadInstructionsAsync();

try
{
    var projectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());

    // The runtime binds the real mock-api tools at request time; the stored definition itself
    // carries no tools (matches the reference init_agents.py PromptAgentDefinition pattern).
    var noTools = Array.Empty<AITool>();

#pragma warning disable CS0618 // rc5 transitional API; native AIProjectClient.Agents APIs are not yet stable.
    // Idempotency: if a named agent already exists, leave it; otherwise create it (new Foundry).
    try
    {
        await projectClient.GetAIAgentAsync(AgentName, noTools);
        Console.WriteLine($"agent-provisioner: agent '{AgentName}' already registered; leaving the existing version in place.");
    }
    catch (Exception)
    {
        var created = await projectClient.CreateAIAgentAsync(
            name: AgentName,
            model: model,
            instructions: instructions,
            description: AgentDescription,
            tools: noTools);
        Console.WriteLine($"agent-provisioner: registered agent '{created.Name}' (model={model}) on the new Foundry surface.");
    }
#pragma warning restore CS0618

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"agent-provisioner: registration failed — {ex.Message}");
    return 1;
}

static async Task<string> LoadInstructionsAsync()
{
    var path = Environment.GetEnvironmentVariable("PROMPT_PATH")
               ?? Path.Combine(AppContext.BaseDirectory, "Prompts", "morning-brief.md");
    if (File.Exists(path))
    {
        return await File.ReadAllTextAsync(path);
    }
    Console.WriteLine($"agent-provisioner: prompt not found at {path}; using a minimal fallback instruction.");
    return "You are the Morning Brief agent. Use the provided tools to gather data and emit a single JSON object matching the morning-brief schema.";
}
