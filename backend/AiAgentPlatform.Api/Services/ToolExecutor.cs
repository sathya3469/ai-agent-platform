using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class ToolExecutor : IToolExecutor
{
    private readonly Dictionary<string, ITool> _tools;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(
        RagService ragService,
        ILogger<ToolExecutor> logger)
    {
        _logger = logger;
        _tools = new Dictionary<string, ITool>
        {
            ["rag_search"] = new RagSearchTool(ragService),
            ["summarize"] = new SummarizeTool(),
            ["calculator"] = new CalculatorTool()
        };
    }

    public List<Tool> GetAvailableTools()
    {
        return _tools.Values
            .Select(tool => new Tool
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = tool.GetParameters()
            })
            .ToList();
    }

    public Tool? GetToolDefinition(string toolName)
    {
        if (!_tools.ContainsKey(toolName))
            return null;

        var tool = _tools[toolName];
        return new Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = tool.GetParameters()
        };
    }

    public async Task<ToolCall> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        var toolCall = new ToolCall
        {
            Tool = toolName,
            Parameters = parameters
        };

        try
        {
            if (!_tools.ContainsKey(toolName))
            {
                toolCall.Error = $"Tool '{toolName}' not found";
                _logger.LogWarning("Tool not found: {Tool}", toolName);
                return toolCall;
            }

            var tool = _tools[toolName];
            _logger.LogInformation("Executing tool: {Tool}", toolName);

            var result = await tool.ExecuteAsync(parameters);
            toolCall.Result = result;
        }
        catch (Exception ex)
        {
            toolCall.Error = ex.Message;
            _logger.LogError(ex, "Error executing tool: {Tool}", toolName);
        }

        return toolCall;
    }
}