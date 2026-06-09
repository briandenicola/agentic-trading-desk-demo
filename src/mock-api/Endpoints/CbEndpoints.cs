using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MockApi;

/// <summary>
/// Maps the Commercial Banking RM resources (course correction). These are additive under
/// <c>/mock/cb/...</c> and back the RM daily-briefing flow; the municipal endpoints in
/// <see cref="MockEndpoints"/> are unchanged. All data is fictional.
/// </summary>
public static class CbEndpoints
{
    public static void MapCbEndpoints(this IEndpointRouteBuilder app)
    {
        // --- Relationship managers ---
        app.MapGet("/mock/cb/relationship-managers", (CbDataStore store) =>
            Results.Json(store.Managers()));

        app.MapGet("/mock/cb/relationship-managers/{rmId}", (string rmId, CbDataStore store) =>
        {
            var rm = store.Manager(rmId);
            return rm is null
                ? NotFound($"Relationship manager '{rmId}'")
                : Results.Json(rm);
        });

        // Book-level snapshot the briefing leads with (manager + customers + KPIs).
        app.MapGet("/mock/cb/relationship-managers/{rmId}/book", (string rmId, string? asOf, CbDataStore store) =>
        {
            var book = store.RmBook(rmId, ParseDate(asOf));
            return book is null
                ? NotFound($"Relationship manager '{rmId}'")
                : Results.Json(book);
        });

        // --- Customers ---
        app.MapGet("/mock/cb/customers", (string? rm, string? state, string? sector, string? region, CbDataStore store) =>
            Results.Json(store.Customers(rm, state, sector, region)));

        app.MapGet("/mock/cb/customers/{customerId}", (string customerId, CbDataStore store) =>
        {
            var customer = store.Customer(customerId);
            return customer is null
                ? NotFound($"Customer '{customerId}'")
                : Results.Json(customer);
        });

        app.MapGet("/mock/cb/customers/{customerId}/opportunities", (string customerId, CbDataStore store) =>
            Results.Json(store.Opportunities(customerId: customerId)));

        app.MapGet("/mock/cb/customers/{customerId}/complaints", (string customerId, CbDataStore store) =>
            Results.Json(store.Complaints(customerId: customerId)));

        app.MapGet("/mock/cb/customers/{customerId}/interactions", (string customerId, CbDataStore store) =>
            Results.Json(store.Interactions(customerId: customerId)));

        // --- Opportunities (pipeline) ---
        app.MapGet("/mock/cb/opportunities", (
            string? rm, string? customerId, string? stage, bool? openOnly,
            int? closingWithinDays, double? minProbability, int? stuckMinDays, string? asOf,
            CbDataStore store) =>
            Results.Json(store.Opportunities(
                rm, customerId, stage, openOnly ?? false,
                closingWithinDays, minProbability, stuckMinDays, ParseDate(asOf))));

        // --- Complaints ---
        app.MapGet("/mock/cb/complaints", (
            string? rm, string? customerId, string? status, string? severity, bool? activeOnly,
            CbDataStore store) =>
            Results.Json(store.Complaints(rm, customerId, status, severity, activeOnly ?? false)));

        // --- Interactions (call logs + follow-ups) ---
        app.MapGet("/mock/cb/interactions", (
            string? rm, string? customerId, string? type, string? followUpDueBy, string? since,
            CbDataStore store) =>
            Results.Json(store.Interactions(rm, customerId, type, ParseDate(followUpDueBy), ParseDate(since))));

        // --- Readiness probe for the CB dataset ---
        app.MapGet("/mock/cb/readyz", (CbDataStore store) =>
            store.IsReady
                ? Results.Json(new { status = "ready" })
                : Results.Json(new { status = "not-ready" }, statusCode: StatusCodes.Status503ServiceUnavailable));
    }

    private static IResult NotFound(string what) =>
        Results.Json(new { error = $"{what} not found." }, statusCode: StatusCodes.Status404NotFound);

    private static DateOnly? ParseDate(string? value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out var d) ? d : null;
}
