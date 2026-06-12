using FluentAssertions;
using OrchestrationApi.Agents.Resilience;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Tests for <see cref="ModelCallGate"/> — the model-call limiter that keeps bursty LIVE fan-outs under
/// the deployment quota. Each test uses its own instance (the production code uses the shared
/// <see cref="ModelCallGate.Default"/>) so the global gate is never perturbed. All data is fictional.
/// </summary>
public sealed class ModelCallGateTests
{
    [Fact]
    public async Task Never_admits_more_than_max_concurrency()
    {
        var gate = new ModelCallGate(maxConcurrency: 2, minInterval: TimeSpan.Zero);

        var inFlight = 0;
        var peak = 0;
        var sync = new object();

        async Task Work()
        {
            using var lease = await gate.AcquireAsync();
            lock (sync) { inFlight++; peak = Math.Max(peak, inFlight); }
            await Task.Delay(40);
            lock (sync) { inFlight--; }
        }

        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Work()));

        peak.Should().BeLessThanOrEqualTo(2);
        peak.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Releasing_a_lease_admits_the_next_caller()
    {
        var gate = new ModelCallGate(maxConcurrency: 1, minInterval: TimeSpan.Zero);

        var lease1 = await gate.AcquireAsync();
        var second = gate.AcquireAsync();
        second.IsCompleted.Should().BeFalse("the single slot is held by the first lease");

        lease1.Dispose();
        var lease2 = await second; // should now complete
        lease2.Dispose();
    }

    [Fact]
    public async Task Min_interval_paces_successive_call_starts()
    {
        var gate = new ModelCallGate(maxConcurrency: 4, minInterval: TimeSpan.FromMilliseconds(50));
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 4; i++)
        {
            using var lease = await gate.AcquireAsync();
        }

        // 4 starts paced by ~50ms ⇒ at least ~3 intervals elapsed.
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(120);
    }
}
