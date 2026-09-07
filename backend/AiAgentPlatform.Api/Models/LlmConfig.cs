namespace AiAgentPlatform.Api.Models;

public class LlmConfig
{
    public string Provider { get; set; } = "ollama"; // "ollama" or "openai"
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "neural-chat";
    public string? ApiKey { get; set; }
    public int Timeout { get; set; } = 30000; // milliseconds
}