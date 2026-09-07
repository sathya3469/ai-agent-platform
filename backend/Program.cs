using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using ChromaDB.Client;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "ai-agent-platform-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api/agents", () => Results.Ok(new[]
{
    new { id = "researcher", name = "Researcher", status = "ready" },
    new { id = "builder", name = "Builder", status = "ready" }
}));

app.Run();
