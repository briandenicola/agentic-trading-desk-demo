using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OrchestrationApi.Models;

/// <summary>
/// Canonical JSON serialization options for the Trading Desk briefing contract: camelCase
/// property names, omit null <c>notes</c>, and stable (non-indented) output so DEMO mode is
/// byte-identical across runs. Mirrors <see cref="RmBriefingJson"/>.
/// </summary>
public static class TdBriefingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        // The DTO tree uses `required` for compile-time safety in the DEMO composer, but the LIVE
        // Foundry synthesizer (a small model emitting a large nested object) frequently omits one
        // or more nested fields. Without this, System.Text.Json throws a JsonException on the first
        // missing `required` member and the ENTIRE briefing is discarded — silently falling back to
        // the deterministic composer, which is why LIVE prompt edits never changed the output.
        // Disabling runtime `required` enforcement (only for this contract) lets the model's actual
        // call list survive partial output; list properties default to [] (see DTO initializers).
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static typeInfo =>
                {
                    if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
                    foreach (var property in typeInfo.Properties)
                    {
                        property.IsRequired = false;
                    }
                },
            },
        },
    };
}
