using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace AiAgentPlatform.IntegrationTests;
/// <summary>
/// Bootstraps the real API for in-process integration testing.
///
/// The test files compile directly into the API assembly (there is no separate test
/// project), so WebApplicationFactory cannot discover the WebApplicationFactoryContentRoot
/// attribute that the SDK normally emits. Without it the content root is computed as
/// "&lt;projectDir&gt;\AiAgentPlatform.Api\", which does not exist, and appsettings.json
/// never loads — so the app fails before the first assertion. Pointing the content root at
/// the output directory (where appsettings.json is copied at build time) fixes both.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development enables Swagger and skips the HTTPS redirect; keep that behavior, but
        // name the environment "Testing" so appsettings.Testing.json can override it.
        builder.UseEnvironment(Environments.Development);
    }
}
