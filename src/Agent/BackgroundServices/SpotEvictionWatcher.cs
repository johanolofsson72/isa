using System.Text.Json;

namespace Agent.BackgroundServices;

/// <summary>
/// Polls Azure IMDS scheduled-events for Preempt/Terminate/Reboot signals and triggers
/// IHostApplicationLifetime.StopApplication() so the drain starts ~20s earlier than
/// waiting for SIGTERM. No-op outside Azure (IMDS calls fail silently and are caught).
/// </summary>
public sealed class SpotEvictionWatcher(
    IHostApplicationLifetime lifetime,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<SpotEvictionWatcher> logger) : BackgroundService
{
    private const string ImdsUrl = "http://169.254.169.254/metadata/scheduledevents?api-version=2020-07-01";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = configuration.GetValue("Spot:ImdsPollIntervalSeconds", 10);
        var http = httpClientFactory.CreateClient("imds");
        http.DefaultRequestHeaders.Remove("Metadata");
        http.DefaultRequestHeaders.Add("Metadata", "true");
        http.Timeout = TimeSpan.FromSeconds(2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var response = await http.GetAsync(ImdsUrl, stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(stoppingToken);
                    if (doc.TryGetProperty("Events", out var events) && events.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ev in events.EnumerateArray())
                        {
                            if (!ev.TryGetProperty("EventType", out var typeProp))
                            {
                                continue;
                            }
                            var type = typeProp.GetString();
                            if (type is "Preempt" or "Terminate" or "Reboot")
                            {
                                logger.LogWarning("Spot eviction notice received: {Type}. Triggering shutdown.", type);
                                lifetime.StopApplication();
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "IMDS poll failed (expected outside Azure)");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
