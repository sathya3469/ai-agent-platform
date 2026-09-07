using AiAgentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly AgentOrchestratorService _agentService;
    private readonly RAGService _ragService;

    public AIController(
        AgentOrchestratorService agentService,
        RAGService ragService)
    {
        _agentService = agentService;
        _ragService = ragService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var response = await _agentService.ChatAsync(request.Message);
        return Ok(new { response });
    }

    [HttpPost("rag/ingest")]
    public async Task<IActionResult> IngestDocuments([FromBody] List<string> documents)
    {
        // Call AddDocumentAsync once per document because the service expects a single string per call.
        for (int i = 0; i < documents.Count; i++)
        {
            // Use a per-document id (here using index; replace with a different scheme if needed)
            var documentId = $"knowledge_base-{i}";
            await _ragService.AddDocumentAsync(documentId, documents[i]);
        }

        return Ok(new { message = "Documents ingested successfully" });
    }

    [HttpPost("rag/query")]
    public async Task<IActionResult> QueryRAG([FromBody] QueryRequest request)
    {
        var response = await _ragService.QueryAsync(
            "knowledge_base",
            new[] { request.Query },
            request.TopK);
        return Ok(new { response });
    }
}

public record ChatRequest(string Message);
public record QueryRequest(string Query, int TopK = 3);