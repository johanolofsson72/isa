using Agent.Services;

namespace Agent.Extensions;

public static class HistoryEndpointExtensions
{
    public static WebApplication MapHistoryEndpoints(this WebApplication app)
    {
        // GET: Get all history entries
        app.MapGet(
                "/api/history",
                async (HistoryService historyService, ILogger<object> logger) =>
                {
                    try
                    {
                        logger.LogInformation("🔍 [HISTORY API] Starting to fetch history entries");

                        var historyEntries = await historyService.GetHistoryAsync();

                        logger.LogInformation(
                            $"📊 [HISTORY API] Retrieved {historyEntries?.Count ?? 0} history entries from service"
                        );

                        if (historyEntries == null || !historyEntries.Any())
                        {
                            logger.LogWarning(
                                "⚠️ [HISTORY API] No history entries found, returning empty list"
                            );
                            return Results.Ok(new List<object>());
                        }

                        // Convert to the format expected by WebChat
                        var result = historyEntries
                            .Select(h => new
                            {
                                id = h.Id,
                                workflowId = h.WorkflowId,
                                workflowName = h.WorkflowName,
                                executedAt = h.StartTime,
                                status = h.Status,
                                duration = h.EndTime.HasValue
                                    ? h.EndTime.Value - h.StartTime
                                    : TimeSpan.Zero,
                                errorMessage = h.ErrorMessage,
                            })
                            .ToList();

                        logger.LogInformation(
                            $"✅ [HISTORY API] Successfully mapped {result.Count} entries for WebChat"
                        );
                        logger.LogDebug(
                            $"🔍 [HISTORY API] Sample entry: {System.Text.Json.JsonSerializer.Serialize(result.FirstOrDefault())}"
                        );

                        return Results.Ok(result);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "❌ [HISTORY API] Error retrieving history entries");
                        return Results.Problem(
                            "An error occurred while retrieving history entries"
                        );
                    }
                }
            )
            .WithName("GetHistoryEntries")
            .WithTags("History")
            .WithOpenApi();

        // GET: Get workflow history
        app.MapGet(
                "/api/workflows/history",
                async (string? workflowId, HistoryService historyService, ILogger<object> logger) =>
                {
                    try
                    {
                        var allHistory = await historyService.GetHistoryAsync();

                        // Filter by workflowId if provided
                        var filteredHistory = string.IsNullOrEmpty(workflowId)
                            ? allHistory
                            : allHistory.Where(h => h.WorkflowId == workflowId).ToList();

                        // Convert to the format expected by WebChat
                        var result = filteredHistory
                            .Select(h => new
                            {
                                id = h.Id,
                                workflowId = h.WorkflowId,
                                workflowName = h.WorkflowName,
                                executedAt = h.StartTime,
                                status = h.Status,
                                duration = h.EndTime.HasValue
                                    ? h.EndTime.Value - h.StartTime
                                    : TimeSpan.Zero,
                                errorMessage = h.ErrorMessage,
                                executionLog = h.ExecutionLog,
                            })
                            .ToList();

                        return Results.Ok(result);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error retrieving workflow history");
                        return Results.Problem(
                            "An error occurred while retrieving workflow history"
                        );
                    }
                }
            )
            .WithName("GetWorkflowHistory")
            .WithTags("History")
            .WithOpenApi();

        // PUT: Update a history entry
        app.MapPut(
                "/api/history/{id}",
                (string id, ILogger<object> logger) =>
                {
                    try
                    {
                        // For now, just return not implemented
                        return Results.Problem(
                            "History entry update not yet implemented",
                            statusCode: 501
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating history entry {HistoryId}", id);
                        return Results.Problem(
                            $"An error occurred while updating history entry: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("UpdateHistoryEntry")
            .WithTags("History")
            .WithOpenApi();

        // GET: Get detailed workflow execution information
        app.MapGet(
                "/api/history/workflow-execution/{executionId}",
                async (string executionId, HistoryService historyService, ILogger<object> logger) =>
                {
                    try
                    {
                        var execution = await historyService.GetHistoryByIdAsync(executionId);
                        if (execution == null)
                        {
                            return Results.NotFound($"Execution {executionId} not found");
                        }

                        // Return detailed execution information
                        var result = new
                        {
                            id = execution.Id,
                            workflowId = execution.WorkflowId,
                            workflowName = execution.WorkflowName,
                            status = execution.Status,
                            executedAt = execution.StartTime,
                            duration = execution.EndTime.HasValue
                                ? execution.EndTime.Value - execution.StartTime
                                : (TimeSpan?)null,
                            errorMessage = execution.ErrorMessage,
                            executionLog = string.Join("\n", execution.ExecutionLog),
                        };

                        return Results.Ok(result);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Error retrieving execution details for {ExecutionId}",
                            executionId
                        );
                        return Results.Problem(
                            $"An error occurred while retrieving execution details: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetExecutionDetail")
            .WithTags("History")
            .WithOpenApi();

        // GET: Get execution steps for a workflow execution
        app.MapGet(
                "/api/history/workflow-execution/{executionId}/steps",
                async (string executionId, HistoryService historyService, ILogger<object> logger) =>
                {
                    try
                    {
                        var execution = await historyService.GetHistoryByIdAsync(executionId);
                        if (execution == null)
                        {
                            return Results.NotFound($"Execution {executionId} not found");
                        }

                        // Parse execution log to extract steps with better data
                        var steps = new List<object>();

                        if (execution.ExecutionLog.Any())
                        {
                            // Parse the execution log to extract step information
                            var stepIndex = 1;
                            string? currentStepName = null;
                            string? currentStepInput = null;
                            string? currentStepOutput = null;
                            string? currentStepStatus = "Completed";
                            DateTime? currentStepStart = null;

                            foreach (var line in execution.ExecutionLog)
                            {
                                // Look for step execution patterns
                                if (line.Contains("→ Executing step:"))
                                {
                                    // Save previous step if we have one
                                    if (currentStepName != null)
                                    {
                                        steps.Add(
                                            CreateStepObject(
                                                currentStepName,
                                                currentStepStatus,
                                                currentStepStart ?? execution.StartTime,
                                                currentStepInput,
                                                currentStepOutput,
                                                stepIndex
                                            )
                                        );
                                        stepIndex++;
                                    }

                                    // Extract step name
                                    var stepNameStart =
                                        line.IndexOf("→ Executing step:")
                                        + "→ Executing step:".Length;
                                    currentStepName = line.Substring(stepNameStart).Trim();
                                    currentStepInput = $"Executing step: {currentStepName}";
                                    currentStepOutput = null;
                                    currentStepStatus = "Running";
                                    currentStepStart = execution.StartTime.AddSeconds(
                                        (stepIndex - 1) * 10
                                    );
                                }
                                else if (
                                    line.Contains("✓") && line.Contains("completed successfully")
                                )
                                {
                                    currentStepStatus = "Completed";
                                    currentStepOutput = line.Trim();
                                }
                                else if (line.Contains("✗") && line.Contains("failed"))
                                {
                                    currentStepStatus = "Failed";
                                    currentStepOutput = line.Trim();
                                }
                                else if (line.Contains("Starting execution of workflow:"))
                                {
                                    currentStepName = "Workflow Initialization";
                                    currentStepInput = line.Trim();
                                    currentStepOutput = "Workflow started successfully";
                                    currentStepStatus = "Completed";
                                    currentStepStart = execution.StartTime;
                                }
                                else if (line.Contains("Executing job:"))
                                {
                                    // Extract job name for better context
                                    var jobNameStart =
                                        line.IndexOf("Executing job:") + "Executing job:".Length;
                                    var jobName = line.Substring(jobNameStart).Trim();
                                    if (currentStepOutput == null)
                                    {
                                        currentStepOutput = $"Started job: {jobName}";
                                    }
                                }
                            }

                            // Add the last step if we have one
                            if (currentStepName != null)
                            {
                                steps.Add(
                                    CreateStepObject(
                                        currentStepName,
                                        currentStepStatus,
                                        currentStepStart ?? execution.StartTime,
                                        currentStepInput,
                                        currentStepOutput,
                                        stepIndex
                                    )
                                );
                            }
                        }
                        else
                        {
                            // Default step if no log
                            steps.Add(
                                CreateStepObject(
                                    "Workflow Execution",
                                    execution.Status,
                                    execution.StartTime,
                                    "Workflow started",
                                    execution.Status == "Completed"
                                        ? "Workflow completed successfully"
                                        : "Workflow execution",
                                    1
                                )
                            );
                        }

                        return Results.Ok(steps);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Error retrieving execution steps for {ExecutionId}",
                            executionId
                        );
                        return Results.Problem(
                            $"An error occurred while retrieving execution steps: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetExecutionSteps")
            .WithTags("History")
            .WithOpenApi();

        // GET: Get execution logs for a workflow execution
        app.MapGet(
                "/api/history/workflow-execution/{executionId}/logs",
                async (string executionId, HistoryService historyService, ILogger<object> logger) =>
                {
                    try
                    {
                        var execution = await historyService.GetHistoryByIdAsync(executionId);
                        if (execution == null)
                        {
                            return Results.NotFound($"Execution {executionId} not found");
                        }

                        var logs = new List<object>();

                        if (execution.ExecutionLog.Any())
                        {
                            var timestamp = execution.StartTime;

                            foreach (var line in execution.ExecutionLog)
                            {
                                var level = "Info";
                                if (
                                    line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
                                    || line.Contains("FAIL", StringComparison.OrdinalIgnoreCase)
                                )
                                    level = "Error";
                                else if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase))
                                    level = "Warning";
                                else if (line.Contains("DEBUG", StringComparison.OrdinalIgnoreCase))
                                    level = "Debug";

                                logs.Add(
                                    new
                                    {
                                        timestamp = timestamp,
                                        level = level,
                                        message = line,
                                    }
                                );

                                timestamp = timestamp.AddMilliseconds(100); // Increment timestamp
                            }
                        }

                        return Results.Ok(logs);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Error retrieving execution logs for {ExecutionId}",
                            executionId
                        );
                        return Results.Problem(
                            $"An error occurred while retrieving execution logs: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("GetExecutionLogs")
            .WithTags("History")
            .WithOpenApi();

        return app;
    }

    /// <summary>
    /// Helper method to create a step object with proper data structure
    /// </summary>
    private static object CreateStepObject(
        string name,
        string status,
        DateTime timestamp,
        string? input,
        string? output,
        int stepIndex
    )
    {
        return new
        {
            name = name,
            status = status,
            timestamp = timestamp,
            duration = TimeSpan.FromSeconds(10), // Default duration, could be improved with real timing
            description = $"Step {stepIndex}: {name}",
            input = input ?? "No input data available",
            output = output ?? "No output data available",
            errorMessage = status == "Failed" ? output ?? "Step failed" : "",
        };
    }
}
