using MockApi;
using WF.Garage.Observability;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "mock-api";

builder.UseSerilog(ServiceName);
builder.AddOpenTelemetry(ServiceName);

// Fictional source-of-record data store (loaded once at startup).
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "Data");
builder.Services.AddSingleton(new MockDataStore(dataDirectory));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCorrelationId();
app.UseGlobalExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapMockEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based contract tests (T009).
public partial class Program;
