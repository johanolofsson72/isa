namespace Agent.Services.Interfaces;

/// <summary>
/// Interface for managing online search functionality with consent and daily limits
/// </summary>
public interface IOnlineSearchService
{
    /// <summary>
    /// Check if user can perform online search today
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>True if user can search today</returns>
    Task<bool> CanSearchTodayAsync(string userId);

    /// <summary>
    /// Perform online search for user with consent and limits
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="searchQuery">Search query</param>
    /// <param name="deviceFingerprint">Device fingerprint</param>
    /// <returns>Search results or null if not allowed</returns>
    Task<string?> SearchForUserAsync(string userId, string searchQuery, string? deviceFingerprint = null);

    /// <summary>
    /// Request consent for online search from user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Consent request message</returns>
    Task<string> RequestOnlineSearchConsentAsync(string userId);

    /// <summary>
    /// Process user consent response
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="response">User's response</param>
    /// <returns>Response message</returns>
    Task<string> ProcessConsentResponseAsync(string userId, string response);

    /// <summary>
    /// Confirm or reject search results
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="confirm">True to confirm, false to reject</param>
    /// <returns>Confirmation message</returns>
    Task<string> ConfirmSearchResultsAsync(string userId, bool confirm);

    /// <summary>
    /// Get search status for user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Search status message</returns>
    Task<string> GetSearchStatusAsync(string userId);
}
