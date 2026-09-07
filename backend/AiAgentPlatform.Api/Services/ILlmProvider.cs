namespace AiAgentPlatform.Api.Services;

public interface ILlmProvider
{
    /// <summary>
    /// Generate a complete response for the given message.
    /// </summary>
    Task<string> GenerateResponseAsync(string message);

    /// <summary>
    /// Stream response tokens one-by-one for real-time UI updates.
    /// </summary>
    IAsyncEnumerable<string> StreamResponseAsync(string message);
}