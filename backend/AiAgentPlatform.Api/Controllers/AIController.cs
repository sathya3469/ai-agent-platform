using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly AgentOrchestratorService _agentService;
    private readonly RagService _ragService;

    public AIController(
        AgentOrchestratorService agentService,
        RagService ragService)
    {
        _agentService = agentService;
        _ragService = ragService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var response = await _agentService.ChatAsync(request.Message);
        return Ok(new { response });
    }
}