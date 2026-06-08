using Microsoft.AspNetCore.Authentication.JwtBearer;
using OrchestrationApi;
using WF.Garage.Observability;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "orchestration-api";
const string CorsPolicy = "cockpit";

builder.UseSerilog(ServiceName);
builder.AddOpenTelemetry(ServiceName);

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

// NOTE: POST /api/agent/morning-brief is implemented in Phase 3 (T015-T016).
// Tests T013/T014 are written now and skipped until then (TDD red placeholder).

app.Run();

// Exposed for WebApplicationFactory-based tests (T012-T014).
public partial class Program;
