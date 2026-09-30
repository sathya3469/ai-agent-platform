using AiAgentPlatform.IntegrationTests;
using AiAgentPlatform.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AiAgentPlatform.Tests;

/// <summary>
/// RagService unit tests. The API surface these exercise lives in
/// AiAgentPlatform.Api/Services/RagService.cs.
/// </summary>
public class RagServiceTests
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ChromaDbService> _mockChromaDb;
    private readonly Mock<ILogger<RagService>> _mockLogger;

    public RagServiceTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockChromaDb = new Mock<ChromaDbService>(null!, null!, TestConfiguration.Instance);
        _mockLogger = new Mock<ILogger<RagService>>();
    }

    [Fact]
    public void ChunkTextByCharacters_SplitsLongTextIntoChunks()
    {
        // Arrange: a provider-independent, non-database code path.
        var text = "This is a test. " + string.Join(" ", Enumerable.Range(0, 100).Select(i => $"word{i}"));
        const int maxChunkSize = 50;

        // Act
        var chunks = CreateSut().ChunkTextByCharacters(text, maxChunkSize, overlap: 0);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c));
    }

    [Fact]
    public void ChunkTextByCharacters_WithEmptyText_ReturnsEmptyList()
    {
        // Act
        var chunks = CreateSut().ChunkTextByCharacters(string.Empty, maxChunkChars: 50, overlap: 0);

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void ChunkTextByCharacters_BoundsEveryChunk()
    {
        // Arrange: chunk length is capped by maxChunkChars, with a small allowance for
        // snapping to a sentence/newline boundary.
        var text = string.Concat(Enumerable.Repeat("a", 500));
        const int maxChunkSize = 50;

        // Act
        var chunks = CreateSut().ChunkTextByCharacters(text, maxChunkSize, overlap: 0);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(c => c.Length <= maxChunkSize + maxChunkSize / 2 + 1);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithNoResults_ReturnsEmpty()
    {
        // Arrange: the query path is already async and returns string.Empty when nothing matched.
        var mockChromaDb = new Mock<ChromaDbService>(null!, null!, TestConfiguration.Instance);
        mockChromaDb
            .Setup(s => s.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<List<float[]>>(),
                It.IsAny<int>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new List<QueryResult>());

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f });

        var sut = new RagService(
            mockChromaDb.Object,
            mockEmbedding.Object,
            new Mock<ILogger<RagService>>().Object,
            db: null!);

        // Act
        var context = await sut.RetrieveContextAsync("What is this?");

        // Assert
        context.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAugmentedPromptAsync_WhenRetrievalFails_ReturnsOriginalQuery()
    {
        // Arrange: retrieval failures fall back to the user's original message rather than
        // surfacing the error (see RagService.GetAugmentedPromptAsync catch-all).
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("ChromaDB is unavailable"));

        var sut = new RagService(
            _mockChromaDb.Object,
            mockEmbedding.Object,
            _mockLogger.Object,
            db: null!);

        const string query = "What is this?";

        // Act
        var result = await sut.GetAugmentedPromptAsync(query);

        // Assert
        result.Should().Be(query);
    }

    private RagService CreateSut()
    {
        return new RagService(
            _mockChromaDb.Object,
            _mockEmbeddingService.Object,
            _mockLogger.Object,
            db: null!);
    }
}
