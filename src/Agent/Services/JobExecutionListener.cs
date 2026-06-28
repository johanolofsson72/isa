using Agent.Models;
using Quartz;

namespace Agent.Services;

/// <summary>
/// Quartz.NET job listener for tracking job executions
/// </summary>
public class JobExecutionListener : IJobListener
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutionListener> _logger;
    private readonly Dictionary<string, JobExecutionRecord> _activeExecutions = new();

    public JobExecutionListener(
        IServiceProvider serviceProvider,
        ILogger<JobExecutionListener> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Name of this listener
    /// </summary>
    public string Name => "JobExecutionTrackingListener";

    /// <summary>
    /// Called by the Scheduler when a JobDetail is about to be executed
    /// </summary>
    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKey = context.JobDetail.Key.ToString();
            _logger.LogDebug("🎯 Job about to execute: {JobKey}", jobKey);

            // Create scope and get tracking service
            using var scope = _serviceProvider.CreateScope();
            var trackingService = scope.ServiceProvider.GetRequiredService<JobExecutionTrackingService>();

            // Record job start
            var executionRecord = await trackingService.RecordJobStartAsync(context);

            // Store the record for later use in completion
            lock (_activeExecutions)
            {
                _activeExecutions[jobKey] = executionRecord;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in JobToBeExecuted for {JobKey}",
                context.JobDetail.Key);
            // Don't rethrow - we don't want to prevent job execution
        }
    }

    /// <summary>
    /// Called by the Scheduler when a JobDetail was about to be executed, but a TriggerListener vetoed its execution
    /// </summary>
    public async Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKey = context.JobDetail.Key.ToString();
            _logger.LogWarning("⚠️ Job execution vetoed: {JobKey}", jobKey);

            // Create scope and get tracking service
            using var scope = _serviceProvider.CreateScope();
            var trackingService = scope.ServiceProvider.GetRequiredService<JobExecutionTrackingService>();

            // Record the veto
            await trackingService.RecordJobVetoedAsync(context);

            // Remove from active executions if it was there
            lock (_activeExecutions)
            {
                _activeExecutions.Remove(jobKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in JobExecutionVetoed for {JobKey}",
                context.JobDetail.Key);
        }
    }

    /// <summary>
    /// Called by the Scheduler after a JobDetail has been executed, and after the associated Trigger's triggered() method has been called
    /// </summary>
    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKey = context.JobDetail.Key.ToString();

            // Get the execution record we stored earlier
            JobExecutionRecord? executionRecord = null;
            lock (_activeExecutions)
            {
                _activeExecutions.TryGetValue(jobKey, out executionRecord);
                _activeExecutions.Remove(jobKey);
            }

            if (executionRecord == null)
            {
                _logger.LogWarning("⚠️ No execution record found for completed job: {JobKey}", jobKey);
                return;
            }

            if (jobException != null)
            {
                _logger.LogError(jobException, "❌ Job failed: {JobKey}", jobKey);
            }
            else
            {
                _logger.LogDebug("✅ Job completed successfully: {JobKey}", jobKey);
            }

            // Create scope and get tracking service
            using var scope = _serviceProvider.CreateScope();
            var trackingService = scope.ServiceProvider.GetRequiredService<JobExecutionTrackingService>();

            // Record job completion
            await trackingService.RecordJobCompletionAsync(executionRecord, context, jobException);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in JobWasExecuted for {JobKey}",
                context.JobDetail.Key);
        }
    }

    /// <summary>
    /// Get currently active executions (for monitoring)
    /// </summary>
    public Dictionary<string, JobExecutionRecord> GetActiveExecutions()
    {
        lock (_activeExecutions)
        {
            return new Dictionary<string, JobExecutionRecord>(_activeExecutions);
        }
    }

    /// <summary>
    /// Get count of currently active executions
    /// </summary>
    public int GetActiveExecutionCount()
    {
        lock (_activeExecutions)
        {
            return _activeExecutions.Count;
        }
    }

    /// <summary>
    /// Check if a specific job is currently executing
    /// </summary>
    public bool IsJobExecuting(string jobKey)
    {
        lock (_activeExecutions)
        {
            return _activeExecutions.ContainsKey(jobKey);
        }
    }

    /// <summary>
    /// Get execution record for a currently running job
    /// </summary>
    public JobExecutionRecord? GetActiveExecution(string jobKey)
    {
        lock (_activeExecutions)
        {
            _activeExecutions.TryGetValue(jobKey, out var record);
            return record;
        }
    }

    /// <summary>
    /// Clear all active executions (for cleanup/reset)
    /// </summary>
    public void ClearActiveExecutions()
    {
        lock (_activeExecutions)
        {
            var count = _activeExecutions.Count;
            _activeExecutions.Clear();
            
            if (count > 0)
            {
                _logger.LogWarning("🧹 Cleared {Count} active execution records", count);
            }
        }
    }
}
