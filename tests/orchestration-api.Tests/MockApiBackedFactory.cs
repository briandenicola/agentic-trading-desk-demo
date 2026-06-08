extern alias MockApiHost;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrchestrationApi;

namespace OrchestrationApi.Tests;

/// <summary>
/// Hosts the real mock-api in-memory (TestServer) and rewires the orchestration-api's
/// typed <see cref="MockApiClient"/> to call it over HTTP in-process. This lets the DEMO
/// contract/perf tests (T013/T047) exercise the full composer → mock-api seam
/// (constitution Principle II / FR-002) without any external process or fixture reads.
/// </summary>
public sealed class MockApiBackedFactory : IDisposable
{
    private readonly WebApplicationFactory<MockApiHost::Program> _mockApi = new();

    /// <summary>
    /// Produce an orchestration-api client in DEMO mode whose MockApiClient is backed by
    /// the in-memory mock-api TestServer.
    /// </summary>
    public HttpClient CreateDemoClient(WebApplicationFactory<Program> orchestration)
    {
        var handler = _mockApi.Server.CreateHandler();
        return orchestration.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DEMO_MODE", "1");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<MockApiClient>(c => c.BaseAddress = new Uri("http://mock-api"))
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        }).CreateClient();
    }

    public void Dispose() => _mockApi.Dispose();
}
