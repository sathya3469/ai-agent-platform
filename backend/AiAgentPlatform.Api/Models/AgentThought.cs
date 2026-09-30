namespace AiAgentPlatform.Api.Models;

public class AgentThought
{
    public int Step { get; set; }
    public required string Type { get; set; } // "THINK", "ACT", "OBSERVE", "FINAL_ANSWER"
    public required string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AgentResponse
{
    public List<AgentThought> Thoughts { get; set; } = new();
    public List<ToolCall> ToolCalls { get; set; } = new();
    public required string FinalAnswer { get; set; }
    public int TotalSteps { get; set; }
    public double ExecutionTimeMs { get; set; }
}