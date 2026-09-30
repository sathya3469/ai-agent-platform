using AiAgentPlatform.Api.Data;
using AiAgentPlatform.Api.Data.Entities;
using AiAgentPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AiAgentPlatform.Api.Services;

// Both AiAgentPlatform.Api.Data.Entities and AiAgentPlatform.Api.Models define Document/DocumentChunk
// types, so alias them to keep the persistence layer (entities) and the API contract (models) distinct.
using DocumentEntity = AiAgentPlatform.Api.Data.Entities.Document;

public class RagService
{
    private readonly ChromaDbService _chromaDbService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RagService> _logger;
    private readonly ApplicationDbContext _db;
    private const string CollectionName = "documents";
    private const int ChunkSize = 1500;   // was 500
    private const int ChunkOverlap = 200; // was 100

    public RagService(ChromaDbService chromaDbService, IEmbeddingService embeddingService, ILogger<RagService> logger, ApplicationDbContext db)
    {
        _chromaDbService = chromaDbService;
        _embeddingService = embeddingService;
        _logger = logger;
        _db = db;
    }

    public async Task<DocumentUploadResponse> AddDocumentAsync(Models.Document document)
    {
        // Chunking is done up front because both stores (PostgreSQL + ChromaDB) need it, and
        // it has no dependency on ChromaDB availability.
        var chunks = ChunkTextByParagraphs(document.Content, ChunkSize, ChunkOverlap);

        // Fallback to character-based chunking if paragraphs are too large
        if (chunks.Count == 1 && chunks[0].Length > ChunkSize * 2)
        {
            chunks = ChunkTextByCharacters(chunks[0], ChunkSize, ChunkOverlap);
        }
        document.Chunks = chunks;

        try
        {
            _logger.LogInformation("Processing document: {FileName} ({ChunkCount} chunks)", document.FileName, chunks.Count);

            // Re-uploading the same file must not create a duplicate document. Look up the
            // existing row by filename and reuse its id so the re-index overwrites (rather than
            // appends to) the previous version in both PostgreSQL and ChromaDB.
            //
            // Deliberately do NOT Include the chunks: leaving the stale chunk entities tracked
            // and then adding the new chunks to the same navigation collection made SaveChanges
            // emit a DELETE and an INSERT against the *same* primary key in a single batch, where
            // one statement affected 0 rows and SaveChanges threw DbUpdateConcurrencyException on
            // every re-upload of an already-indexed file.
            var existingDocument = await _db.Documents
                .FirstOrDefaultAsync(d => d.Filename == document.FileName);

            Guid documentId;
            bool isReupload = existingDocument is not null;

            if (isReupload)
            {
                documentId = existingDocument!.Id;

                // Drop the stale chunk rows by document id. Doing this directly in the database
                // (instead of via the tracked navigation collection) both avoids loading them and
                // guarantees the delete does not share a batch with the inserts below.
                await _db.DocumentChunks
                    .Where(chunk => chunk.DocumentId == documentId)
                    .ExecuteDeleteAsync();

                existingDocument.ContentType = document.FileType ?? "unknown";
                existingDocument.ChunkCount = chunks.Count;

                _logger.LogInformation("Document {DocumentId} already exists — replacing its chunk(s)",
                    documentId);
            }
            else
            {
                documentId = Guid.Parse(document.Id);
            }

            // ChromaDB ids are deterministic per chunk (document id + index), so re-uploading the
            // same file overwrites the previous vectors instead of adding duplicates.
            var ids = chunks.Select((_, i) => $"{documentId}_chunk_{i}").ToList();

            // Persist document provenance + chunk text to PostgreSQL first. This is the source of
            // truth for what was uploaded; ChromaDB is only a search index over the same content.
            var documentEntity = isReupload
                ? existingDocument!
                : new DocumentEntity
                {
                    Id = documentId,
                    Filename = document.FileName,
                    ContentType = document.FileType ?? "unknown",
                    ChunkCount = chunks.Count
                };

            // Fresh chunk rows are added untracked-by-navigation: the collection on an existing
            // document entity is left empty so SaveChanges issues only the INSERTs.
            for (int i = 0; i < chunks.Count; i++)
            {
                _db.DocumentChunks.Add(new DocumentChunk
                {
                    DocumentId = documentId,
                    ChunkIndex = i,
                    Content = chunks[i],
                    EmbeddingId = ids[i]
                });
            }

            if (!isReupload)
            {
                _db.Documents.Add(documentEntity);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} persisted to PostgreSQL with {ChunkCount} chunk rows",
                documentId, chunks.Count);

            // Index into ChromaDB. A failure here must not lose the upload — the metadata is
            // already committed, and a later indexing run can rebuild the collection.
            try
            {
                // Point the model at the resolved (possibly reused) id so ChromaDB metadata and
                // ids match the PostgreSQL row.
                document.Id = documentId.ToString();

                await IndexInChromaDbAsync(document, chunks, ids);

                _logger.LogInformation("Document {DocumentId} indexed in ChromaDB with {ChunkCount} chunks",
                    documentId, chunks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Document {DocumentId} was saved to PostgreSQL but could not be indexed in ChromaDB. " +
                    "Search will be unavailable until ChromaDB is running and the document is re-indexed.",
                    documentId);
            }

            return new DocumentUploadResponse
            {
                DocumentId = documentId.ToString(),
                FileName = document.FileName,
                ChunksCreated = chunks.Count,
                UploadedAt = document.UploadedAt,
                Progress = 100, // Indicate completion
                Message = isReupload
                    ? "Document already exists — existing entry updated and re-indexed"
                    : "Document persisted to PostgreSQL and indexed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document to RAG service");
            throw;
        }
    }

    /// <summary>
    /// Ensures the collection exists and upserts the chunk text + embeddings into ChromaDB.
    /// </summary>
    private async Task IndexInChromaDbAsync(Models.Document document, List<string> chunks, List<string> ids)
    {
        // Ensure the collection exists before processing (fails fast if ChromaDB is down)
        bool exists;
        try
        {
            exists = await _chromaDbService.CollectionExistsAsync(CollectionName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "ChromaDB is unavailable. Please start it on localhost:8000.", ex);
        }

        if (!exists)
        {
            await _chromaDbService.CreateCollectionAsync(CollectionName);
        }

        _logger.LogInformation("Created {ChunkCount} chunks from document. Starting embedding process...", chunks.Count);

        // Generate embeddings in batch for better performance
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);
        _logger.LogInformation("Processed {ProcessedChunks} chunks", chunks.Count);

        // Prepare data for ChromaDB
        var metadatas = chunks.Select((chunk, i) => new Dictionary<string, object>
        {
            { "document_id", document.Id },
            { "file_name", document.FileName },
            { "chunk_index", i },
            { "uploaded_at", document.UploadedAt.ToString("O") },
            { "file_type", document.FileType ?? "unknown" },
            { "chunk_size", chunk.Length }
        }).ToList();

        // Batch ChromaDB inserts for better performance
        await _chromaDbService.AddDocumentsAsync(
            CollectionName,
            ids,
            chunks,
            metadatas,
            embeddings
        );
    }

    // Optimize chunking for PDFs by reducing unnecessary splits.
    // Internal rather than private so the integration test project (which compiles into the
    // API assembly) can cover the chunking heuristics directly without a live ChromaDB.
    internal List<string> ChunkTextByCharacters(string text, int maxChunkChars, int overlap)
    {
        var chunks = new List<string>();
        int start = 0;

        while (start < text.Length)
        {
            int end = Math.Min(start + maxChunkChars, text.Length);

            // Try to end at a sentence boundary
            if (end < text.Length)
            {
                int lastPeriod = text.LastIndexOf('.', end, end - start);
                int lastNewline = text.LastIndexOf('\n', end, end - start);
                int boundary = Math.Max(lastPeriod, lastNewline);
                if (boundary > start + maxChunkChars / 2) // Only use boundary if it's not too far back
                    end = boundary + 1;
            }

            chunks.Add(text.Substring(start, end - start).Trim());

            // Move start forward with overlap
            start = end - overlap;
            if (start >= text.Length) break;
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
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

    public async Task<DocumentFileInventoryResponse> GetIndexedFilesAsync()
    {
        try
        {
            var fileNames = await _chromaDbService.GetIndexedFileNamesAsync(CollectionName);
            return new DocumentFileInventoryResponse
            {
                FileCount = fileNames.Count,
                FileNames = fileNames
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing indexed files from ChromaDB");
            throw;
        }
    }

    public async Task<string> RetrieveContextAsync(string query, int topK = 5)
    {
        try
        {
            var relevantChunks = await RetrieveRelevantChunksAsync(query, topK);

            if (relevantChunks.Count == 0)
            {
                _logger.LogInformation("No relevant documents found for query: {Query}", query);
                return string.Empty;
            }

            return string.Join("\n\n---\n\n", relevantChunks
                .Select(c => $"[From {c.FileName}]\n{c.ChunkText}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context from RAG service");
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

    private List<string> ChunkTextByParagraphs(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();

        // Max chars per chunk to stay under embedding model limit (nomic-embed-text ~8K tokens ≈ 3200 chars safe)
        const int MaxChunkChars = 3000;

        // If text is short enough, return as-is
        if (text.Length <= MaxChunkChars)
        {
            chunks.Add(text.Trim());
            return chunks;
        }

        // Try to split by paragraphs first (before normalizing whitespace)
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        // If no paragraphs or paragraphs are too large, fall back to character-based chunking
        if (paragraphs.Length <= 1 || paragraphs.Any(p => p.Length > MaxChunkChars))
        {
            return ChunkTextByCharacters(text, MaxChunkChars, overlap);
        }

        // Normalize whitespace within each paragraph (avoid regex on entire document)
        paragraphs = paragraphs.Select(p => Regex.Replace(p, @"\s+", " ").Trim()).ToArray();

        var currentChunk = "";
        var currentSize = 0;

        foreach (var para in paragraphs)
        {
            if (currentSize + para.Length > MaxChunkChars && !string.IsNullOrEmpty(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
                var words = currentChunk.Split(' ');
                var overlapWords = words.Length > overlap / 6 ? words.TakeLast(overlap / 6) : words;
                currentChunk = string.Join(" ", overlapWords) + " ";
                currentSize = currentChunk.Length;
            }

            currentChunk += para + "\n\n";
            currentSize += para.Length + 2;
        }

        if (!string.IsNullOrEmpty(currentChunk.Trim()))
            chunks.Add(currentChunk.Trim());

        return chunks;
    }

}