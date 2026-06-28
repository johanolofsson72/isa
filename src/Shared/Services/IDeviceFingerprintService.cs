namespace Shared.Services;

/// <summary>
/// Unified interface for device fingerprint management across Agent and WebChat
/// </summary>
public interface IDeviceFingerprintService
{
    /// <summary>
    /// Get existing device fingerprint from HTTP context (cookies)
    /// </summary>
    /// <returns>Device fingerprint if found, null otherwise</returns>
    string? GetDeviceFingerprint();

    /// <summary>
    /// Get or create device fingerprint with robust persistence strategy
    /// </summary>
    /// <returns>Device fingerprint (existing or newly created)</returns>
    Task<string> GetOrCreateDeviceFingerprintAsync();

    /// <summary>
    /// Create or update device fingerprint cookie
    /// </summary>
    /// <returns>Device fingerprint (existing or newly created)</returns>
    string CreateOrUpdateDeviceFingerprint();

    /// <summary>
    /// Check if this is the first time we see this device (for greeting)
    /// </summary>
    Task<bool> IsFirstTimeSeenAsync();

    /// <summary>
    /// Mark device as seen (sets isa_seen cookie)
    /// </summary>
    Task MarkDeviceAsSeenAsync();

    /// <summary>
    /// Increment page view counter
    /// </summary>
    Task IncrementPageViewAsync();

    /// <summary>
    /// Get profile version for cache invalidation
    /// </summary>
    Task<int> GetProfileVersionAsync();

    /// <summary>
    /// Update profile version for cache invalidation
    /// </summary>
    Task UpdateProfileVersionAsync(int version);
}
