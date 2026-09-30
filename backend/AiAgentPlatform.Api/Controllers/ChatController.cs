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
    public async Task Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("Error: Message cannot be empty");
            return;
        }

        Response.ContentType = "text/plain; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Once the first token is flushed the status code is already committed (200), so a
        // later failure cannot be reported via HTTP status. Append a readable marker to the
        // body instead — the frontend already treats a short/empty answer as a failure.
        var hasWrittenTokens = false;
        try
        {
            await foreach (var token in _chatService.StreamChatAsync(request.Message, request.SessionId))
            {
                await Response.WriteAsync(token);
                await Response.Body.FlushAsync();
                hasWrittenTokens = true;
            }
        }
        catch (OperationCanceledException)
        {
            await WriteStreamErrorAsync(hasWrittenTokens, "The request was canceled while waiting for the model. The model may still be loading — please retry.");
        }
        catch (TimeoutException ex)
        {
            await WriteStreamErrorAsync(hasWrittenTokens, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming chat failed");
            if (!hasWrittenTokens)
            {
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            await WriteStreamErrorAsync(
                hasWrittenTokens,
                hasWrittenTokens
                    ? $"[stream interrupted: {ex.Message}]"
                    : $"The assistant could not be reached. {ex.Message}");
        }
    }

    private async Task WriteStreamErrorAsync(bool hasWrittenTokens, string message)
    {
        if (hasWrittenTokens)
        {
            return;
        }

        await Response.WriteAsync(message);
        await Response.Body.FlushAsync();
    }

    /// <summary>
    /// Send a chat message and receive full response.
    /// </summary>
    [HttpPost("full")]
    public async Task<ChatResponse> ChatFull([FromBody] ChatRequest request)
    {
        var message = await _chatService.ChatAsync(request.Message, request.SessionId);
        return new ChatResponse
        {
            Content = message,
            SessionId = request.SessionId
        };
    }
    /// <summary>
    /// Get conversation history from PostgreSQL.
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<Message>>> GetHistory([FromQuery] string? sessionId = null)
    {
        return Ok(await _chatService.GetConversationHistoryAsync(sessionId));
    }

    /// <summary>
    /// Clear conversation history.
    /// </summary>
    [HttpDelete("history")]
    public async Task<ActionResult> ClearHistory()
    {
        await _chatService.ClearHistoryAsync();
        return Ok(new { message = "History cleared" });
    }
}