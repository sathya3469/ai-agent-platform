using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _ollamaBaseUrl;
    // Pulled from Ollama:EmbeddingModel (see appsettings). The default matches the
    // chunker in RagService, which sizes chunks for this model's context window.
    private readonly string _embeddingModel;

    public EmbeddingService(IHttpClientFactory httpClientFactory, ILogger<EmbeddingService> logger, IConfiguration configuration)
    {
        // "OllamaEmbeddings" is registered in Program.cs with a BaseAddress and a
        // timeout long enough for a cold-model embedding pass. Fall back to the
        // default client so the service still works in tests without that name.
        _httpClient = httpClientFactory.CreateClient("OllamaEmbeddings");
        _logger = logger;
        _ollamaBaseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
        _embeddingModel = configuration.GetValue<string>("Ollama:EmbeddingModel") ?? "nomic-embed-text";

        // Embedding a large document is slow on a cold model. The default 100s
        // HttpClient timeout reliably fires mid-document and surfaces as an
        // upload failure even though the request was progressing fine.
        var timeoutSeconds = configuration.GetValue<int?>("Ollama:EmbeddingTimeoutSeconds") ?? 300;
        if (_httpClient.Timeout < TimeSpan.FromSeconds(timeoutSeconds))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        // The endpoint URLs are built relative to _ollamaBaseUrl, so a
        // BaseAddress configured on the named client must not double-prefix them.
        _httpClient.BaseAddress = null;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            _logger.LogInformation("Generating embedding for text of length {Length}", text.Length);

            // The chunks handed to this method can be ~3000 chars; the "prompt" field
            // is the input for a single text embedding in Ollama's API.
            var request = new { model = _embeddingModel, input = text };

            // Use a CancellationTokenSource to enforce the timeout explicitly
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            var response = await _httpClient.PostAsJsonAsync($"{_ollamaBaseUrl}/api/embed", request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Embedding service error: {Error}", error);
                throw new Exception($"Failed to generate embedding: {response.StatusCode}");
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);

            // /api/embed returns { "embeddings": [ [ ... ] ] }. Fall back to the
            // legacy single-embedding "embedding" field if an older Ollama is in use.
            float[] embedding;
            if (doc.RootElement.TryGetProperty("embeddings", out var embeddingsProp) &&
                embeddingsProp.ValueKind == JsonValueKind.Array &&
                embeddingsProp.GetArrayLength() > 0)
            {
                embedding = embeddingsProp[0].EnumerateArray().Select(e => e.GetSingle()).ToArray();
            }
            else
            {
                embedding = doc.RootElement.GetProperty("embedding").EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();
            }

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
        if (texts.Count == 0)
        {
            return new List<float[]>();
        }

        // Batch a single request instead of one round trip per chunk: generating
        // embeddings sequentially for a 100-chunk document took minutes and made
        // the upload appear to hang (or time out).
        try
        {
            _logger.LogInformation("Generating embeddings for {Count} chunks", texts.Count);

            var request = new { model = _embeddingModel, input = texts };

            // Use a CancellationTokenSource to enforce the timeout explicitly
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            var response = await _httpClient.PostAsJsonAsync($"{_ollamaBaseUrl}/api/embed", request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Batch embedding error: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Failed to generate embeddings: {response.StatusCode}");
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);

            // /api/embed returns one vector per input, in the same order as the inputs.
            if (!doc.RootElement.TryGetProperty("embeddings", out var embeddingsProp) ||
                embeddingsProp.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Embedding response did not contain an 'embeddings' array");
            }

            var embeddings = new List<float[]>(embeddingsProp.GetArrayLength());
            foreach (var item in embeddingsProp.EnumerateArray())
            {
                embeddings.Add(item.EnumerateArray().Select(e => e.GetSingle()).ToArray());
            }

            if (embeddings.Count != texts.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding count ({embeddings.Count}) does not match input count ({texts.Count})");
            }

            return embeddings;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Batch embedding request timed out; falling back to per-chunk requests");
            // Fall back to the per-text path so an Ollama build without batch
            // support can still index a document (more slowly).
            var embeddings = new List<float[]>();
            foreach (var text in texts)
            {
                var embedding = await GenerateEmbeddingAsync(text);
                embeddings.Add(embedding);
            }
            return embeddings;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Batch embedding request timed out; falling back to per-chunk requests");
            // Fall back to the per-text path so an Ollama build without batch
            // support can still index a document (more slowly).
            var embeddings = new List<float[]>();
            foreach (var text in texts)
            {
                var embedding = await GenerateEmbeddingAsync(text);
                embeddings.Add(embedding);
            }
            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating batch embeddings; falling back to per-chunk requests");
            // Fall back to the per-text path so an Ollama build without batch
            // support can still index a document (more slowly).
            var embeddings = new List<float[]>();
            foreach (var text in texts)
            {
                var embedding = await GenerateEmbeddingAsync(text);
                embeddings.Add(embedding);
            }
            return embeddings;
        }
    }
}

