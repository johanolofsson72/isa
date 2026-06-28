using System.ComponentModel.DataAnnotations;

namespace Agent.Models;

/// <summary>
/// Request model for chat API endpoint
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// The user's message
    /// </summary>
    [Required]
    [StringLength(10000, ErrorMessage = "Message cannot exceed 10,000 characters")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Session ID for maintaining conversation context
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Device fingerprint for user identification
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// User information for context
    /// </summary>
    public UserInfo? UserInfo { get; set; }
}

/// <summary>
/// User information for chat context
/// </summary>
public class UserInfo
{
    public string? Browser { get; set; }
    public string? IP { get; set; }
    public string? UserAgent { get; set; }
}
