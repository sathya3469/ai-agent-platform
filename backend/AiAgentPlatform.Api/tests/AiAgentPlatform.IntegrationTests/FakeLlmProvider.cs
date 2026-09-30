using AiAgentPlatform.Api.Services;

namespace AiAgentPlatform.IntegrationTests;

/// <summary>
/// Deterministic <see cref="ILlmProvider"/> used by the controller/service tests so they
/// never depend on a running Ollama instance.
/// </summary>
internal sealed class FakeLlmProvider : ILlmProvider
{
    private readonly string[] _tokens;

    private FakeLlmProvider(string[] tokens)
    {
        _tokens = tokens;
    }

    public Task<string> GenerateResponseAsync(string message)
        => Task.FromResult(string.Concat(_tokens));

    public async IAsyncEnumerable<string> StreamResponseAsync(string message)
    {
        foreach (var token in _tokens)
        {
            await Task.Yield();
            yield return token;
        }
    }

    /// <summary>Provider that streams the message back one whitespace-separated token at a time.</summary>
    public static FakeLlmProvider Streaming(string message)
        => new(Tokenize(message));

    /// <summary>
    /// Splits on spaces while keeping the separators, so concatenating the tokens reproduces the
    /// input exactly. The streaming endpoint writes each token verbatim, so a fake that dropped
    /// the spaces would make the response body differ from the model's reply.
    /// </summary>
    private static string[] Tokenize(string message)
        => message.Split(' ')
            .Select((part, index) => index == 0 ? part : " " + part)
            .ToArray();

    /// <summary>Provider whose stream is a single token (no trailing whitespace).</summary>
    public static FakeLlmProvider SingleToken(string token)
        => new(new[] { token });
}
