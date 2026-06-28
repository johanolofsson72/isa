using Agent.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Extensions;

public static class CacheAndAuditEndpointExtensions
{
    /// <summary>
    /// Maps all cache and audit API endpoints
    /// </summary>
    public static WebApplication MapCacheAndAuditEndpoints(this WebApplication app)
    {
        app.MapCacheEndpoints();
        app.MapAuditEndpoints();
        app.MapWorkflowExecutionEndpoints();

        return app;
    }

    /// <summary>
    /// Maps cache management endpoints
    /// </summary>
    private static WebApplication MapCacheEndpoints(this WebApplication app)
    {
        var cacheGroup = app.MapGroup("/api/cache").WithTags("Cache Management").WithOpenApi();

        // Get cache statistics
        cacheGroup
            .MapGet(
                "/stats",
                async (IQueryCacheService cacheService, ILogger<IQueryCacheService> logger) =>
                {
                    try
                    {
                        var count = await cacheService.GetCacheCountAsync();
                        var sizeBytes = await cacheService.GetCacheSizeBytesAsync();

                        return Results.Ok(
                            new
                            {
                                Count = count,
                                SizeBytes = sizeBytes,
                                SizeMB = Math.Round(sizeBytes / (1024.0 * 1024.0), 2)
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting cache statistics");
                        return Results.Problem("Error getting cache statistics", statusCode: 500);
                    }
                }
            )
            .WithName("GetCacheStats")
            .WithSummary("Get cache statistics")
            .WithDescription("Returns cache count, size in bytes, and size in MB");

        // Clear all cache entries
        cacheGroup
            .MapDelete(
                "/clear",
                async (IQueryCacheService cacheService, ILogger<IQueryCacheService> logger) =>
                {
                    try
                    {
                        await cacheService.ClearAllCacheAsync();
                        logger.LogInformation("Cache cleared by user request");
                        return Results.Ok(new { Message = "Cache cleared successfully" });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error clearing cache");
                        return Results.Problem("Error clearing cache", statusCode: 500);
                    }
                }
            )
            .WithName("ClearCache")
            .WithSummary("Clear all cache entries")
            .WithDescription("Removes all cached query results");

        // Cleanup old cache entries
        cacheGroup
            .MapPost(
                "/cleanup",
                async (
                    IQueryCacheService cacheService,
                    ILogger<IQueryCacheService> logger,
                    [FromQuery] int maxAgeDays = 30
                ) =>
                {
                    try
                    {
                        var maxAge = TimeSpan.FromDays(maxAgeDays);
                        await cacheService.CleanupOldCacheAsync(maxAge);
                        logger.LogInformation(
                            "Cache cleanup completed for entries older than {MaxAgeDays} days",
                            maxAgeDays
                        );
                        return Results.Ok(
                            new
                            {
                                Message = $"Cache cleanup completed for entries older than {maxAgeDays} days"
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error during cache cleanup");
                        return Results.Problem("Error during cache cleanup", statusCode: 500);
                    }
                }
            )
            .WithName("CleanupOldCache")
            .WithSummary("Cleanup old cache entries")
            .WithDescription("Removes cache entries older than the specified number of days");

        // Get OpenAI cache statistics (more detailed than general cache stats)
        cacheGroup
            .MapGet(
                "/openai/stats",
                async (IOpenAICacheService openAICache, ILogger<IOpenAICacheService> logger) =>
                {
                    try
                    {
                        var stats = await openAICache.GetCacheStatisticsAsync();
                        return Results.Ok(stats);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting OpenAI cache statistics");
                        return Results.Problem("Error getting OpenAI cache statistics", statusCode: 500);
                    }
                }
            )
            .WithName("GetOpenAICacheStats")
            .WithSummary("Get detailed OpenAI cache statistics")
            .WithDescription("Returns detailed statistics including hits, entries, and cache size for OpenAI queries");

        // Cleanup expired OpenAI cache entries
        cacheGroup
            .MapPost(
                "/openai/cleanup",
                async (IOpenAICacheService openAICache, ILogger<IOpenAICacheService> logger) =>
                {
                    try
                    {
                        await openAICache.CleanupExpiredCacheAsync();
                        logger.LogInformation("OpenAI cache cleanup completed");
                        return Results.Ok(new { Message = "OpenAI cache cleanup completed" });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error during OpenAI cache cleanup");
                        return Results.Problem("Error during OpenAI cache cleanup", statusCode: 500);
                    }
                }
            )
            .WithName("CleanupExpiredOpenAICache")
            .WithSummary("Cleanup expired OpenAI cache entries")
            .WithDescription("Removes expired OpenAI cache entries based on their expiration time");

        return app;
    }

    /// <summary>
    /// Maps audit log endpoints
    /// </summary>
    private static WebApplication MapAuditEndpoints(this WebApplication app)
    {
        var auditGroup = app.MapGroup("/api/audit").WithTags("Audit Management").WithOpenApi();

        // Get recent audit logs
        auditGroup
            .MapGet(
                "/logs",
                async (
                    IAuditService auditService,
                    ILogger<IAuditService> logger,
                    [FromQuery] int count = 100,
                    [FromQuery] string? activityType = null,
                    [FromQuery] string? source = null
                ) =>
                {
                    try
                    {
                        var logs = await auditService.GetRecentLogsAsync(
                            count,
                            activityType,
                            source
                        );
                        return Results.Ok(logs);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting audit logs");
                        return Results.Problem("Error getting audit logs", statusCode: 500);
                    }
                }
            )
            .WithName("GetAuditLogs")
            .WithSummary("Get recent audit logs")
            .WithDescription(
                "Returns recent audit logs with optional filtering by activity type and source"
            );

        // Get audit logs by date range
        auditGroup
            .MapGet(
                "/logs/range",
                async (
                    IAuditService auditService,
                    ILogger<IAuditService> logger,
                    [FromQuery] DateTime fromDate,
                    [FromQuery] DateTime toDate,
                    [FromQuery] string? activityType = null
                ) =>
                {
                    try
                    {
                        var logs = await auditService.GetLogsByDateRangeAsync(
                            fromDate,
                            toDate,
                            activityType
                        );
                        return Results.Ok(logs);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting audit logs by date range");
                        return Results.Problem(
                            "Error getting audit logs by date range",
                            statusCode: 500
                        );
                    }
                }
            )
            .WithName("GetAuditLogsByDateRange")
            .WithSummary("Get audit logs by date range")
            .WithDescription(
                "Returns audit logs within the specified date range with optional activity type filtering"
            );

        // Get audit log statistics
        auditGroup
            .MapGet(
                "/stats",
                async (IAuditService auditService, ILogger<IAuditService> logger) =>
                {
                    try
                    {
                        var totalCount = await auditService.GetLogCountAsync();
                        var todayCount = await auditService.GetLogCountAsync(
                            null,
                            DateTime.UtcNow.Date
                        );

                        return Results.Ok(new { TotalCount = totalCount, TodayCount = todayCount });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting audit statistics");
                        return Results.Problem("Error getting audit statistics", statusCode: 500);
                    }
                }
            )
            .WithName("GetAuditStats")
            .WithSummary("Get audit statistics")
            .WithDescription("Returns total audit log count and today's count");

        // Clear all audit logs
        auditGroup
            .MapDelete(
                "/clear",
                async (IAuditService auditService, ILogger<IAuditService> logger) =>
                {
                    try
                    {
                        await auditService.ClearAllLogsAsync();
                        logger.LogInformation("Audit logs cleared by user request");
                        return Results.Ok(new { Message = "Audit logs cleared successfully" });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error clearing audit logs");
                        return Results.Problem("Error clearing audit logs", statusCode: 500);
                    }
                }
            )
            .WithName("ClearAuditLogs")
            .WithSummary("Clear all audit logs")
            .WithDescription("Removes all audit log entries");

        // Cleanup old audit logs
        auditGroup
            .MapPost(
                "/cleanup",
                async (
                    IAuditService auditService,
                    ILogger<IAuditService> logger,
                    [FromQuery] int maxAgeDays = 90
                ) =>
                {
                    try
                    {
                        var maxAge = TimeSpan.FromDays(maxAgeDays);
                        await auditService.CleanupOldLogsAsync(maxAge);
                        logger.LogInformation(
                            "Audit log cleanup completed for entries older than {MaxAgeDays} days",
                            maxAgeDays
                        );
                        return Results.Ok(
                            new
                            {
                                Message = $"Audit log cleanup completed for entries older than {maxAgeDays} days"
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error during audit log cleanup");
                        return Results.Problem("Error during audit log cleanup", statusCode: 500);
                    }
                }
            )
            .WithName("CleanupOldAuditLogs")
            .WithSummary("Cleanup old audit logs")
            .WithDescription("Removes audit log entries older than the specified number of days");

        return app;
    }

    /// <summary>
    /// Maps workflow execution endpoints
    /// </summary>
    private static WebApplication MapWorkflowExecutionEndpoints(this WebApplication app)
    {
        var workflowGroup = app.MapGroup("/api/workflow-execution")
            .WithTags("Workflow Execution")
            .WithOpenApi();

        // Get recent workflow executions
        workflowGroup
            .MapGet(
                "/",
                async (
                    IWorkflowExecutionService service,
                    ILogger<IWorkflowExecutionService> logger,
                    [FromQuery] int count = 50
                ) =>
                {
                    try
                    {
                        var executions = await service.GetRecentExecutionsAsync(count);
                        return Results.Ok(executions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting recent workflow executions");
                        return Results.Problem(
                            "Error getting recent workflow executions",
                            statusCode: 500
                        );
                    }
                }
            )
            .WithName("GetRecentWorkflowExecutions")
            .WithSummary("Get recent workflow executions")
            .WithDescription("Returns the most recent workflow executions");

        // Get workflow execution by ID
        workflowGroup
            .MapGet(
                "/{executionId}",
                async (
                    string executionId,
                    IWorkflowExecutionService service,
                    ILogger<IWorkflowExecutionService> logger
                ) =>
                {
                    try
                    {
                        var execution = await service.GetExecutionAsync(executionId);
                        if (execution == null)
                            return Results.NotFound(
                                $"Workflow execution {executionId} not found"
                            );

                        return Results.Ok(execution);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Error getting workflow execution {ExecutionId}",
                            executionId
                        );
                        return Results.Problem(
                            "Error getting workflow execution",
                            statusCode: 500
                        );
                    }
                }
            )
            .WithName("GetWorkflowExecution")
            .WithSummary("Get workflow execution by ID")
            .WithDescription("Returns a specific workflow execution by its ID");

        // Get executions for a specific workflow
        workflowGroup
            .MapGet(
                "/workflow/{workflowName}",
                async (
                    string workflowName,
                    IWorkflowExecutionService service,
                    ILogger<IWorkflowExecutionService> logger,
                    [FromQuery] int count = 20
                ) =>
                {
                    try
                    {
                        var executions = await service.GetExecutionsByWorkflowAsync(
                            workflowName,
                            count
                        );
                        return Results.Ok(executions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Error getting executions for workflow {WorkflowName}",
                            workflowName
                        );
                        return Results.Problem(
                            "Error getting workflow executions",
                            statusCode: 500
                        );
                    }
                }
            )
            .WithName("GetExecutionsByWorkflow")
            .WithSummary("Get executions for a specific workflow")
            .WithDescription("Returns executions for a specific workflow by name");

        return app;
    }
}
