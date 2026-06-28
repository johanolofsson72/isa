namespace WebChat.Services.Interfaces;

/// <summary>
/// Interface for querying Agent's memory system for personal information
/// </summary>
public interface IMemoryQueryService
{
    /// <summary>
    /// Get user information and personal details for device fingerprint
    /// </summary>
    /// <param name="deviceFingerprint">Device fingerprint to query for</param>
    /// <returns>Personal information if found, null otherwise</returns>
    Task<PersonalInfo?> GetPersonalInfoAsync(string deviceFingerprint);
}

/// <summary>
/// Personal information extracted from memory system
/// </summary>
public class PersonalInfo
{
    public string? UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
}
