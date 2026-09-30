using System.Net;
using System.Text;
using System.Text.Json;
using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AiAgentPlatform.IntegrationTests;

/// <summary>
/// Chat controller tests. The API surface these exercise lives in
/// AiAgentPlatform.Api/Controllers/ChatController.cs.
/// </summary>
public class ChatControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ChatControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task PostChat_WithValidMessage_Returns200()
    {
        // Arrange: stub the LLM provider so the endpoint does not need a running Ollama.
        const string expected = "Hello! I'm doing well, thank you.";
        var message = "Hello";

        using var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILlmProvider>();
                services.AddScoped<ILlmProvider>(_ => FakeLlmProvider.Streaming(expected));
            }))
            .CreateClient();

        var request = new { message, sessionId = (string?)null };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be(expected);
    }

    [Fact]
    public async Task PostChat_WithEmptyMessage_Returns400()
    {
        // Arrange: the controller rejects an empty message before touching the LLM.
        using var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILlmProvider>();
                services.AddScoped<ILlmProvider>(_ => FakeLlmProvider.Streaming("unused"));
            }))
            .CreateClient();

        var request = new { message = "", sessionId = (string?)null };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("empty");
    }

    [Fact]
    public async Task GetHistory_Returns200()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/Chat/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task DeleteHistory_Clears()
    {
        // Act
        var response = await _httpClient.DeleteAsync("/api/Chat/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("History cleared");
    }
}
