using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace Agent.Services;

/// <summary>
/// Health check service for Quartz.NET scheduler and job status
/// Provides comprehensive health monitoring for the workflow execution system
/// </summary>
public class QuartzHealthCheckService : IHealthCheck
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly WorkflowMetricsCollector _metricsCollector;
    private readonly ILogger<QuartzHealthCheckService> _logger;

    public QuartzHealthCheckService(
        ISchedulerFactory schedulerFactory,
        WorkflowMetricsCollector metricsCollector,
        ILogger<QuartzHealthCheckService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    /// <summary>
    /// Performs comprehensive health check of Quartz scheduler and workflow system
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthData = new Dictionary<string, object>();
            var issues = new List<string>();
            var warnings = new List<string>();

            // Check scheduler status
            var schedulerHealth = await CheckSchedulerHealthAsync();
            healthData.Add("scheduler", schedulerHealth);

            if (!schedulerHealth.IsHealthy)
            {
                issues.AddRange(schedulerHealth.Issues);
            }
            if (schedulerHealth.Warnings.Any())
            {
                warnings.AddRange(schedulerHealth.Warnings);
            }

            // Check job status
            var jobHealth = await CheckJobHealthAsync();
            healthData.Add("jobs", jobHealth);

            if (!jobHealth.IsHealthy)
            {
                issues.AddRange(jobHealth.Issues);
            }
            if (jobHealth.Warnings.Any())
            {
                warnings.AddRange(jobHealth.Warnings);
            }

            // Check workflow metrics
            var metricsHealth = CheckWorkflowMetricsHealth();
            healthData.Add("metrics", metricsHealth);

            if (!metricsHealth.IsHealthy)
            {
                issues.AddRange(metricsHealth.Issues);
            }
            if (metricsHealth.Warnings.Any())
            {
                warnings.AddRange(metricsHealth.Warnings);
            }

            // Check system resources
            var resourceHealth = CheckSystemResourcesHealth();
            healthData.Add("resources", resourceHealth);

            if (!resourceHealth.IsHealthy)
            {
                issues.AddRange(resourceHealth.Issues);
            }
            if (resourceHealth.Warnings.Any())
            {
                warnings.AddRange(resourceHealth.Warnings);
            }

            // Determine overall health status
            var overallStatus = DetermineOverallHealthStatus(issues, warnings);
            
            // Add summary information
            healthData.Add("summary", new
            {
                Status = overallStatus.ToString(),
                IssueCount = issues.Count,
                WarningCount = warnings.Count,
                CheckedAt = DateTimeOffset.Now,
                Issues = issues,
                Warnings = warnings
            });

            var description = GenerateHealthDescription(overallStatus, issues, warnings);

            _logger.LogDebug("🏥 Health check completed: {Status}, Issues: {IssueCount}, Warnings: {WarningCount}",
                overallStatus, issues.Count, warnings.Count);

            return new HealthCheckResult(overallStatus, description, data: healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Health check failed: {ErrorMessage}", ex.Message);
            
            return new HealthCheckResult(
                HealthStatus.Unhealthy,
                $"Health check failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["checkedAt"] = DateTimeOffset.Now
                });
        }
    }

    /// <summary>
    /// Checks the health of the Quartz scheduler
    /// </summary>
    private async Task<ComponentHealthResult> CheckSchedulerHealthAsync()
    {
        var result = new ComponentHealthResult { Component = "Scheduler" };

        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            
            // Check if scheduler is started
            if (!scheduler.IsStarted)
            {
                result.Issues.Add("Scheduler is not started");
                return result;
            }

            // Check if scheduler is shutdown
            if (scheduler.IsShutdown)
            {
                result.Issues.Add("Scheduler is shutdown");
                return result;
            }

            // Check if scheduler is in standby mode
            if (scheduler.InStandbyMode)
            {
                result.Warnings.Add("Scheduler is in standby mode");
            }

            // Get scheduler metadata
            var metadata = await scheduler.GetMetaData();
            result.Data.Add("schedulerName", metadata.SchedulerName);
            result.Data.Add("schedulerInstanceId", metadata.SchedulerInstanceId);
            result.Data.Add("schedulerType", metadata.SchedulerType?.Name ?? "Unknown");
            result.Data.Add("version", metadata.Version);
            result.Data.Add("runningSince", metadata.RunningSince?.ToString() ?? "Unknown");
            result.Data.Add("numberOfJobsExecuted", metadata.NumberOfJobsExecuted);
            result.Data.Add("jobStoreType", metadata.JobStoreType?.Name ?? "Unknown");
            result.Data.Add("threadPoolType", metadata.ThreadPoolType?.Name ?? "Unknown");
            result.Data.Add("threadPoolSize", metadata.ThreadPoolSize);

            // Check thread pool utilization
            if (metadata.ThreadPoolSize > 0)
            {
                // This is a simplified check - in a real scenario you'd want more detailed thread pool monitoring
                result.Data.Add("threadPoolUtilization", "Available"); // Placeholder
            }

            result.IsHealthy = true;
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Scheduler check failed: {ex.Message}");
            _logger.LogError(ex, "❌ Scheduler health check failed");
        }

        return result;
    }

    /// <summary>
    /// Checks the health of scheduled jobs
    /// </summary>
    private async Task<ComponentHealthResult> CheckJobHealthAsync()
    {
        var result = new ComponentHealthResult { Component = "Jobs" };

        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            
            // Get all job groups
            var jobGroupNames = await scheduler.GetJobGroupNames();
            result.Data.Add("jobGroupCount", jobGroupNames.Count);

            var totalJobs = 0;
            var runningJobs = 0;
            var pausedJobs = 0;
            var triggersCount = 0;

            foreach (var groupName in jobGroupNames)
            {
                var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(groupName));
                totalJobs += jobKeys.Count;

                foreach (var jobKey in jobKeys)
                {
                    // Check if job is currently executing
                    var currentlyExecutingJobs = await scheduler.GetCurrentlyExecutingJobs();
                    if (currentlyExecutingJobs.Any(ej => ej.JobDetail.Key.Equals(jobKey)))
                    {
                        runningJobs++;
                    }

                    // Get triggers for this job
                    var triggers = await scheduler.GetTriggersOfJob(jobKey);
                    triggersCount += triggers.Count;

                    foreach (var trigger in triggers)
                    {
                        var triggerState = await scheduler.GetTriggerState(trigger.Key);
                        if (triggerState == TriggerState.Paused)
                        {
                            pausedJobs++;
                        }
                    }
                }
            }

            result.Data.Add("totalJobs", totalJobs);
            result.Data.Add("runningJobs", runningJobs);
            result.Data.Add("pausedJobs", pausedJobs);
            result.Data.Add("triggersCount", triggersCount);

            // Check for issues
            if (totalJobs == 0)
            {
                result.Warnings.Add("No jobs are scheduled");
            }

            if (pausedJobs > 0)
            {
                result.Warnings.Add($"{pausedJobs} jobs are paused");
            }

            // Check for stuck jobs (running for too long)
            var executingJobs = await scheduler.GetCurrentlyExecutingJobs();
            var stuckJobs = executingJobs.Where(ej =>
                DateTimeOffset.Now - ej.FireTimeUtc > TimeSpan.FromHours(1)).ToList();

            if (stuckJobs.Any())
            {
                result.Issues.Add($"{stuckJobs.Count} jobs appear to be stuck (running > 1 hour)");
                result.Data.Add("stuckJobs", stuckJobs.Select(sj => new
                {
                    JobKey = sj.JobDetail.Key.ToString(),
                    FireTime = sj.FireTimeUtc,
                    RunTime = DateTimeOffset.Now - sj.FireTimeUtc
                }));
            }

            result.IsHealthy = true;
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Job health check failed: {ex.Message}");
            _logger.LogError(ex, "❌ Job health check failed");
        }

        return result;
    }

    /// <summary>
    /// Checks the health of workflow metrics
    /// </summary>
    private ComponentHealthResult CheckWorkflowMetricsHealth()
    {
        var result = new ComponentHealthResult { Component = "Metrics" };

        try
        {
            var summary = _metricsCollector.GetPerformanceSummary();
            var allMetrics = _metricsCollector.GetAllWorkflowMetrics();

            result.Data.Add("totalWorkflows", summary.TotalWorkflows);
            result.Data.Add("totalExecutions", summary.TotalExecutions);
            result.Data.Add("successRate", summary.OverallSuccessRate);
            result.Data.Add("averageExecutionTime", summary.AverageExecutionTime.TotalSeconds);

            // Check success rate
            if (summary.OverallSuccessRate < 80 && summary.TotalExecutions > 10)
            {
                result.Issues.Add($"Low success rate: {summary.OverallSuccessRate:F1}%");
            }
            else if (summary.OverallSuccessRate < 90 && summary.TotalExecutions > 5)
            {
                result.Warnings.Add($"Success rate below 90%: {summary.OverallSuccessRate:F1}%");
            }

            // Check for workflows with consecutive failures
            var unhealthyWorkflows = allMetrics.Values.Where(m => m.ConsecutiveFailures >= 3).ToList();
            if (unhealthyWorkflows.Any())
            {
                result.Issues.Add($"{unhealthyWorkflows.Count} workflows have 3+ consecutive failures");
                result.Data.Add("unhealthyWorkflows", unhealthyWorkflows.Select(w => new
                {
                    w.WorkflowId,
                    w.WorkflowName,
                    w.ConsecutiveFailures,
                    w.LastFailureTime,
                    w.LastErrorMessage
                }));
            }

            result.IsHealthy = true;
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Metrics health check failed: {ex.Message}");
            _logger.LogError(ex, "❌ Metrics health check failed");
        }

        return result;
    }

    /// <summary>
    /// Checks system resource health
    /// </summary>
    private ComponentHealthResult CheckSystemResourcesHealth()
    {
        var result = new ComponentHealthResult { Component = "Resources" };

        try
        {
            // Check memory usage
            var memoryUsage = GC.GetTotalMemory(false);
            result.Data.Add("memoryUsageBytes", memoryUsage);
            result.Data.Add("memoryUsageMB", memoryUsage / (1024 * 1024));

            // Check GC information
            result.Data.Add("gen0Collections", GC.CollectionCount(0));
            result.Data.Add("gen1Collections", GC.CollectionCount(1));
            result.Data.Add("gen2Collections", GC.CollectionCount(2));

            // Check thread count
            var threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            result.Data.Add("threadCount", threadCount);

            // Simple resource checks
            if (memoryUsage > 1024 * 1024 * 1024) // > 1GB
            {
                result.Warnings.Add($"High memory usage: {memoryUsage / (1024 * 1024)} MB");
            }

            if (threadCount > 100)
            {
                result.Warnings.Add($"High thread count: {threadCount}");
            }

            result.IsHealthy = true;
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Resource health check failed: {ex.Message}");
            _logger.LogError(ex, "❌ Resource health check failed");
        }

        return result;
    }

    /// <summary>
    /// Determines overall health status based on issues and warnings
    /// </summary>
    private HealthStatus DetermineOverallHealthStatus(List<string> issues, List<string> warnings)
    {
        if (issues.Any())
        {
            return HealthStatus.Unhealthy;
        }

        if (warnings.Count >= 3)
        {
            return HealthStatus.Degraded;
        }

        if (warnings.Any())
        {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }

    /// <summary>
    /// Generates human-readable health description
    /// </summary>
    private string GenerateHealthDescription(HealthStatus status, List<string> issues, List<string> warnings)
    {
        return status switch
        {
            HealthStatus.Healthy => "All workflow systems are operating normally",
            HealthStatus.Degraded => $"Workflow systems are operational with {warnings.Count} warning(s): {string.Join(", ", warnings.Take(3))}",
            HealthStatus.Unhealthy => $"Workflow systems have {issues.Count} critical issue(s): {string.Join(", ", issues.Take(3))}",
            _ => "Workflow system health status unknown"
        };
    }
}

/// <summary>
/// Health result for individual components
/// </summary>
public class ComponentHealthResult
{
    public string Component { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}
