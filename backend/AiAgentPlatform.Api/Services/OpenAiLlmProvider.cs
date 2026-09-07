using System.Text;
using System.Text.Json;
using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class OpenAiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiLlmProvider  > _logger;
    private readonly string _endpoint;
    private readonly string _model;

    // Retry configuration
    private const int MaxRetries = 3;
    private const int InitialDelayMs = 500; // 0.5 second (Ollama is local, so shorter waits)
    private const double ExponentialBackoffMultiplier = 2.0;
    private const int MaxDelayMs = 16000; // 16 seconds

    // Rate limiting configuration (local Ollama is less strict than API services)
    private const int MaxRequestsPerMinute = 30; // Conservative limit for local Ollama
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly object _rateLimitLock = new();

    public OpenAiLlmProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiLlmProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var config = configuration.GetSection("LLM").Get<LlmConfig>() ?? new LlmConfig();
        _endpoint = config.Endpoint;
        _model = config.Model;

        // Set timeout for local service
        _httpClient.Timeout = TimeSpan.FromSeconds(120);

        _logger.LogInformation("OllamaLlmProvider initialized. Endpoint: {Endpoint}, Model: {Model}", _endpoint, _model);
    }

    public async Task<string> GenerateResponseAsync(string message)
    {
        var response = new StringBuilder();
        await foreach (var token in StreamResponseAsync(message))
        {
            response.Append(token);
        }
        return response.ToString();
    }

    /// <summary>
    /// Enforces client-side rate limiting to avoid overwhelming Ollama.
    /// Uses a sliding window approach with MaxRequestsPerMinute limit.
    /// </summary>
    private async Task EnforceRateLimitAsync()
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddSeconds(-60);

            // Remove timestamps older than 1 minute
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < oneMinuteAgo)
            {
                _requestTimestamps.Dequeue();
            }

            // If we've hit the limit, calculate wait time
            if (_requestTimestamps.Count >= MaxRequestsPerMinute)
            {
                var oldestRequest = _requestTimestamps.Peek();
                var waitTime = oldestRequest.AddSeconds(60) - now;
                if (waitTime > TimeSpan.Zero)
                {
                    _logger.LogWarning("Rate limit approaching ({Count}/{Max}). Waiting {WaitTimeMs}ms before next request.",
                        _requestTimestamps.Count, MaxRequestsPerMinute, waitTime.TotalMilliseconds);
                    Task.Delay(waitTime).Wait();
                }
            }

            // Record this request
            _requestTimestamps.Enqueue(now);
        }
    }

    /// <summary>
    /// Sends an HTTP POST request to Ollama with exponential backoff retry logic.
    /// Retries on 429 (Too Many Requests) and other transient errors.
    /// </summary>
    private async Task<HttpResponseMessage> PostWithRetryAsync(string url, HttpContent content)
    {
        int delayMs = InitialDelayMs;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Sending request to Ollama (attempt {Attempt}/{MaxRetries}).", attempt, MaxRetries);

                var response = await _httpClient.PostAsync(url, content);

                // Check if we got a 429 (Too Many Requests)
                if ((int)response.StatusCode == 429)
                {
                    if (attempt < MaxRetries)
                    {
                        _logger.LogWarning("Rate limited (429). Waiting {DelayMs}ms before retry (attempt {Attempt}/{MaxRetries}).",
                            delayMs, attempt, MaxRetries);
                        await Task.Delay(delayMs);
                        delayMs = Math.Min((int)(delayMs * ExponentialBackoffMultiplier), MaxDelayMs);
                        continue;
                    }
                }

                // For other transient errors (5xx, timeouts), retry
                if (response.StatusCode >= System.Net.HttpStatusCode.InternalServerError && attempt < MaxRetries)
                {
                    _logger.LogWarning("Transient error {StatusCode}. Waiting {DelayMs}ms before retry (attempt {Attempt}/{MaxRetries}).",
                        response.StatusCode, delayMs, attempt, MaxRetries);
                    response.Dispose();
                    await Task.Delay(delayMs);
                    delayMs = Math.Min((int)(delayMs * ExponentialBackoffMultiplier), MaxDelayMs);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Request failed. Waiting {DelayMs}ms before retry (attempt {Attempt}/{MaxRetries}).",
                    delayMs, attempt, MaxRetries);
                await Task.Delay(delayMs);
                delayMs = Math.Min((int)(delayMs * ExponentialBackoffMultiplier), MaxDelayMs);
            }
            catch (TaskCanceledException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Request timeout. Waiting {DelayMs}ms before retry (attempt {Attempt}/{MaxRetries}).",
                    delayMs, attempt, MaxRetries);
                await Task.Delay(delayMs);
                delayMs = Math.Min((int)(delayMs * ExponentialBackoffMultiplier), MaxDelayMs);
            }
        }

        // If all retries failed, make one final attempt
        return await _httpClient.PostAsync(url, content);
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(string message)
    {
        string? errorMessage = null;
        var tokens = new List<string>();

        try
        {
            _logger.LogInformation("Generating response for message: {Message}", message[..Math.Min(50, message.Length)]);

            // Apply client-side rate limiting before making the request
            await EnforceRateLimitAsync();

            var request = new
            {
                model = _model,
                prompt = message,
                stream = true
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var url = $"{_endpoint}/api/generate";
            _logger.LogInformation("POST request to: {Url}", url);

            // Use retry logic with exponential backoff
            using var response = await PostWithRetryAsync(url, content);

            _logger.LogInformation("Ollama response status: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ollama error ({StatusCode}): {Content}", response.StatusCode, errorContent[..Math.Min(200, errorContent.Length)]);
                errorMessage = $"Error: Ollama returned {response.StatusCode}";
            }
            else
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("response", out var responseElement))
                        {
                            var token = responseElement.GetString() ?? "";
                            if (!string.IsNullOrEmpty(token))
                            {
                                tokens.Add(token);
                            }
                        }

                        // Check if generation is done
                        if (doc.RootElement.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean())
                        {
                            _logger.LogInformation("Ollama generation complete.");
                            break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse Ollama response line");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming from Ollama");
            errorMessage = $"Error: {ex.Message}";
        }

        // Yield all tokens
        foreach (var token in tokens)
        {
            yield return token;
        }

        // Yield error if any
        if (errorMessage != null)
        {
            yield return errorMessage;
        }
    }
}