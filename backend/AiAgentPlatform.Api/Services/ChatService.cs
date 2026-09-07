using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class ChatService
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ChatService> _logger;
    private readonly List<Message> _conversationHistory = new();

    public ChatService(ILlmProvider llmProvider, ILogger<ChatService> logger)
    {
        _llmProvider = llmProvider;
        _logger = logger;
    }

    public async Task<string> ChatAsync(string message)
    {
        _logger.LogInformation("Processing chat message: {Message}", message);
        _conversationHistory.Add(new Message { Role = "user", Content = message });

        try
        {
            var response = await _llmProvider.GenerateResponseAsync(message);
            _conversationHistory.Add(new Message { Role = "assistant", Content = response });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatAsync");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(string message)
    {
        _logger.LogInformation("Streaming chat message: {Message}", message);
        _conversationHistory.Add(new Message { Role = "user", Content = message });

        var fullResponse = "";
        var tokens = new List<string>();
        try
        {
            await foreach (var token in _llmProvider.StreamResponseAsync(message))
            {
                tokens.Add(token);
                fullResponse += token;
            }
            _conversationHistory.Add(new Message { Role = "assistant", Content = fullResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in StreamChatAsync");
            throw;
        }

        foreach (var t in tokens)
        {
            yield return t;
        }
    }

    public IEnumerable<Message> GetConversationHistory()
    {
        return _conversationHistory.AsReadOnly();
    }

    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }
}