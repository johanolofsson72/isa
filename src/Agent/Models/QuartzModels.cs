namespace Agent.Models;

/// <summary>
/// Sammanfattning av alla Quartz jobb och scheduler status.
/// </summary>
public class QuartzJobSummary
{
    public int TotalJobs { get; set; }
    public int RunningJobs { get; set; }
    public int PausedJobs { get; set; }
    public string SchedulerName { get; set; } = string.Empty;
    public string SchedulerInstanceId { get; set; } = string.Empty;
    public bool IsStarted { get; set; }
    public List<QuartzJobInfo> Jobs { get; set; } = new();
}

/// <summary>
/// Information om ett Quartz jobb.
/// </summary>
public class QuartzJobInfo
{
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JobClass { get; set; } = string.Empty;
    public List<QuartzTriggerInfo> Triggers { get; set; } = new();
}

/// <summary>
/// Detaljerad information om ett Quartz jobb.
/// </summary>
public class QuartzJobDetail
{
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JobClass { get; set; } = string.Empty;
    public bool IsDurable { get; set; }
    public bool RequestsRecovery { get; set; }
    public Dictionary<string, string?> JobDataMap { get; set; } = new();
    public List<QuartzTriggerInfo> Triggers { get; set; } = new();
}

/// <summary>
/// Information om en Quartz trigger.
/// </summary>
public class QuartzTriggerInfo
{
    public string TriggerKey { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public string TriggerGroup { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? NextFireTime { get; set; }
    public string? PreviousFireTime { get; set; }
    public string? CronExpression { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int Priority { get; set; }
    public int MisfireInstruction { get; set; }
}

/// <summary>
/// Status för Quartz scheduler.
/// </summary>
public class QuartzSchedulerStatus
{
    public string SchedulerName { get; set; } = string.Empty;
    public string SchedulerInstanceId { get; set; } = string.Empty;
    public bool IsStarted { get; set; }
    public bool IsInStandbyMode { get; set; }
    public bool IsShutdown { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? RunningSince { get; set; }
    public int NumberOfJobsExecuted { get; set; }
    public string? JobStoreClass { get; set; }
    public string? ThreadPoolClass { get; set; }
    public int ThreadPoolSize { get; set; }
    public int TotalJobs { get; set; }
    public List<string> JobGroups { get; set; } = new();
}

/// <summary>
/// Request för att skapa ett nytt Quartz jobb.
/// </summary>
public class CreateQuartzJobRequest
{
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = "workflows";
    public string Description { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public Dictionary<string, string> JobData { get; set; } = new();
}

/// <summary>
/// Response för job execution resultat.
/// </summary>
public class QuartzJobExecutionResult
{
    public string JobKey { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object?> Result { get; set; } = new();
}

/// <summary>
/// Historik för job executions.
/// </summary>
public class QuartzJobExecutionHistory
{
    public string JobKey { get; set; } = string.Empty;
    public List<QuartzJobExecutionResult> Executions { get; set; } = new();
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double AverageDurationMs { get; set; }
    public DateTime? LastExecution { get; set; }
    public DateTime? NextExecution { get; set; }
}
