using ChromaDB.Client;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Logging;

namespace AiAgentPlatform.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAgentFramework(this IServiceCollection services)
        {
            // Register Semantic Kernel with lazy initialization to avoid blocking startup
            services.AddSingleton<IChatClient>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var config = configuration.GetSection("LLM").Get<LlmConfig>() ?? new LlmConfig();
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                try
                {
                    return new OllamaChatClient(
                        new Uri(config.Endpoint ?? "http://localhost:11434"),
                        config.Model ?? "llama3.2",
                        httpClient
                    );
                }
                catch (Exception ex)
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ServiceCollectionExtensions).FullName ?? "ServiceCollectionExtensions");
                    logger.LogWarning(ex, "Failed to initialize OllamaChatClient. Make sure Ollama is running at {Endpoint}", config.Endpoint);
                    throw;
                }
            });             

            services.AddHttpClient();
            services.AddSingleton<ChromaClient>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                try
                {
                    var options = new ChromaConfigurationOptions().WithUri("http://localhost:8000");
                    return new ChromaClient(options, httpClient);
                }
                catch (Exception ex)
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ServiceCollectionExtensions).FullName ?? "ServiceCollectionExtensions");
                    logger.LogWarning(ex, "Failed to initialize ChromaClient. Make sure ChromaDB is running at http://localhost:8000");
                    throw;
                }
            });

            return services;
        }
    }
}