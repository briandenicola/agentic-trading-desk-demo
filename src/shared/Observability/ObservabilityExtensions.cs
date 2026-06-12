using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace AgenticTradersDesk.Observability;

/// <summary>
/// Shared cross-cutting observability wiring (T006): Serilog structured logging,
/// OpenTelemetry tracing/metrics, a correlation-id middleware, and a global
/// exception handler that always emits structured JSON (never HTML) per FR-011.
/// </summary>
public static class ObservabilityExtensions
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    private const string CorrelationIdItemKey = "CorrelationId";

    /// <summary>
    /// Configure Serilog as the host logger, enriched with the service name and
    /// correlation id. Writes structured output to the console (offline-friendly).
    /// </summary>
    public static WebApplicationBuilder UseSerilog(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service.name", serviceName)
                .ReadFrom.Configuration(context.Configuration)
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {service.name} {CorrelationId} {Message:lj}{NewLine}{Exception}");
        });
        return builder;
    }

    /// <summary>
    /// Register OpenTelemetry tracing + metrics for ASP.NET Core and outbound HTTP.
    /// Exporters are attached based on configuration so DEMO mode stays fully offline:
    /// the OTLP exporter only attaches when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set,
    /// and the Azure Monitor exporter only attaches when
    /// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set. Callers may register
    /// additional <see cref="ActivitySource"/> / <see cref="System.Diagnostics.Metrics.Meter"/>
    /// names (e.g. the Agent Framework GenAI source) via the optional parameters so
    /// agent-run and tool-call spans plus token-usage metrics flow to the backend.
    /// </summary>
    public static WebApplicationBuilder AddOpenTelemetry(
        this WebApplicationBuilder builder,
        string serviceName,
        string[]? additionalSources = null,
        string[]? additionalMeters = null)
    {
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (additionalSources is not null)
                {
                    foreach (var source in additionalSources)
                    {
                        tracing.AddSource(source);
                    }
                }
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter();
                }
                if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
                {
                    tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsightsConnectionString);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (additionalMeters is not null)
                {
                    foreach (var meter in additionalMeters)
                    {
                        metrics.AddMeter(meter);
                    }
                }
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter();
                }
                if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
                {
                    metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = appInsightsConnectionString);
                }
            });

        return builder;
    }

    /// <summary>
    /// Read or mint a correlation id for each request, echo it on the response,
    /// and push it into the Serilog LogContext so every log line carries it.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var value)
                                && !string.IsNullOrWhiteSpace(value)
                ? value.ToString()
                : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("n");

            context.Items[CorrelationIdItemKey] = correlationId;
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });
    }

    /// <summary>
    /// Convert any unhandled exception into a structured JSON error response
    /// (never an HTML error page), preserving the correlation id (FR-011).
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                var correlationId = context.Items.TryGetValue(CorrelationIdItemKey, out var cid)
                    ? cid?.ToString()
                    : null;

                Log.Error(ex, "Unhandled exception (correlationId={CorrelationId})", correlationId);

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "An unexpected error occurred.",
                        correlationId
                    });
                }
            }
        });
    }

    /// <summary>Get the correlation id assigned to the current request, if any.</summary>
    public static string? GetCorrelationId(this HttpContext context)
        => context.Items.TryGetValue(CorrelationIdItemKey, out var cid) ? cid?.ToString() : null;
}
