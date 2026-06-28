using Agent.Models;

namespace Agent.Services.Interfaces;

/// <summary>
/// Interface for workflow validation service
/// </summary>
public interface IWorkflowValidationService
{
    /// <summary>
    /// Validate a workflow definition
    /// </summary>
    /// <param name="workflow">The workflow to validate</param>
    /// <returns>Validation result</returns>
    WorkflowValidationResult ValidateWorkflow(WorkflowDefinition workflow);

    /// <summary>
    /// Validate multiple workflows
    /// </summary>
    /// <param name="workflows">The workflows to validate</param>
    /// <returns>Validation results</returns>
    List<WorkflowValidationResult> ValidateWorkflows(IEnumerable<WorkflowDefinition> workflows);
}
