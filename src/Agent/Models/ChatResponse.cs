namespace Agent.Models;

/// <summary>
/// Response model for chat API endpoint
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// The agent's response message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Session ID for maintaining conversation context
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the response
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if there was an error
    /// </summary>
    public bool HasError { get; set; }

    /// <summary>
    /// Error message if any
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}
