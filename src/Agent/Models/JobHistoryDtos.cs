namespace Agent.Models;

/// <summary>
/// DTO for job execution record in API responses
/// </summary>
public class JobExecutionRecordDto
{
    public int Id { get; set; }
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string TriggerKey { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public string TriggerGroup { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? DurationMs { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? WorkflowId { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRunning { get; set; }
    public bool IsSuccessful { get; set; }
    public bool IsFailed { get; set; }
}

/// <summary>
/// DTO for paginated job execution history response
/// </summary>
public class JobExecutionHistoryResponse
{
    public List<JobExecutionRecordDto> Records { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// DTO for job statistics in API responses
/// </summary>
public class JobStatisticsDto
{
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string? WorkflowId { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public int CancelledExecutions { get; set; }
    public int VetoedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public long MinExecutionTimeMs { get; set; }
    public long MaxExecutionTimeMs { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public string? LastExecutionStatus { get; set; }
    public DateTime? NextScheduledTime { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsHealthy { get; set; }
    public bool HasRecentActivity { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
}

/// <summary>
/// DTO for performance metrics across all jobs
/// </summary>
public class PerformanceMetricsDto
{
    public int TotalJobs { get; set; }
    public int ActiveExecutions { get; set; }
    public int TotalExecutions { get; set; }
    public int TotalSuccessfulExecutions { get; set; }
    public int TotalFailedExecutions { get; set; }
    public double OverallSuccessRate { get; set; }
    public double AverageExecutionTime { get; set; }
    public int HealthyJobs { get; set; }
    public int UnhealthyJobs { get; set; }
    public int JobsWithRecentActivity { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// DTO for scheduler health status
/// </summary>
public class SchedulerHealthDto
{
    public bool IsHealthy { get; set; }
    public int ActiveExecutions { get; set; }
    public List<string> CurrentlyRunningJobs { get; set; } = new();
    public DateTime LastChecked { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO for currently active job executions
/// </summary>
public class ActiveExecutionDto
{
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public string? WorkflowId { get; set; }
}
