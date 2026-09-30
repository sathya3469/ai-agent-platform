using AiAgentPlatform.Api.Data;
using AiAgentPlatform.Api.Data.Entities;
using AiAgentPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

using ConversationEntity = AiAgentPlatform.Api.Data.Entities.Conversation;
using MessageEntity = AiAgentPlatform.Api.Data.Entities.Message;

public class ChatService
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ChatService> _logger;
    private readonly RagService _ragService;
    private readonly ApplicationDbContext _db;
    private bool _useRag = true;

    public ChatService(ILlmProvider llmProvider, ILogger<ChatService> logger, RagService ragService, ApplicationDbContext db)
    {
        _llmProvider = llmProvider;
        _logger = logger;
        _ragService = ragService;
        _db = db;
    }

    /// <summary>
    /// Returns the conversation for the given session id, creating (and saving) one when it
    /// does not yet exist. Chat history is persisted in PostgreSQL so it survives restarts.
    /// </summary>
    private async Task<ConversationEntity> GetOrCreateConversationAsync(string? sessionId, CancellationToken cancellationToken = default)
    {
        Guid conversationId;

        if (!string.IsNullOrWhiteSpace(sessionId) && Guid.TryParse(sessionId, out var sessionGuid))
        {
            conversationId = sessionGuid;
        }
        else
        {
            conversationId = Guid.NewGuid();
        }

        // Attach the conversation as a tracked, new-or-existing entity without an extra SELECT +
        // LEFT JOIN. SaveChanges later issues a single INSERT (via the EF value-converter for the
        // client-generated Guid) which is idempotent for an already-present row.
        var conversation = new ConversationEntity { Id = conversationId };

        if (await _db.Conversations.FindAsync(new object?[] { conversationId }, cancellationToken) is { } existing)
        {
            return existing;
        }

        _db.Conversations.Add(conversation);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted the same conversation id. Detach the local
            // entry and re-read rather than surfacing a duplicate-key failure to the caller.
            _db.Entry(conversation).State = EntityState.Detached;
            return (await _db.Conversations.FindAsync(new object?[] { conversationId }, cancellationToken))!;
        }

        return conversation;
    }

    public async Task<string> ChatAsync(string message, string? sessionId = null)
    {
        _logger.LogInformation("Processing chat message: {Message}", message);

        var conversation = await GetOrCreateConversationAsync(sessionId);
        await PersistMessageAsync(conversation, "user", message);

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
            await PersistMessageAsync(conversation, "assistant", response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatAsync");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(string message, string? sessionId = null)
    {
        _logger.LogInformation("Streaming chat message: {Message}", message);

        var conversation = await GetOrCreateConversationAsync(sessionId);
        await PersistMessageAsync(conversation, "user", message);

        var fullResponse = "";

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

        // Note: no try/catch around yield return � yield inside a try that has a catch is illegal.
        await foreach (var token in _llmProvider.StreamResponseAsync(augmentedMessage))
        {
            fullResponse += token;
            yield return token;
        }

        await PersistMessageAsync(conversation, "assistant", fullResponse);
    }

    /// <summary>
    /// Appends a message to the conversation and commits it to PostgreSQL.
    /// </summary>
    private async Task PersistMessageAsync(ConversationEntity conversation, string role, string content)
    {
        // Explicitly add (rather than relying solely on the navigation collection) so the change
        // tracker treats the message as new (INSERT) � attaching a non-empty client-generated Guid
        // via the collection alone makes EF Core think the row already exists and issue an UPDATE
        // that matches 0 rows.
        var message = new MessageEntity
        {
            ConversationId = conversation.Id,
            Role = role,
            Content = content
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Persisted {Role} message to PostgreSQL for conversation {ConversationId}",
            role, conversation.Id);
    }

    /// <summary>
    /// Loads the most recent conversation's messages from PostgreSQL, ordered chronologically.
    /// </summary>
    public async Task<IEnumerable<Models.Message>> GetConversationHistoryAsync(string? sessionId = null)
    {
        var query = _db.Messages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(sessionId) && Guid.TryParse(sessionId, out var sessionGuid))
        {
            query = query.Where(m => m.ConversationId == sessionGuid);
            return await query
                .OrderBy(m => m.Timestamp)
                .Select(m => new Models.Message
                {
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                })
                .ToListAsync();
        }

        // Fall back to the latest conversation so the UI has something to show.
        var latestConversationId = await _db.Conversations
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        if (latestConversationId is null)
        {
            return Array.Empty<Models.Message>();
        }

        return await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == latestConversationId)
            .OrderBy(m => m.Timestamp)
            .Select(m => new Models.Message
            {
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp
            })
            .ToListAsync();
    }

    /// <summary>
    /// Clears every persisted conversation. Use sparingly � this is destructive across all sessions.
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        _db.Conversations.RemoveRange(_db.Conversations);
        await _db.SaveChangesAsync();
    }

    public void SetRagEnabled(bool enabled)
    {
        _useRag = enabled;
        _logger.LogInformation("RAG mode set to: {Enabled}", enabled);
    }

    public bool IsRagEnabled() => _useRag;
}
