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

    public OllamaLlmProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaLlmProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var config = configuration.GetSection("LLM").Get<LlmConfig>() ?? new LlmConfig();
        _endpoint = config.Endpoint;
        _model = config.Model;
        
        // Set timeout to avoid rate limiting issues
        _httpClient.Timeout = TimeSpan.FromSeconds(300); // 5 minutes instead of 2
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
        using var response = await _httpClient.PostAsync(url, content);
        _logger.LogInformation("Ollama response received in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Ollama error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Ollama error: {response.StatusCode} - {errorContent}");
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        var tokenCount = 0;
        while ((line = await reader.ReadLineAsync()) != null)
        {
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

            tokenCount++;
            if (tokenCount % 10 == 0) // Log every 10 tokens
                _logger.LogInformation("Processed {TokenCount} tokens in {ElapsedMs}ms", 
                    tokenCount, stopwatch.ElapsedMilliseconds);
        }
    }
}