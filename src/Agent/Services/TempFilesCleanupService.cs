using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace Agent.Services;

/// <summary>
/// Background service that automatically cleans up expired temporary files every minute
/// </summary>
public class TempFilesCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TempFilesCleanupService> _logger;
    private static readonly TimeSpan CLEANUP_INTERVAL = TimeSpan.FromMinutes(1);

    public TempFilesCleanupService(IServiceProvider serviceProvider, ILogger<TempFilesCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TempFiles cleanup service started. Cleanup interval: {Interval}", CLEANUP_INTERVAL);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();
                
                var cleanedCount = await tempFilesService.CleanupExpiredFilesAsync();
                
                if (cleanedCount > 0)
                {
                    _logger.LogInformation("Cleanup completed: {Count} files removed", cleanedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temp files cleanup");
            }

            await Task.Delay(CLEANUP_INTERVAL, stoppingToken);
        }

        _logger.LogInformation("TempFiles cleanup service stopped");
    }
}
