using System.Data;

namespace AiAgentPlatform.Api.Services;

public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Perform mathematical calculations";

    public List<ToolParameter> GetParameters()
    {
        return new List<ToolParameter>
        {
            new ToolParameter
            {
                Name = "expression",
                Type = "string",
                Description = "Mathematical expression (e.g., '2+2', '10*5')",
                Required = true
            }
        };
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("expression"))
            throw new ArgumentException("expression parameter required");

        var expression = parameters["expression"].ToString() ?? "";

        try
        {
            var dt = new DataTable();
            var result = dt.Compute(expression, null);
            return $"{expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"Error calculating '{expression}': {ex.Message}";
        }
    }
}