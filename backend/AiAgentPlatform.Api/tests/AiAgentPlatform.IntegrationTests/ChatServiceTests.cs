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
/// ChatService unit tests. The API surface these exercise lives in
/// AiAgentPlatform.Api/Services/ChatService.cs.
/// </summary>
public class ChatServiceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ChatServiceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Builds ChatService with its LLM provider replaced by a deterministic stub, so no live
    /// Ollama is required. The remaining dependencies come from the integration container
    /// because the constructor needs a real DbContext (history is persisted in PostgreSQL).
    /// </summary>
    private ChatService CreateSut(Mock<ILlmProvider> provider)
    {
        // Keep the scope alive for the life of the SUT: ChatService holds the scoped
        // DbContext and uses it lazily on every call, so disposing the scope here would
        // make the first assertion an ObjectDisposedException.
        var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        return new ChatService(
            provider.Object,
            services.GetRequiredService<ILogger<ChatService>>(),
            services.GetRequiredService<RagService>(),
            services.GetRequiredService<ApplicationDbContext>());
    }

    [Fact]
    public async Task ChatAsync_ReturnsLlmResponse()
    {
        // Arrange: ChatAsync forwards the (possibly RAG-augmented) prompt to the LLM provider
        // and returns its full response (see ChatService.ChatAsync).
        var provider = new Mock<ILlmProvider>();
        const string expected = "Hello there";
        provider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<string>()))
            .ReturnsAsync(expected);

        var sut = CreateSut(provider);

        // Act
        var result = await sut.ChatAsync("hi");

        // Assert
        result.Should().Be(expected);
        provider.Verify(p => p.GenerateResponseAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StreamChatAsync_WithValidMessage_ReturnsTokens()
    {
        // Arrange: StreamChatAsync forwards each token the provider yields, then persists the
        // concatenated assistant reply (see ChatService.StreamChatAsync).
        var provider = new Mock<ILlmProvider>();
        var expectedTokens = new[] { "Hello", ",", " I'm", " doing", " well", "." };
        provider
            .Setup(p => p.StreamResponseAsync(It.IsAny<string>()))
            .Returns(expectedTokens.ToAsyncEnumerable());

        var sut = CreateSut(provider);

        // Act
        var result = new List<string>();
        await foreach (var token in sut.StreamChatAsync("Hello, how are you?"))
        {
            result.Add(token);
        }

        // Assert
        result.Should().Equal(expectedTokens);
        provider.Verify(p => p.StreamResponseAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_ReturnsEmptyForUnknownSession()
    {
        // Arrange
        var provider = new Mock<ILlmProvider>();
        var sut = CreateSut(provider);

        // Act: an unknown session id has no messages, so the query returns an empty list
        // rather than throwing (see ChatService.GetConversationHistoryAsync).
        var history = await sut.GetConversationHistoryAsync(Guid.NewGuid().ToString());

        // Assert
        history.Should().NotBeNull();
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConversationHistoryAsync_ReturnsPersistedMessages()
    {
        // Arrange: send a message so there is a conversation to read back. The tests share one
        // PostgreSQL database and xUnit runs them in parallel, so this uses its own session id
        // rather than the "latest conversation" fallback, which could be another test's
        // conversation (see ChatService.GetConversationHistoryAsync).
        var provider = new Mock<ILlmProvider>();
        provider
            .Setup(p => p.GenerateResponseAsync(It.IsAny<string>()))
            .ReturnsAsync("persisted answer");

        var sessionId = Guid.NewGuid().ToString();
        var sut = CreateSut(provider);
        await sut.ChatAsync("persisted question", sessionId);

        // Act: the session id pins the read to the conversation written above.
        var history = await sut.GetConversationHistoryAsync(sessionId);

        // Assert
        history.Should().NotBeEmpty();
        history.Should().Contain(m => m.Role == "user" && m.Content == "persisted question");
        history.Should().Contain(m => m.Role == "assistant" && m.Content == "persisted answer");
    }

    [Fact]
    public void Message_RoleAndContent_RoundTrip()
    {
        // Arrange
        var message = new Message { Role = "user", Content = "Hi" };

        // Assert
        message.Role.Should().Be("user");
        message.Content.Should().Be("Hi");
    }
}
