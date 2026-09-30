using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AiAgentPlatform.Api.Models;

namespace AiAgentPlatform.Api.Services;

public class ChromaDbService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChromaDbService> _logger;
    private readonly string _chromaUrl;
    private const string DefaultCollection = "documents";
    private static readonly ConcurrentDictionary<string, string> _collectionIdCache = new();

    public ChromaDbService(HttpClient httpClient, ILogger<ChromaDbService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _chromaUrl = configuration.GetValue<string>("ChromaDB:Url")?.TrimEnd('/') ?? "http://localhost:8000";
    }

    private async Task<string?> GetCollectionIdAsync(string collectionName)
    {
        if (_collectionIdCache.TryGetValue(collectionName, out var cachedId))
        {
            return cachedId;
        }

        try
        {
            // Try v2 API first
            var v2Url = $"{_chromaUrl}/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionName}";
            var response = await _httpClient.GetAsync(v2Url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    var id = idProp.GetString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        _collectionIdCache[collectionName] = id;
                        return id;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // A non-2xx (e.g. 404 for a not-yet-created collection) is expected here
            // — the caller creates the collection on demand. Only log at debug so the
            // noise does not drown out real failures.
            _logger.LogDebug(ex, "Error getting collection ID via v2");
        }

        return null;
    }

    // virtual so the unit tests can stub the ChromaDB round trip with Moq; the real
    // implementation never overrides them.
    public virtual async Task<bool> CollectionExistsAsync(string collectionName = DefaultCollection)
    {
        try
        {
            var id = await GetCollectionIdAsync(collectionName);
            if (!string.IsNullOrEmpty(id))
            {
                return true;
            }

            // Fallback check v1. A 404 is a legitimate "does not exist", so it
            // must not bubble up as an exception.
            var response = await _httpClient.GetAsync($"{_chromaUrl}/api/v1/collections/{collectionName}");
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Collection lookup for {CollectionName} returned {StatusCode}", collectionName, response.StatusCode);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking collection existence");
            return false;
        }
    }

    public virtual async Task CreateCollectionAsync(string collectionName = DefaultCollection)
    {
        try
        {
            // Try v2 creation first
            var v2Url = $"{_chromaUrl}/api/v2/tenants/default_tenant/databases/default_database/collections";
            var v2Payload = new
            {
                name = collectionName,
                get_or_create = true
            };

            var v2Response = await _httpClient.PostAsJsonAsync(v2Url, v2Payload);
            if (v2Response.IsSuccessStatusCode)
            {
                var json = await v2Response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    var id = idProp.GetString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        _collectionIdCache[collectionName] = id;
                        _logger.LogInformation("Collection {CollectionName} (ID: {Id}) created/retrieved via v2", collectionName, id);
                        return;
                    }
                }
            }

            // Fallback to v1
            var v1Payload = new
            {
                name = collectionName,
                metadata = new { hnsw_space = "cosine" }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_chromaUrl}/api/v1/collections", v1Payload);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                // A duplicate-collection create on v1 is not a hard failure, but anything
                // else is: the caller cannot add documents to a collection that was never made.
                _logger.LogWarning("Collection creation returned {StatusCode}: {Error}", response.StatusCode, error);
                throw new InvalidOperationException(
                    $"Failed to create ChromaDB collection '{collectionName}': {response.StatusCode} - {error}");
            }

            _logger.LogInformation("Collection {CollectionName} created successfully via v1", collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection");
            throw;
        }
    }

    public virtual async Task AddDocumentsAsync(string collectionName, List<string> ids, List<string> documents,
        List<Dictionary<string, object>>? metadatas = null, List<float[]>? embeddings = null)
    {
        try
        {
            // Ensure collection exists and get its ID if on v2
            if (!await CollectionExistsAsync(collectionName))
            {
                await CreateCollectionAsync(collectionName);
            }

            var collectionId = await GetCollectionIdAsync(collectionName);

            var payload = new
            {
                ids = ids,
                documents = documents,
                metadatas = metadatas,
                embeddings = embeddings
            };

            HttpResponseMessage response;
            if (!string.IsNullOrEmpty(collectionId))
            {
                var v2Url = $"{_chromaUrl}/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/add";
                response = await _httpClient.PostAsJsonAsync(v2Url, payload);
            }
            else
            {
                var v1Url = $"{_chromaUrl}/api/v1/collections/{collectionName}/add";
                response = await _httpClient.PostAsJsonAsync(v1Url, payload);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to add documents: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Failed to add documents to ChromaDB: {response.StatusCode} - {error}");
            }

            _logger.LogInformation("Added {Count} documents to collection {CollectionName}", ids.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding documents to ChromaDB");
            throw;
        }
    }

    public virtual async Task<List<QueryResult>> QueryAsync(string collectionName, List<float[]> queryEmbeddings,
        int nResults = 5, string? whereFilter = null)
    {
        try
        {
            var collectionId = await GetCollectionIdAsync(collectionName);

            var payload = new
            {
                query_embeddings = queryEmbeddings,
                n_results = nResults,
                where = whereFilter
            };

            HttpResponseMessage response;
            if (!string.IsNullOrEmpty(collectionId))
            {
                var v2Url = $"{_chromaUrl}/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/query";
                response = await _httpClient.PostAsJsonAsync(v2Url, payload);
            }
            else
            {
                var v1Url = $"{_chromaUrl}/api/v1/collections/{collectionName}/query";
                response = await _httpClient.PostAsJsonAsync(v1Url, payload);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Query failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new Exception($"Failed to query ChromaDB: {response.StatusCode} - {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // ChromaDB v2 returns a single object containing lists, v1 might return an array or object
            if (responseBody.TrimStart().StartsWith("["))
            {
                return JsonSerializer.Deserialize<List<QueryResult>>(responseBody, jsonOptions) ?? new List<QueryResult>();
            }
            else
            {
                var singleResult = JsonSerializer.Deserialize<QueryResult>(responseBody, jsonOptions);
                return singleResult != null ? new List<QueryResult> { singleResult } : new List<QueryResult>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying ChromaDB");
            throw;
        }
    }

    public virtual async Task<List<string>> GetIndexedFileNamesAsync(string collectionName = DefaultCollection)
    {
        try
        {
            var collectionId = await GetCollectionIdAsync(collectionName);
            HttpResponseMessage response;

            if (!string.IsNullOrEmpty(collectionId))
            {
                var v2Url = $"{_chromaUrl}/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/get";
                response = await _httpClient.PostAsJsonAsync(v2Url, new
                {
                    include = new[] { "metadatas" }
                });
            }
            else
            {
                var v1Url = $"{_chromaUrl}/api/v1/collections/{collectionName}/get";
                response = await _httpClient.PostAsJsonAsync(v1Url, new
                {
                    include = new[] { "metadatas" }
                });
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Unable to list files from collection {CollectionName}: {StatusCode} - {Error}", collectionName, response.StatusCode, error);
                return new List<string>();
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("metadatas", out var metadatasElement) && metadatasElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var metadata in metadatasElement.EnumerateArray())
                {
                    if (metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("file_name", out var fileNameProp))
                    {
                        var fileName = fileNameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            files.Add(fileName);
                        }
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("file_name", out var fileNameProp))
                    {
                        var fileName = fileNameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            files.Add(fileName);
                        }
                    }
                }
            }

            return files.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading indexed file names from ChromaDB collection {CollectionName}", collectionName);
            return new List<string>();
        }
    }

    public virtual async Task<List<QueryResult>> QueryByTextAsync(string collectionName, List<string> queryTexts,
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
