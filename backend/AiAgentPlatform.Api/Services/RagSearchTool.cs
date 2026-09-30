namespace AiAgentPlatform.Api.Services;

public class RagSearchTool : ITool
{
    private readonly RagService _ragService;

    public string Name => "rag_search";
    public string Description => "Search through uploaded documents for relevant information";

    public RagSearchTool(RagService ragService)
    {
        _ragService = ragService;
    }

    public List<ToolParameter> GetParameters()
    {
        return new List<ToolParameter>
        {
            new ToolParameter
            {
                Name = "query",
                Type = "string",
                Description = "The search query or question",
                Required = true
            },
            new ToolParameter
            {
                Name = "topK",
                Type = "integer",
                Description = "Number of results to return (default: 3)",
                Required = false
            }
        };
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("query"))
            throw new ArgumentException("query parameter required");

        var query = parameters["query"].ToString() ?? "";
        var topK = parameters.ContainsKey("topK")
            ? Convert.ToInt32(parameters["topK"])
            : 3;

        var context = await _ragService.RetrieveContextAsync(query, topK);
        return string.IsNullOrWhiteSpace(context)
            ? "No relevant documents found."
            : context;
    }
}