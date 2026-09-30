using AiAgentPlatform.Api.Models;

namespace AiAgentPlatform.Api.Services;

public interface IAgentOrchestrator
{
    Task<AgentResponse> ExecuteAsync(string query, int maxSteps = 10);
    Task<List<AgentThought>> GetThoughtsAsync();
    void ClearState();
}
