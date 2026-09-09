using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _ollamaBaseUrl;
    private readonly string _embeddingModel = "nomic-embed-text";

    public EmbeddingService(HttpClient httpClient, ILogger<EmbeddingService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ollamaBaseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            _logger.LogInformation("Generating embedding for text of length {Length}", text.Length);

            var request = new { model = _embeddingModel, prompt = text };
            var response = await _httpClient.PostAsJsonAsync($"{_ollamaBaseUrl}/api/embeddings", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Embedding service error: {Error}", error);
                throw new Exception($"Failed to generate embedding: {response.StatusCode}");
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(jsonString);
            var embedding = result.GetProperty("embedding").EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            throw;
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<float[]>();
        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text);
            embeddings.Add(embedding);
        }
        return embeddings;
    }
}

