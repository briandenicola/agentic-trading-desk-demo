using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchestrationApi;
using OrchestrationApi.Agents;
using OrchestrationApi.Agents.Demo;
using OrchestrationApi.Agents.Tools;
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

app.Run();

// Exposed for WebApplicationFactory-based tests (T012-T014).
public partial class Program;
