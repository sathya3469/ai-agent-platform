using System.Net;
using System.Text;
using System.Text.Json;
using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AiAgentPlatform.IntegrationTests;

/// <summary>
/// Agent controller tests. The API surface these exercise lives in
/// AiAgentPlatform.Api/Controllers/AgentController.cs.
/// </summary>
public class AgentControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AgentControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWith(IAgentOrchestrator orchestrator)
    {
        return _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAgentOrchestrator>();
                services.AddScoped<IAgentOrchestrator>(_ => orchestrator);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task RunAgent_WithValidQuery_ReturnsResponse()
    {
        // Arrange: stub the orchestrator so the endpoint does not need a live LLM.
        const string expectedAnswer = "4";
        using var client = CreateClientWith(new FakeAgentOrchestrator(expectedAnswer));

        var request = new { query = "What is 2+2?", maxSteps = 10 };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/agent/run", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("finalAnswer");

        var agentResponse = JsonSerializer.Deserialize<AgentResponse>(json, TestJsonSerializerOptions.Default);
        agentResponse.Should().NotBeNull();
        agentResponse!.FinalAnswer.Should().Be(expectedAnswer);
        agentResponse.TotalSteps.Should().BeGreaterThan(0);
        agentResponse.Thoughts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetThoughts_ReturnsAgentThoughts()
    {
        // Arrange
        const string thoughtContent = "Considering the available tools";
        var orchestrator = new FakeAgentOrchestrator("final answer");
        await orchestrator.ExecuteAsync("seed query");

        using var client = CreateClientWith(orchestrator);

        // Act
        var response = await client.GetAsync("/api/agent/thoughts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        var thoughts = JsonSerializer.Deserialize<List<AgentThought>>(json, TestJsonSerializerOptions.Default);
        thoughts.Should().NotBeEmpty();
        thoughts![0].Content.Should().Be(thoughtContent);
    }

    [Fact]
    public async Task ClearState_RemovesThoughts()
    {
        // Arrange
        var orchestrator = new FakeAgentOrchestrator("final answer");
        await orchestrator.ExecuteAsync("seed query");
        (await orchestrator.GetThoughtsAsync()).Should().NotBeEmpty();

        using var client = CreateClientWith(orchestrator);

        // Act
        var response = await client.PostAsync("/api/agent/clear", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await orchestrator.GetThoughtsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RunAgent_WhenOrchestratorThrows_Returns500()
    {
        // Arrange: an unhandled orchestrator failure is turned into a 500 by the controller's
        // try/catch rather than propagating as a 200 with an error payload.
        using var client = CreateClientWith(new ThrowingAgentOrchestrator());

        var request = new { query = "anything" };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/agent/run", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("error");
    }
}

/// <summary>
/// Deterministic orchestrator that records reasoning steps without calling a model.
/// </summary>
internal sealed class FakeAgentOrchestrator : IAgentOrchestrator
{
    private readonly string _finalAnswer;
    private readonly List<AgentThought> _thoughts = new();

    public FakeAgentOrchestrator(string finalAnswer)
    {
        _finalAnswer = finalAnswer;
    }

    public Task<AgentResponse> ExecuteAsync(string query, int maxSteps = 10)
    {
        _thoughts.Clear();
        _thoughts.Add(new AgentThought
        {
            Step = 1,
            Type = "THINK",
            Content = "Considering the available tools"
        });
        _thoughts.Add(new AgentThought
        {
            Step = 2,
            Type = "FINAL_ANSWER",
            Content = _finalAnswer
        });

        return Task.FromResult(new AgentResponse
        {
            Thoughts = _thoughts.ToList(),
            ToolCalls = new List<ToolCall>(),
            FinalAnswer = _finalAnswer,
            // Serialized as camelCase "finalAnswer", the field the frontend reads
            // (see services/chatService.tsx).
            TotalSteps = _thoughts.Count,
            ExecutionTimeMs = 1
        });
    }

    public Task<List<AgentThought>> GetThoughtsAsync() => Task.FromResult(_thoughts.ToList());

    public void ClearState() => _thoughts.Clear();
}

/// <summary>Orchestrator that always fails, to cover the controller's error path.</summary>
internal sealed class ThrowingAgentOrchestrator : IAgentOrchestrator
{
    public Task<AgentResponse> ExecuteAsync(string query, int maxSteps = 10)
        => throw new InvalidOperationException("The agent is unavailable in this environment.");

    public Task<List<AgentThought>> GetThoughtsAsync()
        => throw new InvalidOperationException("The agent is unavailable in this environment.");

    public void ClearState() { }
}
