namespace Shared.Models;

/// <summary>
/// Simplified memory entry for API responses and cross-service communication
/// </summary>
public class MemoryEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Confidence { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Source { get; set; }
}

/// <summary>
/// User information for API responses
/// </summary>
public class UserInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? Locale { get; set; }
}

/// <summary>
/// Device information for API responses
/// </summary>
public class DeviceInfoDto
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public string? PrimaryUserId { get; set; }
}
