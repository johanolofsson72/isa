using System.Collections.Concurrent;
using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Agent.Services;

/// <summary>
/// Service responsible for managing workflow execution contexts and preventing duplicate executions
/// at multiple levels (workflow, step, and plugin level).
/// This is a critical service for ensuring communication plugins like Mail, SMS, Teams etc.
/// are only executed once per workflow execution.
/// </summary>
public interface IExecutionContextService
{
    /// <summary>
    /// Starts a new workflow execution context with database tracking
    /// </summary>
    Task<string> StartWorkflowExecutionAsync(
        string workflowId,
        string workflowName,
        string trigger = "Manual"
    );

    /// <summary>
    /// Attempts to acquire an execution lock for a workflow
    /// </summary>
    Task<bool> TryAcquireWorkflowLockAsync(string workflowId, string executionId);

    /// <summary>
    /// Releases a workflow execution lock
    /// </summary>
    Task ReleaseWorkflowLockAsync(string workflowId, string executionId);

    /// <summary>
    /// Attempts to acquire a plugin execution lock within a workflow execution context
    /// </summary>
    Task<bool> TryAcquirePluginLockAsync(
        string executionId,
        string stepId,
        string pluginName,
        string actionHash
    );

    /// <summary>
    /// Completes a workflow execution
    /// </summary>
    Task CompleteWorkflowExecutionAsync(
        string executionId,
        string status,
        string? errorMessage = null
    );

    /// <summary>
    /// Checks if a workflow is currently executing
    /// </summary>
    Task<bool> IsWorkflowExecutingAsync(string workflowId);

    /// <summary>
    /// Gets active executions for monitoring
    /// </summary>
    Task<List<WorkflowExecution>> GetActiveExecutionsAsync();
}

public class ExecutionContextService : IExecutionContextService
{
    private readonly AgentDbContext _context;
    private readonly ILogger<ExecutionContextService> _logger;

    // In-memory tracking for fast duplicate prevention
    private static readonly ConcurrentDictionary<string, string> _activeWorkflowExecutions = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> _executionPluginActions =
        new();
    private static readonly SemaphoreSlim _workflowLockSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _pluginLockSemaphore = new(1, 1);

    public ExecutionContextService(AgentDbContext context, ILogger<ExecutionContextService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> StartWorkflowExecutionAsync(
        string workflowId,
        string workflowName,
        string trigger = "Manual"
    )
    {
        var executionId = Guid.NewGuid().ToString();

        try
        {
            // Create database record
            var execution = new WorkflowExecution
            {
                ExecutionId = executionId,
                WorkflowName = workflowName,
                WorkflowPath = workflowId, // Using ID as path for now
                StartedAt = DateTime.UtcNow,
                Status = "Running",
                Trigger = trigger,
                TotalSteps = 0,
                CompletedSteps = 0,
                FailedSteps = 0,
            };

            _context.WorkflowExecutions.Add(execution);
            await _context.SaveChangesAsync();

            // Initialize plugin action tracking for this execution
            _executionPluginActions[executionId] = new HashSet<string>();

            _logger.LogInformation(
                "🚀 EXECUTION CONTEXT STARTED: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}, Trigger={Trigger}",
                workflowId,
                executionId,
                trigger
            );

            return executionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ FAILED TO START EXECUTION CONTEXT: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}",
                workflowId,
                executionId
            );
            throw;
        }
    }

    public async Task<bool> TryAcquireWorkflowLockAsync(string workflowId, string executionId)
    {
        await _workflowLockSemaphore.WaitAsync();
        try
        {
            // Check if workflow is already executing
            if (_activeWorkflowExecutions.ContainsKey(workflowId))
            {
                var existingExecutionId = _activeWorkflowExecutions[workflowId];
                _logger.LogWarning(
                    "🚨 WORKFLOW LOCK DENIED: WorkflowId={WorkflowId} already executing with ExecutionId={ExistingExecutionId}, rejecting new ExecutionId={ExecutionId}",
                    workflowId,
                    existingExecutionId,
                    executionId
                );
                return false;
            }

            // Check database for any recent active executions
            var recentExecutions = await _context
                .WorkflowExecutions.Where(e =>
                    e.WorkflowPath == workflowId
                    && e.Status == "Running"
                    && e.StartedAt > DateTime.UtcNow.AddMinutes(-5)
                )
                .ToListAsync();

            if (recentExecutions.Any(e => e.ExecutionId != executionId))
            {
                _logger.LogWarning(
                    "🚨 DATABASE LOCK DENIED: WorkflowId={WorkflowId} has {Count} recent active executions in database",
                    workflowId,
                    recentExecutions.Count
                );
                return false;
            }

            // Acquire the lock
            _activeWorkflowExecutions[workflowId] = executionId;

            _logger.LogInformation(
                "✅ WORKFLOW LOCK ACQUIRED: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}",
                workflowId,
                executionId
            );

            return true;
        }
        finally
        {
            _workflowLockSemaphore.Release();
        }
    }

    public async Task ReleaseWorkflowLockAsync(string workflowId, string executionId)
    {
        await _workflowLockSemaphore.WaitAsync();
        try
        {
            if (
                _activeWorkflowExecutions.TryGetValue(workflowId, out var currentExecutionId)
                && currentExecutionId == executionId
            )
            {
                _activeWorkflowExecutions.TryRemove(workflowId, out _);
                _logger.LogInformation(
                    "🔓 WORKFLOW LOCK RELEASED: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}",
                    workflowId,
                    executionId
                );
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ WORKFLOW LOCK RELEASE MISMATCH: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}, CurrentExecutionId={CurrentExecutionId}",
                    workflowId,
                    executionId,
                    currentExecutionId
                );
            }

            // Clean up plugin action tracking
            _executionPluginActions.TryRemove(executionId, out _);
        }
        finally
        {
            _workflowLockSemaphore.Release();
        }
    }

    public async Task<bool> TryAcquirePluginLockAsync(
        string executionId,
        string stepId,
        string pluginName,
        string actionHash
    )
    {
        await _pluginLockSemaphore.WaitAsync();
        try
        {
            // Create a unique action identifier
            var actionId = $"{stepId}:{pluginName}:{actionHash}";

            if (!_executionPluginActions.TryGetValue(executionId, out var actions))
            {
                _logger.LogError(
                    "❌ PLUGIN LOCK DENIED: ExecutionId={ExecutionId} not found in plugin action tracking",
                    executionId
                );
                return false;
            }

            // Check if this exact action has already been executed in this execution context
            if (actions.Contains(actionId))
            {
                _logger.LogWarning(
                    "🚨 PLUGIN DUPLICATE PREVENTED: ExecutionId={ExecutionId}, StepId={StepId}, Plugin={Plugin}, ActionHash={ActionHash}",
                    executionId,
                    stepId,
                    pluginName,
                    actionHash
                );
                return false;
            }

            // Mark this action as executing
            actions.Add(actionId);

            _logger.LogInformation(
                "✅ PLUGIN LOCK ACQUIRED: ExecutionId={ExecutionId}, StepId={StepId}, Plugin={Plugin}, ActionId={ActionId}",
                executionId,
                stepId,
                pluginName,
                actionId
            );

            return true;
        }
        finally
        {
            _pluginLockSemaphore.Release();
        }
    }

    public async Task CompleteWorkflowExecutionAsync(
        string executionId,
        string status,
        string? errorMessage = null
    )
    {
        try
        {
            var execution = await _context.WorkflowExecutions.FirstOrDefaultAsync(e =>
                e.ExecutionId == executionId
            );

            if (execution != null)
            {
                execution.CompletedAt = DateTime.UtcNow;
                execution.Status = status;
                if (!string.IsNullOrEmpty(errorMessage))
                    execution.ErrorMessage = errorMessage;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "🏁 EXECUTION CONTEXT COMPLETED: ExecutionId={ExecutionId}, Status={Status}, Duration={Duration}ms",
                    executionId,
                    status,
                    (execution.CompletedAt - execution.StartedAt)?.TotalMilliseconds
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ FAILED TO COMPLETE EXECUTION CONTEXT: ExecutionId={ExecutionId}",
                executionId
            );
        }
    }

    public async Task<bool> IsWorkflowExecutingAsync(string workflowId)
    {
        // Check in-memory first (fast)
        if (_activeWorkflowExecutions.ContainsKey(workflowId))
        {
            _logger.LogInformation(
                "🔍 WORKFLOW EXECUTING CHECK: WorkflowId={WorkflowId} found in memory",
                workflowId
            );
            return true;
        }

        // Check database for any recent active executions (expanded time window for safety)
        var recentExecutions = await _context
            .WorkflowExecutions.Where(e =>
                e.WorkflowPath == workflowId
                && e.Status == "Running"
                && e.StartedAt > DateTime.UtcNow.AddMinutes(-15)
            ) // Increased from 10 to 15 minutes
            .ToListAsync();

        if (recentExecutions.Any())
        {
            _logger.LogWarning(
                "🔍 DATABASE EXECUTION CHECK: WorkflowId={WorkflowId} has {Count} recent active executions in database",
                workflowId,
                recentExecutions.Count
            );
        }

        return recentExecutions.Any();
    }

    public async Task<List<WorkflowExecution>> GetActiveExecutionsAsync()
    {
        return await _context
            .WorkflowExecutions.Where(e => e.Status == "Running")
            .OrderByDescending(e => e.StartedAt)
            .Take(50)
            .ToListAsync();
    }
}
