using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;

namespace Agent.Services;

/// <summary>
/// Quartz.NET job listener for monitoring workflow execution
/// Provides detailed logging, metrics collection, and execution tracking
/// </summary>
public class WorkflowJobListener : IJobListener
{
    private readonly ILogger<WorkflowJobListener> _logger;
    private readonly WorkflowMetricsCollector _metricsCollector;
    private readonly WorkflowNotificationService _notificationService;

    public WorkflowJobListener(
        ILogger<WorkflowJobListener> logger,
        WorkflowMetricsCollector metricsCollector,
        WorkflowNotificationService notificationService)
    {
        _logger = logger;
        _metricsCollector = metricsCollector;
        _notificationService = notificationService;
    }

    public string Name => "WorkflowJobListener";

    /// <summary>
    /// Called when a job is about to be executed
    /// </summary>
    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowId = context.JobDetail.JobDataMap.GetString("WorkflowId");
        var workflowName = context.JobDetail.JobDataMap.GetString("WorkflowName");
        var fireInstanceId = context.FireInstanceId;

        _logger.LogInformation(
            "🚀 WORKFLOW EXECUTION START - ID: {WorkflowId}, Name: {WorkflowName}, FireInstance: {FireInstanceId}, Trigger: {TriggerKey}",
            workflowId, workflowName, fireInstanceId, context.Trigger.Key);

        // Record execution start metrics
        await _metricsCollector.RecordExecutionStartAsync(workflowId ?? "unknown", workflowName ?? "unknown", context.FireTimeUtc);

        // Store execution start time in job data for duration calculation
        context.JobDetail.JobDataMap.Put("ExecutionStartTime", Stopwatch.GetTimestamp());
        context.JobDetail.JobDataMap.Put("FireInstanceId", fireInstanceId);
    }

    /// <summary>
    /// Called when a job execution was vetoed (prevented from executing)
    /// </summary>
    public async Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowId = context.JobDetail.JobDataMap.GetString("WorkflowId");
        var workflowName = context.JobDetail.JobDataMap.GetString("WorkflowName");

        _logger.LogWarning(
            "⚠️ WORKFLOW EXECUTION VETOED - ID: {WorkflowId}, Name: {WorkflowName}, Trigger: {TriggerKey}",
            workflowId, workflowName, context.Trigger.Key);

        // Record vetoed execution
        await _metricsCollector.RecordExecutionVetoedAsync(workflowId ?? "unknown", workflowName ?? "unknown", context.FireTimeUtc);

        // Send notification for vetoed execution
        await _notificationService.NotifyExecutionVetoedAsync(workflowId ?? "unknown", workflowName ?? "unknown", context.Trigger.Key.ToString());
    }

    /// <summary>
    /// Called when a job has completed execution
    /// </summary>
    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
    {
        var workflowId = context.JobDetail.JobDataMap.GetString("WorkflowId");
        var workflowName = context.JobDetail.JobDataMap.GetString("WorkflowName");
        var fireInstanceId = context.JobDetail.JobDataMap.GetString("FireInstanceId");
        
        // Calculate execution duration
        var startTimestamp = context.JobDetail.JobDataMap.GetLong("ExecutionStartTime");
        var duration = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - startTimestamp);

        var success = jobException == null;
        var status = success ? "SUCCESS" : "FAILED";
        var statusEmoji = success ? "✅" : "❌";

        if (success)
        {
            _logger.LogInformation(
                "{StatusEmoji} WORKFLOW EXECUTION SUCCESS - ID: {WorkflowId}, Name: {WorkflowName}, Duration: {Duration:F2}s, FireInstance: {FireInstanceId}",
                statusEmoji, workflowId, workflowName, duration.TotalSeconds, fireInstanceId);

            // Record successful execution metrics
            await _metricsCollector.RecordExecutionSuccessAsync(workflowId ?? "unknown", workflowName ?? "unknown", duration, context.FireTimeUtc);
        }
        else
        {
            var errorMessage = jobException?.Message ?? "Unknown error";
            var innerException = jobException?.InnerException?.Message;

            _logger.LogError(jobException,
                "{StatusEmoji} WORKFLOW EXECUTION FAILED - ID: {WorkflowId}, Name: {WorkflowName}, Duration: {Duration:F2}s, Error: {ErrorMessage}, Inner: {InnerException}, FireInstance: {FireInstanceId}",
                statusEmoji, workflowId, workflowName, duration.TotalSeconds, errorMessage, innerException, fireInstanceId);

            // Record failed execution metrics
            await _metricsCollector.RecordExecutionFailureAsync(workflowId ?? "unknown", workflowName ?? "unknown", duration, errorMessage, context.FireTimeUtc);

            // Send failure notification
            await _notificationService.NotifyExecutionFailureAsync(workflowId ?? "unknown", workflowName ?? "unknown", errorMessage, duration, context.Trigger.Key.ToString());
        }

        // Record execution history
        await RecordExecutionHistoryAsync(context, success, duration, jobException?.Message);

        // Log trigger information
        LogTriggerInformation(context);

        // Check for consecutive failures and alert if needed
        if (!success)
        {
            await CheckConsecutiveFailuresAsync(workflowId ?? "unknown", workflowName ?? "unknown");
        }
    }

    /// <summary>
    /// Records execution history for later analysis
    /// </summary>
    private async Task RecordExecutionHistoryAsync(IJobExecutionContext context, bool success, TimeSpan duration, string? errorMessage)
    {
        var workflowId = context.JobDetail.JobDataMap.GetString("WorkflowId");
        var workflowName = context.JobDetail.JobDataMap.GetString("WorkflowName");

        var executionRecord = new WorkflowExecutionRecord
        {
            WorkflowId = workflowId ?? string.Empty,
            WorkflowName = workflowName ?? string.Empty,
            FireInstanceId = context.FireInstanceId,
            ScheduledFireTime = context.ScheduledFireTimeUtc,
            ActualFireTime = context.FireTimeUtc,
            CompletionTime = DateTimeOffset.Now,
            Duration = duration,
            Success = success,
            ErrorMessage = errorMessage,
            TriggerKey = context.Trigger.Key.ToString(),
            TriggerGroup = context.Trigger.Key.Group,
            JobKey = context.JobDetail.Key.ToString(),
            JobGroup = context.JobDetail.Key.Group,
            RefireCount = context.RefireCount,
            RecoveringJob = context.Recovering
        };

        await _metricsCollector.RecordExecutionHistoryAsync(executionRecord);
    }

    /// <summary>
    /// Logs detailed trigger information for debugging
    /// </summary>
    private void LogTriggerInformation(IJobExecutionContext context)
    {
        var trigger = context.Trigger;
        
        _logger.LogDebug(
            "📊 TRIGGER INFO - Key: {TriggerKey}, Type: {TriggerType}, NextFire: {NextFireTime}, PrevFire: {PreviousFireTime}, Refire: {RefireCount}",
            trigger.Key, trigger.GetType().Name, trigger.GetNextFireTimeUtc(), trigger.GetPreviousFireTimeUtc(), context.RefireCount);

        // Log cron trigger specific information
        if (trigger is ICronTrigger cronTrigger)
        {
            _logger.LogDebug(
                "⏰ CRON TRIGGER - Expression: {CronExpression}, Timezone: {Timezone}, Misfire: {MisfireInstruction}",
                cronTrigger.CronExpressionString, cronTrigger.TimeZone.Id, cronTrigger.MisfireInstruction);
        }
    }

    /// <summary>
    /// Checks for consecutive failures and sends alerts if threshold is exceeded
    /// </summary>
    private async Task CheckConsecutiveFailuresAsync(string workflowId, string workflowName)
    {
        const int consecutiveFailureThreshold = 3;
        
        var consecutiveFailures = await _metricsCollector.GetConsecutiveFailureCountAsync(workflowId);
        
        if (consecutiveFailures >= consecutiveFailureThreshold)
        {
            _logger.LogCritical(
                "🚨 CRITICAL: Workflow {WorkflowId} ({WorkflowName}) has failed {ConsecutiveFailures} consecutive times!",
                workflowId, workflowName, consecutiveFailures);

            await _notificationService.NotifyConsecutiveFailuresAsync(workflowId, workflowName, consecutiveFailures);
        }
        else if (consecutiveFailures > 1)
        {
            _logger.LogWarning(
                "⚠️ WARNING: Workflow {WorkflowId} ({WorkflowName}) has failed {ConsecutiveFailures} consecutive times",
                workflowId, workflowName, consecutiveFailures);
        }
    }
}

/// <summary>
/// Represents a workflow execution record for history tracking
/// </summary>
public class WorkflowExecutionRecord
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string FireInstanceId { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledFireTime { get; set; }
    public DateTimeOffset? ActualFireTime { get; set; }
    public DateTimeOffset CompletionTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string TriggerKey { get; set; } = string.Empty;
    public string TriggerGroup { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public int RefireCount { get; set; }
    public bool RecoveringJob { get; set; }
}
