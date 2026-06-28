using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Agent.Services;

/// <summary>
/// Collects and manages workflow execution metrics
/// Provides performance statistics, success rates, and execution history
/// </summary>
public class WorkflowMetricsCollector
{
    private readonly ILogger<WorkflowMetricsCollector> _logger;
    private readonly ConcurrentDictionary<string, WorkflowMetrics> _workflowMetrics;
    private readonly ConcurrentQueue<WorkflowExecutionRecord> _executionHistory;
    private readonly object _lockObject = new();

    // Configuration
    private const int MaxExecutionHistorySize = 10000;
    private const int ConsecutiveFailureWindow = 24; // hours

    public WorkflowMetricsCollector(ILogger<WorkflowMetricsCollector> logger)
    {
        _logger = logger;
        _workflowMetrics = new ConcurrentDictionary<string, WorkflowMetrics>();
        _executionHistory = new ConcurrentQueue<WorkflowExecutionRecord>();
    }

    /// <summary>
    /// Records the start of a workflow execution
    /// </summary>
    public async Task RecordExecutionStartAsync(string workflowId, string workflowName, DateTimeOffset? fireTime)
    {
        var metrics = GetOrCreateMetrics(workflowId, workflowName);
        
        lock (_lockObject)
        {
            metrics.TotalExecutions++;
            metrics.LastExecutionTime = fireTime ?? DateTimeOffset.Now;
        }

        _logger.LogDebug("📊 Recorded execution start for workflow {WorkflowId}", workflowId);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Records a successful workflow execution
    /// </summary>
    public async Task RecordExecutionSuccessAsync(string workflowId, string workflowName, TimeSpan duration, DateTimeOffset? fireTime)
    {
        var metrics = GetOrCreateMetrics(workflowId, workflowName);
        
        lock (_lockObject)
        {
            metrics.SuccessfulExecutions++;
            metrics.TotalExecutionTime += duration;
            metrics.LastSuccessTime = fireTime ?? DateTimeOffset.Now;
            metrics.ConsecutiveFailures = 0; // Reset consecutive failures on success
            
            // Update duration statistics
            UpdateDurationStatistics(metrics, duration);
        }

        _logger.LogDebug("📊 Recorded successful execution for workflow {WorkflowId}, Duration: {Duration:F2}s", 
            workflowId, duration.TotalSeconds);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Records a failed workflow execution
    /// </summary>
    public async Task RecordExecutionFailureAsync(string workflowId, string workflowName, TimeSpan duration, string errorMessage, DateTimeOffset? fireTime)
    {
        var metrics = GetOrCreateMetrics(workflowId, workflowName);
        
        lock (_lockObject)
        {
            metrics.FailedExecutions++;
            metrics.TotalExecutionTime += duration;
            metrics.LastFailureTime = fireTime ?? DateTimeOffset.Now;
            metrics.LastErrorMessage = errorMessage;
            metrics.ConsecutiveFailures++;
            
            // Update duration statistics (even for failures)
            UpdateDurationStatistics(metrics, duration);
        }

        _logger.LogDebug("📊 Recorded failed execution for workflow {WorkflowId}, Duration: {Duration:F2}s, Error: {ErrorMessage}", 
            workflowId, duration.TotalSeconds, errorMessage);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Records a vetoed (prevented) workflow execution
    /// </summary>
    public async Task RecordExecutionVetoedAsync(string workflowId, string workflowName, DateTimeOffset? fireTime)
    {
        var metrics = GetOrCreateMetrics(workflowId, workflowName);
        
        lock (_lockObject)
        {
            metrics.VetoedExecutions++;
            metrics.LastVetoTime = fireTime ?? DateTimeOffset.Now;
        }

        _logger.LogDebug("📊 Recorded vetoed execution for workflow {WorkflowId}", workflowId);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Records detailed execution history
    /// </summary>
    public async Task RecordExecutionHistoryAsync(WorkflowExecutionRecord record)
    {
        _executionHistory.Enqueue(record);
        
        // Maintain history size limit
        while (_executionHistory.Count > MaxExecutionHistorySize)
        {
            _executionHistory.TryDequeue(out _);
        }

        _logger.LogDebug("📊 Recorded execution history for workflow {WorkflowId}, Success: {Success}", 
            record.WorkflowId, record.Success);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets consecutive failure count for a workflow within the time window
    /// </summary>
    public Task<int> GetConsecutiveFailureCountAsync(string workflowId)
    {
        var metrics = _workflowMetrics.GetValueOrDefault(workflowId);
        if (metrics == null) return Task.FromResult(0);

        // Check if the consecutive failures are within the time window
        var cutoffTime = DateTimeOffset.Now.AddHours(-ConsecutiveFailureWindow);
        if (metrics.LastFailureTime.HasValue && metrics.LastFailureTime.Value > cutoffTime)
        {
            return Task.FromResult(metrics.ConsecutiveFailures);
        }

        return Task.FromResult(0);
    }

    /// <summary>
    /// Gets metrics for a specific workflow
    /// </summary>
    public WorkflowMetrics? GetWorkflowMetrics(string workflowId)
    {
        return _workflowMetrics.GetValueOrDefault(workflowId);
    }

    /// <summary>
    /// Gets metrics for all workflows
    /// </summary>
    public Dictionary<string, WorkflowMetrics> GetAllWorkflowMetrics()
    {
        return new Dictionary<string, WorkflowMetrics>(_workflowMetrics);
    }

    /// <summary>
    /// Gets execution history for a specific workflow
    /// </summary>
    public List<WorkflowExecutionRecord> GetWorkflowExecutionHistory(string workflowId, int limit = 100)
    {
        return _executionHistory
            .Where(r => r.WorkflowId == workflowId)
            .OrderByDescending(r => r.CompletionTime)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Gets recent execution history across all workflows
    /// </summary>
    public List<WorkflowExecutionRecord> GetRecentExecutionHistory(int limit = 100)
    {
        return _executionHistory
            .OrderByDescending(r => r.CompletionTime)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Gets performance summary for all workflows
    /// </summary>
    public WorkflowPerformanceSummary GetPerformanceSummary()
    {
        var allMetrics = _workflowMetrics.Values.ToList();
        
        return new WorkflowPerformanceSummary
        {
            TotalWorkflows = allMetrics.Count,
            TotalExecutions = allMetrics.Sum(m => m.TotalExecutions),
            TotalSuccessfulExecutions = allMetrics.Sum(m => m.SuccessfulExecutions),
            TotalFailedExecutions = allMetrics.Sum(m => m.FailedExecutions),
            TotalVetoedExecutions = allMetrics.Sum(m => m.VetoedExecutions),
            OverallSuccessRate = CalculateOverallSuccessRate(allMetrics),
            AverageExecutionTime = CalculateAverageExecutionTime(allMetrics),
            WorkflowsWithFailures = allMetrics.Count(m => m.FailedExecutions > 0),
            WorkflowsWithConsecutiveFailures = allMetrics.Count(m => m.ConsecutiveFailures > 0),
            GeneratedAt = DateTimeOffset.Now
        };
    }

    /// <summary>
    /// Exports metrics to JSON for external analysis
    /// </summary>
    public string ExportMetricsToJson()
    {
        var exportData = new
        {
            Summary = GetPerformanceSummary(),
            WorkflowMetrics = GetAllWorkflowMetrics(),
            RecentHistory = GetRecentExecutionHistory(500),
            ExportedAt = DateTimeOffset.Now
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Gets or creates metrics for a workflow
    /// </summary>
    private WorkflowMetrics GetOrCreateMetrics(string workflowId, string workflowName)
    {
        return _workflowMetrics.GetOrAdd(workflowId, _ => new WorkflowMetrics
        {
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            CreatedAt = DateTimeOffset.Now
        });
    }

    /// <summary>
    /// Updates duration statistics for a workflow
    /// </summary>
    private void UpdateDurationStatistics(WorkflowMetrics metrics, TimeSpan duration)
    {
        var durationMs = duration.TotalMilliseconds;
        
        if (metrics.MinExecutionTime == TimeSpan.Zero || duration < metrics.MinExecutionTime)
        {
            metrics.MinExecutionTime = duration;
        }
        
        if (duration > metrics.MaxExecutionTime)
        {
            metrics.MaxExecutionTime = duration;
        }
        
        // Calculate average execution time
        var totalExecutions = metrics.SuccessfulExecutions + metrics.FailedExecutions;
        if (totalExecutions > 0)
        {
            metrics.AverageExecutionTime = TimeSpan.FromTicks(metrics.TotalExecutionTime.Ticks / totalExecutions);
        }
    }

    /// <summary>
    /// Calculates overall success rate across all workflows
    /// </summary>
    private double CalculateOverallSuccessRate(List<WorkflowMetrics> allMetrics)
    {
        var totalExecutions = allMetrics.Sum(m => m.SuccessfulExecutions + m.FailedExecutions);
        var totalSuccessful = allMetrics.Sum(m => m.SuccessfulExecutions);
        
        return totalExecutions > 0 ? (double)totalSuccessful / totalExecutions * 100 : 0;
    }

    /// <summary>
    /// Calculates average execution time across all workflows
    /// </summary>
    private TimeSpan CalculateAverageExecutionTime(List<WorkflowMetrics> allMetrics)
    {
        var totalTime = allMetrics.Sum(m => m.TotalExecutionTime.Ticks);
        var totalExecutions = allMetrics.Sum(m => m.SuccessfulExecutions + m.FailedExecutions);
        
        return totalExecutions > 0 ? TimeSpan.FromTicks(totalTime / totalExecutions) : TimeSpan.Zero;
    }
}

/// <summary>
/// Metrics for a specific workflow
/// </summary>
public class WorkflowMetrics
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    
    // Execution counts
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public int VetoedExecutions { get; set; }
    public int ConsecutiveFailures { get; set; }
    
    // Timing information
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    
    // Last execution times
    public DateTimeOffset? LastExecutionTime { get; set; }
    public DateTimeOffset? LastSuccessTime { get; set; }
    public DateTimeOffset? LastFailureTime { get; set; }
    public DateTimeOffset? LastVetoTime { get; set; }
    
    // Error information
    public string? LastErrorMessage { get; set; }
    
    // Calculated properties
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / (SuccessfulExecutions + FailedExecutions) * 100 : 0;
    public bool IsHealthy => ConsecutiveFailures == 0 && SuccessRate >= 90;
}

/// <summary>
/// Overall performance summary across all workflows
/// </summary>
public class WorkflowPerformanceSummary
{
    public int TotalWorkflows { get; set; }
    public int TotalExecutions { get; set; }
    public int TotalSuccessfulExecutions { get; set; }
    public int TotalFailedExecutions { get; set; }
    public int TotalVetoedExecutions { get; set; }
    public double OverallSuccessRate { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public int WorkflowsWithFailures { get; set; }
    public int WorkflowsWithConsecutiveFailures { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}
