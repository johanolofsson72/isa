using Shared.Models;

namespace Shared.Services;

/// <summary>
/// Service for managing temporary files with automatic cleanup
/// </summary>
public interface ITempFilesService
{
    /// <summary>
    /// Saves a file to the temporary storage and returns a unique file ID
    /// </summary>
    /// <param name="fileName">Original filename</param>
    /// <param name="content">File content as stream</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="source">Source of the file (e.g., "Dropbox", "GoogleDrive")</param>
    /// <param name="sourcePath">Original source path</param>
    /// <returns>Unique file ID for later retrieval</returns>
    Task<Guid> SaveFileAsync(string fileName, Stream content, string? contentType = null, string? source = null, string? sourcePath = null);

    /// <summary>
    /// Saves a file to the temporary storage from byte array
    /// </summary>
    /// <param name="fileName">Original filename</param>
    /// <param name="content">File content as byte array</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="source">Source of the file (e.g., "Dropbox", "GoogleDrive")</param>
    /// <param name="sourcePath">Original source path</param>
    /// <returns>Unique file ID for later retrieval</returns>
    Task<Guid> SaveFileAsync(string fileName, byte[] content, string? contentType = null, string? source = null, string? sourcePath = null);

    /// <summary>
    /// Gets file information by ID
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <returns>File record or null if not found</returns>
    Task<FileRecord?> GetFileAsync(Guid fileId);

    /// <summary>
    /// Gets the physical file path for a file ID
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <returns>Full path to the file or null if not found</returns>
    Task<string?> GetFilePathAsync(Guid fileId);

    /// <summary>
    /// Reads file content as byte array
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <returns>File content or null if not found</returns>
    Task<byte[]?> ReadFileAsync(Guid fileId);

    /// <summary>
    /// Reads file content as stream
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <returns>File stream or null if not found</returns>
    Task<Stream?> ReadFileStreamAsync(Guid fileId);

    /// <summary>
    /// Deletes a file by ID
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteFileAsync(Guid fileId);

    /// <summary>
    /// Cleans up expired files (older than 5 minutes)
    /// </summary>
    /// <returns>Number of files cleaned up</returns>
    Task<int> CleanupExpiredFilesAsync();

    /// <summary>
    /// Gets all files (for admin/debugging purposes)
    /// </summary>
    /// <returns>List of all file records</returns>
    Task<List<FileRecord>> GetAllFilesAsync();
}
