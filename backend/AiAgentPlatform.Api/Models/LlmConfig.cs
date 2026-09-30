namespace AiAgentPlatform.Api.Models;

public class LlmConfig
{
    public string Provider { get; set; } = "ollama"; // "ollama" or "openai"
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "embeddinggemma";
    public string? ApiKey { get; set; }
    public int Timeout { get; set; } = 30000; // milliseconds
}