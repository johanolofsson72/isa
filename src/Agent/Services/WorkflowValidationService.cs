using Agent.Models;
using Agent.Services.Interfaces;

namespace Agent.Services;

/// <summary>
/// Service for validating workflow definitions and step references
/// </summary>
public class WorkflowValidationService : IWorkflowValidationService
{
    private readonly ILogger<WorkflowValidationService> _logger;

    public WorkflowValidationService(ILogger<WorkflowValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a workflow definition for structural integrity and step references
    /// </summary>
    public WorkflowValidationResult ValidateWorkflow(WorkflowDefinition workflow)
    {
        var result = new WorkflowValidationResult { IsValid = true };

        try
        {
            // Validate basic workflow properties
            ValidateBasicProperties(workflow, result);

            // Validate misfire handling
            ValidateMisfireHandling(workflow, result);

            // Collect all step IDs for reference validation
            var allStepIds = GetAllStepIds(workflow);

            // Validate workflow-level step references
            ValidateWorkflowStepReferences(workflow, allStepIds, result);

            // Validate step-level references
            ValidateStepReferences(workflow, allStepIds, result);

            // Check for circular references
            ValidateCircularReferences(workflow, result);

            _logger.LogDebug("Workflow validation completed for {WorkflowId}: {IsValid}", 
                workflow.Id, result.IsValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow {WorkflowId}", workflow.Id);
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Gets all step IDs from a workflow
    /// </summary>
    public HashSet<string> GetAllStepIds(WorkflowDefinition workflow)
    {
        var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var job in workflow.Jobs)
        {
            foreach (var step in job.Steps)
            {
                if (!string.IsNullOrWhiteSpace(step.Id))
                {
                    stepIds.Add(step.Id);
                }
            }
        }

        return stepIds;
    }

    /// <summary>
    /// Finds a step by ID in the workflow
    /// </summary>
    public WorkflowStep? FindStepById(WorkflowDefinition workflow, string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId))
            return null;

        foreach (var job in workflow.Jobs)
        {
            foreach (var step in job.Steps)
            {
                if (string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase))
                {
                    return step;
                }
            }
        }

        return null;
    }

    private void ValidateBasicProperties(WorkflowDefinition workflow, WorkflowValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(workflow.Id))
        {
            result.IsValid = false;
            result.Errors.Add("Workflow ID is required");
        }

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            result.IsValid = false;
            result.Errors.Add("Workflow name is required");
        }

        if (workflow.Jobs.Count == 0)
        {
            result.Warnings.Add("Workflow has no jobs defined");
        }
    }

    private void ValidateMisfireHandling(WorkflowDefinition workflow, WorkflowValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(workflow.MisfireHandling))
        {
            var validOptions = new[] { "Skip", "RunImmediately", "RunOnNextSchedule", "RunImmediatelyAndContinue" };
            if (!validOptions.Contains(workflow.MisfireHandling, StringComparer.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid misfire handling option: {workflow.MisfireHandling}. Valid options: {string.Join(", ", validOptions)}");
            }
        }
    }

    private void ValidateWorkflowStepReferences(WorkflowDefinition workflow, HashSet<string> allStepIds, WorkflowValidationResult result)
    {
        // Validate workflow-level OnSuccess reference
        if (!string.IsNullOrWhiteSpace(workflow.OnSuccess))
        {
            if (!allStepIds.Contains(workflow.OnSuccess))
            {
                result.IsValid = false;
                result.Errors.Add($"Workflow OnSuccess references non-existent step: {workflow.OnSuccess}");
            }
        }

        // Validate workflow-level OnError reference
        if (!string.IsNullOrWhiteSpace(workflow.OnError))
        {
            if (!allStepIds.Contains(workflow.OnError))
            {
                result.IsValid = false;
                result.Errors.Add($"Workflow OnError references non-existent step: {workflow.OnError}");
            }
        }
    }

    private void ValidateStepReferences(WorkflowDefinition workflow, HashSet<string> allStepIds, WorkflowValidationResult result)
    {
        foreach (var job in workflow.Jobs)
        {
            foreach (var step in job.Steps)
            {
                // Validate step OnSuccess reference
                if (!string.IsNullOrWhiteSpace(step.OnSuccess) && 
                    !IsSpecialStepName(step.OnSuccess) &&
                    !allStepIds.Contains(step.OnSuccess))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Step '{step.Id}' OnSuccess references non-existent step: {step.OnSuccess}");
                }

                // Validate step OnFailure reference
                if (!string.IsNullOrWhiteSpace(step.OnFailure) && 
                    !IsSpecialStepName(step.OnFailure) &&
                    !allStepIds.Contains(step.OnFailure))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Step '{step.Id}' OnFailure references non-existent step: {step.OnFailure}");
                }
            }
        }
    }

    private void ValidateCircularReferences(WorkflowDefinition workflow, WorkflowValidationResult result)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recursionStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var job in workflow.Jobs)
        {
            foreach (var step in job.Steps)
            {
                if (!visited.Contains(step.Id))
                {
                    if (HasCircularReference(workflow, step.Id, visited, recursionStack))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Circular reference detected starting from step: {step.Id}");
                    }
                }
            }
        }
    }

    private bool HasCircularReference(WorkflowDefinition workflow, string stepId, 
        HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (string.IsNullOrWhiteSpace(stepId) || IsSpecialStepName(stepId))
            return false;

        visited.Add(stepId);
        recursionStack.Add(stepId);

        var step = FindStepById(workflow, stepId);
        if (step != null)
        {
            // Check OnSuccess path
            if (!string.IsNullOrWhiteSpace(step.OnSuccess))
            {
                if (recursionStack.Contains(step.OnSuccess))
                    return true;

                if (!visited.Contains(step.OnSuccess) && 
                    HasCircularReference(workflow, step.OnSuccess, visited, recursionStack))
                    return true;
            }

            // Check OnFailure path
            if (!string.IsNullOrWhiteSpace(step.OnFailure))
            {
                if (recursionStack.Contains(step.OnFailure))
                    return true;

                if (!visited.Contains(step.OnFailure) && 
                    HasCircularReference(workflow, step.OnFailure, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(stepId);
        return false;
    }

    private bool IsSpecialStepName(string stepName)
    {
        var specialNames = new[] { "completed", "failed", "cancelled", "end", "exit" };
        return specialNames.Contains(stepName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validate multiple workflows
    /// </summary>
    /// <param name="workflows">The workflows to validate</param>
    /// <returns>Validation results</returns>
    public List<WorkflowValidationResult> ValidateWorkflows(IEnumerable<WorkflowDefinition> workflows)
    {
        var results = new List<WorkflowValidationResult>();

        foreach (var workflow in workflows)
        {
            results.Add(ValidateWorkflow(workflow));
        }

        return results;
    }
}

/// <summary>
/// Result of workflow validation
/// </summary>
public class WorkflowValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
