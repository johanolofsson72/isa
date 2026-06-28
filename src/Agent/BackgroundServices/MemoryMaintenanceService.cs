using Agent.Services;
using Agent.Services.Interfaces;

namespace Agent.BackgroundServices;

/// <summary>
/// Background service for memory decay and cleanup operations
/// </summary>
public class MemoryMaintenanceService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MemoryMaintenanceService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1); // Run every hour

    public MemoryMaintenanceService(
        IServiceProvider serviceProvider,
        ILogger<MemoryMaintenanceService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Memory Maintenance Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMaintenanceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during memory maintenance");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Memory Maintenance Service stopped");
    }

    private async Task PerformMaintenanceAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var memoryService = scope.ServiceProvider.GetRequiredService<IProductionMemoryService>();
        var conversationHistoryService = scope.ServiceProvider.GetRequiredService<IConversationHistoryService>();

        _logger.LogInformation("Starting memory maintenance cycle");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Existing memory cleanup
        await memoryService.ConsolidateAndCleanupAsync();

        // New conversation history cleanup
        await conversationHistoryService.CleanupExpiredHistoryAsync();

        stopwatch.Stop();

        _logger.LogInformation("Memory maintenance completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }
}