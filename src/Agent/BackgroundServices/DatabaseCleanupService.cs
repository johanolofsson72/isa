using Agent.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Agent.BackgroundServices;

/// <summary>
/// Background service for periodic cleanup of cache and audit logs
/// </summary>
public class DatabaseCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseCleanupService> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseCleanupService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseCleanupService> logger,
        IConfiguration configuration
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int cleanupIntervalHours;
        try
        {
            cleanupIntervalHours = _configuration.GetValue<int>("DatabaseCleanup:IntervalHours");
        }
        catch
        {
            _logger.LogWarning("Invalid or missing 'DatabaseCleanup:IntervalHours'. Defaulting to 1 hour.");
            cleanupIntervalHours = 1;
        }

        int auditRetentionDays;
        try
        {
            auditRetentionDays = _configuration.GetValue<int>("DatabaseCleanup:AuditRetentionDays");
        }
        catch
        {
            _logger.LogWarning("Invalid or missing 'DatabaseCleanup:AuditRetentionDays'. Defaulting to 30 days.");
            auditRetentionDays = 30;
        }

        var interval = TimeSpan.FromHours(cleanupIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting database cleanup...");

                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<IOpenAICacheService>();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                await cacheService.CleanupExpiredCacheAsync();
                await auditService.CleanupOldLogsAsync(auditRetentionDays);

                _logger.LogInformation("Database cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database cleanup");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DatabaseCleanupService is stopping...");
                break;
            }
        }
    }
}
