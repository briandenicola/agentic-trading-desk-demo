using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MockApi;

/// <summary>
/// Maps every operation declared in <c>openapi/tools.yaml</c> onto the fictional
/// <see cref="MockDataStore"/> (T008), plus liveness/readiness probes.
/// </summary>
public static class MockEndpoints
{
    public static void MapMockEndpoints(this IEndpointRouteBuilder app)
    {
        // --- Tableau: client value ---
        app.MapGet("/mock/tableau/clients", (MockDataStore store) =>
            Results.Json(store.AllClients()));

        app.MapGet("/mock/tableau/clients/{cid}", (string cid, MockDataStore store) =>
        {
            var client = store.Client(cid);
            return client is null
                ? Results.Json(new { error = $"Client '{cid}' not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Json(client);
        });

        // --- Dynamics: engagement footprint ---
        app.MapGet("/mock/dynamics/clients/{cid}/engagement", (string cid, MockDataStore store) =>
        {
            var engagement = store.Engagement(cid);
            return engagement is null
                ? Results.Json(new { error = $"Engagement for '{cid}' not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Json(engagement);
        });

        // --- Trading: axes + holdings ---
        app.MapGet("/mock/trading/axes", (MockDataStore store) =>
            Results.Json(store.AllAxes()));

        app.MapGet("/mock/trading/holdings", (string? cusip, string? state, string? sector, MockDataStore store) =>
            Results.Json(store.Holdings(cusip, state, sector)));

        // --- Calendar: new issues ---
        app.MapGet("/mock/calendar/newissues", (MockDataStore store) =>
            Results.Json(store.NewIssues()));

        // --- Market data + relative value ---
        app.MapGet("/mock/marketdata", (MockDataStore store) =>
            Results.Json(store.MarketData()));

        app.MapGet("/mock/marketdata/relval/{event_id}", (string event_id, MockDataStore store) =>
        {
            var relval = store.RelativeValue(event_id);
            return relval is null
                ? Results.Json(new { error = $"No relative-value context for event '{event_id}'." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Json(relval);
        });

        // --- News resolution ---
        app.MapGet("/mock/news/{event_id}", (string event_id, MockDataStore store) =>
        {
            var news = store.News(event_id);
            return news is null
                ? Results.Json(new { error = $"No news for event '{event_id}'." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Json(news);
        });

        // --- Coalition: by client, then by sector (literal segment wins over {cid}) ---
        app.MapGet("/mock/coalition/sector/{sector}", (string sector, MockDataStore store) =>
        {
            var node = store.CoalitionSector(sector);
            return node is null
                ? Results.Json(new { error = $"No coalition data for sector '{sector}'." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Json(node);
        });

        app.MapGet("/mock/coalition/{cid}", (string cid, MockDataStore store) =>
        {
            var node = store.Coalition(cid);
            return node is null
                ? Results.Json(new { error = $"No coalition data for client '{cid}'." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Json(node);
        });

        // --- Probes ---
        app.MapGet("/healthz", () => Results.Json(new { status = "ok" }));
        app.MapGet("/readyz", (MockDataStore store) =>
            store.IsReady
                ? Results.Json(new { status = "ready" })
                : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
    }
}
