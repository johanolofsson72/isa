using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Service for handling workflow step navigation and execution flow control
/// </summary>
public class WorkflowNavigationService
{
    private readonly ILogger<WorkflowNavigationService> _logger;
    private readonly WorkflowValidationService _validationService;

    public WorkflowNavigationService(
        ILogger<WorkflowNavigationService> logger,
        WorkflowValidationService validationService)
    {
        _logger = logger;
        _validationService = validationService;
    }

    /// <summary>
    /// Determines the next step to execute based on the current step's result
    /// </summary>
    public NavigationResult GetNextStep(WorkflowDefinition workflow, string currentStepId, bool wasSuccessful, string? errorMessage = null)
    {
        try
        {
            var currentStep = _validationService.FindStepById(workflow, currentStepId);
            if (currentStep == null)
            {
                return NavigationResult.Error($"Current step '{currentStepId}' not found in workflow");
            }

            string? nextStepId = null;

            if (wasSuccessful)
            {
                nextStepId = GetSuccessNextStep(currentStep);
                _logger.LogDebug("Step '{CurrentStepId}' succeeded, next step: {NextStepId}", currentStepId, nextStepId ?? "none");
            }
            else
            {
                nextStepId = GetFailureNextStep(currentStep);
                _logger.LogDebug("Step '{CurrentStepId}' failed, next step: {NextStepId}", currentStepId, nextStepId ?? "none");
            }

            // Handle special step names
            if (IsWorkflowTerminationStep(nextStepId))
            {
                return NavigationResult.Complete(nextStepId, GetTerminationReason(nextStepId));
            }

            // If no specific next step, determine default behavior
            if (string.IsNullOrWhiteSpace(nextStepId))
            {
                return GetDefaultNextStep(workflow, currentStepId, wasSuccessful);
            }

            // Validate the next step exists
            var nextStep = _validationService.FindStepById(workflow, nextStepId);
            if (nextStep == null)
            {
                return NavigationResult.Error($"Next step '{nextStepId}' not found in workflow");
            }

            return NavigationResult.Continue(nextStepId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining next step for {CurrentStepId}", currentStepId);
            return NavigationResult.Error($"Navigation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the execution path for a workflow, useful for debugging and visualization
    /// </summary>
    public List<string> GetPossibleExecutionPaths(WorkflowDefinition workflow, string startStepId)
    {
        var paths = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        TraverseExecutionPath(workflow, startStepId, new List<string>(), paths, visited, maxDepth: 20);
        
        return paths;
    }

    private string? GetSuccessNextStep(WorkflowStep step)
    {
        return !string.IsNullOrWhiteSpace(step.OnSuccess) ? step.OnSuccess : null;
    }

    private string? GetFailureNextStep(WorkflowStep step)
    {
        return !string.IsNullOrWhiteSpace(step.OnFailure) ? step.OnFailure : null;
    }

    private NavigationResult GetDefaultNextStep(WorkflowDefinition workflow, string currentStepId, bool wasSuccessful)
    {
        // Find the current step's position and get the next step in order
        foreach (var job in workflow.Jobs.OrderBy(j => j.Order))
        {
            var steps = job.Steps.OrderBy(s => s.Order).ToList();
            var currentIndex = steps.FindIndex(s => string.Equals(s.Id, currentStepId, StringComparison.OrdinalIgnoreCase));
            
            if (currentIndex >= 0)
            {
                // If there's a next step in the same job, continue to it
                if (currentIndex + 1 < steps.Count)
                {
                    var nextStep = steps[currentIndex + 1];
                    _logger.LogDebug("Default navigation: continuing to next step {NextStepId}", nextStep.Id);
                    return NavigationResult.Continue(nextStep.Id);
                }
                
                // If this was the last step in the job, check for workflow-level navigation
                if (wasSuccessful && !string.IsNullOrWhiteSpace(workflow.OnSuccess))
                {
                    _logger.LogDebug("Workflow completed successfully, executing OnSuccess step: {OnSuccessStep}", workflow.OnSuccess);
                    return NavigationResult.Continue(workflow.OnSuccess);
                }
                
                if (!wasSuccessful && !string.IsNullOrWhiteSpace(workflow.OnError))
                {
                    _logger.LogDebug("Workflow failed, executing OnError step: {OnErrorStep}", workflow.OnError);
                    return NavigationResult.Continue(workflow.OnError);
                }
                
                // No more steps, workflow is complete
                var status = wasSuccessful ? "completed" : "failed";
                _logger.LogDebug("Workflow execution finished with status: {Status}", status);
                return NavigationResult.Complete(status, $"Workflow {status}");
            }
        }

        return NavigationResult.Error($"Could not find current step '{currentStepId}' in workflow");
    }

    private bool IsWorkflowTerminationStep(string? stepName)
    {
        if (string.IsNullOrWhiteSpace(stepName))
            return false;

        var terminationSteps = new[] { "completed", "failed", "cancelled", "end", "exit" };
        return terminationSteps.Contains(stepName, StringComparer.OrdinalIgnoreCase);
    }

    private string GetTerminationReason(string? stepName)
    {
        return stepName?.ToLowerInvariant() switch
        {
            "completed" => "Workflow completed successfully",
            "failed" => "Workflow failed",
            "cancelled" => "Workflow was cancelled",
            "end" or "exit" => "Workflow terminated",
            _ => "Workflow finished"
        };
    }

    private void TraverseExecutionPath(WorkflowDefinition workflow, string stepId, List<string> currentPath, 
        List<string> allPaths, HashSet<string> visited, int maxDepth)
    {
        if (maxDepth <= 0 || visited.Contains(stepId) || IsWorkflowTerminationStep(stepId))
        {
            allPaths.Add(string.Join(" -> ", currentPath.Concat(new[] { stepId })));
            return;
        }

        visited.Add(stepId);
        currentPath.Add(stepId);

        var step = _validationService.FindStepById(workflow, stepId);
        if (step != null)
        {
            // Explore success path
            if (!string.IsNullOrWhiteSpace(step.OnSuccess))
            {
                TraverseExecutionPath(workflow, step.OnSuccess, new List<string>(currentPath), allPaths, new HashSet<string>(visited), maxDepth - 1);
            }

            // Explore failure path
            if (!string.IsNullOrWhiteSpace(step.OnFailure))
            {
                TraverseExecutionPath(workflow, step.OnFailure, new List<string>(currentPath), allPaths, new HashSet<string>(visited), maxDepth - 1);
            }

            // If no explicit next steps, follow default flow
            if (string.IsNullOrWhiteSpace(step.OnSuccess) && string.IsNullOrWhiteSpace(step.OnFailure))
            {
                var defaultNext = GetDefaultNextStep(workflow, stepId, true);
                if (defaultNext.Action == NavigationAction.Continue)
                {
                    TraverseExecutionPath(workflow, defaultNext.NextStepId!, new List<string>(currentPath), allPaths, new HashSet<string>(visited), maxDepth - 1);
                }
                else
                {
                    allPaths.Add(string.Join(" -> ", currentPath.Concat(new[] { defaultNext.NextStepId ?? "end" })));
                }
            }
        }

        currentPath.RemoveAt(currentPath.Count - 1);
        visited.Remove(stepId);
    }
}

/// <summary>
/// Result of workflow navigation decision
/// </summary>
public class NavigationResult
{
    public NavigationAction Action { get; set; }
    public string? NextStepId { get; set; }
    public string? Reason { get; set; }
    public string? ErrorMessage { get; set; }

    public static NavigationResult Continue(string nextStepId)
    {
        return new NavigationResult
        {
            Action = NavigationAction.Continue,
            NextStepId = nextStepId
        };
    }

    public static NavigationResult Complete(string? finalStepId, string reason)
    {
        return new NavigationResult
        {
            Action = NavigationAction.Complete,
            NextStepId = finalStepId,
            Reason = reason
        };
    }

    public static NavigationResult Error(string errorMessage)
    {
        return new NavigationResult
        {
            Action = NavigationAction.Error,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Navigation action types
/// </summary>
public enum NavigationAction
{
    Continue,   // Continue to next step
    Complete,   // Workflow is complete
    Error       // Navigation error occurred
}
