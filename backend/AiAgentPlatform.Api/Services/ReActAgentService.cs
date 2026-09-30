using System.Text;
using System.Text.Json;
using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Services;

public class ReActAgentService : IAgentOrchestrator
{
    private readonly ChatService _chatService;
    private readonly IToolExecutor _toolExecutor;
    private readonly ILogger<ReActAgentService> _logger;
    private readonly List<AgentThought> _thoughts = new();
    private readonly List<ToolCall> _toolCalls = new();

    public ReActAgentService(
        ChatService chatService,
        IToolExecutor toolExecutor,
        ILogger<ReActAgentService> logger)
    {
        _chatService = chatService;
        _toolExecutor = toolExecutor;
        _logger = logger;
    }

    public async Task<AgentResponse> ExecuteAsync(string query, int maxSteps = 10)
    {
        ClearState();
        var startTime = DateTime.UtcNow;
        var step = 1;

        try
        {
            // STEP 1: THINK - Analyze query and decide what tools to use
            var thinkPrompt = GenerateThinkPrompt(query);
            var thoughtContent = await GenerateThoughtAsync(thinkPrompt);
            _thoughts.Add(new AgentThought
            {
                Step = step,
                Type = "THINK",
                Content = thoughtContent
            });
            step++;

            // Parse if we should use tools
            if (thoughtContent.Contains("search") || thoughtContent.Contains("use"))
            {
                // STEP 2: ACT - Execute tools
                var tools = _toolExecutor.GetAvailableTools();
                var toolToUse = DetermineTool(thoughtContent, tools);

                if (toolToUse != null)
                {
                    var toolParams = ExtractParameters(thoughtContent, query);
                    var toolCall = await _toolExecutor.ExecuteToolAsync(toolToUse.Name, toolParams);
                    _toolCalls.Add(toolCall);

                    _thoughts.Add(new AgentThought
                    {
                        Step = step,
                        Type = "ACT",
                        Content = $"Using tool: {toolToUse.Name}"
                    });
                    step++;

                    // STEP 3: OBSERVE - Get and process tool results
                    _thoughts.Add(new AgentThought
                    {
                        Step = step,
                        Type = "OBSERVE",
                        Content = toolCall.Result?.ToString() ?? toolCall.Error ?? "No result"
                    });
                    step++;
                }
            }

            // STEP 4: Generate final answer
            var finalPrompt = GenerateFinalPrompt(query, _thoughts, _toolCalls);
            var finalAnswer = await GenerateThoughtAsync(finalPrompt);

            _thoughts.Add(new AgentThought
            {
                Step = step,
                Type = "FINAL_ANSWER",
                Content = finalAnswer
            });

            return new AgentResponse
            {
                Thoughts = _thoughts,
                ToolCalls = _toolCalls,
                FinalAnswer = finalAnswer,
                TotalSteps = step,
                ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing agent");
            return new AgentResponse
            {
                Thoughts = _thoughts,
                ToolCalls = _toolCalls,
                FinalAnswer = $"Error: {ex.Message}",
                TotalSteps = step,
                ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }
    }

    private string GenerateThinkPrompt(string query)
    {
        return $"""
            You are an intelligent agent. Analyze this query and decide what to do:
            
            Query: {query}
            
            Available tools:
            - rag_search: Search documents
            - summarize: Summarize text
            - calculator: Do math
            
            Think step by step. Do you need to use any tools? Which one?
            """;
    }

    private string GenerateFinalPrompt(string query, List<AgentThought> thoughts, List<ToolCall> toolCalls)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Original query: {query}");
        sb.AppendLine("\nThinking process:");
        foreach (var thought in thoughts)
        {
            sb.AppendLine($"{thought.Type}: {thought.Content}");
        }

        if (toolCalls.Count > 0)
        {
            sb.AppendLine("\nTool results:");
            foreach (var call in toolCalls)
            {
                sb.AppendLine($"{call.Tool}: {call.Result}");
            }
        }

        sb.AppendLine("\nBased on the above, provide a clear and concise final answer:");
        return sb.ToString();
    }

    private async Task<string> GenerateThoughtAsync(string prompt)
    {
        var response = new StringBuilder();
        await foreach (var token in _chatService.StreamChatAsync(prompt))
        {
            response.Append(token);
        }
        return response.ToString();
    }

    private Tool? DetermineTool(string thought, List<Tool> availableTools)
    {
        var lowerThought = thought.ToLower();

        if (lowerThought.Contains("search") || lowerThought.Contains("document"))
            return availableTools.FirstOrDefault(t => t.Name == "rag_search");
        if (lowerThought.Contains("summar"))
            return availableTools.FirstOrDefault(t => t.Name == "summarize");
        if (lowerThought.Contains("calcul") || lowerThought.Contains("math"))
            return availableTools.FirstOrDefault(t => t.Name == "calculator");

        return null;
    }

    private Dictionary<string, object> ExtractParameters(string thought, string query)
    {
        return new Dictionary<string, object>
        {
            ["query"] = query,
            ["text"] = query
        };
    }

    public async Task<List<AgentThought>> GetThoughtsAsync()
    {
        return await Task.FromResult(_thoughts);
    }

    public void ClearState()
    {
        _thoughts.Clear();
        _toolCalls.Clear();
    }
}