using Agent.Services.Resilience;

namespace Agent.BackgroundServices;

/// <summary>
/// On shutdown, flips the readiness gate to unready (so /health/ready returns 503 and
/// the load balancer stops sending new traffic), then waits a configurable delay to let
/// the LB notice and drain. Order matters — fail readiness FIRST, then let in-flight
/// requests finish, then SqliteCheckpointOnStop runs.
/// </summary>
public sealed class GracefulDrain(
    IReadinessGate readiness,
    IConfiguration configuration,
    ILogger<GracefulDrain> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        readiness.MarkUnready();
        var delay = configuration.GetValue("Spot:DrainDelaySeconds", 3);
        logger.LogInformation("Readiness flipped to unready; waiting {Delay}s for load balancer drain", delay);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown timeout reached; stop waiting and let downstream IHostedServices run.
        }
    }
}
