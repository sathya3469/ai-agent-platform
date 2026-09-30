namespace AiAgentPlatform.Api.Services;

public class SummarizeTool : ITool
{
    public string Name => "summarize";
    public string Description => "Summarize provided text";

    public List<ToolParameter> GetParameters()
    {
        return new List<ToolParameter>
        {
            new ToolParameter
            {
                Name = "text",
                Type = "string",
                Description = "Text to summarize",
                Required = true
            },
            new ToolParameter
            {
                Name = "maxLength",
                Type = "integer",
                Description = "Maximum summary length in characters (default: 200)",
                Required = false
            }
        };
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("text"))
            throw new ArgumentException("text parameter required");

        var text = parameters["text"].ToString() ?? "";
        var maxLength = parameters.ContainsKey("maxLength")
            ? Convert.ToInt32(parameters["maxLength"])
            : 200;

        // Simple summarization: take first sentences
        var sentences = text.Split(new[] { '.', '!', '?' }, System.StringSplitOptions.RemoveEmptyEntries);
        var summary = "";

        foreach (var sentence in sentences)
        {
            if ((summary + sentence + ".").Length > maxLength)
                break;
            summary += sentence + ". ";
        }

        return summary.Trim();
    }
}