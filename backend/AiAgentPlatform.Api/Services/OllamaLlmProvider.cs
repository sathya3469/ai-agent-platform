using System.Text;
using System.Text.Json;
using System.Diagnostics;
using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmProvider> _logger;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly TimeSpan _streamReadTimeout;

    public OllamaLlmProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaLlmProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var config = configuration.GetSection("LLM").Get<LlmConfig>() ?? new LlmConfig();
        _endpoint = config.Endpoint;
        _model = config.Model;

        // For streaming with large context (RAG + PDFs), we need a much longer timeout.
        // The timeout from config is in milliseconds; default to 5 minutes for PDF/RAG workloads.
        var timeoutMs = config.Timeout > 0 ? config.Timeout : 300000; // 5 minutes default
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _streamReadTimeout = TimeSpan.FromMilliseconds(timeoutMs);
        _logger.LogInformation("OllamaLlmProvider configured with timeout: {TimeoutSeconds}s", timeoutMs / 1000);
    }

    public async Task<string> GenerateResponseAsync(string message)
    {
        var response = new StringBuilder();
        try
        {
            await foreach (var token in StreamResponseAsync(message))
            {
                response.Append(token);
            }
            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response from Ollama");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(string message)
    {
        _logger.LogInformation("Calling Ollama at {Endpoint} with model {Model}", _endpoint, _model);
        
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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        // Use SendAsync with ResponseHeadersRead so the call returns as soon as
        // headers arrive. PostAsync's default (ResponseContentRead) waits for the
        // full body, which blocks streaming and causes HttpClient.Timeout to fire
        // mid-generation.
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        // Give the model time to load on a cold start, but still fail rather than hang forever.
        using var requestCts = new CancellationTokenSource(_streamReadTimeout);
        using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, requestCts.Token);
        _logger.LogInformation("Ollama response headers received in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Ollama error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Ollama error: {response.StatusCode} - {errorContent}");
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        // Bound each individual read. A stalled Ollama process would otherwise hold the
        // request open indefinitely now that the absolute HttpClient.Timeout is disabled.
        using var readCts = new CancellationTokenSource(_streamReadTimeout);

        string? line;
        var tokenCount = 0;
        var startTime = DateTime.UtcNow;
        while (true)
        {
            try
            {
                line = await reader.ReadLineAsync(readCts.Token);
            }
            catch (OperationCanceledException)
            {
                // The read stall is a hard failure: we cannot trust that Ollama is still
                // making progress. Surface a targeted error rather than a generic 500.
                _logger.LogError("Ollama stream stalled: no data received within {TimeoutSeconds}s", _streamReadTimeout.TotalSeconds);
                throw new TimeoutException(
                    $"Ollama stopped responding mid-generation after {_streamReadTimeout.TotalSeconds:F0}s. " +
                    "The model may still be loading; retry shortly.");
            }

            if (line is null)
            {
                break;
            }

            _logger.LogDebug("Received line from Ollama: {Line}", line);
            tokenCount++;
            if (tokenCount % 10 == 0)
            {
                _logger.LogInformation("Streaming tokens: {TokenCount} processed in {ElapsedMs}ms", tokenCount, (DateTime.UtcNow - startTime).TotalMilliseconds);
            }
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string? token = null;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    token = responseElement.GetString() ?? "";
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Ollama response line");
            }

            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }

        }
    }
}