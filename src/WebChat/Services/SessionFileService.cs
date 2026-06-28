using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebChat.Services.Interfaces;
using Shared.Models;

namespace WebChat.Services;

/// <summary>
/// Session-based file management service for WebChat
/// </summary>
public class SessionFileService : ISessionFileService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SessionFileService> _logger;
    private readonly ConcurrentDictionary<string, List<SessionFileInfo>> _sessionFiles = new();

    public SessionFileService(
        IHttpClientFactory httpClientFactory,
        ILogger<SessionFileService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(string sessionId, IFormFile file)
    {
        if (file.Length == 0)
            throw new ArgumentException("File is empty");

        // Upload file to Agent via HTTP API
        var agentClient = _httpClientFactory.CreateClient("agent");

        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);

        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        content.Add(streamContent, "file", file.FileName ?? "unknown");
        content.Add(new StringContent("WebChat"), "source");
        content.Add(new StringContent(file.FileName ?? "unknown"), "sourcePath");

        var response = await agentClient.PostAsync("/api/files/upload", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to upload file to Agent: {response.StatusCode} - {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var uploadResult = JsonSerializer.Deserialize<FileUploadResult>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (uploadResult?.FileId == null)
            throw new Exception("Agent returned invalid response - no FileId");

        // Create session file info for local tracking
        var sessionFile = new SessionFileInfo
        {
            FileId = uploadResult.FileId,
            FileName = file.FileName ?? "unknown",
            FileSize = file.Length,
            ContentType = file.ContentType,
            UploadedAt = DateTime.UtcNow,
            FilePath = "" // Agent manages the path
        };

        // Add to session files for local tracking
        _sessionFiles.AddOrUpdate(
            sessionId,
            new List<SessionFileInfo> { sessionFile },
            (key, existing) =>
            {
                existing.Add(sessionFile);
                return existing;
            });

        _logger.LogInformation("File uploaded for session {SessionId}: {FileName} ({FileSize} bytes) with FileID: {FileId}",
            sessionId, file.FileName, file.Length, uploadResult.FileId);

        return uploadResult.FileId;
    }

    public Task<List<SessionFileInfo>> GetSessionFilesAsync(string sessionId)
    {
        return Task.FromResult(_sessionFiles.GetValueOrDefault(sessionId, new List<SessionFileInfo>()));
    }

    public async Task<bool> DeleteFileAsync(string sessionId, string fileId)
    {
        if (!_sessionFiles.TryGetValue(sessionId, out var files))
            return false;

        var fileToDelete = files.FirstOrDefault(f => f.FileId == fileId);
        if (fileToDelete == null)
            return false;

        // Remove from local session tracking
        files.Remove(fileToDelete);

        // Delete via Agent HTTP API
        try
        {
            var agentClient = _httpClientFactory.CreateClient("agent");
            var response = await agentClient.DeleteAsync($"/api/files/{fileId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to delete file via Agent API: {FileId} - {StatusCode}", fileId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file via Agent API: {FileId}", fileId);
        }

        _logger.LogInformation("File deleted from session {SessionId}: {FileId}", sessionId, fileId);
        return true;
    }

    public async Task ClearSessionFilesAsync(string sessionId)
    {
        if (!_sessionFiles.TryRemove(sessionId, out var files))
            return;

        // Delete all files via Agent HTTP API
        var agentClient = _httpClientFactory.CreateClient("agent");
        foreach (var file in files)
        {
            try
            {
                var response = await agentClient.DeleteAsync($"/api/files/{file.FileId}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to delete file via Agent API: {FileId} - {StatusCode}", file.FileId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file via Agent API: {FileId}", file.FileId);
            }
        }

        _logger.LogInformation("Cleared {Count} files for session {SessionId}", files.Count, sessionId);
    }

    public Task<SessionFileInfo?> GetFileAsync(string sessionId, string fileId)
    {
        if (!_sessionFiles.TryGetValue(sessionId, out var files))
            return Task.FromResult<SessionFileInfo?>(null);

        var file = files.FirstOrDefault(f => f.FileId == fileId);
        return Task.FromResult(file);
    }

    /// <summary>
    /// Mark files as consumed in a dialog - clears them from local session cache
    /// The actual files remain in Agent's TempFilesService for automatic cleanup
    /// </summary>
    public Task MarkFilesAsConsumedAsync(string sessionId)
    {
        if (_sessionFiles.TryRemove(sessionId, out var files))
        {
            _logger.LogInformation("Cleared {FileCount} files from session cache: {SessionId}",
                files.Count, sessionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Mark a specific file as consumed - removes it from local session cache
    /// </summary>
    public Task MarkFileAsConsumedAsync(string sessionId, string fileId)
    {
        if (_sessionFiles.TryGetValue(sessionId, out var files))
        {
            var fileToRemove = files.FirstOrDefault(f => f.FileId == fileId);
            if (fileToRemove != null)
            {
                files.Remove(fileToRemove);
                _logger.LogInformation("Marked file as consumed and removed from session cache: {FileId}", fileId);

                // If no files left, remove the session entry
                if (files.Count == 0)
                {
                    _sessionFiles.TryRemove(sessionId, out _);
                }
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Response from Agent's file upload API
/// </summary>
public class FileUploadResult
{
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
}