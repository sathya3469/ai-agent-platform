using System.Text.Json;

namespace AiAgentPlatform.IntegrationTests;

/// <summary>
/// Shared serializer options. The API configures camelCase naming via AddJsonOptions in
/// Program.cs, so tests must read the response JSON the same way the frontend does.
/// </summary>
internal static class TestJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
