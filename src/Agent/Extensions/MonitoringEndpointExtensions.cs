using Agent.Models;
using Agent.Services;
using Agent.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Agent.Extensions;

/// <summary>
/// Extension methods for workflow monitoring API endpoints
/// </summary>
public static class MonitoringEndpointExtensions
{
    /// <summary>
    /// Maps workflow monitoring endpoints
    /// </summary>
    public static WebApplication MapMonitoringEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/monitoring")
            .WithTags("Monitoring")
            .WithOpenApi();

        // GET: Get workflow execution metrics
        group.MapGet("/metrics", GetWorkflowMetricsAsync)
            .WithName("GetWorkflowMetrics")
            .WithSummary("Get workflow execution metrics")
            .WithDescription("Get comprehensive metrics about workflow executions");

        // GET: Get job execution history
        group.MapGet("/history", GetJobExecutionHistoryAsync)
            .WithName("GetJobExecutionHistory")
            .WithSummary("Get job execution history")
            .WithDescription("Get detailed history of job executions");

        // GET: Get job execution history for specific workflow
        group.MapGet("/history/{workflowId}", GetWorkflowExecutionHistoryAsync)
            .WithName("GetWorkflowExecutionHistory")
            .WithSummary("Get execution history for specific workflow")
            .WithDescription("Get detailed execution history for a specific workflow");

        // GET: Get current job statistics
        group.MapGet("/statistics", GetJobStatisticsAsync)
            .WithName("GetJobStatistics")
            .WithSummary("Get job statistics")
            .WithDescription("Get current statistics about job executions");

        // GET: Get Quartz scheduler status
        group.MapGet("/scheduler/status", GetSchedulerStatusAsync)
            .WithName("GetSchedulerStatus")
            .WithSummary("Get Quartz scheduler status")
            .WithDescription("Get current status and information about the Quartz scheduler");

        // GET: Get all scheduled jobs
        group.MapGet("/jobs", GetScheduledJobsAsync)
            .WithName("GetScheduledJobs")
            .WithSummary("Get all scheduled jobs")
            .WithDescription("Get information about all currently scheduled jobs");

        // GET: Get plugin usage statistics
        group.MapGet("/plugin-usage", GetPluginUsageStatsAsync)
            .WithName("GetPluginUsageStats")
            .WithSummary("Get plugin usage statistics")
            .WithDescription("Get statistics about plugin function calls");

        // GET: Get chat history statistics
        group.MapGet("/chat-history", GetChatHistoryStatsAsync)
            .WithName("GetChatHistoryStats")
            .WithSummary("Get chat history statistics")
            .WithDescription("Get statistics about chat history size and token usage");

        // GET: Get specific job details
        group.MapGet("/jobs/{jobGroup}/{jobName}", GetJobDetailsAsync)
            .WithName("GetJobDetails")
            .WithSummary("Get job details")
            .WithDescription("Get detailed information about a specific job");

        // POST: Trigger job manually
        group.MapPost("/jobs/{jobGroup}/{jobName}/trigger", TriggerJobAsync)
            .WithName("TriggerJob")
            .WithSummary("Trigger job manually")
            .WithDescription("Manually trigger a job execution");

        // POST: Pause job
        group.MapPost("/jobs/{jobGroup}/{jobName}/pause", PauseJobAsync)
            .WithName("PauseJob")
            .WithSummary("Pause job")
            .WithDescription("Pause a scheduled job");

        // POST: Resume job
        group.MapPost("/jobs/{jobGroup}/{jobName}/resume", ResumeJobAsync)
            .WithName("ResumeJob")
            .WithSummary("Resume job")
            .WithDescription("Resume a paused job");

        // Note: do not return here; additional admin alias endpoints follow
        // return app;

        // Backwards-compatible Quartz Admin alias endpoints used by quartz-admin.html
        var adminGroup = app.MapGroup("/api/quartzadmin")
            .WithTags("QuartzAdmin")
            .WithOpenApi();

        // GET: /api/quartzadmin/status
        adminGroup.MapGet("/status", async ([FromServices] IScheduler scheduler) =>
        {
            var meta = await scheduler.GetMetaData();
            var status = new
            {
                schedulerName = scheduler.SchedulerName,
                schedulerInstanceId = scheduler.SchedulerInstanceId,
                version = meta.Version,
                isStarted = scheduler.IsStarted,
                isShutdown = scheduler.IsShutdown,
                isStandbyMode = scheduler.InStandbyMode,
                runningSince = meta.RunningSince
            };
            return Results.Ok(status);
        });

        // GET: /api/quartzadmin/jobs
        adminGroup.MapGet("/jobs", async ([FromServices] IScheduler scheduler) =>
        {
            var jobs = new List<object>();
            var jobGroups = await scheduler.GetJobGroupNames();
            foreach (var groupName in jobGroups)
            {
                var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(groupName));
                foreach (var jobKey in jobKeys)
                {
                    var jobDetail = await scheduler.GetJobDetail(jobKey);
                    var triggers = await scheduler.GetTriggersOfJob(jobKey);
                    var triggerDtos = new List<object>();
                    foreach (var t in triggers)
                    {
                        var state = await scheduler.GetTriggerState(t.Key);
                        triggerDtos.Add(new
                        {
                            name = t.Key.Name,
                            group = t.Key.Group,
                            nextFireTime = t.GetNextFireTimeUtc(),
                            previousFireTime = t.GetPreviousFireTimeUtc(),
                            state = state.ToString()
                        });
                    }

                    jobs.Add(new
                    {
                        jobName = jobKey.Name,
                        jobGroup = jobKey.Group,
                        description = jobDetail?.Description,
                        triggers = triggerDtos
                    });
                }
            }
            return Results.Ok(new { jobs });
        });

        // Compatibility group to serve jobs in shape expected by WebChat (/api/quartz/jobs)
        var compatGroup = app.MapGroup("/api/quartz").WithTags("QuartzCompat").WithOpenApi();

        compatGroup.MapGet("/jobs", async ([FromServices] IScheduler scheduler) =>
        {
            var result = new List<object>();
            var jobGroups = await scheduler.GetJobGroupNames();
            foreach (var groupName in jobGroups)
            {
                var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(groupName));
                foreach (var jobKey in jobKeys)
                {
                    var jobDetail = await scheduler.GetJobDetail(jobKey);
                    var triggers = (await scheduler.GetTriggersOfJob(jobKey)).ToList();
                    var firstTrigger = triggers.FirstOrDefault();
                    var state = firstTrigger != null ? (await scheduler.GetTriggerState(firstTrigger.Key)).ToString() : "Unknown";
                    // Compose a minimal JobData map if available
                    var jobData = new Dictionary<string, string>();
                    foreach (var key in jobDetail?.JobDataMap?.Keys ?? Array.Empty<string>())
                    {
                        var val = jobDetail!.JobDataMap?.GetString(key);
                        if (val != null) jobData[key] = val;
                    }
                    result.Add(new
                    {
                        Name = jobKey.Name,
                        Group = jobKey.Group,
                        Description = jobDetail?.Description,
                        State = state,
                        LastFireTime = firstTrigger?.GetPreviousFireTimeUtc()?.UtcDateTime,
                        NextFireTime = firstTrigger?.GetNextFireTimeUtc()?.UtcDateTime,
                        JobData = jobData
                    });
                }
            }
            return Results.Ok(result);
        });

        // POST: /api/quartz/validate — validate schedule using server-side parser
        compatGroup.MapPost("/validate", ([FromBody] ScheduleValidationRequest req, [FromServices] WorkflowScheduleParser parser) =>
        {
            var result = parser.ParseSchedule(req.Schedule, req.Timezone ?? "Europe/Stockholm");
            return Results.Ok(result);
        });

        // POST: /api/quartzadmin/jobs/{jobName}/pause?jobGroup=... (pause job)
        adminGroup.MapPost("/jobs/{jobName}/pause", async (string jobName, [FromQuery] string jobGroup, [FromServices] IScheduler scheduler) =>
        {
            var key = new JobKey(jobName, jobGroup);
            await scheduler.PauseJob(key);
            return Results.Ok();
        });

        // POST: /api/quartzadmin/jobs/{jobName}/resume?jobGroup=... (resume job)
        adminGroup.MapPost("/jobs/{jobName}/resume", async (string jobName, [FromQuery] string jobGroup, [FromServices] IScheduler scheduler) =>
        {
            var key = new JobKey(jobName, jobGroup);
            await scheduler.ResumeJob(key);
            return Results.Ok();
        });

        // POST: /api/quartzadmin/jobs/{jobName}/trigger?jobGroup=... (trigger now)
        adminGroup.MapPost("/jobs/{jobName}/trigger", async (string jobName, [FromQuery] string jobGroup, [FromServices] IScheduler scheduler) =>
        {
            var key = new JobKey(jobName, jobGroup);
            await scheduler.TriggerJob(key);
            return Results.Ok();
        });

        // DELETE: /api/quartzadmin/jobs/{jobName}?jobGroup=... (delete job)
        adminGroup.MapDelete("/jobs/{jobName}", async (string jobName, [FromQuery] string jobGroup, [FromServices] IScheduler scheduler) =>
        {
            var key = new JobKey(jobName, jobGroup);
            var removed = await scheduler.DeleteJob(key);
            return removed ? Results.Ok() : Results.NotFound(new { error = "Job not found" });
        });

        // Finish mapping admin alias endpoints then return app
        return app;

    }

    public class ScheduleValidationRequest
    {
        public string Schedule { get; set; } = string.Empty;
        public string? Timezone { get; set; }
    }

    private static Task<IResult> GetWorkflowMetricsAsync(
        [FromServices] WorkflowMetricsCollector metricsCollector,
        [FromServices] ILogger<WorkflowMetricsCollector> logger)
    {
        try
        {
            logger.LogInformation("📊 API request: Get workflow metrics");

            var performanceSummary = metricsCollector.GetPerformanceSummary();
            var allMetrics = metricsCollector.GetAllWorkflowMetrics();

            var result = new
            {
                Summary = performanceSummary,
                WorkflowMetrics = allMetrics
            };

            return Task.FromResult(Results.Ok(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting workflow metrics");
            return Task.FromResult(Results.Problem(
                detail: ex.Message,
                title: "Error getting workflow metrics",
                statusCode: 500));
        }
    }

    private static async Task<IResult> GetJobExecutionHistoryAsync(
        [FromServices] JobExecutionTrackingService trackingService,
        [FromServices] ILogger<JobExecutionTrackingService> logger,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            logger.LogInformation("📋 API request: Get job execution history (page {Page}, size {PageSize})", page, pageSize);

            var (records, totalCount) = await trackingService.GetJobExecutionHistoryAsync(
                pageNumber: page,
                pageSize: pageSize);

            var result = new
            {
                Records = records,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting job execution history");
            return Results.Problem(
                detail: ex.Message,
                title: "Error getting job execution history",
                statusCode: 500);
        }
    }

    private static Task<IResult> GetWorkflowExecutionHistoryAsync(
        string workflowId,
        [FromServices] WorkflowMetricsCollector metricsCollector,
        [FromServices] ILogger<WorkflowMetricsCollector> logger,
        [FromQuery] int limit = 50)
    {
        try
        {
            logger.LogInformation("📋 API request: Get execution history for workflow {WorkflowId}", workflowId);

            var history = metricsCollector.GetWorkflowExecutionHistory(workflowId, limit);

            return Task.FromResult(Results.Ok(history));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting workflow execution history");
            return Task.FromResult(Results.Problem(
                detail: ex.Message,
                title: "Error getting workflow execution history",
                statusCode: 500));
        }
    }

    private static async Task<IResult> GetJobStatisticsAsync(
        [FromServices] JobExecutionTrackingService trackingService,
        [FromServices] ILogger<JobExecutionTrackingService> logger)
    {
        try
        {
            logger.LogInformation("📊 API request: Get job statistics");

            var statistics = await trackingService.GetJobStatisticsAsync();

            return Results.Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting job statistics");
            return Results.Problem(
                detail: ex.Message,
                title: "Error getting job statistics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetSchedulerStatusAsync(
        [FromServices] IScheduler scheduler,
        [FromServices] ILogger<IScheduler> logger)
    {
        try
        {
            logger.LogInformation("🔍 API request: Get scheduler status");

            var status = new
            {
                SchedulerName = scheduler.SchedulerName,
                SchedulerInstanceId = scheduler.SchedulerInstanceId,
                IsStarted = scheduler.IsStarted,
                IsShutdown = scheduler.IsShutdown,
                IsStandbyMode = scheduler.InStandbyMode,
                JobGroupNames = await scheduler.GetJobGroupNames(),
                TriggerGroupNames = await scheduler.GetTriggerGroupNames(),
                NumberOfJobsExecuted = await scheduler.GetMetaData().ContinueWith(t => t.Result.NumberOfJobsExecuted),
                RunningSince = await scheduler.GetMetaData().ContinueWith(t => t.Result.RunningSince)
            };

            return Results.Ok(status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting scheduler status");
            return Results.Problem(
                detail: ex.Message,
                title: "Error getting scheduler status",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetScheduledJobsAsync(
        [FromServices] IScheduler scheduler,
        [FromServices] ILogger<IScheduler> logger)
    {
        try
        {
            logger.LogInformation("📋 API request: Get all scheduled jobs");

            var jobGroups = await scheduler.GetJobGroupNames();
            var jobs = new List<object>();

            foreach (var group in jobGroups)
            {
                var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(group));

                foreach (var jobKey in jobKeys)
                {
                    var jobDetail = await scheduler.GetJobDetail(jobKey);
                    var triggers = await scheduler.GetTriggersOfJob(jobKey);

                    jobs.Add(new
                    {
                        JobKey = new { jobKey.Name, jobKey.Group },
                        JobType = jobDetail?.JobType.Name,
                        Description = jobDetail?.Description,
                        Triggers = triggers.Select(t => new
                        {
                            TriggerKey = new { t.Key.Name, t.Key.Group },
                            NextFireTime = t.GetNextFireTimeUtc(),
                            PreviousFireTime = t.GetPreviousFireTimeUtc(),
                            TriggerState = scheduler.GetTriggerState(t.Key).Result.ToString()
                        })
                    });
                }
            }

            return Results.Ok(jobs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting scheduled jobs");
            return Results.Problem(
                detail: ex.Message,
                title: "Error getting scheduled jobs",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetJobDetailsAsync(
        string jobGroup,
        string jobName,
        [FromServices] IScheduler scheduler,
        [FromServices] ILogger<IScheduler> logger)
    {
        try
        {
            logger.LogInformation("🔍 API request: Get job details for {JobGroup}.{JobName}", jobGroup, jobName);

            var jobKey = new JobKey(jobName, jobGroup);
            var jobDetail = await scheduler.GetJobDetail(jobKey);

            if (jobDetail == null)
            {
                return Results.NotFound($"Job {jobGroup}.{jobName} not found");
            }

            var triggers = await scheduler.GetTriggersOfJob(jobKey);

            var details = new
            {
                JobKey = new { jobKey.Name, jobKey.Group },
                JobType = jobDetail.JobType.Name,
                Description = jobDetail.Description,
                JobDataMap = jobDetail.JobDataMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Durable = jobDetail.Durable,
                PersistJobDataAfterExecution = jobDetail.PersistJobDataAfterExecution,
                ConcurrentExecutionDisallowed = jobDetail.ConcurrentExecutionDisallowed,
                Triggers = triggers.Select(t => new
                {
                    TriggerKey = new { t.Key.Name, t.Key.Group },
                    TriggerType = t.GetType().Name,
                    NextFireTime = t.GetNextFireTimeUtc(),
                    PreviousFireTime = t.GetPreviousFireTimeUtc(),
                    StartTime = t.StartTimeUtc,
                    EndTime = t.EndTimeUtc,
                    TriggerState = scheduler.GetTriggerState(t.Key).Result.ToString(),
                    Priority = t.Priority,
                    MisfireInstruction = t.MisfireInstruction
                })
            };

            return Results.Ok(details);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting job details");
            return Results.Problem(
                detail: ex.Message,
                title: "Error getting job details",
                statusCode: 500);
        }
    }

    private static async Task<IResult> TriggerJobAsync(
        string jobGroup,
        string jobName,
        [FromServices] IScheduler scheduler,
        [FromServices] ILogger<IScheduler> logger)
    {
        try
        {
            logger.LogInformation("🚀 API request: Trigger job {JobGroup}.{JobName}", jobGroup, jobName);

            var jobKey = new JobKey(jobName, jobGroup);
            await scheduler.TriggerJob(jobKey);

            return Results.Ok(new { message = $"Job {jobGroup}.{jobName} triggered successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error triggering job");
            return Results.Problem(
                detail: ex.Message,
                title: "Error triggering job",
                statusCode: 500);
        }
    }

    private static async Task<IResult> PauseJobAsync(
        string jobGroup,
        string jobName,
        [FromServices] IScheduler scheduler,
        [FromServices] ILogger<IScheduler> logger)
    {
        try
        {
            logger.LogInformation("⏸️ API request: Pause job {JobGroup}.{JobName}", jobGroup, jobName);

            var jobKey = new JobKey(jobName, jobGroup);
            await scheduler.PauseJob(jobKey);

            return Results.Ok(new { message = $"Job {jobGroup}.{jobName} paused successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error pausing job");
            return Results.Problem(
                detail: ex.Message,
                title: "Error pausing job",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ResumeJobAsync(
        string jobGroup,
        string jobName,
        [FromServices] IScheduler scheduler,
        [FromServices] ILogger<IScheduler> logger)
    {
        try
        {
            logger.LogInformation("▶️ API request: Resume job {JobGroup}.{JobName}", jobGroup, jobName);

            var jobKey = new JobKey(jobName, jobGroup);
            await scheduler.ResumeJob(jobKey);

            return Results.Ok(new { message = $"Job {jobGroup}.{jobName} resumed successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error resuming job");
            return Results.Problem(
                detail: ex.Message,
                title: "Error resuming job",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetPluginUsageStatsAsync(
        [FromServices] AgentDbContext dbContext,
        [FromServices] ILogger<IScheduler> logger,
        [FromQuery] string? sessionId = null)
    {
        try
        {
            var query = dbContext.AuditLogs
                .Where(log => log.EventType == "PluginExecution")
                .AsQueryable();

            // Filter by session if provided
            if (!string.IsNullOrEmpty(sessionId))
            {
                query = query.Where(log => log.SessionId == sessionId);
            }

            // Get plugin usage stats grouped by plugin and function
            var pluginStats = await query
                .GroupBy(log => new { log.PluginName, log.FunctionName })
                .Select(g => new
                {
                    PluginName = g.Key.PluginName ?? "Unknown",
                    FunctionName = g.Key.FunctionName ?? "Unknown",
                    CallCount = g.Count(),
                    LastUsed = g.Max(log => log.Timestamp),
                    Status = g.OrderByDescending(log => log.Timestamp).First().Status ?? "Unknown"
                })
                .OrderByDescending(s => s.LastUsed)
                .Take(20) // Limit to most recent 20 plugin functions
                .ToListAsync();

            return Results.Ok(pluginStats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting plugin usage stats");
            return Results.Problem(
                detail: ex.Message,
                title: "Error getting plugin usage stats",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetChatHistoryStatsAsync(
        [FromServices] ChatSessionService sessionService,
        [FromServices] ILogger<IScheduler> logger,
        [FromQuery] string? sessionId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                // Return empty stats if no session ID provided
                var emptyStats = new
                {
                    MessageCount = 0,
                    TotalCharacters = 0,
                    EstimatedTokens = 0,
                    LastMessageTime = DateTime.MinValue
                };
                return Results.Ok(emptyStats);
            }

            // Get chat history for the session
            var chatHistory = await sessionService.GetOrCreateSessionAsync(sessionId);

            var messageCount = chatHistory.Count;
            var totalCharacters = chatHistory.Sum(msg => msg.Content?.Length ?? 0);
            var estimatedTokens = (int)Math.Round(totalCharacters / 4.0); // 4 chars per token
            var lastMessageTime = DateTime.UtcNow; // Use current time since ChatMessageContent doesn't have Timestamp

            var stats = new
            {
                MessageCount = messageCount,
                TotalCharacters = totalCharacters,
                EstimatedTokens = estimatedTokens,
                LastMessageTime = lastMessageTime
            };

            return Results.Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting chat history stats");
            return Results.Problem(
                detail: ex.Message,
                title: "Error getting chat history stats",
                statusCode: 500);
        }
    }
}
