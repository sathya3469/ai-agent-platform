using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AiAgentPlatform.Api.Services;

public class RagService
{
    private readonly ChromaDbService _chromaDbService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RagService> _logger;
    private const string CollectionName = "documents";
    private const int ChunkSize = 500;
    private const int ChunkOverlap = 100;

    public RagService(ChromaDbService chromaDbService, IEmbeddingService embeddingService, ILogger<RagService> logger)
    {
        _chromaDbService = chromaDbService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<DocumentUploadResponse> AddDocumentAsync(Document document)
    {
        try
        {
            _logger.LogInformation("Processing document: {FileName}", document.FileName);

            // Split document into chunks
            var chunks = ChunkText(document.Content, ChunkSize, ChunkOverlap);
            document.Chunks = chunks;

            _logger.LogInformation("Created {ChunkCount} chunks from document", chunks.Count);

            // Generate embeddings for each chunk
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);

            // Prepare data for ChromaDB
            var ids = chunks.Select((_, i) => $"{document.Id}_chunk_{i}").ToList();
            var metadatas = chunks.Select((chunk, i) => new Dictionary<string, object>
            {
                { "document_id", document.Id },
                { "file_name", document.FileName },
                { "chunk_index", i },
                { "uploaded_at", document.UploadedAt.ToString("O") },
                { "file_type", document.FileType ?? "unknown" }
            }).ToList();

            // Add to ChromaDB
            await _chromaDbService.AddDocumentsAsync(
                CollectionName,
                ids,
                chunks,
                metadatas,
                embeddings
            );

            _logger.LogInformation("Document {DocumentId} indexed successfully with {ChunkCount} chunks",
                document.Id, chunks.Count);

            return new DocumentUploadResponse
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                ChunksCreated = chunks.Count,
                UploadedAt = document.UploadedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document to RAG service");
            throw;
        }
    }

    public async Task<List<ChunkResult>> RetrieveRelevantChunksAsync(string query, int topK = 5)
    {
        try
        {
            _logger.LogInformation("Retrieving relevant chunks for query: {Query}", query);

            // Generate embedding for query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            // Query ChromaDB
            var results = await _chromaDbService.QueryAsync(
                CollectionName,
                new List<float[]> { queryEmbedding },
                topK
            );

            // Parse results
            var chunkResults = new List<ChunkResult>();

            if (results.Count > 0 && results[0].Documents.Count > 0)
            {
                var documents = results[0].Documents[0];
                var distances = results[0].Distances[0];
                var metadatas = results[0].Metadatas[0];

                for (int i = 0; i < documents.Count; i++)
                {
                    var metadata = metadatas[i];
                    var fileName = metadata.ContainsKey("file_name") ? metadata["file_name"].ToString() ?? "Unknown" : "Unknown";
                    var documentId = metadata.ContainsKey("document_id") ? metadata["document_id"].ToString() ?? "" : "";

                    // Convert distance to similarity (cosine distance to similarity)
                    var similarity = 1 - (distances[i] / 2.0);

                    chunkResults.Add(new ChunkResult
                    {
                        ChunkText = documents[i],
                        DocumentId = documentId,
                        FileName = fileName,
                        Similarity = similarity
                    });
                }
            }

            _logger.LogInformation("Retrieved {Count} relevant chunks", chunkResults.Count);
            return chunkResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chunks from RAG service");
            throw;
        }
    }

    public async Task<string> GetAugmentedPromptAsync(string userQuery, int topK = 5)
    {
        try
        {
            var relevantChunks = await RetrieveRelevantChunksAsync(userQuery, topK);

            if (relevantChunks.Count == 0)
            {
                _logger.LogInformation("No relevant documents found for query");
                return userQuery;
            }

            var context = string.Join("\n\n---\n\n", relevantChunks
                .Select(c => $"[From {c.FileName}]\n{c.ChunkText}"));

            var augmentedPrompt = $@"Context from documents:
---
{context}
---

User Query: {userQuery}

Based on the provided context, please answer the user's query. If the context doesn't contain relevant information, you can use your general knowledge but mention that the answer is not based on the provided documents.";

            return augmentedPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating augmented prompt");
            // Return original query if RAG fails
            return userQuery;
        }
    }

    private List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();

        // Clean text
        text = Regex.Replace(text, @"\s+", " ").Trim();

        var sentences = text.Split(new[] { ".", "!", "?" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim() + ".")
            .ToList();

        var currentChunk = "";
        var currentSize = 0;

        foreach (var sentence in sentences)
        {
            if (currentSize + sentence.Length > chunkSize && !string.IsNullOrEmpty(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
                // Keep overlap
                var words = currentChunk.Split(' ');
                var overlapText = string.Join(" ", words.TakeLast(overlap / 5)); // Approximate word-based overlap
                currentChunk = overlapText + " ";
                currentSize = currentChunk.Length;
            }

            currentChunk += sentence + " ";
            currentSize += sentence.Length + 1;
        }

        if (!string.IsNullOrEmpty(currentChunk.Trim()))
        {
            chunks.Add(currentChunk.Trim());
        }

        return chunks;
    }
}
