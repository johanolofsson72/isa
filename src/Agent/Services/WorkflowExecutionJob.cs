using Quartz;
using Agent.Services;

namespace Agent.Services;

/// <summary>
/// Quartz.NET job that executes YAML-based workflows.
/// This job acts as a bridge between Quartz.NET scheduling and our existing WorkflowExecutor.
/// </summary>
[DisallowConcurrentExecution] // Prevent overlapping executions of the same workflow
public class WorkflowExecutionJob : IJob
{
    private readonly WorkflowExecutor _workflowExecutor;
    private readonly ILogger<WorkflowExecutionJob> _logger;

    public WorkflowExecutionJob(
        WorkflowExecutor workflowExecutor,
        ILogger<WorkflowExecutionJob> logger)
    {
        _workflowExecutor = workflowExecutor;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobData = context.JobDetail.JobDataMap;
        var workflowId = jobData.GetString("WorkflowId");
        var workflowName = jobData.GetString("WorkflowName") ?? workflowId;
        var sourceFile = jobData.GetString("SourceFile") ?? $"{workflowId}.yaml";
        
        var executionId = Guid.NewGuid().ToString();
        var fireTime = context.FireTimeUtc.ToString("yyyy-MM-dd HH:mm:ss UTC");

        try
        {
            _logger.LogInformation(
                "🚀 QUARTZ JOB START: Workflow '{WorkflowName}' (ID: {WorkflowId}) - Execution: {ExecutionId} - Scheduled: {FireTime}",
                workflowName, workflowId, executionId, fireTime);

            // Validate that we have a workflow ID
            if (string.IsNullOrEmpty(workflowId))
            {
                var validationException = new ArgumentException("WorkflowId is required in JobDataMap");
                throw new JobExecutionException(validationException, refireImmediately: false);
            }

            // Execute the YAML workflow using existing WorkflowExecutor
            var result = await _workflowExecutor.ExecuteWorkflowAsync(workflowId);

            // Log success with detailed information
            var durationMs = result.Duration?.TotalMilliseconds ?? 0;
            _logger.LogInformation(
                "✅ QUARTZ JOB SUCCESS: Workflow '{WorkflowName}' completed successfully - Execution: {ExecutionId} - Duration: {Duration}ms - Status: {Status}",
                workflowName, executionId, durationMs, result.Status);

            // Store execution result in job context for monitoring
            context.Result = new
            {
                Status = "Success",
                ExecutionId = executionId,
                Duration = durationMs,
                JobResults = result.JobResults?.Count ?? 0,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ QUARTZ JOB FAILED: Workflow '{WorkflowName}' (ID: {WorkflowId}) - Execution: {ExecutionId} - Error: {ErrorMessage}",
                workflowName, workflowId, executionId, ex.Message);

            // Store failure information in job context
            context.Result = new
            {
                Status = "Failed",
                ExecutionId = executionId,
                Error = ex.Message,
                FailedAt = DateTime.UtcNow
            };

            // Create JobExecutionException to let Quartz handle retry logic
            // refireImmediately: false means Quartz will use the configured retry policy
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
