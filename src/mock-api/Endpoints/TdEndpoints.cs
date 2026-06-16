using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MockApi;

/// <summary>
/// Maps the Trading Desk / Capital Markets resources under <c>/mock/td/...</c> (course
/// correction: real dataset swapped into the existing cockpit). Includes cross-dataset
/// aggregates (client activity, security interest). All data is fictional.
/// </summary>
public static class TdEndpoints
{
    public static void MapTdEndpoints(this IEndpointRouteBuilder app)
    {
        // --- Clients ---
        app.MapGet("/mock/td/clients", (string? type, string? region, string? salesperson, string? assetClass, TdDataStore store) =>
            Results.Json(store.Clients(type, region, salesperson, assetClass)));

        app.MapGet("/mock/td/clients/{clientId}", (string clientId, TdDataStore store) =>
        {
            var c = store.Client(clientId);
            return c is null ? NotFound($"Client '{clientId}'") : Results.Json(c);
        });

        app.MapGet("/mock/td/clients/{clientId}/activity", (string clientId, string? since, TdDataStore store) =>
        {
            var activity = store.ClientActivity(clientId, ParseDate(since));
            return activity is null ? NotFound($"Client '{clientId}'") : Results.Json(activity);
        });

        app.MapGet("/mock/td/clients/{clientId}/holdings", (string clientId, TdDataStore store) =>
            Results.Json(store.Holdings(clientId: clientId)));

        // --- Securities ---
        app.MapGet("/mock/td/securities", (string? assetClass, string? sector, string? issuer, string? region, TdDataStore store) =>
            Results.Json(store.Securities(assetClass, sector, issuer, region)));

        app.MapGet("/mock/td/securities/{securityId}", (string securityId, TdDataStore store) =>
        {
            var s = store.Security(securityId);
            return s is null ? NotFound($"Security '{securityId}'") : Results.Json(s);
        });

        app.MapGet("/mock/td/securities/{securityId}/interest", (string securityId, string? since, TdDataStore store) =>
        {
            var interest = store.SecurityInterest(securityId, ParseDate(since));
            return interest is null ? NotFound($"Security '{securityId}'") : Results.Json(interest);
        });

        // --- Transactional resources ---
        app.MapGet("/mock/td/trades", (string? clientId, string? securityId, string? direction, string? since, TdDataStore store) =>
            Results.Json(store.Trades(clientId, securityId, direction, ParseDate(since))));

        app.MapGet("/mock/td/rfqs", (string? clientId, string? securityId, string? status, string? since, TdDataStore store) =>
            Results.Json(store.Rfqs(clientId, securityId, status, ParseDate(since))));

        app.MapGet("/mock/td/crm", (string? clientId, string? urgency, string? since, TdDataStore store) =>
            Results.Json(store.Crm(clientId, urgency, ParseDate(since))));

        // Dealer inventory / axes.
        app.MapGet("/mock/td/inventory", (string? securityId, string? desk, TdDataStore store) =>
            Results.Json(store.Inventory(securityId, desk)));

        app.MapGet("/mock/td/inquiries", (string? clientId, string? securityId, string? sentiment, string? since, TdDataStore store) =>
            Results.Json(store.Inquiries(clientId, securityId, sentiment, ParseDate(since))));

        app.MapGet("/mock/td/news", (string? securityId, string? sector, string? macroTheme, string? since, TdDataStore store) =>
            Results.Json(store.News(securityId, sector, macroTheme, ParseDate(since))));

        app.MapGet("/mock/td/research", (string? securityId, string? sector, string? ratingAction, TdDataStore store) =>
            Results.Json(store.Research(securityId, sector, ratingAction)));

        app.MapGet("/mock/td/narrative-themes", (TdDataStore store) =>
            Results.Json(store.NarrativeThemes()));

        // --- New-issue deals + our syndicate role (lead-left bookrunner highlighting) ---
        // GET lists seeded + uploaded deals (optional issuer / securityId filter); POST ingests
        // deals parsed from an uploaded spreadsheet (non-durable — resets on restart).
        app.MapGet("/mock/td/new-issues", (string? issuer, string? securityId, TdNewIssueStore store) =>
            Results.Json(store.List(issuer, securityId)));

        app.MapPost("/mock/td/new-issues", (MockApi.Models.TdNewIssue[]? deals, TdNewIssueStore store) =>
        {
            if (deals is null || deals.Length == 0)
                return Results.Json(new { error = "Provide at least one new-issue deal." }, statusCode: StatusCodes.Status400BadRequest);

            var (added, updated, all) = store.Ingest(deals);
            return Results.Json(
                new { added, updated, total = all.Count, deals = all },
                statusCode: StatusCodes.Status201Created);
        });

        // --- Readiness probe for the trading-desk dataset ---
        app.MapGet("/mock/td/readyz", (TdDataStore store) =>
            store.IsReady
                ? Results.Json(new { status = "ready" })
                : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
    }

    private static IResult NotFound(string what) =>
        Results.Json(new { error = $"{what} not found." }, statusCode: StatusCodes.Status404NotFound);

    private static DateOnly? ParseDate(string? value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out var d) ? d : null;
}
