using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Agent.Services;

/// <summary>
/// Service for tracking workflow executions
/// </summary>
public interface IWorkflowExecutionService
{
    Task<WorkflowExecution> StartExecutionAsync(string workflowName, string workflowPath, string trigger = "Manual", string? triggeredBy = null);
    Task UpdateExecutionAsync(string executionId, int completedSteps, int? failedSteps = null, string? resultMessage = null);
    Task CompleteExecutionAsync(string executionId, string status, string? resultMessage = null, string? errorMessage = null);
    Task<List<WorkflowExecution>> GetRecentExecutionsAsync(int count = 50);
    Task<WorkflowExecution?> GetExecutionAsync(string executionId);
    Task<List<WorkflowExecution>> GetExecutionsByWorkflowAsync(string workflowName, int count = 20);
}

/// <summary>
/// Implementation of workflow execution tracking service
/// </summary>
public class WorkflowExecutionService : IWorkflowExecutionService
{
    private readonly AgentDbContext _context;
    private readonly ILogger<WorkflowExecutionService> _logger;

    public WorkflowExecutionService(AgentDbContext context, ILogger<WorkflowExecutionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WorkflowExecution> StartExecutionAsync(string workflowName, string workflowPath, string trigger = "Manual", string? triggeredBy = null)
    {
        try
        {
            var execution = new WorkflowExecution
            {
                ExecutionId = Guid.NewGuid().ToString(),
                WorkflowName = workflowName,
                WorkflowPath = workflowPath,
                StartedAt = DateTime.UtcNow,
                Status = "Running",
                Trigger = trigger,
                TriggeredBy = triggeredBy,
                TotalSteps = 0,
                CompletedSteps = 0,
                FailedSteps = 0
            };

            _context.WorkflowExecutions.Add(execution);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Started workflow execution: {WorkflowName} (ID: {ExecutionId})", 
                workflowName, execution.ExecutionId);

            return execution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow execution for {WorkflowName}", workflowName);
            throw;
        }
    }

    public async Task UpdateExecutionAsync(string executionId, int completedSteps, int? failedSteps = null, string? resultMessage = null)
    {
        try
        {
            var execution = await _context.WorkflowExecutions
                .FirstOrDefaultAsync(e => e.ExecutionId == executionId);

            if (execution == null)
            {
                _logger.LogWarning("Workflow execution not found: {ExecutionId}", executionId);
                return;
            }

            execution.CompletedSteps = completedSteps;
            if (failedSteps.HasValue)
                execution.FailedSteps = failedSteps.Value;
            if (!string.IsNullOrEmpty(resultMessage))
                execution.ResultMessage = resultMessage;

            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated workflow execution: {ExecutionId} - {CompletedSteps}/{TotalSteps} steps completed", 
                executionId, completedSteps, execution.TotalSteps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow execution {ExecutionId}", executionId);
        }
    }

    public async Task CompleteExecutionAsync(string executionId, string status, string? resultMessage = null, string? errorMessage = null)
    {
        try
        {
            var execution = await _context.WorkflowExecutions
                .FirstOrDefaultAsync(e => e.ExecutionId == executionId);

            if (execution == null)
            {
                _logger.LogWarning("Workflow execution not found: {ExecutionId}", executionId);
                return;
            }

            execution.CompletedAt = DateTime.UtcNow;
            execution.Status = status;
            if (!string.IsNullOrEmpty(resultMessage))
                execution.ResultMessage = resultMessage;
            if (!string.IsNullOrEmpty(errorMessage))
                execution.ErrorMessage = errorMessage;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Completed workflow execution: {WorkflowName} (ID: {ExecutionId}) - Status: {Status}", 
                execution.WorkflowName, executionId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing workflow execution {ExecutionId}", executionId);
        }
    }

    public async Task<List<WorkflowExecution>> GetRecentExecutionsAsync(int count = 50)
    {
        try
        {
            return await _context.WorkflowExecutions
                .OrderByDescending(e => e.StartedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent workflow executions");
            return new List<WorkflowExecution>();
        }
    }

    public async Task<WorkflowExecution?> GetExecutionAsync(string executionId)
    {
        try
        {
            return await _context.WorkflowExecutions
                .FirstOrDefaultAsync(e => e.ExecutionId == executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow execution {ExecutionId}", executionId);
            return null;
        }
    }

    public async Task<List<WorkflowExecution>> GetExecutionsByWorkflowAsync(string workflowName, int count = 20)
    {
        try
        {
            return await _context.WorkflowExecutions
                .Where(e => e.WorkflowName == workflowName)
                .OrderByDescending(e => e.StartedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting executions for workflow {WorkflowName}", workflowName);
            return new List<WorkflowExecution>();
        }
    }
}
