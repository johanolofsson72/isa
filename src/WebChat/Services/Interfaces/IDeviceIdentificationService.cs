namespace WebChat.Services.Interfaces;

/// <summary>
/// Interface for device identification service
/// </summary>
public interface IDeviceIdentificationService
{
    /// <summary>
    /// Get or create device ID from isa_did cookie
    /// </summary>
    Task<string> GetOrCreateDeviceIdAsync();

    /// <summary>
    /// Check if this is the first time we see this device (for greeting)
    /// </summary>
    Task<bool> IsFirstTimeSeenAsync();

    /// <summary>
    /// Mark device as seen (sets isa_seen cookie)
    /// </summary>
    Task MarkDeviceAsSeenAsync();

    /// <summary>
    /// Get profile version for cache invalidation
    /// </summary>
    Task<int> GetProfileVersionAsync();

    /// <summary>
    /// Update profile version for cache invalidation
    /// </summary>
    Task UpdateProfileVersionAsync(int version);
}
