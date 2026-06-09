using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchestrationApi;
using OrchestrationApi.Agents;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Agents.Tools;
using OrchestrationApi.Live;
using OrchestrationApi.Models;
using WF.Garage.Observability;

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

// --- Reactive SSE hub (002 US2): live event push to open briefings (FR-010..FR-013) ---
builder.Services.AddSingleton<BriefingEventStream>();
builder.Services.AddHostedService<EventStreamPollingService>();

// --- RM Daily Briefing (PRIMARY scene): DEMO composer (offline) + LIVE tools/runner (Foundry) ---
builder.Services.AddScoped<RmBriefingComposer>();
builder.Services.AddScoped<RmBriefingTools>();
builder.Services.AddScoped<RmAgentRunner>();

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

app.Run();

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
