using System.Diagnostics;

namespace OrchestrationApi.Agents.Resilience;

/// <summary>
/// Process-wide rate limiter for model/Foundry calls. The LIVE briefing path makes many model calls
/// per run (one synthesizer call plus one <c>event-specialist</c> call per current event, on top of
/// the agent's internal tool hops), and several scenes/replicas can fire back-to-back — bursting past
/// the model deployment's per-minute quota and triggering HTTP 429 <c>rate_limit_exceeded</c>.
///
/// <para>An instance bounds the number of model calls <b>in flight at once</b> and optionally paces
/// successive call <i>starts</i> by a minimum interval, so the deployment quota is respected regardless
/// of how many briefings run concurrently. <see cref="FoundryRetry.ExecuteAsync{T}"/> acquires a lease
/// from the shared <see cref="Default"/> instance around every attempt; the lease is released during
/// retry backoff so a throttled call never holds a slot while it sleeps.</para>
///
/// <para>Tunable via <c>MODEL_MAX_CONCURRENCY</c> (default 3) and <c>MODEL_MIN_INTERVAL_MS</c>
/// (default 0 = no pacing). Unconfigured, <see cref="Default"/> admits effectively unbounded calls so
/// the DEMO path and unit tests are unaffected. Tests construct their own instances for isolation.</para>
/// </summary>
public sealed class ModelCallGate
{
    private readonly SemaphoreSlim _gate;
    private readonly TimeSpan _minInterval;
    private long _lastStartTicks;
    private readonly object _pace = new();

    /// <summary>Max concurrent model calls this instance admits.</summary>
    public int MaxConcurrency { get; }

    public ModelCallGate(int maxConcurrency, TimeSpan minInterval)
    {
        MaxConcurrency = Math.Max(1, maxConcurrency);
        _gate = new SemaphoreSlim(MaxConcurrency);
        _minInterval = minInterval < TimeSpan.Zero ? TimeSpan.Zero : minInterval;
    }

    /// <summary>
    /// Acquire a model-call slot. Awaits a free concurrency slot, then (if pacing is enabled) waits
    /// until at least the minimum interval has elapsed since the previous call start. Dispose the
    /// returned lease to release the slot.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await PaceAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            _gate.Release();
            throw;
        }
        return new Lease(_gate);
    }

    private async Task PaceAsync(CancellationToken ct)
    {
        if (_minInterval <= TimeSpan.Zero) return;

        TimeSpan wait;
        lock (_pace)
        {
            var now = Stopwatch.GetTimestamp();
            if (_lastStartTicks != 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(_lastStartTicks, now);
                wait = elapsed < _minInterval ? _minInterval - elapsed : TimeSpan.Zero;
            }
            else
            {
                wait = TimeSpan.Zero;
            }
            // Reserve this call's start slot up front so concurrent acquirers stagger.
            _lastStartTicks = now + (long)(wait.TotalSeconds * Stopwatch.Frequency);
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, ct).ConfigureAwait(false);
        }
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.Release();
        }
    }

    // ----------------------------------------------------------------- shared default (production)

    private static ModelCallGate _default = new(int.MaxValue, TimeSpan.Zero);
    private static int _configured;

    /// <summary>The process-wide gate used by <see cref="FoundryRetry"/>.</summary>
    public static ModelCallGate Default => _default;

    /// <summary>
    /// Configure the shared <see cref="Default"/> gate from app configuration, only if not already
    /// configured (first call wins). Used at app startup so that, in a test host where multiple app
    /// instances boot in parallel, the shared gate is not repeatedly swapped out under in-flight leases.
    /// </summary>
    public static void EnsureConfigured(IConfiguration config)
    {
        if (Interlocked.Exchange(ref _configured, 1) != 0) return;
        var max = int.TryParse(config["MODEL_MAX_CONCURRENCY"], out var c) && c > 0 ? c : 3;
        var intervalMs = int.TryParse(config["MODEL_MIN_INTERVAL_MS"], out var m) && m > 0 ? m : 0;
        _default = new ModelCallGate(max, TimeSpan.FromMilliseconds(intervalMs));
    }
}
