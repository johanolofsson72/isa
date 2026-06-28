using System.ComponentModel.DataAnnotations;

namespace Agent.Models;

/// <summary>
/// Entity for audit logging of chat interactions and workflow executions
/// </summary>
public class AuditLog
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Type of event (ChatMessage, WorkflowExecution, PluginExecution, etc.)
    /// </summary>
    [Required]
    public string EventType { get; set; } = string.Empty;
    
    /// <summary>
    /// SignalR connection ID or other session identifier
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// User message or action description
    /// </summary>
    public string? UserInput { get; set; }
    
    /// <summary>
    /// AI response or result
    /// </summary>
    public string? AIResponse { get; set; }
    
    /// <summary>
    /// Workflow ID if applicable
    /// </summary>
    public string? WorkflowId { get; set; }
    
    /// <summary>
    /// Plugin name if applicable
    /// </summary>
    public string? PluginName { get; set; }
    
    /// <summary>
    /// Function name if applicable
    /// </summary>
    public string? FunctionName { get; set; }
    
    /// <summary>
    /// Success, Error, Warning, etc.
    /// </summary>
    [Required]
    public string Status { get; set; } = "Success";
    
    /// <summary>
    /// Error message if status is Error
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }
    
    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }
}
