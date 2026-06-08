using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace OrchestrationApi.Tests;

/// <summary>
/// Shared helper (T012) that validates a MorningBrief payload against the canonical
/// <c>contracts/morning-brief.schema.json</c> (JSON Schema draft 2020-12). Reused by
/// the DEMO contract test (T013) and any future parity test (T032).
/// </summary>
public static class MorningBriefSchemaValidator
{
    private static readonly Lazy<JsonSchema> Schema = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "contracts", "morning-brief.schema.json");
        return JsonSchema.FromText(File.ReadAllText(path));
    });

    /// <summary>Evaluate a payload; returns whether it is valid plus any error detail.</summary>
    public static (bool IsValid, string Errors) Validate(JsonNode? payload)
    {
        // JsonSchema.Net 9.x evaluates a JsonElement; project the node onto one.
        var element = JsonSerializer.SerializeToElement(payload);

        var results = Schema.Value.Evaluate(element, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        if (results.IsValid)
        {
            return (true, string.Empty);
        }

        var messages = (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} -> {e.Value}"));

        return (false, string.Join(Environment.NewLine, messages));
    }

    /// <summary>Validate a raw JSON string payload.</summary>
    public static (bool IsValid, string Errors) Validate(string json)
        => Validate(JsonNode.Parse(json));
}
