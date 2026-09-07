using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;

namespace AiAgentPlatform.Api.Services
{
    public class AgentOrchestratorService
    {
        private readonly Kernel _kernel;
        private readonly RAGService _ragService;

        public AgentOrchestratorService(
            Kernel kernel,
            RAGService ragService)
        {
            _kernel = kernel;
            _ragService = ragService;
        }

        public async Task<string> ChatAsync(string userMessage)
        {
            // Create a prompt with RAG context
            var context = await _ragService.QueryAsync("knowledge_base", new[] { userMessage }, 3);

            var prompt = $"""
                You are a helpful AI assistant. Use the following context to answer the question.
                
                Context:
                {context}
                
                Question: {userMessage}
                
                Answer:
                """;

            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString();
        }
    }
}