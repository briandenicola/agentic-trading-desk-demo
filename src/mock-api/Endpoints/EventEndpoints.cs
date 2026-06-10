using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MockApi.Models;

namespace MockApi;

/// <summary>
/// Maps the reactive event-store resources under <c>/mock/events</c> (002-reactive-event-cockpit).
/// Backs the multi-event briefing synthesis and the Admin inject path. The orchestration layer
/// consumes these over HTTP only (constitution Principle II). All data is fictional.
/// </summary>
public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        // List all current events (optionally by scope: overnight | intraday).
        app.MapGet("/mock/events", (string? scope, EventStore store) =>
            Results.Json(store.List(scope), EventJson.Options));

        // Events affecting one portfolio entity (kind: customer | ticker | sector | issuer; default any).
        app.MapGet("/mock/events/by-entity", (string? value, string? kind, EventStore store) =>
            string.IsNullOrWhiteSpace(value)
                ? Results.Json(new { error = "Query parameter 'value' is required." }, statusCode: StatusCodes.Status400BadRequest)
                : Results.Json(store.ByEntity(value, kind), EventJson.Options));

        // Ingest an intraday event (Admin inject / feed). Validates, dedupes, server-sets fields.
        app.MapPost("/mock/events", (MarketEvent incoming, EventStore store) =>
        {
            var error = Validate(incoming);
            if (error is not null)
                return Results.Json(new { error }, statusCode: StatusCodes.Status400BadRequest);

            var (stored, added) = store.Ingest(incoming);
            return Results.Json(
                new { @event = stored, added },
                EventJson.Options,
                statusCode: added ? StatusCodes.Status201Created : StatusCodes.Status200OK);
        });

        // Readiness probe for the event dataset.
        app.MapGet("/mock/events/readyz", (EventStore store) =>
            store.IsReady
                ? Results.Json(new { status = "ready" })
                : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
    }

    private static string? Validate(MarketEvent e)
    {
        if (e is null) return "Event body is required.";
        if (string.IsNullOrWhiteSpace(e.Headline)) return "headline is required.";
        if (string.IsNullOrWhiteSpace(e.Summary)) return "summary is required.";
        if (e.AffectedEntities is null || !e.AffectedEntities.HasAny)
            return "affectedEntities must contain at least one selector (customerIds, tickers, sectors, or issuers).";
        if (!IsOneOf(e.Severity, "low", "medium", "high")) return "severity must be one of: low, medium, high.";
        if (!IsOneOf(e.Type, "macro_rate", "sector", "issuer_credit", "client_headline"))
            return "type must be one of: macro_rate, sector, issuer_credit, client_headline.";
        return null;
    }

    private static bool IsOneOf(string? value, params string[] allowed) =>
        value is not null && allowed.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
}
