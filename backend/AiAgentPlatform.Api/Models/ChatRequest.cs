
namespace AiAgentPlatform.Api.Models;

public class ChatRequest
{
    public required string Message { get; set; }
    public string? SessionId { get; set; }
}