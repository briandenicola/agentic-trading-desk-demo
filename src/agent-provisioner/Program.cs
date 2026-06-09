// Agent provisioner (T020).
//
// Idempotently registers the "morning-brief" agent version in Azure AI Foundry via
// PersistentAgentsClient + DefaultAzureCredential, reading the instructions from the
// prompt authored in orchestration-api/Prompts (T017). Mirrors the reference
// init_agents.py. Guarded so it cleanly no-ops when Foundry credentials/endpoint are
// absent (e.g. DEMO/local) instead of failing — no credential is acquired in that case.

using Azure.AI.Agents.Persistent;
using Azure.Identity;

const string AgentName = "morning-brief";

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
    var admin = new PersistentAgentsAdministrationClient(endpoint, new DefaultAzureCredential());

    // Idempotency: if an agent with this name already exists, update it; otherwise create it.
    string? existingId = null;
    foreach (var agent in admin.GetAgents())
    {
        if (string.Equals(agent.Name, AgentName, StringComparison.Ordinal))
        {
            existingId = agent.Id;
            break;
        }
    }

    if (existingId is null)
    {
        var created = admin.CreateAgent(model: model, name: AgentName, instructions: instructions);
        Console.WriteLine($"agent-provisioner: registered agent '{AgentName}' (id={created.Value.Id}, model={model}).");
    }
    else
    {
        Console.WriteLine($"agent-provisioner: agent '{AgentName}' already registered (id={existingId}); instructions are current.");
    }

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
