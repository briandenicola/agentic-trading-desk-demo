using System.ClientModel;
using System.ClientModel.Primitives;
using Azure;

namespace OrchestrationApi.Agents.Resilience;

/// <summary>
/// Lightweight retry-with-backoff for transient Foundry/model throttling (002 follow-up). Each LIVE
/// briefing makes roughly one synthesizer call plus one <c>event-specialist</c> call per current
/// event, so firing several scenes back-to-back can momentarily exceed the model deployment's
/// per-minute quota (HTTP 429 <c>rate_limit_exceeded</c>) — and 503/408 happen under load.
///
/// <see cref="ExecuteAsync{T}"/> retries only those transient statuses, honoring a server
/// <c>Retry-After</c> header when present and otherwise using exponential backoff with full jitter
/// (so concurrent fan-out specialists don't retry in lockstep). Non-transient errors and
/// cancellation propagate immediately. Tunable via <c>FOUNDRY_RETRY_MAX_ATTEMPTS</c> /
/// <c>FOUNDRY_RETRY_BASE_DELAY_MS</c>.
/// </summary>
public static class FoundryRetry
{
    private const double MaxBackoffMs = 30_000;
    private const double MaxRetryAfterSeconds = 60;

    /// <summary>Read the retry budget from configuration (defaults: 4 attempts, 500 ms base delay).</summary>
    public static (int MaxAttempts, TimeSpan BaseDelay) SettingsFrom(IConfiguration config)
    {
        var attempts = int.TryParse(config["FOUNDRY_RETRY_MAX_ATTEMPTS"], out var a) && a > 0 ? a : 4;
        var baseMs = int.TryParse(config["FOUNDRY_RETRY_BASE_DELAY_MS"], out var b) && b > 0 ? b : 500;
        return (attempts, TimeSpan.FromMilliseconds(baseMs));
    }

    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        int maxAttempts,
        TimeSpan baseDelay,
        ILogger logger,
        string operation,
        CancellationToken ct = default)
    {
        maxAttempts = Math.Max(1, maxAttempts);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action(ct);
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested || attempt >= maxAttempts || !IsTransient(ex, out var retryAfter))
                {
                    throw;
                }

                var delay = retryAfter ?? Backoff(baseDelay, attempt);
                logger.LogWarning(
                    "Transient throttling on {Operation} (attempt {Attempt}/{Max}); retrying in {DelayMs} ms. {Error}",
                    operation, attempt, maxAttempts, (int)delay.TotalMilliseconds, ex.Message);
                await Task.Delay(delay, ct);
            }
        }
    }

    internal static TimeSpan Backoff(TimeSpan baseDelay, int attempt)
    {
        // Exponential (2^(attempt-1)) with full jitter, capped — spreads concurrent retries out.
        var expMs = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var jittered = Random.Shared.NextDouble() * Math.Min(expMs, MaxBackoffMs);
        return TimeSpan.FromMilliseconds(jittered);
    }

    internal static bool IsTransient(Exception ex, out TimeSpan? retryAfter)
    {
        retryAfter = null;
        for (var e = ex; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case RequestFailedException rfe when IsTransientStatus(rfe.Status):
                    retryAfter = RetryAfter(HeaderOrNull(rfe.GetRawResponse()));
                    return true;
                case ClientResultException cre when IsTransientStatus(cre.Status):
                    retryAfter = RetryAfter(HeaderOrNull(cre.GetRawResponse()));
                    return true;
            }
        }

        // Fallback: the typed status isn't always surfaced through the Agent Framework wrappers, so
        // sniff the message for throttling tokens as a last resort.
        var msg = ex.Message;
        return msg.Contains("429")
            || msg.Contains("rate_limit", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientStatus(int status) => status is 429 or 503 or 408;

    private static string? HeaderOrNull(Response? response) =>
        response is not null && response.Headers.TryGetValue("Retry-After", out var value) ? value : null;

    private static string? HeaderOrNull(PipelineResponse? response) =>
        response is not null && response.Headers.TryGetValue("Retry-After", out var value) ? value : null;

    internal static TimeSpan? RetryAfter(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return null;
        if (int.TryParse(headerValue, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryAfterSeconds));
        }
        if (DateTimeOffset.TryParse(headerValue, out var when))
        {
            var delta = when - DateTimeOffset.UtcNow;
            if (delta <= TimeSpan.Zero) return TimeSpan.Zero;
            return delta.TotalSeconds < MaxRetryAfterSeconds ? delta : TimeSpan.FromSeconds(MaxRetryAfterSeconds);
        }
        return null;
    }
}
