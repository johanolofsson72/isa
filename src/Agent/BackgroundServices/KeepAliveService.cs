using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Services
{
    /// <summary>
    /// Background service that keeps the agent process alive and optionally logs heartbeat messages.
    /// </summary>
    public class KeepAliveService : BackgroundService
    {
        private readonly ILogger<KeepAliveService> _logger;

        public KeepAliveService(ILogger<KeepAliveService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("KeepAliveService started. Keeping agent running...");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Optionally log heartbeat or perform liveness checks here
                    _logger.LogDebug("KeepAliveService heartbeat at {Time}", DateTime.UtcNow);
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown, no need to log as error
                _logger.LogInformation("KeepAliveService received shutdown signal.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in KeepAliveService.");
            }

            _logger.LogInformation("KeepAliveService is stopping.");
        }
    }
}