using AiAgentPlatform.Api.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Serilog;
using AiAgentPlatform.Api.Services;
using AiAgentPlatform.Api.Middleware;
using AiAgentPlatform.Api.Models;
using ChromaDB.Client;

var builder = WebApplication.CreateBuilder(args);
    
    Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Fix circular references
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ChromaDbService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<AgentOrchestratorService>(); // Fix missing service
builder.Services.Configure<LlmConfig>(builder.Configuration.GetSection("LLM"));
builder.Services.AddAgentFramework();
// Add CORS
var llmConfig = builder.Configuration.GetSection("LLM").Get<LlmConfig>();
var llmProvider = llmConfig?.Provider ?? "ollama";
Log.Information("Configuring LLM Provider: {Provider}", llmProvider);

if (llmProvider.Equals("openai", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<ILlmProvider, OpenAiLlmProvider>();
    Log.Information("Using OpenAI provider");
}
else
{
    builder.Services.AddHttpClient<ILlmProvider, OllamaLlmProvider>();
    Log.Information("Using Ollama provider (default)");
}
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "https://localhost:7005") // Allow Swagger/Frontend origin
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI Agent Platform API",
        Version = "v1",
        Description = "API for AI chat agent with streaming responses"
    });
    c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
});

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

// Skip HTTPS redirect in development to avoid CORS issues with mixed protocols
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthorization();

app.MapControllers();

// Configure URLs
//app.Urls.Add("http://0.0.0.0:5000");

app.Run();