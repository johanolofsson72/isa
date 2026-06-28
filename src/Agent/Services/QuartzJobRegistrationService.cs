using Quartz;
using Agent.Models;
using Agent.Services;
using Agent.Services.Interfaces;
using Shared.Models;

namespace Agent.Services;

/// <summary>
/// Service responsible for dynamic registration and management of Quartz.NET jobs
/// based on workflow definitions. Provides centralized job lifecycle management.
/// </summary>
public class QuartzJobRegistrationService
{
    private readonly IScheduler _scheduler;
    private readonly IWorkflowScheduleParser _scheduleParser;
    private readonly ILogger<QuartzJobRegistrationService> _logger;
    
    // Constants for job organization
    private const string WORKFLOW_JOB_GROUP = "workflows";
    private const string WORKFLOW_TRIGGER_GROUP = "workflows";
    private const string DEFAULT_TIMEZONE = "Europe/Stockholm";

    public QuartzJobRegistrationService(
        ISchedulerFactory schedulerFactory,
        IWorkflowScheduleParser scheduleParser,
        ILogger<QuartzJobRegistrationService> logger)
    {
        _scheduler = schedulerFactory.GetScheduler().Result;
        _scheduleParser = scheduleParser;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new workflow as a Quartz job with proper validation and error handling
    /// </summary>
    public async Task<bool> RegisterWorkflowJobAsync(WorkflowDefinition workflow)
    {
        if (workflow == null)
        {
            _logger.LogWarning("Cannot register null workflow");
            return false;
        }

        try
        {
            // Validate workflow is schedulable
            if (!IsWorkflowSchedulable(workflow))
            {
                _logger.LogDebug("Workflow {WorkflowId} is not schedulable, skipping registration", workflow.Id);
                return false;
            }

            // Validate schedule using interface methods
            if (!_scheduleParser.IsValidSchedule(workflow.Schedule))
            {
                _logger.LogError("Invalid schedule for workflow {WorkflowId}: {Schedule}",
                    workflow.Id, workflow.Schedule);
                return false;
            }

            // Validate timezone if provided
            if (!string.IsNullOrEmpty(workflow.Timezone) && !_scheduleParser.IsValidTimezone(workflow.Timezone))
            {
                _logger.LogError("Invalid timezone for workflow {WorkflowId}: {Timezone}",
                    workflow.Id, workflow.Timezone);
                return false;
            }

            var jobKey = new JobKey(workflow.Id, WORKFLOW_JOB_GROUP);

            // Remove existing job if it exists (update scenario)
            if (await _scheduler.CheckExists(jobKey))
            {
                await UnregisterWorkflowJobAsync(workflow.Id);
                _logger.LogDebug("Removed existing job for workflow {WorkflowId} before re-registration", workflow.Id);
            }

            // Create job detail with enhanced metadata
            var jobDetail = CreateJobDetail(workflow);

            // Create trigger with parsed schedule using interface method
            var trigger = _scheduleParser.ParseSchedule(workflow.Schedule, jobKey);

            // Schedule the job
            await _scheduler.ScheduleJob(jobDetail, trigger);

            _logger.LogInformation(
                "✅ Registered Quartz job: {WorkflowId} ({WorkflowName}) - Next run: {NextRun}",
                workflow.Id, workflow.Name, trigger.GetNextFireTimeUtc()?.ToString("yyyy-MM-dd HH:mm:ss UTC"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register workflow job for {WorkflowId}", workflow.Id);
            return false;
        }
    }

    /// <summary>
    /// Unregisters a workflow job from Quartz scheduler
    /// </summary>
    public async Task<bool> UnregisterWorkflowJobAsync(string workflowId)
    {
        if (string.IsNullOrEmpty(workflowId))
        {
            _logger.LogWarning("Cannot unregister workflow with empty ID");
            return false;
        }

        try
        {
            var jobKey = new JobKey(workflowId, WORKFLOW_JOB_GROUP);
            
            if (await _scheduler.CheckExists(jobKey))
            {
                var deleted = await _scheduler.DeleteJob(jobKey);
                if (deleted)
                {
                    _logger.LogInformation("🗑️ Unregistered Quartz job for workflow: {WorkflowId}", workflowId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to delete Quartz job for workflow: {WorkflowId}", workflowId);
                    return false;
                }
            }
            else
            {
                _logger.LogDebug("No Quartz job found for workflow: {WorkflowId}", workflowId);
                return true; // Consider it successful if job doesn't exist
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering workflow job for {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Updates an existing workflow job with new configuration
    /// </summary>
    public async Task<bool> UpdateWorkflowJobAsync(WorkflowDefinition workflow)
    {
        if (workflow == null)
        {
            _logger.LogWarning("Cannot update null workflow");
            return false;
        }

        try
        {
            // If workflow is no longer schedulable, unregister it
            if (!IsWorkflowSchedulable(workflow))
            {
                await UnregisterWorkflowJobAsync(workflow.Id);
                _logger.LogInformation("Unregistered non-schedulable workflow: {WorkflowId}", workflow.Id);
                return true;
            }

            // Otherwise, register/re-register it (RegisterWorkflowJobAsync handles updates)
            return await RegisterWorkflowJobAsync(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow job for {WorkflowId}", workflow.Id);
            return false;
        }
    }

    /// <summary>
    /// Gets all currently registered workflow jobs
    /// </summary>
    public async Task<IReadOnlyList<JobKey>> GetRegisteredWorkflowJobsAsync()
    {
        try
        {
            var jobKeys = await _scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(WORKFLOW_JOB_GROUP));
            return jobKeys.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting registered workflow jobs");
            return new List<JobKey>().AsReadOnly();
        }
    }

    /// <summary>
    /// Checks if a workflow job is currently registered
    /// </summary>
    public async Task<bool> IsWorkflowJobRegisteredAsync(string workflowId)
    {
        if (string.IsNullOrEmpty(workflowId))
            return false;

        try
        {
            var jobKey = new JobKey(workflowId, WORKFLOW_JOB_GROUP);
            return await _scheduler.CheckExists(jobKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if workflow job is registered for {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Manually triggers a workflow job execution
    /// </summary>
    public async Task<bool> TriggerWorkflowJobAsync(string workflowId)
    {
        if (string.IsNullOrEmpty(workflowId))
        {
            _logger.LogWarning("Cannot trigger workflow with empty ID");
            return false;
        }

        try
        {
            var jobKey = new JobKey(workflowId, WORKFLOW_JOB_GROUP);
            
            if (await _scheduler.CheckExists(jobKey))
            {
                await _scheduler.TriggerJob(jobKey);
                _logger.LogInformation("🚀 Manually triggered workflow job: {WorkflowId}", workflowId);
                return true;
            }
            else
            {
                _logger.LogWarning("Cannot trigger non-existent workflow job: {WorkflowId}", workflowId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering workflow job for {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Pauses a workflow job (stops scheduling but keeps job registered)
    /// </summary>
    public async Task<bool> PauseWorkflowJobAsync(string workflowId)
    {
        if (string.IsNullOrEmpty(workflowId))
        {
            _logger.LogWarning("Cannot pause workflow with empty ID");
            return false;
        }

        try
        {
            var jobKey = new JobKey(workflowId, WORKFLOW_JOB_GROUP);
            
            if (await _scheduler.CheckExists(jobKey))
            {
                await _scheduler.PauseJob(jobKey);
                _logger.LogInformation("⏸️ Paused workflow job: {WorkflowId}", workflowId);
                return true;
            }
            else
            {
                _logger.LogWarning("Cannot pause non-existent workflow job: {WorkflowId}", workflowId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing workflow job for {WorkflowId}", workflowId);
            return false;
        }
    }

    /// <summary>
    /// Resumes a paused workflow job
    /// </summary>
    public async Task<bool> ResumeWorkflowJobAsync(string workflowId)
    {
        if (string.IsNullOrEmpty(workflowId))
        {
            _logger.LogWarning("Cannot resume workflow with empty ID");
            return false;
        }

        try
        {
            var jobKey = new JobKey(workflowId, WORKFLOW_JOB_GROUP);
            
            if (await _scheduler.CheckExists(jobKey))
            {
                await _scheduler.ResumeJob(jobKey);
                _logger.LogInformation("▶️ Resumed workflow job: {WorkflowId}", workflowId);
                return true;
            }
            else
            {
                _logger.LogWarning("Cannot resume non-existent workflow job: {WorkflowId}", workflowId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming workflow job for {WorkflowId}", workflowId);
            return false;
        }
    }

    #region Private Helper Methods

    private bool IsWorkflowSchedulable(WorkflowDefinition workflow)
    {
        return workflow.Enabled &&
               string.Equals(workflow.ExecutionType, "scheduled", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(workflow.Schedule);
    }

    private IJobDetail CreateJobDetail(WorkflowDefinition workflow)
    {
        var jobBuilder = JobBuilder.Create<WorkflowExecutionJob>()
            .WithIdentity(workflow.Id, WORKFLOW_JOB_GROUP)
            .WithDescription($"YAML Workflow: {workflow.Name}")
            .UsingJobData("WorkflowId", workflow.Id)
            .UsingJobData("WorkflowName", workflow.Name)
            .UsingJobData("SourceFile", $"{workflow.Id}.yaml")
            .UsingJobData("OriginalSchedule", workflow.Schedule)
            .UsingJobData("ScheduleDescription", _scheduleParser.GetScheduleDescription(workflow.Schedule))
            .StoreDurably(true); // Keep job even if no triggers

        // Add enhanced metadata
        if (!string.IsNullOrEmpty(workflow.Timezone))
            jobBuilder.UsingJobData("Timezone", workflow.Timezone);

        if (!string.IsNullOrEmpty(workflow.MisfireHandling))
            jobBuilder.UsingJobData("MisfireHandling", workflow.MisfireHandling);

        if (!string.IsNullOrEmpty(workflow.ScheduleDescription))
            jobBuilder.UsingJobData("UserScheduleDescription", workflow.ScheduleDescription);

        return jobBuilder.Build();
    }

    private ITrigger CreateTrigger(WorkflowDefinition workflow, IJobDetail jobDetail, string cronExpression)
    {
        // Determine timezone
        var timezone = !string.IsNullOrEmpty(workflow.Timezone) && _scheduleParser.IsValidTimezone(workflow.Timezone)
            ? workflow.Timezone
            : DEFAULT_TIMEZONE;

        // Determine misfire handling
        var misfireHandling = ParseMisfireHandling(workflow.MisfireHandling);

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity($"{workflow.Id}-trigger", WORKFLOW_TRIGGER_GROUP)
            .WithDescription($"Schedule for {workflow.Name}")
            .ForJob(jobDetail);

        // Configure CRON schedule with timezone and misfire handling
        triggerBuilder.WithCronSchedule(cronExpression, x =>
        {
            x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timezone));

            // Apply misfire handling instruction
            switch (misfireHandling)
            {
                case MisfireHandling.RunImmediately:
                    x.WithMisfireHandlingInstructionFireAndProceed();
                    break;
                case MisfireHandling.RunImmediatelyAndContinue:
                    x.WithMisfireHandlingInstructionIgnoreMisfires();
                    break;
                case MisfireHandling.RunOnNextSchedule:
                    x.WithMisfireHandlingInstructionDoNothing();
                    break;
                case MisfireHandling.Skip:
                default:
                    x.WithMisfireHandlingInstructionDoNothing();
                    break;
            }
        });

        return triggerBuilder.Build();
    }

    private MisfireHandling ParseMisfireHandling(string? misfireHandling)
    {
        if (string.IsNullOrWhiteSpace(misfireHandling))
            return MisfireHandling.Skip;

        return misfireHandling.ToLowerInvariant() switch
        {
            "skip" => MisfireHandling.Skip,
            "runimmediately" => MisfireHandling.RunImmediately,
            "runonnextschedule" => MisfireHandling.RunOnNextSchedule,
            "runimmediatelyandcontinue" => MisfireHandling.RunImmediatelyAndContinue,
            // Legacy support
            "donothing" => MisfireHandling.Skip,
            "firenow" => MisfireHandling.RunImmediately,
            "reschedulenow" => MisfireHandling.RunImmediatelyAndContinue,
            _ => MisfireHandling.Skip // Default
        };
    }

    #endregion
}
