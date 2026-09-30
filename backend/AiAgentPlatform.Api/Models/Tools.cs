namespace AiAgentPlatform.Api.Models;

public class Tool
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public List<ToolParameter> Parameters { get; set; } = new();
}

public class ToolParameter
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Description { get; set; }
    public bool Required { get; set; } = true;
}

public class ToolCall
{
    public required string Tool { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public object? Result { get; set; }
    public string? Error { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}