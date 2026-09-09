using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }
   
    /// <summary>
    /// Send a chat message and receive a streaming response.
    /// </summary>
    [HttpPost("stream")]
    public async IAsyncEnumerable<string> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            yield return "Error: Message cannot be empty";
            yield break;
        }

        await foreach (var token in _chatService.StreamChatAsync(request.Message))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Send a chat message and receive full response.
    /// </summary>
    [HttpPost("full")]
    public async Task<ChatResponse> ChatFull([FromBody] ChatRequest request)
    {
        var message = await _chatService.ChatAsync(request.Message);
        return new ChatResponse
        {
            Content = message,
            SessionId = request.SessionId
        };
    }
    /// <summary>
    /// Get conversation history.
    /// </summary>
    [HttpGet("history")]
    public ActionResult<IEnumerable<Message>> GetHistory()
    {
        return Ok(_chatService.GetConversationHistory());
    }

    /// <summary>
    /// Clear conversation history.
    /// </summary>
    [HttpDelete("history")]
    public ActionResult ClearHistory()
    {
        _chatService.ClearHistory();
        return Ok(new { message = "History cleared" });
    }
}

public record ChatRequest : IEquatable<ChatRequest>
{
  public string Message { get; init; }
  public string? SessionId { get; init; }
}