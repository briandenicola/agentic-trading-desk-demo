using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OrchestrationApi.Agents;

/// <summary>
/// Telemetry primitives for the LIVE morning-brief path (backlog 005 — full agent and
/// tool-call traceability). The <see cref="ActivitySource"/> and <see cref="Meter"/> share
/// a single <see cref="SourceName"/> that is registered with OpenTelemetry in
/// <c>Program.cs</c>, so three things flow to App Insights / OTLP under one name:
/// <list type="bullet">
///   <item>the Agent Framework GenAI spans emitted by <c>UseOpenTelemetry()</c> (model + token usage);</item>
///   <item>the parent agent-run span started in <see cref="AgentRunner"/>;</item>
///   <item>one child span per tool call (tool name, args, duration, result bytes).</item>
/// </list>
/// Nothing is exported in DEMO mode: the exporters only attach when a backend connection
/// string / OTLP endpoint is configured.
/// </summary>
internal static class OrchestrationTelemetry
{
    /// <summary>Shared ActivitySource + Meter name; also passed to the Agent Framework <c>UseOpenTelemetry(sourceName: ...)</c>.</summary>
    public const string SourceName = "WF.Garage.Orchestration";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    /// <summary>Total tokens consumed per LIVE morning-brief run (input + output).</summary>
    public static readonly Histogram<long> TokenUsage =
        Meter.CreateHistogram<long>("wf.morning_brief.tokens", unit: "{token}", description: "Total tokens per LIVE morning-brief run.");

    /// <summary>Wall-clock duration of each mock-api tool call, in milliseconds.</summary>
    public static readonly Histogram<double> ToolDuration =
        Meter.CreateHistogram<double>("wf.tool.duration", unit: "ms", description: "Duration of each mock-api tool call.");
}
