using Agent.Models;

namespace Agent.Services.Interfaces;

/// <summary>
/// Interface for workflow management service
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Load all workflows
    /// </summary>
    Task<List<WorkflowDefinition>> LoadWorkflowsAsync();

    /// <summary>
    /// Get a specific workflow by ID
    /// </summary>
    Task<WorkflowDefinition?> GetWorkflowAsync(string workflowId);

    /// <summary>
    /// Save a workflow
    /// </summary>
    Task SaveWorkflowAsync(WorkflowDefinition workflow);

    /// <summary>
    /// Delete a workflow
    /// </summary>
    Task<bool> DeleteWorkflowAsync(string id);
}
