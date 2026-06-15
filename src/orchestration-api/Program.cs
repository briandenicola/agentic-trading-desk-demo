using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Nodes;
using OrchestrationApi;
using OrchestrationApi.Agents;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Live;
using OrchestrationApi.Models;
using AgenticTradersDesk.Observability;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "orchestration-api";
const string CorsPolicy = "cockpit";

builder.UseSerilog(ServiceName);
builder.AddOpenTelemetry(
    ServiceName,
    additionalSources: [OrchestrationTelemetry.SourceName],
    additionalMeters: [OrchestrationTelemetry.SourceName]);

// --- Run mode (DEMO default, offline; LIVE engages Foundry in Phase 3) ---
var mode = ModeOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(mode);

// --- Typed HttpClient to the mock-api over HTTP only (Principle II / FR-002) ---
var mockBaseUrl = builder.Configuration["MOCK_API_BASEURL"] ?? "http://localhost:8080";
builder.Services.AddHttpClient<MockApiClient>(client =>
{
    client.BaseAddress = new Uri(mockBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// --- Morning-brief agents: DEMO composer (offline) + LIVE tools/runner (Foundry) ---
// Both are registered, but the LIVE path (AgentRunner) only constructs Foundry clients
// and DefaultAzureCredential when it is actually invoked — never in DEMO (FR-008).
builder.Services.AddScoped<MorningBriefComposer>();
builder.Services.AddScoped<MorningBriefTools>();
builder.Services.AddScoped<AgentRunner>();

// --- Reactive event tools (002): HTTP wrappers over the mock-api event store ---
builder.Services.AddScoped<EventTools>();

// --- Per-event multi-agent fan-out (002 US4): specialist assessments feed the LIVE
// synthesizer. Construction is side-effect free; nothing touches Foundry in DEMO. ---
builder.Services.AddScoped<OrchestrationApi.Agents.EventSynthesis.EventFanOut>();
builder.Services.AddScoped<OrchestrationApi.Agents.EventSynthesis.FoundryEventSpecialist>();

// --- Reactive SSE hub (002 US2): live event push to open briefings (FR-010..FR-013) ---
builder.Services.AddSingleton<BriefingEventStream>();
builder.Services.AddHostedService<EventStreamPollingService>();

// --- RM Daily Briefing (PRIMARY scene): DEMO composer (offline) + LIVE tools/runner (Foundry) ---
builder.Services.AddScoped<RmBriefingComposer>();
builder.Services.AddScoped<RmBriefingTools>();
builder.Services.AddScoped<RmAgentRunner>();

// --- Trading Desk morning briefing (Institutional Sales & Trading): DEMO composer (offline) +
// LIVE tools/runner (Foundry). The LIVE path (TdAgentRunner) only constructs Foundry clients and
// DefaultAzureCredential when invoked — never in DEMO. Same TdBriefing shape in both modes
// (Principle III). ---
builder.Services.AddScoped<TdBriefingComposer>();
builder.Services.AddScoped<TdBriefingTools>();
builder.Services.AddScoped<TdAgentRunner>();

// --- New Issue Radar storyboard: DEMO composer (offline) + LIVE Foundry runner. Same
// TdNewIssueStoryboard shape in both modes (Principle III). ---
builder.Services.AddScoped<TdNewIssueComposer>();
builder.Services.AddScoped<TdNewIssueRunner>();

// --- Markets-Intelligence assistant ("AI Chat"): DEMO intent responder (offline) + LIVE Foundry
// chat agent with the RM mock-api tools bound. Both grounded in the same systems-of-record. ---
builder.Services.AddScoped<ChatResponder>();
builder.Services.AddScoped<ChatAgentRunner>();

// Trading-desk "Open Chat": DEMO intent responder + LIVE Foundry chat agent, both grounded in the
// trading-desk systems-of-record (/mock/td/*). Routed when the request carries a salespersonId.
builder.Services.AddScoped<TdChatResponder>();
builder.Services.AddScoped<TdChatAgentRunner>();

// --- CORS for the React cockpit (tightened for deployment in Phase 8) ---
var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (string.IsNullOrWhiteSpace(corsOrigins))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// --- Entra ID / JWT-bearer scaffolding (not enforced until secured scenes land) ---
var authority = builder.Configuration["AZURE_AD_AUTHORITY"];
var audience = builder.Configuration["AZURE_AD_AUDIENCE"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (!string.IsNullOrWhiteSpace(authority)) options.Authority = authority;
        if (!string.IsNullOrWhiteSpace(audience)) options.Audience = audience;
        // No credentials required in DEMO; tokens are only validated when configured.
        options.RequireHttpsMetadata = false;
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Bound concurrent model/Foundry calls process-wide so bursty LIVE briefing fan-outs stay under the
// deployment's per-minute quota (avoids cascading HTTP 429s). Tunable via MODEL_MAX_CONCURRENCY /
// MODEL_MIN_INTERVAL_MS; no effect on DEMO mode (no model calls).
OrchestrationApi.Agents.Resilience.ModelCallGate.EnsureConfigured(builder.Configuration);

app.UseCorrelationId();
app.UseGlobalExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// --- Probes ---
app.MapGet("/healthz", () => Results.Json(new { status = "ok" }));
app.MapGet("/readyz", (ModeOptions m) => Results.Json(new { status = "ready", mode = m.Mode }));

// --- Build provenance: GET /version and /api/version report the baked git SHA so the
// running container can be matched to a committed-and-pushed source revision in the repo. ---
app.MapVersionEndpoints("orchestration-api");

// --- Scene endpoint: POST /api/agent/morning-brief (T016, per contracts/agent-api.yaml) ---
// Same MorningBrief shape in both modes (Principle III). DEMO → deterministic composer;
// LIVE → Foundry agent. The composer degrades to a structured brief with notes on
// upstream failure rather than throwing (FR-011), so the response is always JSON.
app.MapPost("/api/agent/morning-brief", async (
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MorningBriefRequest? request,
    ModeOptions modeOpts,
    MorningBriefComposer composer,
    AgentRunner runner,
    CancellationToken ct) =>
{
    var eventId = string.IsNullOrWhiteSpace(request?.Payload?.EventId)
        ? "fed_surprise_hike"
        : request!.Payload!.EventId!;
    var date = request?.Payload?.Date;

    try
    {
        var brief = modeOpts.DemoMode
            ? await composer.ComposeAsync(eventId, date, ct)
            : await runner.RunAsync(eventId, date, ct);

        return Results.Json(brief, MorningBriefJson.Options);
    }
    catch (UnknownMorningBriefEventException ex)
    {
        return Results.Problem(
            title: "Unknown morning-brief event",
            detail: $"Could not resolve eventId '{ex.EventId}'. Provide a known mock news event id.",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["eventId"] = ex.EventId });
    }
});

// --- PRIMARY scene endpoint: POST /api/agent/rm-briefing (Commercial Banking RM Daily Briefing) ---
// Same RmBriefing shape in both modes (Principle III). DEMO → deterministic composer;
// LIVE → Foundry agent. The composer degrades to a structured briefing with notes on
// upstream failure rather than throwing (FR-011), so the response is always JSON.
app.MapPost("/api/agent/rm-briefing", async (
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RmBriefingRequest? request,
    ModeOptions modeOpts,
    RmBriefingComposer composer,
    RmAgentRunner runner,
    CancellationToken ct) =>
{
    var rmId = string.IsNullOrWhiteSpace(request?.Payload?.RmId)
        ? RmBriefingComposer.DefaultRmId
        : request!.Payload!.RmId!;
    var date = request?.Payload?.Date;

    try
    {
        var brief = modeOpts.DemoMode
            ? await composer.ComposeAsync(rmId, date, ct)
            : await runner.RunAsync(rmId, date, ct);

        return Results.Json(brief, RmBriefingJson.Options);
    }
    catch (UnknownRelationshipManagerException ex)
    {
        return Results.Problem(
            title: "Unknown relationship manager",
            detail: $"Could not resolve rmId '{ex.RmId}'. Provide a known relationship-manager id (e.g. RM-104).",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["rmId"] = ex.RmId });
    }
});

// --- Trading Desk scene endpoint: POST /api/agent/td-briefing (Institutional Sales & Trading) ---
// Same TdBriefing shape in both modes (Principle III). DEMO → deterministic composer;
// LIVE → Foundry agent (TdAgentRunner). The composer/runner degrade to a structured briefing
// on upstream failure (FR-011), so the response is always JSON.
app.MapPost("/api/agent/td-briefing", async (
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TdBriefingRequest? request,
    ModeOptions modeOpts,
    TdBriefingComposer composer,
    TdAgentRunner runner,
    CancellationToken ct) =>
{
    var salespersonId = string.IsNullOrWhiteSpace(request?.Payload?.SalespersonId)
        ? TdBriefingComposer.DefaultSalespersonId
        : request!.Payload!.SalespersonId!;
    var date = request?.Payload?.Date;

    try
    {
        var brief = modeOpts.DemoMode
            ? await composer.ComposeAsync(salespersonId, date, ct)
            : await runner.RunAsync(salespersonId, date, ct);

        return Results.Json(brief, TdBriefingJson.Options);
    }
    catch (UnknownSalespersonException ex)
    {
        return Results.Problem(
            title: "Unknown coverage salesperson",
            detail: $"Could not resolve salesperson '{ex.SalespersonId}'. Provide a known coverage salesperson (e.g. 'Theo Wexler').",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["salespersonId"] = ex.SalespersonId });
    }
});

// --- New Issue Radar storyboard endpoint: POST /api/agent/td-new-issue ---
// Same TdNewIssueStoryboard shape in both modes (Principle III). DEMO → deterministic composer;
// LIVE → Foundry agent (TdNewIssueRunner), which degrades to the composer on upstream failure.
app.MapPost("/api/agent/td-new-issue", async (
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TdNewIssueRequest? request,
    ModeOptions modeOpts,
    TdNewIssueComposer composer,
    TdNewIssueRunner runner,
    EventTools eventTools,
    CancellationToken ct) =>
{
    var issuerSecurityId = request?.Payload?.IssuerSecurityId;
    var clientId = request?.Payload?.ClientId;
    var date = request?.Payload?.Date;

    var storyboard = modeOpts.DemoMode
        ? await composer.ComposeAsync(issuerSecurityId, clientId, date, ct)
        : await runner.RunAsync(issuerSecurityId, clientId, date, ct);

    // Fold in any injected events that touch this issuer so a single POST reflects the current
    // event store (parity with the briefings, where the composer already reads live events).
    try
    {
        var events = await eventTools.ListEventsAsync(ct: ct);
        (storyboard, _) = TdNewIssueLive.ApplyEvents(storyboard, events);
    }
    catch
    {
        // Event store unavailable — return the base storyboard unchanged.
    }

    return Results.Json(storyboard, TdNewIssueJson.Options);
});

// --- Admin / feed ingest endpoints (002 US3): same ingestion + reactive path as a real
// intraday event (FR-016). The browser reaches the mock-api event store ONLY through this
// orchestration proxy (Principle II); the SSE poller then pushes the reaction to open briefings. ---
app.MapGet("/api/events", async (EventTools eventTools, [FromQuery] string? scope, CancellationToken ct) =>
{
    var events = await eventTools.ListEventsAsync(scope, ct);
    return Results.Json(events, RmBriefingJson.Options);
});

// Customer directory for the admin News Desk type-ahead (id + display name). Proxies the
// internal mock-api over HTTP (Principle II / FR-002) — the browser never reaches mock-api
// directly. Degrades to an empty list so the form still works if the lookup is unavailable.
app.MapGet("/api/customers", async (MockApiClient mockApi, CancellationToken ct) =>
{
    JsonNode? node;
    try
    {
        node = await mockApi.GetJsonAsync("/mock/cb/customers", ct);
    }
    catch
    {
        return Results.Json(Array.Empty<object>());
    }

    var options = (node as JsonArray ?? [])
        .OfType<JsonObject>()
        .Select(c => new
        {
            customerId = (string?)c["customerId"] ?? string.Empty,
            name = (string?)c["dba"] ?? (string?)c["legalName"] ?? (string?)c["customerId"] ?? string.Empty
        })
        .Where(o => o.customerId.Length > 0)
        .OrderBy(o => o.customerId, StringComparer.Ordinal)
        .ToList();

    return Results.Json(options);
});

// --- Grounded Markets-Intelligence assistant ("AI Chat"): DEMO intent responder or LIVE Foundry
// chat agent, both grounded in the same mock systems-of-record. Mode-blind to the UI (Principle III). ---
app.MapPost("/api/chat", async (
    [FromBody] ChatRequest? request,
    ModeOptions modeOpts,
    ChatResponder responder,
    ChatAgentRunner runner,
    TdChatResponder tdResponder,
    TdChatAgentRunner tdRunner,
    CancellationToken ct) =>
{
    if (request is null || request.Messages is null || request.Messages.Count == 0 ||
        !request.Messages.Any(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.Content)))
    {
        return Results.Problem(
            title: "Empty chat request",
            detail: "Provide at least one user message in 'messages'.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    // A salespersonId routes to the trading-desk assistant (grounded in /mock/td/*); otherwise the
    // Commercial Banking RM assistant answers. Both share the mode-blind ChatReply shape.
    var trading = !string.IsNullOrWhiteSpace(request.SalespersonId);
    var reply = trading
        ? (modeOpts.DemoMode
            ? await tdResponder.RespondAsync(request, ct)
            : await tdRunner.RunAsync(request, ct))
        : (modeOpts.DemoMode
            ? await responder.RespondAsync(request, ct)
            : await runner.RunAsync(request, ct));

    return Results.Json(reply, RmBriefingJson.Options);
});

app.MapPost("/api/events", async (
    [FromBody] AdminNewsSubmission submission,
    EventTools eventTools,
    CancellationToken ct) =>
{
    var error = ValidateSubmission(submission);
    if (error is not null)
    {
        return Results.Problem(
            title: "Invalid news submission",
            detail: error,
            statusCode: StatusCodes.Status400BadRequest);
    }

    var marketEvent = new MarketEvent
    {
        Id = string.Empty, // server-set by the event store
        Type = submission.Type!,
        Headline = submission.Headline!,
        Summary = submission.Summary!,
        Source = string.IsNullOrWhiteSpace(submission.Source) ? "Admin desk (fictional)" : submission.Source,
        Severity = submission.Severity!,
        Direction = submission.Direction,
        Origin = "admin",
        AffectedEntities = submission.AffectedEntities ?? new AffectedEntities()
    };

    var result = await eventTools.IngestEventAsync(marketEvent, ct);
    if (!result.Succeeded || result.Event is null)
    {
        return Results.Problem(
            title: "Ingestion failed",
            detail: "The event store rejected or could not accept the submission.",
            statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Json(
        result.Event,
        RmBriefingJson.Options,
        statusCode: result.Added ? StatusCodes.Status201Created : StatusCodes.Status200OK);
});

app.MapGet("/api/agent/rm-briefing/stream", (
    HttpContext ctx,
    BriefingEventStream stream,
    [FromQuery] string? rmId,
    CancellationToken ct) =>
        StreamSceneAsync(ctx, stream, "rm-briefing", string.IsNullOrWhiteSpace(rmId) ? null : rmId, ct));

app.MapGet("/api/agent/morning-brief/stream", (
    HttpContext ctx,
    BriefingEventStream stream,
    CancellationToken ct) =>
        StreamSceneAsync(ctx, stream, "morning-brief", null, ct));

app.MapGet("/api/agent/td-briefing/stream", (
    HttpContext ctx,
    BriefingEventStream stream,
    [FromQuery] string? salespersonId,
    CancellationToken ct) =>
        StreamSceneAsync(ctx, stream, "td-briefing", string.IsNullOrWhiteSpace(salespersonId) ? null : salespersonId, ct));

app.MapGet("/api/agent/td-new-issue/stream", (
    HttpContext ctx,
    BriefingEventStream stream,
    [FromQuery] string? issuerSecurityId,
    CancellationToken ct) =>
        StreamSceneAsync(ctx, stream, "td-new-issue", string.IsNullOrWhiteSpace(issuerSecurityId) ? null : issuerSecurityId, ct));

app.Run();

static string? ValidateSubmission(AdminNewsSubmission s)
{
    if (s is null) return "Submission body is required.";
    if (string.IsNullOrWhiteSpace(s.Headline)) return "headline is required.";
    if (string.IsNullOrWhiteSpace(s.Summary)) return "summary is required.";
    if (!IsOneOf(s.Severity, "low", "medium", "high")) return "severity must be one of: low, medium, high.";
    if (!IsOneOf(s.Type, "macro_rate", "sector", "issuer_credit", "client_headline"))
        return "type must be one of: macro_rate, sector, issuer_credit, client_headline.";
    var ae = s.AffectedEntities;
    var hasSelector =
        (ae?.CustomerIds?.Count ?? 0) > 0 ||
        (ae?.Tickers?.Count ?? 0) > 0 ||
        (ae?.Sectors?.Count ?? 0) > 0 ||
        (ae?.Issuers?.Count ?? 0) > 0;
    if (!hasSelector)
        return "affectedEntities must contain at least one selector (customerIds, tickers, sectors, or issuers).";
    return null;

    static bool IsOneOf(string? value, params string[] allowed) =>
        value is not null && allowed.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
}

// --- SSE stream endpoints (002 US2): GET /api/agent/{scene}/stream (contracts/event-stream.sse.md) ---
// A long-lived text/event-stream: emits `ready`, an initial snapshot, then `briefing-update`
// frames as intraday events arrive, with `heartbeat` pings to hold the connection open. Every
// update is a full re-synthesized DTO (R7) so reconnects reconcile from the latest snapshot (R4).
static async Task StreamSceneAsync(
    HttpContext ctx,
    BriefingEventStream stream,
    string scene,
    string? persona,
    CancellationToken ct)
{
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers["Connection"] = "keep-alive";
    ctx.Response.Headers["X-Accel-Buffering"] = "no"; // belt-and-braces against proxy buffering
    ctx.Response.ContentType = "text/event-stream";
    await ctx.Response.Body.FlushAsync(ct);

    var sub = stream.Subscribe(scene, persona);
    var reader = sub.Frames.Reader;
    try
    {
        await ctx.Response.WriteAsync(stream.FormatReadyFrame(scene), ct);
        var snapshot = await stream.BuildSnapshotFrameAsync(scene, persona, ct);
        if (snapshot is not null)
        {
            await ctx.Response.WriteAsync(snapshot, ct);
        }
        await ctx.Response.Body.FlushAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            bool hasItem;
            try
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
                idle.CancelAfter(TimeSpan.FromSeconds(15));
                hasItem = await reader.WaitToReadAsync(idle.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await ctx.Response.WriteAsync(BriefingEventStream.FormatHeartbeatFrame(), ct);
                await ctx.Response.Body.FlushAsync(ct);
                continue;
            }

            if (!hasItem) break;

            while (reader.TryRead(out var frame))
            {
                await ctx.Response.WriteAsync(frame, ct);
            }
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected — normal stream teardown.
    }
    finally
    {
        stream.Unsubscribe(sub.Id);
    }
}

// Exposed for WebApplicationFactory-based tests (T012-T014).
public partial class Program;
