using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly RagService _ragService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(RagService ragService, ILogger<DocumentController> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// Upload and index a document for RAG
    /// </summary>
    [HttpPost("upload")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<DocumentUploadResponse>> UploadDocument([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided" });
            }

            _logger.LogInformation("Uploading document: {FileName}", file.FileName);

            // Read file content
            using var stream = new StreamReader(file.OpenReadStream());
            var content = await stream.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { message = "File is empty" });
            }

            // Create document model
            var document = new Document
            {
                Id = Guid.NewGuid().ToString(),
                FileName = file.FileName,
                Content = content,
                FileSizeBytes = file.Length,
                FileType = Path.GetExtension(file.FileName)
            };

            // Process and index document
            var response = await _ragService.AddDocumentAsync(document);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, new { message = "Error uploading document", error = ex.Message });
        }
    }

    /// <summary>
    /// Search for relevant document chunks
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<List<ChunkResult>>> SearchDocuments([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { message = "Query cannot be empty" });
            }

            _logger.LogInformation("Searching documents with query: {Query}", request.Query);

            var topK = request.TopK > 0 ? request.TopK : 5;
            var results = await _ragService.RetrieveRelevantChunksAsync(request.Query, topK);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return StatusCode(500, new { message = "Error searching documents", error = ex.Message });
        }
    }

    /// <summary>
    /// Get augmented prompt for a query with context
    /// </summary>
    [HttpPost("augment-prompt")]
    public async Task<ActionResult<AugmentedPromptResponse>> GetAugmentedPrompt([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { message = "Query cannot be empty" });
            }

            var topK = request.TopK > 0 ? request.TopK : 5;
            var augmentedPrompt = await _ragService.GetAugmentedPromptAsync(request.Query, topK);

            return Ok(new AugmentedPromptResponse
            {
                OriginalQuery = request.Query,
                AugmentedPrompt = augmentedPrompt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error augmenting prompt");
            return StatusCode(500, new { message = "Error augmenting prompt", error = ex.Message });
        }
    }
}

public class SearchRequest
{
    public required string Query { get; set; }
    public int TopK { get; set; } = 5;
}

public class AugmentedPromptResponse
{
    public required string OriginalQuery { get; set; }
    public required string AugmentedPrompt { get; set; }
}
