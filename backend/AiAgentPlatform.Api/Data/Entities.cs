namespace AiAgentPlatform.Api.Data.Entities;

/// <summary>
/// A chat conversation. Owns many <see cref="Message"/> entities.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Message> Messages { get; set; } = new();
}

/// <summary>
/// A persisted chat message belonging to a <see cref="Conversation"/>.
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public string Role { get; set; } = ""; // "user" or "assistant"
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metadata for an indexed document. Chunk text lives in ChromaDB; this table
/// tracks provenance and counts only.
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Filename { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int ChunkCount { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentChunk> Chunks { get; set; } = new();
}

/// <summary>
/// A chunk of a <see cref="Document"/>. <see cref="EmbeddingId"/> is the ChromaDB id.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = "";
    public string EmbeddingId { get; set; } = "";
}
