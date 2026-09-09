using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class ChatService
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ChatService> _logger;
    private readonly RagService _ragService;
    private readonly List<Message> _conversationHistory = new();
    private bool _useRag = true;

    public ChatService(ILlmProvider llmProvider, ILogger<ChatService> logger, RagService ragService)
    {
        _llmProvider = llmProvider;
        _logger = logger;
        _ragService = ragService;
    }

    public async Task<string> ChatAsync(string message)
    {
        _logger.LogInformation("Processing chat message: {Message}", message);
        _conversationHistory.Add(new Message { Role = "user", Content = message });

        try
        {
            // Augment message with RAG context if enabled
            var augmentedMessage = message;
            if (_useRag)
            {
                try
                {
                    augmentedMessage = await _ragService.GetAugmentedPromptAsync(message, topK: 5);
                    _logger.LogInformation("Message augmented with RAG context");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RAG augmentation failed, using original message");
                }
            }

            var response = await _llmProvider.GenerateResponseAsync(augmentedMessage);
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
            // Augment message with RAG context if enabled
            var augmentedMessage = message;
            if (_useRag)
            {
                try
                {
                    augmentedMessage = await _ragService.GetAugmentedPromptAsync(message, topK: 5);
                    _logger.LogInformation("Message augmented with RAG context");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RAG augmentation failed, using original message");
                }
            }

            await foreach (var token in _llmProvider.StreamResponseAsync(augmentedMessage))
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

    public void SetRagEnabled(bool enabled)
    {
        _useRag = enabled;
        _logger.LogInformation("RAG mode set to: {Enabled}", enabled);
    }

    public bool IsRagEnabled() => _useRag;
}