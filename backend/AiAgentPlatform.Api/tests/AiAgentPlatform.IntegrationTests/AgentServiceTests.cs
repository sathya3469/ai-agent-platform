using AiAgentPlatform.IntegrationTests;
using AiAgentPlatform.Api.Data;
using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AiAgentPlatform.Tests;

/// <summary>
/// ReActAgentService unit tests. The API surface these exercise lives in
/// AiAgentPlatform.Api/Services/ReActAgentService.cs.
/// </summary>
public class AgentServiceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly Mock<IToolExecutor> _mockToolExecutor = new();

    public AgentServiceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Builds the service under test with a deterministic LLM so no live Ollama is required.
    /// ChatService is resolved from the integration container because its constructor needs a
    /// DbContext; ReActAgentService only calls its StreamChatAsync, which the stub satisfies.
    /// </summary>
    private ReActAgentService CreateSut(Mock<ILlmProvider> llm)
    {
        // Build ChatService with a deterministic LLM; the remaining dependencies come from
        // the integration container because its constructor needs a real DbContext.
        // The scope is intentionally not disposed: ChatService holds the scoped DbContext and
        // uses it lazily, so disposing would break the assertions that follow.
        var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var chatService = new ChatService(
            llm.Object,
            services.GetRequiredService<ILogger<ChatService>>(),
            services.GetRequiredService<RagService>(),
            services.GetRequiredService<ApplicationDbContext>());

        var logger = services.GetRequiredService<ILogger<ReActAgentService>>();
        return new ReActAgentService(chatService, _mockToolExecutor.Object, logger);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAgentResponse()
    {
        // Arrange: every model call in the ReAct loop goes through ChatService.StreamChatAsync
        // (see ReActAgentService.GenerateThoughtAsync).
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm
            .Setup(p => p.StreamResponseAsync(It.IsAny<string>()))
            .Returns(() => new List<string> { "I should search the documents", "The answer is 42" }.ToAsyncEnumerable());

        var sut = CreateSut(mockLlm);

        // Act
        var response = await sut.ExecuteAsync("What is in my document?");

        // Assert
        response.Should().NotBeNull();
        response.Thoughts.Should().NotBeEmpty();
        response.FinalAnswer.Should().NotBeEmpty();
        response.ToolCalls.Should().NotBeNull();
        response.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
        response.TotalSteps.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithToolSuggestion_ExecutesTool()
    {
        // Arrange: the service treats a thought containing "search"/"use" as a tool trigger
        // (see ReActAgentService.ExecuteAsync → thoughtContent.Contains("search")).
        // NOTE: AiAgentPlatform.Api.Models.Tool and AiAgentPlatform.Api.Services.Tool both
        // exist; IToolExecutor is defined in terms of the Services one.
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm
            .Setup(p => p.StreamResponseAsync(It.IsAny<string>()))
            .Returns(() => new List<string> { "I will search the documents", "final answer" }.ToAsyncEnumerable());

        var tools = new List<AiAgentPlatform.Api.Services.Tool>
        {
            new() { Name = "rag_search", Description = "Search documents" }
        };

        _mockToolExecutor
            .Setup(e => e.GetAvailableTools())
            .Returns(tools);

        _mockToolExecutor
            .Setup(e => e.ExecuteToolAsync("rag_search", It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new ToolCall
            {
                Tool = "rag_search",
                Parameters = new Dictionary<string, object>(),
                Result = "search result"
            });

        var sut = CreateSut(mockLlm);

        // Act
        var withToolCall = await sut.ExecuteAsync("What is in my document?");

        // Assert
        withToolCall.ToolCalls.Should().ContainSingle();
        withToolCall.ToolCalls[0].Tool.Should().Be("rag_search");
        _mockToolExecutor.Verify(e => e.ExecuteToolAsync("rag_search", It.IsAny<Dictionary<string, object>>()), Times.Once);
    }

    [Fact]
    public async Task ClearState_ClearsThoughts()
    {
        // Arrange
        var mockLlm = new Mock<ILlmProvider>();
        mockLlm
            .Setup(p => p.StreamResponseAsync(It.IsAny<string>()))
            .Returns(() => new List<string> { "thought", "final answer" }.ToAsyncEnumerable());

        var sut = CreateSut(mockLlm);
        await sut.ExecuteAsync("seed query");

        // Act
        sut.ClearState();

        // Assert
        (await sut.GetThoughtsAsync()).Should().BeEmpty();
    }
}
