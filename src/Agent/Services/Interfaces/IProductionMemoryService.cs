using Agent.Models;

namespace Agent.Services.Interfaces;

/// <summary>
/// Core memory service for production-ready identity and memory management
/// </summary>
public interface IProductionMemoryService
{
    /// <summary>
    /// Generate system prompt with memory context for a specific device/user
    /// </summary>
    /// <param name="deviceFingerprint">Device identifier from cookie</param>
    /// <param name="baseSystemPrompt">Base system prompt to enhance with memory context</param>
    /// <returns>Complete system prompt with memory context</returns>
    Task<string> GenerateSystemPromptAsync(string? deviceFingerprint = null, string? baseSystemPrompt = null);

    /// <summary>
    /// Store a memory entry with embedding generation
    /// </summary>
    /// <param name="entry">Memory entry to store</param>
    Task StoreMemoryAsync(MemoryEntry entry);

    /// <summary>
    /// Retrieve memories using similarity search with thresholds
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="type">Memory type to filter by</param>
    /// <param name="threshold">Similarity threshold</param>
    /// <param name="topK">Maximum number of results</param>
    /// <param name="userId">User ID for access control</param>
    /// <returns>List of matching memories</returns>
    Task<List<MemoryEntry>> RetrieveMemoriesAsync(
        string query, 
        MemoryType type, 
        double threshold, 
        int topK = 5,
        string? userId = null);

    /// <summary>
    /// Consolidate and cleanup expired memories
    /// </summary>
    Task ConsolidateAndCleanupAsync();

    /// <summary>
    /// Get or create user based on device fingerprint
    /// </summary>
    /// <param name="deviceFingerprint">Device identifier</param>
    /// <returns>User information</returns>
    Task<User> GetOrCreateUserAsync(string deviceFingerprint);

    /// <summary>
    /// Find user by display name (for name-based identification)
    /// </summary>
    /// <param name="displayName">User's display name</param>
    /// <returns>User entity if found, null otherwise</returns>
    Task<User?> FindUserByNameAsync(string displayName);

    /// <summary>
    /// Update user information
    /// </summary>
    /// <param name="user">User to update</param>
    Task UpdateUserAsync(User user);

    /// <summary>
    /// Get user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User entity if found, null otherwise</returns>
    Task<User?> GetUserByIdAsync(string userId);

    /// <summary>
    /// Link device to existing user (for name-based identification)
    /// </summary>
    /// <param name="deviceFingerprint">Device identifier</param>
    /// <param name="userId">User ID to link to</param>
    Task LinkDeviceToUserAsync(string deviceFingerprint, string userId);

    /// <summary>
    /// Consolidate device fingerprints when the same user is identified from multiple devices
    /// </summary>
    /// <param name="currentDeviceFingerprint">Current device fingerprint</param>
    /// <param name="userId">User ID</param>
    /// <returns>Recommended device fingerprint to use, or null if no consolidation needed</returns>
    Task<string?> ConsolidateDeviceFingerprintAsync(string currentDeviceFingerprint, string userId);

    /// <summary>
    /// Check if user has given consent for specific operation
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="consentKind">Type of consent</param>
    /// <returns>True if consent granted</returns>
    Task<bool> HasConsentAsync(string userId, string consentKind);

    /// <summary>
    /// Update user consent
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="consentKind">Type of consent</param>
    /// <param name="granted">Whether consent is granted</param>
    Task UpdateConsentAsync(string userId, string consentKind, bool granted);

    /// <summary>
    /// Get or create user runtime state
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User runtime state</returns>
    Task<UserRuntimeState> GetOrCreateUserRuntimeStateAsync(string userId);

    /// <summary>
    /// Update user runtime state
    /// </summary>
    /// <param name="state">Runtime state to update</param>
    Task UpdateUserRuntimeStateAsync(UserRuntimeState state);

    /// <summary>
    /// Get all users who have given consent for a specific operation
    /// </summary>
    /// <param name="consentKind">Type of consent</param>
    /// <returns>List of users with consent</returns>
    Task<List<User>> GetUsersWithConsentAsync(string consentKind);

    /// <summary>
    /// Get memory count for user (for debugging)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of memories for the user</returns>
    Task<int> GetMemoryCountForUserAsync(string userId);
}