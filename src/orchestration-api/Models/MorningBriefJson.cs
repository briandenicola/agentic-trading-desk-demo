using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrchestrationApi.Models;

/// <summary>
/// Canonical JSON serialization options for the MorningBrief contract. camelCase
/// property names, omit null <c>notes</c>, and stable (non-indented) output so DEMO
/// mode can be byte-identical across runs (SC-002, used later in Phase 6).
/// </summary>
public static class MorningBriefJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
