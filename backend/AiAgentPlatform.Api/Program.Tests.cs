using AiAgentPlatform.Api.Extensions;

// Exposes the minimal-API entry point to the test project so that
// WebApplicationFactory<Program> can resolve it (the generated <Program>$ type is
// internal by default, which makes IClassFixture<WebApplicationFactory<Program>>
// emit CS0051 "Inconsistent accessibility").
public partial class Program
{
    protected Program() { }
}
