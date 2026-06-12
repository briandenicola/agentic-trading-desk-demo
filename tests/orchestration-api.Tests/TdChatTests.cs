using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrchestrationApi.Tests;

/// <summary>
/// Tests for the grounded chat surface <c>POST /api/chat</c> when a <c>salespersonId</c> is
/// supplied — routing to the Institutional Sales &amp; Trading assistant (<see cref="OrchestrationApi.Agents.Demo.TdChatResponder"/>)
/// grounded in <c>/mock/td/*</c>. The DEMO path runs against the real mock-api hosted in-memory
/// (<see cref="MockApiBackedFactory"/>) so the full responder → trading mock-api seam is exercised
/// over HTTP (Principle II / FR-002). All data is fictional.
/// </summary>
public sealed class TdChatTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TdChatTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static object Payload(string text) => new
    {
        salespersonId = "Theo Wexler",
        messages = new[] { new { role = "user", content = text } },
    };

    private async Task<JsonNode> PostAsync(string text)
    {
        using var backing = new MockApiBackedFactory();
        var client = backing.CreateDemoClient(_factory);
        var response = await client.PostAsJsonAsync("/api/chat", Payload(text));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    [Fact]
    public async Task Who_to_call_routes_to_the_trading_desk_and_names_the_salesperson()
    {
        var body = await PostAsync("Who should I call first this morning?");

        body["mode"]!.GetValue<string>().Should().Be("DEMO");
        var message = body["message"]!.GetValue<string>();
        message.Should().Contain("Theo Wexler");
        message.Should().Contain("CL-");
    }

    [Fact]
    public async Task Client_id_question_returns_a_grounded_client_profile()
    {
        var body = await PostAsync("Tell me about CL-2001");

        body["mode"]!.GetValue<string>().Should().Be("DEMO");
        var message = body["message"]!.GetValue<string>();
        message.Should().Contain("CL-2001");
        message.Should().Contain("Hyperion Capital");
    }

    [Fact]
    public async Task Unknown_client_id_is_reported_plainly_without_fabrication()
    {
        var body = await PostAsync("Tell me about CL-9999");

        var message = body["message"]!.GetValue<string>();
        message.Should().Contain("couldn't find");
        message.Should().Contain("CL-9999");
    }

    [Fact]
    public async Task Off_topic_greeting_returns_the_trading_desk_help_fallback()
    {
        var body = await PostAsync("hi");

        var message = body["message"]!.GetValue<string>();
        message.Should().Contain("Trading Desk assistant");
    }
}
