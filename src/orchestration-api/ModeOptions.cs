namespace OrchestrationApi;

/// <summary>
/// Resolved run mode for the orchestration API (T011). DEMO is the default and
/// requires no credentials; LIVE engages the Foundry agent path (Phase 3).
/// </summary>
public sealed record ModeOptions(bool DemoMode, string Mode)
{
    public static ModeOptions FromConfiguration(IConfiguration config)
    {
        // DEMO_MODE wins if explicitly set; otherwise fall back to MODE; default DEMO.
        var demoRaw = config["DEMO_MODE"];
        var modeRaw = config["MODE"];

        bool demo;
        if (!string.IsNullOrWhiteSpace(demoRaw))
        {
            demo = demoRaw is "1" || demoRaw.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrWhiteSpace(modeRaw))
        {
            demo = !modeRaw.Equals("LIVE", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            demo = true; // safe offline default
        }

        return new ModeOptions(demo, demo ? "DEMO" : "LIVE");
    }
}
