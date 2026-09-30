using Microsoft.Extensions.Configuration;

namespace AiAgentPlatform.IntegrationTests;

/// <summary>
/// Minimal configuration for the service unit tests.
///
/// ChromaDbService's constructor reads ChromaDB:Url and rejects a null IConfiguration, so the
/// Moq stubs for it need a real (empty) root. Values are not resolved here — the ChromaDbService
/// instances are mocks, and the endpoints they would call are never hit.
/// </summary>
internal static class TestConfiguration
{
    public static readonly IConfiguration Instance = new ConfigurationBuilder().Build();
}
