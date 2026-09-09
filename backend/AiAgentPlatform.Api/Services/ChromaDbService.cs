using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class ChromaDbService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChromaDbService> _logger;
    private readonly string _chromaUrl;
    private const string DefaultCollection = "documents";

    public ChromaDbService(HttpClient httpClient, ILogger<ChromaDbService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _chromaUrl = configuration.GetValue<string>("ChromaDB:Url") ?? "http://localhost:8000";
    }

    public async Task<bool> CollectionExistsAsync(string collectionName = DefaultCollection)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_chromaUrl}/api/v1/collections/{collectionName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking collection existence");
            return false;
        }
    }

    public async Task CreateCollectionAsync(string collectionName = DefaultCollection)
    {
        try
        {
            var payload = new
            {
                name = collectionName,
                metadata = new { hnsw_space = "cosine" }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_chromaUrl}/api/v1/collections", payload);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Collection creation returned {StatusCode}: {Error}", response.StatusCode, error);
            }
            else
            {
                _logger.LogInformation("Collection {CollectionName} created successfully", collectionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection");
            throw;
        }
    }

    public async Task AddDocumentsAsync(string collectionName, List<string> ids, List<string> documents,
        List<Dictionary<string, object>>? metadatas = null, List<float[]>? embeddings = null)
    {
        try
        {
            // Ensure collection exists
            if (!await CollectionExistsAsync(collectionName))
            {
                await CreateCollectionAsync(collectionName);
            }

            var payload = new
            {
                ids = ids,
                documents = documents,
                metadatas = metadatas,
                embeddings = embeddings
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_chromaUrl}/api/v1/collections/{collectionName}/add",
                payload
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to add documents: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Failed to add documents to ChromaDB: {response.StatusCode}");
            }

            _logger.LogInformation("Added {Count} documents to collection {CollectionName}", ids.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding documents to ChromaDB");
            throw;
        }
    }

    public async Task<List<QueryResult>> QueryAsync(string collectionName, List<float[]> queryEmbeddings,
        int nResults = 5, string? whereFilter = null)
    {
        try
        {
            var payload = new
            {
                query_embeddings = queryEmbeddings,
                n_results = nResults,
                where = whereFilter
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_chromaUrl}/api/v1/collections/{collectionName}/query",
                payload
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Query failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Failed to query ChromaDB: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<List<QueryResult>>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<QueryResult>();

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying ChromaDB");
            throw;
        }
    }

    public async Task<List<QueryResult>> QueryByTextAsync(string collectionName, List<string> queryTexts,
        IEmbeddingService embeddingService, int nResults = 5)
    {
        try
        {
            // Generate embeddings for query texts
            var embeddings = await embeddingService.GenerateEmbeddingsAsync(queryTexts);

            // Query with embeddings
            return await QueryAsync(collectionName, embeddings, nResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying ChromaDB by text");
            throw;
        }
    }
}

public class QueryResult
{
    public List<List<string>> Documents { get; set; } = new();
    public List<List<double>> Distances { get; set; } = new();
    public List<List<string>> Ids { get; set; } = new();
    public List<List<Dictionary<string, object>>> Metadatas { get; set; } = new();
}
