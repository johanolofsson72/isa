using Shared.Models;

namespace WebChat.Services.Interfaces;

/// <summary>
/// Service for managing session-based file uploads in WebChat
/// </summary>
public interface ISessionFileService
{
    /// <summary>
    /// Upload a file for a specific session
    /// </summary>
    Task<string> UploadFileAsync(string sessionId, IFormFile file);

    /// <summary>
    /// Get all files for a session
    /// </summary>
    Task<List<SessionFileInfo>> GetSessionFilesAsync(string sessionId);

    /// <summary>
    /// Delete a file from a session
    /// </summary>
    Task<bool> DeleteFileAsync(string sessionId, string fileId);

    /// <summary>
    /// Clear all files for a session (when chat is opened)
    /// </summary>
    Task ClearSessionFilesAsync(string sessionId);

    /// <summary>
    /// Get a specific file for processing
    /// </summary>
    Task<SessionFileInfo?> GetFileAsync(string sessionId, string fileId);

    /// <summary>
    /// Mark all files in session as consumed - clears them from local cache
    /// Files remain in Agent's TempFilesService for automatic cleanup
    /// </summary>
    Task MarkFilesAsConsumedAsync(string sessionId);

    /// <summary>
    /// Mark a specific file as consumed - removes it from local cache
    /// </summary>
    Task MarkFileAsConsumedAsync(string sessionId, string fileId);
}

/// <summary>
/// Information about a file in a session
/// </summary>
public class SessionFileInfo
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; }
    public string FilePath { get; set; } = string.Empty;
}