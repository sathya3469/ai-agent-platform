namespace AiAgentPlatform.Api.Services;
using AiAgentPlatform.Api.Models;   
public interface IToolExecutor
{
    List<Tool> GetAvailableTools();
    Task<ToolCall> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters);
    Tool? GetToolDefinition(string toolName);
}

public class Tool
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public List<ToolParameter> Parameters { get; set; } = new();
}