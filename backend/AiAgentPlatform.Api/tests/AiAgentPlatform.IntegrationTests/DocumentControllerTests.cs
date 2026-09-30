using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;

namespace AiAgentPlatform.IntegrationTests;

/// <summary>
/// Document controller tests. The API surface these exercise lives in
/// AiAgentPlatform.Api/Controllers/DocumentController.cs.
/// </summary>
public class DocumentControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public DocumentControllerTests(TestWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task UploadDocument_WithoutFile_ReturnsBadRequest()
    {
        // Arrange: a multipart request with no "file" part hits the [FromForm] IFormFile
        // guard in DocumentController.UploadDocument.
        using var content = new MultipartFormDataContent();

        // Act
        var response = await _httpClient.PostAsync("/api/Document/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadDocument_WithValidFile_Returns200()
    {
        // Arrange: AddDocumentAsync persists to PostgreSQL first and indexes into ChromaDB
        // best-effort, so a successful upload returns the document metadata even when the
        // vector store is unavailable.
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Test document content"), "file", "test.txt");

        // Act
        var response = await _httpClient.PostAsync("/api/Document/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("test.txt");
        json.Should().Contain("chunksCreated");
    }

    [Fact]
    public async Task UploadDocument_WithUnsupportedExtension_ReturnsBadRequest()
    {
        // Arrange: only text-based extensions are accepted (DocumentController.UploadDocument).
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("binary"), "file", "payload.exe");

        // Act
        var response = await _httpClient.PostAsync("/api/Document/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Unsupported file type");
    }

    [Fact]
    public async Task SearchDocuments_WithEmptyQuery_ReturnsBadRequest()
    {
        // Arrange
        using var content = new StringContent(
            "{\"query\":\"\"}",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/Document/search", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Query cannot be empty");
    }

    [Fact]
    public async Task GetIndexedFiles_Returns200()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/Document/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task ChromaHealth_Returns200()
    {
        // Act: reports reachability either way — 200 when reachable, 503 when not.
        var response = await _httpClient.GetAsync("/api/Document/health/chroma");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("chromaReachable");
    }
}
