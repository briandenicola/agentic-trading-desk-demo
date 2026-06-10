using System.ClientModel;
using Azure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrchestrationApi.Agents.Resilience;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Tests for <see cref="FoundryRetry"/> (002 follow-up): retry-with-backoff for transient model
/// throttling (HTTP 429 <c>rate_limit_exceeded</c> / 503 / 408). Asserts that transient statuses are
/// retried up to the configured budget, non-transient errors and cancellation propagate immediately,
/// and the transient classification covers the typed Azure/System.ClientModel exceptions plus a
/// message-based fallback.
/// </summary>
public sealed class FoundryRetryTests
{
    private static readonly TimeSpan FastDelay = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task ExecuteAsync_retries_a_transient_429_then_succeeds()
    {
        var attempts = 0;
        var result = await FoundryRetry.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new RequestFailedException(429, "rate_limit_exceeded");
                }
                return Task.FromResult("ok");
            },
            maxAttempts: 4, baseDelay: FastDelay, logger: NullLogger.Instance, operation: "test");

        result.Should().Be("ok");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_retry_a_non_transient_400()
    {
        var attempts = 0;
        var act = async () => await FoundryRetry.ExecuteAsync<string>(
            _ =>
            {
                attempts++;
                throw new RequestFailedException(400, "bad request");
            },
            maxAttempts: 4, baseDelay: FastDelay, logger: NullLogger.Instance, operation: "test");

        await act.Should().ThrowAsync<RequestFailedException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_throws_the_last_error_after_exhausting_attempts()
    {
        var attempts = 0;
        var act = async () => await FoundryRetry.ExecuteAsync<string>(
            _ =>
            {
                attempts++;
                throw new RequestFailedException(429, "still throttled");
            },
            maxAttempts: 3, baseDelay: FastDelay, logger: NullLogger.Instance, operation: "test");

        await act.Should().ThrowAsync<RequestFailedException>();
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_retry_when_cancellation_is_requested()
    {
        using var cts = new CancellationTokenSource();
        var attempts = 0;
        var act = async () => await FoundryRetry.ExecuteAsync<string>(
            _ =>
            {
                attempts++;
                cts.Cancel();
                throw new RequestFailedException(429, "throttled then cancelled");
            },
            maxAttempts: 4, baseDelay: FastDelay, logger: NullLogger.Instance, operation: "test", ct: cts.Token);

        await act.Should().ThrowAsync<RequestFailedException>();
        attempts.Should().Be(1);
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(408, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(500, false)]
    public void IsTransient_classifies_azure_request_failed_by_status(int status, bool expected)
    {
        FoundryRetry.IsTransient(new RequestFailedException(status, "x"), out _).Should().Be(expected);
    }

    [Fact]
    public void IsTransient_classifies_client_result_exception_by_message()
    {
        // System.ClientModel surfaces model throttling as ClientResultException; when no response is
        // captured the typed status is unavailable, so the message fallback classifies the throttle.
        FoundryRetry.IsTransient(new ClientResultException("HTTP 429 (error: rate_limit_exceeded)"), out _)
            .Should().BeTrue();
        FoundryRetry.IsTransient(new ClientResultException("invalid request payload"), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void IsTransient_falls_back_to_message_sniffing_for_wrapped_throttling()
    {
        FoundryRetry.IsTransient(
            new InvalidOperationException("LIVE agent run failed: HTTP 429 (error: rate_limit_exceeded)"), out _)
            .Should().BeTrue();
        FoundryRetry.IsTransient(new InvalidOperationException("Too Many Requests"), out _).Should().BeTrue();
        FoundryRetry.IsTransient(new InvalidOperationException("null reference"), out _).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_unwraps_inner_exceptions()
    {
        var wrapped = new InvalidOperationException("outer", new RequestFailedException(503, "service busy"));
        FoundryRetry.IsTransient(wrapped, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("0", 0)]
    [InlineData("120", 60)]   // capped at 60s
    public void RetryAfter_parses_delta_seconds(string header, int expectedSeconds)
    {
        FoundryRetry.RetryAfter(header).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void RetryAfter_returns_null_for_missing_or_unparseable_values()
    {
        FoundryRetry.RetryAfter(null).Should().BeNull();
        FoundryRetry.RetryAfter("").Should().BeNull();
        FoundryRetry.RetryAfter("not-a-number").Should().BeNull();
    }

    [Fact]
    public void Backoff_stays_within_the_jittered_exponential_ceiling()
    {
        // attempt 3 ceiling = baseDelay * 2^2 = 400ms; full-jitter result is in [0, ceiling).
        var baseDelay = TimeSpan.FromMilliseconds(100);
        for (var i = 0; i < 50; i++)
        {
            FoundryRetry.Backoff(baseDelay, attempt: 3).Should().BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(400));
        }
    }
}
