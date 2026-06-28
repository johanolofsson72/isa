using System.ComponentModel.DataAnnotations;

namespace Agent.Models;

/// <summary>
/// Represents workflow execution history and status
/// </summary>
public class WorkflowExecution
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for this execution instance
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the workflow that was executed
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Path to the workflow file
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string WorkflowPath { get; set; } = string.Empty;

    /// <summary>
    /// When the workflow execution started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the workflow execution completed (null if still running)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Current status of the workflow execution
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Running"; // Running, Completed, Failed, Cancelled

    /// <summary>
    /// Trigger that started this execution (Manual, Scheduled, Event)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Trigger { get; set; } = "Manual";

    /// <summary>
    /// User who triggered the workflow (if manual)
    /// </summary>
    [MaxLength(100)]
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// Total number of steps in the workflow
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Number of completed steps
    /// </summary>
    public int CompletedSteps { get; set; }

    /// <summary>
    /// Number of failed steps
    /// </summary>
    public int FailedSteps { get; set; }

    /// <summary>
    /// Overall result message
    /// </summary>
    public string? ResultMessage { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Execution log and step details in JSON format
    /// </summary>
    public string? ExecutionLog { get; set; }
}
