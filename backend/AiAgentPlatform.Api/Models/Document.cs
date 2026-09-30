namespace AiAgentPlatform.Api.Models;

public class Document
{
    public required string Id { get; set; }
    public required string FileName { get; set; }
    public required string Content { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long FileSizeBytes { get; set; }
    public string? FileType { get; set; }
    public List<string> Chunks { get; set; } = new();
}

public class ChunkResult
{
    public required string ChunkText { get; set; }
    public required string DocumentId { get; set; }
    public required string FileName { get; set; }
    public double Similarity { get; set; }
}

public class DocumentUploadResponse
{
    public required string DocumentId { get; set; }
    public required string FileName { get; set; }
    public int ChunksCreated { get; set; }
    public DateTime UploadedAt { get; set; }
    public int Progress { get; set; }
    public string Message { get; set; } = "Document uploaded and indexed successfully";
}

public class DocumentFileInventoryResponse
{
    public int FileCount { get; set; }
    public List<string> FileNames { get; set; } = new();
}
