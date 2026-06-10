namespace OrchestrationApi.Live;

/// <summary>
/// Background poller that drives the reactive SSE hub (002-reactive-event-cockpit, US2). It calls
/// <see cref="BriefingEventStream.PollAndBroadcastAsync"/> on a fixed interval
/// (<c>SSE_POLL_INTERVAL_MS</c>, default 1000 ms) so intraday events ingested into the mock-api
/// event store reach open briefings within the 10 s budget (SC-002). A failed cycle is logged and
/// retried on the next tick — a transient mock-api hiccup never tears down the stream.
/// </summary>
public sealed class EventStreamPollingService : BackgroundService
{
    private readonly BriefingEventStream _stream;
    private readonly ILogger<EventStreamPollingService> _logger;
    private readonly TimeSpan _interval;

    public EventStreamPollingService(
        BriefingEventStream stream,
        IConfiguration configuration,
        ILogger<EventStreamPollingService> logger)
    {
        _stream = stream;
        _logger = logger;
        var intervalMs = int.TryParse(configuration["SSE_POLL_INTERVAL_MS"], out var ms) ? ms : 1000;
        _interval = TimeSpan.FromMilliseconds(Math.Max(250, intervalMs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _stream.PollAndBroadcastAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reactive SSE poll cycle failed; retrying next tick.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
