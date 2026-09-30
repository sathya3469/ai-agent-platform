using AiAgentPlatform.Api.Models;
using Microsoft.Extensions.Options;

namespace AiAgentPlatform.Api.Services;

/// <summary>
/// Keeps the Ollama model resident so the first request after idle does not stall on
/// model loading. Ollama unloads models after <c>OLLAMA_KEEP_ALIVE</c> (default 5m);
/// without a warm-up that cold start can exceed the client's timeout and surface as a
/// 500, which is why a manual "ping" appeared to fix the problem.
/// </summary>
/// <remarks>
/// Warm-up is strictly best-effort. Nothing this service does may ever be allowed to
/// escape <see cref="ExecuteAsync"/>: when Ollama is unreachable the ping fails with a
/// <see cref="TaskCanceledException"/>, which derives from
/// <see cref="OperationCanceledException"/> and therefore slips through a naive
/// "not OperationCanceledException" filter. An exception escaping a hosted service is
/// fatal under the default <c>BackgroundServiceExceptionBehavior</c> and would take the
/// whole API down with it.
/// </remarks>
public class OllamaWarmupService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<LlmConfig> _llmOptions;
    private readonly ILogger<OllamaWarmupService> _logger;

    public OllamaWarmupService(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmConfig> llmOptions,
        ILogger<OllamaWarmupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _llmOptions = llmOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _llmOptions.Value;
        if (string.IsNullOrWhiteSpace(config.Endpoint) || string.IsNullOrWhiteSpace(config.Model))
        {
            _logger.LogInformation("Ollama warm-up skipped: endpoint or model not configured");
            return;
        }

        // Per-ping deadline: generous enough to absorb a cold model load, but bounded so a
        // hung/dead Ollama surfaces as a warning instead of blocking the loop for the full
        // HttpClient.Timeout (which is what previously crashed the host).
        var pingTimeout = config.Timeout > 0
            ? TimeSpan.FromMilliseconds(config.Timeout)
            : TimeSpan.FromMinutes(5);

        var client = _httpClientFactory.CreateClient("OllamaWarmup");
        // Redundant with the client configuration in Program.cs; kept here so this
        // invariant (client timeout > ping deadline) survives a move between hosts.
        client.Timeout = pingTimeout + TimeSpan.FromMinutes(1);

        // Let the API finish startup before touching Ollama, which may itself still be loading.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        await PingAsync(client, config, pingTimeout, isStartup: true, stoppingToken);

        // Re-warm roughly as often as Ollama's default keep-alive window.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(4));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PingAsync(client, config, pingTimeout, isStartup: false, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        catch (Exception ex)
        {
            // Belt and braces: never let an unexpected failure escape and stop the host.
            _logger.LogError(ex, "Ollama warm-up loop terminated; the API continues without model warm-up");
        }
    }

    private async Task PingAsync(
        HttpClient client,
        LlmConfig config,
        TimeSpan pingTimeout,
        bool isStartup,
        CancellationToken stoppingToken)
    {
        var phase = isStartup ? "at startup" : "periodic";
        var url = $"{config.Endpoint.TrimEnd('/')}/api/generate";
        // num_predict: 1 keeps the ping to a single token. A full-length reply made the
        // warm-up generation occupy the model for minutes, and Ollama serves a loaded
        // model serially — so a queued embedding request (during document upload) would
        // block behind it until the whole response was generated.
        var payload = """
            {"model":"__MODEL__","prompt":"ping","stream":false,"options":{"num_predict":1},"keep_alive":-1}
            """;
        using var content = new StringContent(
            payload.Replace("__MODEL__", config.Model),
            System.Text.Encoding.UTF8,
            "application/json");

        // Bound the ping independently of stoppingToken so that distinguishing "we are
        // shutting down" from "Ollama is not answering" stays possible below.
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        pingCts.CancelAfter(pingTimeout);

        try
        {
            using var response = await client.PostAsync(url, content, pingCts.Token);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Ollama warm-up {Phase} succeeded for model {Model}",
                    phase,
                    config.Model);
            }
            else
            {
                _logger.LogWarning(
                    "Ollama warm-up {Phase} returned {StatusCode}",
                    phase,
                    response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: rethrow so the loop unwinds without logging an error.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Our own ping deadline (or a stalled connection) fired. This is exactly the
            // condition that used to crash the host; it is informational here.
            _logger.LogWarning(
                "Ollama warm-up {Phase} timed out after {TimeoutSeconds:F0}s; " +
                "Ollama may be starting, still loading the model, or unreachable",
                phase,
                pingTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            // Warm-up is best-effort; failing here must not stop the API from serving requests.
            _logger.LogWarning(ex, "Ollama warm-up {Phase} failed; Ollama may not be running yet", phase);
        }
    }
}
