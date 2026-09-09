using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;

namespace AiAgentPlatform.Api.Services
{
    public class AgentOrchestratorService
    {
        private readonly Kernel _kernel;
        private readonly RagService _ragService;

        public AgentOrchestratorService(
            Kernel kernel,
            RagService ragService)
        {
            _kernel = kernel;
            _ragService = ragService;
        }

        public async Task<string> ChatAsync(string userMessage)
        {
            // Get augmented prompt with RAG context
            var augmentedPrompt = await _ragService.GetAugmentedPromptAsync(userMessage, topK: 3);

            var result = await _kernel.InvokePromptAsync(augmentedPrompt);
            return result.ToString();
        }
    }
}