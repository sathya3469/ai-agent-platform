using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IAgentOrchestrator _agent;
    private readonly ILogger<AgentController> _logger;

    public AgentController(IAgentOrchestrator agent, ILogger<AgentController> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    [HttpPost("run")]
    public async Task<ActionResult<AgentResponse>> RunAgent([FromBody] AgentRequest request)
    {
        try
        {
            var response = await _agent.ExecuteAsync(request.Query, request.MaxSteps ?? 10);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running agent");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("thoughts")]
    public async Task<ActionResult<List<AgentThought>>> GetThoughts()
    {
        var thoughts = await _agent.GetThoughtsAsync();
        return Ok(thoughts);
    }

    [HttpPost("clear")]
    public ActionResult ClearState()
    {
        _agent.ClearState();
        return Ok(new { message = "Agent state cleared" });
    }
}

public class AgentRequest
{
    public required string Query { get; set; }
    public int? MaxSteps { get; set; } = 10;
}