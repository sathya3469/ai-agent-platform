namespace AiAgentPlatform.Api.Models;

public class ChatResponse
{
    public required string Content { get; set; }
    public string? SessionId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}