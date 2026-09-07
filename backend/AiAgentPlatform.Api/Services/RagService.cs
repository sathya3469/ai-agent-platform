using ChromaDB.Client;


namespace AiAgentPlatform.Api.Services
{
    public class RAGService
    {
        private readonly ChromaClient _chromaDb;

        public RAGService(ChromaClient chromaDb)
        {
            _chromaDb = chromaDb;
        }

       
        public async Task<string> QueryAsync(string collectionName, string[] queryTexts, int nResults = 3)
        {
            // Search ChromaDB for relevant documents
            var results = await _chromaDb.QueryAsync(
                collectionName: collectionName,
                queryTexts: queryTexts,
                nResults: nResults
            );

            return string.Join("\n", results.Documents?.FirstOrDefault() ?? Array.Empty<string>());
        }

        public async Task AddDocumentAsync(string documentId, string content, string? metadata = null)
        {
            // Add a document to the knowledge base in ChromaDB
            await _chromaDb.AddAsync(
                collectionName: "knowledge_base",
                ids: new[] { documentId },
                documents: new[] { content },
                metadatas: metadata != null
                    ? new List<Dictionary<string, string>> { new Dictionary<string, string> { { "metadata", metadata } } }
                    : null
            );
        }
    }
}

namespace ChromaDB.Client
{
    public class QueryResult
    {
        // RAGService expects Documents to be a list of string arrays
        public List<string[]> Documents { get; set; } = new();
    }

    public static class ChromaClientExtensions
    {
        public static Task<QueryResult> QueryAsync(this ChromaClient client,
                                                   string collectionName,
                                                   string[] queryTexts,
                                                   int nResults = 3,
                                                   string? tenant = null,
                                                   string? database = null)
        {
            // TODO: implement by calling the actual Chroma backend / HTTP endpoint or SDK method.
            throw new NotImplementedException("Implement Chroma query logic here or call the proper SDK method.");
        }

        public static Task AddAsync(this ChromaClient client,
                                    string collectionName,
                                    string[] ids,
                                    string[] documents,
                                    List<Dictionary<string, string>>? metadatas = null)
        {
            // TODO: implement by calling the actual Chroma backend / HTTP endpoint or SDK method.
            throw new NotImplementedException("Implement Chroma add logic here or call the proper SDK method.");
        }
    }
}