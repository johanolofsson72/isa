using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Services;

/// <summary>
/// Background service that sends daily workflow performance summaries
/// Runs once per day and sends notifications with workflow statistics
/// </summary>
public class DailySummaryService : BackgroundService
{
    private readonly ILogger<DailySummaryService> _logger;
    private readonly WorkflowMetricsCollector _metricsCollector;
    private readonly WorkflowNotificationService _notificationService;
    private readonly TimeSpan _dailyInterval = TimeSpan.FromHours(24);
    private readonly TimeSpan _summaryTime = TimeSpan.FromHours(9); // 9 AM

    public DailySummaryService(
        ILogger<DailySummaryService> logger,
        WorkflowMetricsCollector metricsCollector,
        WorkflowNotificationService notificationService)
    {
        _logger = logger;
        _metricsCollector = metricsCollector;
        _notificationService = notificationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📊 Daily Summary Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextSummaryTime = CalculateNextSummaryTime();
                var delay = nextSummaryTime - DateTimeOffset.Now;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogDebug("📊 Next daily summary scheduled for {NextSummaryTime} (in {Delay})",
                        nextSummaryTime, delay);
                    
                    await Task.Delay(delay, stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await SendDailySummaryAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in Daily Summary Service: {ErrorMessage}", ex.Message);
                
                // Wait a bit before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("📊 Daily Summary Service stopped");
    }

    /// <summary>
    /// Sends the daily workflow performance summary
    /// </summary>
    private async Task SendDailySummaryAsync()
    {
        try
        {
            _logger.LogInformation("📊 Generating daily workflow summary...");

            var summary = _metricsCollector.GetPerformanceSummary();
            
            // Only send summary if there's meaningful data
            if (summary.TotalExecutions > 0)
            {
                await _notificationService.SendDailySummaryAsync(summary);
                
                _logger.LogInformation(
                    "📊 Daily summary sent - {TotalExecutions} executions, {SuccessRate:F1}% success rate, {TotalWorkflows} workflows",
                    summary.TotalExecutions, summary.OverallSuccessRate, summary.TotalWorkflows);
            }
            else
            {
                _logger.LogInformation("📊 No workflow executions in the last 24 hours - skipping daily summary");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send daily summary: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Calculates the next time to send a daily summary
    /// </summary>
    private DateTimeOffset CalculateNextSummaryTime()
    {
        var now = DateTimeOffset.Now;
        var today = now.Date;
        var todaySummaryTime = today.Add(_summaryTime);

        // If it's already past today's summary time, schedule for tomorrow
        if (now >= todaySummaryTime)
        {
            return todaySummaryTime.AddDays(1);
        }
        else
        {
            return todaySummaryTime;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("📊 Daily Summary Service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
