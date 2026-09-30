using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

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

            var allowedExtensions = new HashSet<string> { ".txt", ".md", ".csv", ".html", ".xml", ".json", ".log", ".pdf", ".doc", ".docx" };
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"Unsupported file type '{extension}'. Only text-based files are supported (TXT, MD, CSV, HTML, XML, JSON, LOG, PDF, DOC, DOCX)." });
            }

            _logger.LogInformation("Uploading document: {FileName}", file.FileName);

            string content;

            if (extension == ".pdf")
            {
                // Optimize PDF extraction by using a more efficient approach
                using var pdfStream = file.OpenReadStream();
                using var pdf = PdfDocument.Open(pdfStream);
                
                // Extract text with minimal overhead
                var textBuilder = new System.Text.StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    var pageText = ContentOrderTextExtractor.GetText(page);
                    textBuilder.Append(pageText).Append("\n\n");
                }
                content = textBuilder.ToString().Trim();
            }
            else
            {
                // For non-PDF files, use a more efficient stream reader
                using var stream = new StreamReader(file.OpenReadStream(), encoding: System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 65536);
                content = await stream.ReadToEndAsync();
            }

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
                FileType = extension
            };

            // Process and index document
            var response = await _ragService.AddDocumentAsync(document);

            return Ok(response);
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "ChromaDB or embedding service is unavailable");
            return StatusCode(503, new { message = "Document service is unavailable. Please ensure ChromaDB is running on port 8000." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, new { message = $"Error uploading document: {ex.Message}" });
        }
    }

    /// <summary>
    /// Return the number of indexed files and the list of file names currently stored in ChromaDB.
    /// </summary>
    [HttpGet("files")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<DocumentFileInventoryResponse>> GetIndexedFiles()
    {
        try
        {
            var response = await _ragService.GetIndexedFilesAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from ChromaDB");
            return StatusCode(500, new { message = "Error listing indexed files", error = ex.Message });
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

    [HttpGet("health/chroma")]
    public async Task<ActionResult> ChromaHealth()
    {
        try
        {
            var result = await _ragService.GetIndexedFilesAsync();
            return Ok(new { chromaReachable = true, fileCount = result.FileCount, fileNames = result.FileNames });
        }
        catch
        {
            return StatusCode(503, new { chromaReachable = false });
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
