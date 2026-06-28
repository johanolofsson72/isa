using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Agent.Models;

/// <summary>
/// Represents a single job execution record for tracking and auditing purposes
/// </summary>
[Table("JobExecutions")]
public class JobExecutionRecord
{
    /// <summary>
    /// Unique identifier for this execution record
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Quartz job key (combination of name and group)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string JobKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable job name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Quartz job group
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>
    /// Quartz trigger key that fired this execution
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string TriggerKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable trigger name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TriggerName { get; set; } = string.Empty;

    /// <summary>
    /// Quartz trigger group
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TriggerGroup { get; set; } = string.Empty;

    /// <summary>
    /// When the job execution started
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the job execution completed (null if still running)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Total execution duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Execution status
    /// </summary>
    [Required]
    public JobExecutionStatus Status { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace if execution failed
    /// </summary>
    [MaxLength(4000)]
    public string? StackTrace { get; set; }

    /// <summary>
    /// Associated workflow ID from YAML configuration
    /// </summary>
    [MaxLength(100)]
    public string? WorkflowId { get; set; }

    /// <summary>
    /// When this execution was originally scheduled to run
    /// </summary>
    public DateTime? ScheduledTime { get; set; }

    /// <summary>
    /// When this record was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    [MaxLength(1000)]
    public string? Metadata { get; set; }

    /// <summary>
    /// Calculated property for execution duration
    /// </summary>
    [NotMapped]
    public TimeSpan? Duration => DurationMs.HasValue ? TimeSpan.FromMilliseconds(DurationMs.Value) : null;

    /// <summary>
    /// Calculated property to check if execution is still running
    /// </summary>
    [NotMapped]
    public bool IsRunning => Status == JobExecutionStatus.Running;

    /// <summary>
    /// Calculated property to check if execution was successful
    /// </summary>
    [NotMapped]
    public bool IsSuccessful => Status == JobExecutionStatus.Success;

    /// <summary>
    /// Calculated property to check if execution failed
    /// </summary>
    [NotMapped]
    public bool IsFailed => Status == JobExecutionStatus.Failed;
}

/// <summary>
/// Possible job execution statuses
/// </summary>
public enum JobExecutionStatus
{
    /// <summary>
    /// Job is currently running
    /// </summary>
    Running = 0,

    /// <summary>
    /// Job completed successfully
    /// </summary>
    Success = 1,

    /// <summary>
    /// Job failed with an error
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Job was cancelled before completion
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Job execution was vetoed by a listener
    /// </summary>
    Vetoed = 4
}
