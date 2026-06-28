using Agent.Models;
using Agent.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Extensions;

public static class WorkflowEndpointExtensions
{
    public static WebApplication MapWorkflowSchedulingEndpoints(this WebApplication app)
    {
        // GET: Get all workflow schedules
        app.MapGet(
                "/api/workflows/schedules",
                async ([FromServices] WorkflowService workflowService, [FromServices] ILogger<WorkflowService> logger) =>
                {
                    try
                    {
                        logger.LogInformation("Loading workflows for schedules endpoint...");
                        var workflows = await workflowService.LoadWorkflowsAsync();
                        logger.LogInformation($"Loaded {workflows.Count} total workflows");

                        var workflowsWithSchedule = workflows
                            .Where(w => !string.IsNullOrEmpty(w.Schedule))
                            .ToList();
                        logger.LogInformation(
                            $"Found {workflowsWithSchedule.Count} workflows with schedules"
                        );

                        foreach (var w in workflowsWithSchedule)
                        {
                            logger.LogInformation(
                                $"Workflow: {w.Name}, Schedule: {w.Schedule}, ExecutionType: {w.ExecutionType}, Enabled: {w.Enabled}"
                            );
                        }

                        var scheduledWorkflows = workflows
                            .Where(w => !string.IsNullOrEmpty(w.Schedule))
                            .Select(w => new WorkflowScheduleInfo
                            {
                                Id = w.Id,
                                Name = w.Name,
                                Description = w.Description,
                                Schedule = w.Schedule,
                                Enabled =
                                    w.Enabled
                                    && string.Equals(
                                        w.ExecutionType,
                                        "scheduled",
                                        StringComparison.OrdinalIgnoreCase
                                    ),
                                LastExecuted = w.LastExecuted,
                                NextExecutionTime = w.NextExecutionTime,
                            })
                            .ToList();

                        logger.LogInformation(
                            $"Returning {scheduledWorkflows.Count} scheduled workflows"
                        );
                        return Results.Ok(scheduledWorkflows);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error retrieving workflow schedules");
                        return Results.Problem(
                            "An error occurred while retrieving workflow schedules"
                        );
                    }
                }
            )
            .WithName("GetWorkflowSchedules")
            .WithTags("Workflows")
            .WithOpenApi();

        // PUT: Update a workflow schedule
        app.MapPut(
                "/api/workflows/{id}/schedule",
                async (
                    string id,
                    WorkflowScheduleUpdate update,
                    [FromServices] WorkflowService workflowService,
                    [FromServices] ILogger<WorkflowService> logger
                ) =>
                {
                    try
                    {
                        var workflows = await workflowService.LoadWorkflowsAsync();
                        var workflow = workflows.FirstOrDefault(w => w.Id == id);

                        if (workflow == null)
                        {
                            return Results.NotFound($"Workflow with ID {id} not found");
                        }

                        // Update schedule properties
                        workflow.Schedule = update.Schedule;
                        workflow.ExecutionType = update.Enabled ? "scheduled" : "manual";

                        // Optional extended scheduling fields
                        if (!string.IsNullOrWhiteSpace(update.Timezone))
                        {
                            workflow.Timezone = update.Timezone;
                        }
                        if (!string.IsNullOrWhiteSpace(update.MisfireHandling))
                        {
                            workflow.MisfireHandling = update.MisfireHandling;
                        }
                        if (!string.IsNullOrWhiteSpace(update.OnSuccess))
                        {
                            workflow.OnSuccess = update.OnSuccess;
                        }
                        if (!string.IsNullOrWhiteSpace(update.OnError))
                        {
                            workflow.OnError = update.OnError;
                        }
                        if (!string.IsNullOrWhiteSpace(update.ScheduleDescription))
                        {
                            workflow.ScheduleDescription = update.ScheduleDescription;
                        }

                        // Save the workflow
                        await workflowService.SaveWorkflowAsync(workflow);

                        return Results.Ok();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating workflow schedule");
                        return Results.Problem(
                            "An error occurred while updating the workflow schedule"
                        );
                    }
                }
            )
            .WithName("UpdateWorkflowSchedule")
            .WithTags("Workflows")
            .WithOpenApi();

        // GET: Get a list of all workflows
        app.MapGet(
                "/api/workflows",
                async ([FromServices] WorkflowService workflowService, [FromServices] ILogger<WorkflowService> logger) =>
                {
                    try
                    {
                        var workflows = await workflowService.LoadWorkflowsAsync();

                        var workflowList = workflows
                            .Select(w => new WorkflowInfo
                            {
                                Id = w.Id,
                                Name = w.Name,
                                Description = w.Description,
                                Enabled = w.Enabled,
                                ExecutionType = w.ExecutionType,
                                Schedule = w.Schedule,
                            })
                            .ToList();

                        return Results.Ok(workflowList);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error retrieving workflows");
                        return Results.Problem("An error occurred while retrieving workflows");
                    }
                }
            )
            .WithName("GetWorkflowsApi")
            .WithTags("Workflows")
            .WithOpenApi();

        // GET: Get a specific workflow by ID
        app.MapGet(
                "/api/workflows/{id}",
                async (
                    string id,
                    [FromServices] WorkflowService workflowService,
                    [FromServices] ILogger<WorkflowService> logger
                ) =>
                {
                    try
                    {
                        var workflows = await workflowService.LoadWorkflowsAsync();
                        var workflow = workflows.FirstOrDefault(w => w.Id == id);

                        if (workflow == null)
                        {
                            return Results.NotFound($"Workflow with ID {id} not found");
                        }

                        return Results.Ok(workflow);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error retrieving workflow");
                        return Results.Problem("An error occurred while retrieving the workflow");
                    }
                }
            )
            .WithName("GetWorkflowById")
            .WithTags("Workflows")
            .WithOpenApi();

        // POST: Run/execute a specific workflow
        app.MapPost(
                "/api/workflows/{id}/run",
                async (
                    string id,
                    [FromServices] WorkflowExecutor workflowExecutor,
                    [FromServices] ILogger<WorkflowService> logger
                ) =>
                {
                    try
                    {
                        var result = await workflowExecutor.ExecuteWorkflowAsync(id);
                        return Results.Ok(result);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error executing workflow {WorkflowId}", id);
                        return Results.Problem(
                            $"An error occurred while executing workflow: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("RunWorkflow")
            .WithTags("Workflows")
            .WithOpenApi();

        // POST: Create a new workflow
        app.MapPost(
                "/api/workflows",
                async (
                    WorkflowCreateRequest request,
                    [FromServices] WorkflowService workflowService,
                    [FromServices] ILogger<WorkflowService> logger
                ) =>
                {
                    try
                    {
                        // Create workflow from request
                        var workflow = new WorkflowDefinition
                        {
                            Id = request.Id,
                            Name = request.Name,
                            Description = request.Description,
                            ExecutionType = request.ExecutionType,
                            Schedule = request.Schedule ?? string.Empty,
                            Enabled = request.Enabled,
                            OnSuccess = request.OnSuccess,
                            OnError = request.OnError,
                            Jobs = request.Steps.Any()
                                ? new List<WorkflowJob>
                                {
                                    new()
                                    {
                                        Name = "Main Job",
                                        Description = "Auto-generated job for workflow steps",
                                        Order = 1,
                                        Enabled = true,
                                        Steps = request
                                            .Steps.Select(
                                                (step, index) =>
                                                    new WorkflowStep
                                                    {
                                                        Id = $"step{index + 1}",
                                                        Prompt = step.Content,
                                                        Order = index + 1,
                                                        // ActionType and ToolName no longer used - all steps use natural language
                                                        Timeout = step.Timeout > 0 ? step.Timeout : 300,
                                                        Retries = step.Retries >= 0 ? step.Retries : 2,
                                                        OnSuccess = !string.IsNullOrWhiteSpace(step.OnSuccess)
                                                            ? step.OnSuccess
                                                            : (index == request.Steps.Count - 1 ? "completed" : $"step{index + 2}"),
                                                        OnFailure = !string.IsNullOrWhiteSpace(step.OnFailure) ? step.OnFailure : "failed",
                                                    }
                                            )
                                            .ToList(),
                                    },
                                }
                                : new List<WorkflowJob>
                                {
                                    new()
                                    {
                                        Name = "Main Job",
                                        Description = "Auto-generated default job",
                                        Order = 1,
                                        Enabled = true,
                                        Steps = new List<WorkflowStep>
                                        {
                                            new()
                                            {
                                                Id = "step1",
                                                Prompt =
                                                    "Detta är ett exempel på ett arbetsflödessteg. Redigera detta för att anpassa till dina behov.",
                                                Order = 1,
                                                // ActionType no longer used - all steps use natural language
                                                Timeout = 300,
                                                Retries = 2,
                                                OnSuccess = "completed",
                                                OnFailure = "failed",
                                            },
                                        },
                                    },
                                },
                        };

                        await workflowService.SaveWorkflowAsync(workflow);
                        // Ensure top-level transitions are persisted if provided
                        if (!string.IsNullOrWhiteSpace(request.OnSuccess))
                        {
                            workflow.OnSuccess = request.OnSuccess;
                        }
                        if (!string.IsNullOrWhiteSpace(request.OnError))
                        {
                            workflow.OnError = request.OnError;
                        }

                        return Results.Created($"/api/workflows/{workflow.Id}", workflow);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error creating workflow");
                        return Results.Problem(
                            $"An error occurred while creating workflow: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("CreateWorkflow")
            .WithTags("Workflows")
            .WithOpenApi();

        // PUT: Update an existing workflow
        app.MapPut(
                "/api/workflows/{id}",
                async (
                    string id,
                    WorkflowUpdateRequest request,
                    [FromServices] WorkflowService workflowService,
                    [FromServices] ILogger<WorkflowService> logger
                ) =>
                {
                    try
                    {
                        // Check if workflow exists
                        var workflows = await workflowService.LoadWorkflowsAsync();
                        var existingWorkflow = workflows.FirstOrDefault(w => w.Id == id);

                        if (existingWorkflow == null)
                        {
                            return Results.NotFound($"Workflow with ID {id} not found");
                        }

                        // Update workflow properties
                        existingWorkflow.Name = request.Name;
                        existingWorkflow.Description = request.Description;
                        existingWorkflow.ExecutionType = request.ExecutionType;
                        existingWorkflow.Schedule = request.Schedule ?? string.Empty;
                        existingWorkflow.Enabled = request.Enabled;
                        existingWorkflow.OnSuccess = request.OnSuccess ?? existingWorkflow.OnSuccess;
                        existingWorkflow.OnError = request.OnError ?? existingWorkflow.OnError;

                        // Update steps if provided
                        if (request.Steps.Any())
                        {
                            existingWorkflow.Jobs = new List<WorkflowJob>
                            {
                                new()
                                {
                                    Name = "Main Job",
                                    Description = "Auto-generated job for workflow steps",
                                    Order = 1,
                                    Enabled = true,
                                    Steps = request
                                        .Steps.Select(
                                            (step, index) =>
                                                new WorkflowStep
                                                {
                                                    Id = $"step{index + 1}",
                                                    Prompt = step.Content,
                                                    Order = index + 1,
                                                    // ActionType and ToolName no longer used - all steps use natural language
                                                    Timeout = step.Timeout > 0 ? step.Timeout : 300,
                                                    Retries = step.Retries >= 0 ? step.Retries : 2,
                                                    OnSuccess = !string.IsNullOrWhiteSpace(step.OnSuccess)
                                                        ? step.OnSuccess
                                                        : (index == request.Steps.Count - 1 ? "completed" : $"step{index + 2}"),
                                                    OnFailure = !string.IsNullOrWhiteSpace(step.OnFailure) ? step.OnFailure : "failed",
                                                }
                                        )
                                        .ToList(),
                                },
                            };
                        }

                        await workflowService.SaveWorkflowAsync(existingWorkflow);
                        return Results.Ok(existingWorkflow);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating workflow {WorkflowId}", id);
                        return Results.Problem(
                            $"An error occurred while updating workflow: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("UpdateWorkflow")
            .WithTags("Workflows")
            .WithOpenApi();

        // DELETE: Delete a workflow (placeholder for future implementation)
        app.MapDelete(
                "/api/workflows/{id}",
                async (
                    string id,
                    [FromServices] WorkflowService workflowService,
                    [FromServices] ILogger<WorkflowService> logger
                ) =>
                {
                    try
                    {
                        logger.LogInformation("Attempting to delete workflow: {WorkflowId}", id);

                        // Allow deletion of all workflows

                        var success = await workflowService.DeleteWorkflowAsync(id);
                        if (success)
                        {
                            logger.LogInformation(
                                "Successfully deleted workflow: {WorkflowId}",
                                id
                            );
                            return Results.Ok(new { message = "Workflow deleted successfully" });
                        }
                        else
                        {
                            logger.LogWarning("Failed to delete workflow: {WorkflowId}", id);
                            return Results.NotFound("Workflow not found or could not be deleted");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error deleting workflow {WorkflowId}", id);
                        return Results.Problem(
                            $"An error occurred while deleting workflow: {ex.Message}"
                        );
                    }
                }
            )
            .WithName("DeleteWorkflow")
            .WithTags("Workflows")
            .WithOpenApi();

        return app;
    }
}

// DTOs for workflow scheduling
public class WorkflowScheduleInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastExecuted { get; set; }
    public DateTime? NextExecutionTime { get; set; }
}

public class WorkflowScheduleUpdate
{
    public string Schedule { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Timezone { get; set; }
    public string? MisfireHandling { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
    public string? ScheduleDescription { get; set; }
}

public class WorkflowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ExecutionType { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
}

// DTOs for workflow creation and update
public class WorkflowCreateRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string ExecutionType { get; set; } = "manual";
    public string? Schedule { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
    public List<WorkflowStepRequest> Steps { get; set; } = new();
}

public class WorkflowUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ExecutionType { get; set; } = string.Empty;
    public string? Schedule { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnError { get; set; }
    public List<WorkflowStepRequest> Steps { get; set; } = new();
}

public class WorkflowStepRequest
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    // ToolName no longer used - all steps use natural language instructions
    public int Order { get; set; }
    public int Timeout { get; set; } = 300;
    public int Retries { get; set; } = 2;
    public string? OnSuccess { get; set; }
    public string? OnFailure { get; set; }
}
