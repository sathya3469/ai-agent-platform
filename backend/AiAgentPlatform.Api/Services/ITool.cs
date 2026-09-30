namespace AiAgentPlatform.Api.Services;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    List<ToolParameter> GetParameters();
    Task<string> ExecuteAsync(Dictionary<string, object> parameters);
}

public class ToolParameter
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Description { get; set; }
    public bool Required { get; set; } = true;
}