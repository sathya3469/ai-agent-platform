using AiAgentPlatform.Api.Data;
using AiAgentPlatform.Api.Extensions;
using AiAgentPlatform.Api.Middleware;
using AiAgentPlatform.Api.Models;
using AiAgentPlatform.Api.Services;
using ChromaDB.Client;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Serilog;

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

// Document uploads can be large and slow to embed. Kestrel/MVC caps multipart
// requests at ~28 MB by default, which rejects legitimate documents outright.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    // 200 MB per uploaded file.
    options.MultipartBodyLengthLimit = 200 * 1024 * 1024;
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
});
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    options.MaxModelBindingCollectionSize = int.MaxValue;
});
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ChromaDbService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<AgentOrchestratorService>();
builder.Services.AddScoped<IToolExecutor, ToolExecutor>();
builder.Services.AddScoped<IAgentOrchestrator, ReActAgentService>();
builder.Services.Configure<LlmConfig>(builder.Configuration.GetSection("LLM"));

builder.Services.AddHttpClient("OllamaEmbeddings", client =>
{
    var ollamaBase = builder.Configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
    var timeoutSeconds = builder.Configuration.GetValue<int?>("Ollama:EmbeddingTimeoutSeconds") ?? 300;
    client.BaseAddress = new Uri(ollamaBase);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
// Legacy nameless client kept for other consumers that resolve via IHttpClientFactory.
builder.Services.AddHttpClient();
builder.Services.AddAgentFramework();

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString = string.IsNullOrWhiteSpace(databaseUrl)
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : ParseDatabaseUrl(databaseUrl);

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No PostgreSQL connection string configured. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()
    ));

static string ParseDatabaseUrl(string url)
{
    // Convert the URI form (postgres://user:pass@host:port/database) into an Npgsql connection string.
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return url; // Assume it is already a connection string.
    }

    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');
    var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
    var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "postgres";

    return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
}
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


builder.Services.AddHttpClient("OllamaWarmup", client =>
{
    // Must exceed the warm-up ping deadline (see OllamaWarmupService) so the
    // linked cancellation token is what fires, never the client's own timeout.
    var timeoutSeconds = builder.Configuration.GetValue<int?>("LLM:Timeout") ?? 300;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds) + TimeSpan.FromMinutes(1);
});

// Keeps the Ollama model resident so idle requests do not stall on model loading.
builder.Services.AddHostedService<OllamaWarmupService>();

// Background services are best-effort (warm-up, etc.). The default behavior stops the
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
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
                "https://localhost:7005"
                ) // Allow Swagger/Frontend origin
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        dbContext.Database.Migrate();
        app.Logger.LogInformation("Database migration completed");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed");
        throw;
    }
}
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (contextFeature != null)
        {
            app.Logger.LogError(contextFeature.Error, "Unhandled exception");

            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = context.Response.StatusCode,
                message = "An error occurred processing your request"
            });
        }
    });
});
// Configure URLs
//app.Urls.Add("http://0.0.0.0:5000");

app.Run();