using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Agent.Models;

/// <summary>
/// Aggregated statistics for job performance and monitoring
/// </summary>
[Table("JobStatistics")]
public class JobStatistics
{
    /// <summary>
    /// Quartz job key (primary key)
    /// </summary>
    [Key]
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
    /// Associated workflow ID from YAML configuration
    /// </summary>
    [MaxLength(100)]
    public string? WorkflowId { get; set; }

    /// <summary>
    /// Total number of executions recorded
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Number of successful executions
    /// </summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>
    /// Number of failed executions
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Number of cancelled executions
    /// </summary>
    public int CancelledExecutions { get; set; }

    /// <summary>
    /// Number of vetoed executions
    /// </summary>
    public int VetoedExecutions { get; set; }

    /// <summary>
    /// Average execution time in milliseconds
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Minimum execution time in milliseconds
    /// </summary>
    public long MinExecutionTimeMs { get; set; }

    /// <summary>
    /// Maximum execution time in milliseconds
    /// </summary>
    public long MaxExecutionTimeMs { get; set; }

    /// <summary>
    /// When the last execution occurred
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// Status of the last execution
    /// </summary>
    public JobExecutionStatus? LastExecutionStatus { get; set; }

    /// <summary>
    /// When the next execution is scheduled
    /// </summary>
    public DateTime? NextScheduledTime { get; set; }

    /// <summary>
    /// When these statistics were last updated
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When these statistics were first created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Calculated success rate as percentage (0-100)
    /// </summary>
    [NotMapped]
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;

    /// <summary>
    /// Calculated failure rate as percentage (0-100)
    /// </summary>
    [NotMapped]
    public double FailureRate => TotalExecutions > 0 ? (double)FailedExecutions / TotalExecutions * 100 : 0;

    /// <summary>
    /// Average execution time as TimeSpan
    /// </summary>
    [NotMapped]
    public TimeSpan AverageExecutionTime => TimeSpan.FromMilliseconds(AverageExecutionTimeMs);

    /// <summary>
    /// Minimum execution time as TimeSpan
    /// </summary>
    [NotMapped]
    public TimeSpan MinExecutionTime => TimeSpan.FromMilliseconds(MinExecutionTimeMs);

    /// <summary>
    /// Maximum execution time as TimeSpan
    /// </summary>
    [NotMapped]
    public TimeSpan MaxExecutionTime => TimeSpan.FromMilliseconds(MaxExecutionTimeMs);

    /// <summary>
    /// Check if job is currently healthy (good success rate)
    /// </summary>
    [NotMapped]
    public bool IsHealthy => SuccessRate >= 80 && TotalExecutions >= 5;

    /// <summary>
    /// Check if job has recent activity (executed within last 24 hours)
    /// </summary>
    [NotMapped]
    public bool HasRecentActivity => LastExecutionTime.HasValue && 
                                   LastExecutionTime.Value > DateTime.UtcNow.AddHours(-24);

    /// <summary>
    /// Get health status description
    /// </summary>
    [NotMapped]
    public string HealthStatus
    {
        get
        {
            if (TotalExecutions == 0) return "No Data";
            if (SuccessRate >= 95) return "Excellent";
            if (SuccessRate >= 80) return "Good";
            if (SuccessRate >= 60) return "Warning";
            return "Critical";
        }
    }
}
