using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Services;
using Agent.Data;
using System.Security.Cryptography;
using System.Text;

namespace Agent.Services;

/// <summary>
/// Service for managing temporary files with automatic cleanup
/// Files are automatically deleted after 5 minutes
/// </summary>
public class TempFilesService : ITempFilesService
{
    private readonly FilesDbContext _context;
    private readonly ILogger<TempFilesService> _logger;
    private readonly string _tempFilesPath;
    private readonly DbContextOptions<FilesDbContext> _filesDbOptions;
    private static readonly TimeSpan FILE_EXPIRY = TimeSpan.FromMinutes(30); // Extended for OCR testing

    public TempFilesService(FilesDbContext context, ILogger<TempFilesService> logger, IConfiguration configuration, DbContextOptions<FilesDbContext> filesDbOptions)
    {
        _context = context;
        _logger = logger;
        _filesDbOptions = filesDbOptions;

        // Get TempFiles path from configuration, with fallback to default
        var configuredPath = configuration["TempFiles:Path"];
        if (!string.IsNullOrEmpty(configuredPath))
        {
            // Use configured path (can be absolute or relative)
            _tempFilesPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(Directory.GetCurrentDirectory(), configuredPath);
        }
        else
        {
            // Fallback to default path
            _tempFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles");
        }

        // Ensure TempFiles directory exists
        Directory.CreateDirectory(_tempFilesPath);
        _logger.LogInformation("TempFiles directory configured: {Path} (from config: {ConfigPath})",
            _tempFilesPath, configuredPath ?? "default");
    }

    public async Task<Guid> SaveFileAsync(string fileName, Stream content, string? contentType = null, string? source = null, string? sourcePath = null)
    {
        var fileId = Guid.NewGuid();
        var tempFileName = $"{fileId}_{fileName}";
        var filePath = Path.Combine(_tempFilesPath, tempFileName);

        try
        {
            // Save file to disk
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await content.CopyToAsync(fileStream);
            }

            var fileInfo = new FileInfo(filePath);
            var fileHash = await CalculateFileHashAsync(filePath);

            // Create file record
            var fileRecord = new FileRecord
            {
                Id = fileId,
                FileName = fileName,
                FilePath = filePath,
                ContentType = contentType ?? GetContentTypeFromExtension(Path.GetExtension(fileName)),
                FileSize = fileInfo.Length,
                FileHash = fileHash,
                Source = source,
                SourcePath = sourcePath,
                CreatedAt = DateTime.UtcNow
            };

            // Save to database
            _context.Files.Add(fileRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Saved temp file: {FileId} - {FileName} ({FileSize} bytes) from {Source}", 
                fileId, fileName, fileInfo.Length, source ?? "Unknown");

            return fileId;
        }
        catch (Exception ex)
        {
            // Clean up file if database save failed
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }
            
            _logger.LogError(ex, "Failed to save temp file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Guid> SaveFileAsync(string fileName, byte[] content, string? contentType = null, string? source = null, string? sourcePath = null)
    {
        using var stream = new MemoryStream(content);
        return await SaveFileAsync(fileName, stream, contentType, source, sourcePath);
    }

    public async Task<FileRecord?> GetFileAsync(Guid fileId)
    {
        try
        {
            var fileRecord = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsArchived);
            if (fileRecord == null)
            {
                _logger.LogDebug("File not found in database: {FileId}", fileId);
            }
            else
            {
                _logger.LogDebug("File found in database: {FileId}, Path: {FilePath}, Archived: {IsArchived}",
                    fileId, fileRecord.FilePath, fileRecord.IsArchived);
            }
            return fileRecord;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file from database: {FileId}", fileId);
            return null;
        }
    }

    public async Task<string?> GetFilePathAsync(Guid fileId)
    {
        _logger.LogInformation("🔍 TempFilesService: Looking for file with ID: {FileId}", fileId);

        var fileRecord = await GetFileAsync(fileId);
        if (fileRecord == null)
        {
            _logger.LogWarning("❌ TempFilesService: No file record found in database for ID: {FileId}", fileId);

            // Debug: List all files in database
            var allFiles = await _context.Files.Select(f => new { f.Id, f.FileName }).ToListAsync();
            _logger.LogInformation("📋 TempFilesService: All files in database: {AllFiles}",
                string.Join(", ", allFiles.Select(f => $"{f.Id}:{f.FileName}")));

            return null;
        }

        _logger.LogInformation("✅ TempFilesService: Found file record - ID: {FileId}, FileName: {FileName}, FilePath: {FilePath}",
            fileRecord.Id, fileRecord.FileName, fileRecord.FilePath);

        var fileExists = File.Exists(fileRecord.FilePath);
        _logger.LogInformation("📁 TempFilesService: File path: {FilePath}, Exists: {FileExists}", fileRecord.FilePath, fileExists);

        if (!fileExists)
        {
            return null;
        }

        return fileRecord.FilePath;
    }

    public async Task<byte[]?> ReadFileAsync(Guid fileId)
    {
        var filePath = await GetFilePathAsync(fileId);
        if (filePath == null)
        {
            return null;
        }

        try
        {
            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file: {FileId}", fileId);
            return null;
        }
    }

    public async Task<Stream?> ReadFileStreamAsync(Guid fileId)
    {
        var filePath = await GetFilePathAsync(fileId);
        if (filePath == null)
        {
            return null;
        }

        try
        {
            return new FileStream(filePath, FileMode.Open, FileAccess.Read);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file stream: {FileId}", fileId);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(Guid fileId)
    {
        var fileRecord = await GetFileAsync(fileId);
        if (fileRecord == null)
        {
            return false;
        }

        try
        {
            // Mark as archived in database
            fileRecord.IsArchived = true;
            fileRecord.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Delete physical file
            if (File.Exists(fileRecord.FilePath))
            {
                File.Delete(fileRecord.FilePath);
            }

            _logger.LogInformation("Deleted temp file: {FileId} - {FileName}", fileId, fileRecord.FileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FileId}", fileId);
            return false;
        }
    }

    public async Task<int> CleanupExpiredFilesAsync()
    {
        var cutoffTime = DateTime.UtcNow.Subtract(FILE_EXPIRY);
        var cleanedCount = 0;

        try
        {
            // Process files in smaller batches to avoid concurrency issues
            var batchSize = 10;
            var processedCount = 0;

            while (true)
            {
                var expiredFiles = await _context.Files
                    .Where(f => f.CreatedAt < cutoffTime && !f.IsArchived)
                    .Take(batchSize)
                    .ToListAsync();

                if (!expiredFiles.Any())
                    break;

                foreach (var file in expiredFiles)
                {
                    try
                    {
                        // Use a separate context for each file to avoid concurrency conflicts
                        using var fileContext = new FilesDbContext(_filesDbOptions);
                        var fileToUpdate = await fileContext.Files.FindAsync(file.Id);

                        if (fileToUpdate == null || fileToUpdate.IsArchived)
                            continue; // Already processed by another instance

                        // Delete physical file
                        if (File.Exists(fileToUpdate.FilePath))
                        {
                            File.Delete(fileToUpdate.FilePath);
                        }

                        // Mark as archived
                        fileToUpdate.IsArchived = true;
                        fileToUpdate.UpdatedAt = DateTime.UtcNow;

                        await fileContext.SaveChangesAsync();
                        cleanedCount++;
                    }
                    catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
                    {
                        // Another process already updated this file, skip it
                        _logger.LogDebug("File {FileId} was already processed by another cleanup instance", file.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to cleanup file: {FileId} - {FileName}", file.Id, file.FileName);
                    }
                }

                processedCount += expiredFiles.Count;

                // Safety break to avoid infinite loops
                if (processedCount > 1000)
                {
                    _logger.LogWarning("Cleanup processed more than 1000 files, stopping to prevent runaway cleanup");
                    break;
                }
            }

            if (cleanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired temp files", cleanedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during temp files cleanup");
        }

        return cleanedCount;
    }

    public async Task<List<FileRecord>> GetAllFilesAsync()
    {
        return await _context.Files
            .Where(f => !f.IsArchived)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await md5.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetContentTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
