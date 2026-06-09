using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrchestrationApi.Models;

/// <summary>
/// Canonical JSON serialization options for the RM Daily Briefing contract: camelCase
/// property names, omit null <c>notes</c>, and stable (non-indented) output so DEMO
/// mode is byte-identical across runs (SC-002). Mirrors <see cref="MorningBriefJson"/>.
/// </summary>
public static class RmBriefingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
