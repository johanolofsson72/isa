namespace Agent.Models;

/// <summary>
/// Result of workflow discovery operation
/// Contains information about all workflows found in the file system
/// </summary>
public class WorkflowDiscoveryResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalWorkflowsFound { get; set; }
    public int ScheduledWorkflowsCount { get; set; }
    public int WorkflowsRequiringMigration { get; set; }
    public List<WorkflowInfo> DiscoveredWorkflows { get; set; } = new();
    public List<WorkflowFileError> ErrorFiles { get; set; } = new();
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// Information about a discovered workflow
/// </summary>
public class WorkflowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Schedule { get; set; }
    public bool IsScheduled { get; set; }
    public bool RequiresMigration { get; set; }
    public string ExecutionType { get; set; } = "manual";
    public int JobCount { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Error information for workflow files that couldn't be processed
/// </summary>
public class WorkflowFileError
{
    public string FilePath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTimeOffset ErrorTime { get; set; } = DateTimeOffset.Now;
}
