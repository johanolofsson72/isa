using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Models;

namespace Agent.Endpoints;

/// <summary>
/// File upload and management endpoints for Agent
/// </summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        var fileGroup = app.MapGroup("/api/files")
            .WithTags("Files")
            .WithOpenApi();

        fileGroup.MapPost("/upload", UploadFile)
            .WithName("UploadFile")
            .WithSummary("Upload a file to Agent")
            .Produces<FileUploadResult>()
            .ProducesValidationProblem()
            .DisableAntiforgery(); // Disable antiforgery for file uploads

        fileGroup.MapDelete("/{fileId}", DeleteFile)
            .WithName("DeleteFile")
            .WithSummary("Delete a file from Agent")
            .Produces(204)
            .Produces(404);

        fileGroup.MapGet("/{fileId}", GetFile)
            .WithName("GetFile")
            .WithSummary("Get file information")
            .Produces<FileRecord>()
            .Produces(404);
    }

    private static async Task<IResult> UploadFile(
        HttpRequest request,
        [FromServices] ITempFilesService tempFilesService,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest("Request must be multipart/form-data");
            }

            var form = await request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file provided");
            }

            var source = form["source"].FirstOrDefault() ?? "WebChat";
            var sourcePath = form["sourcePath"].FirstOrDefault() ?? file.FileName;

            logger.LogInformation("Uploading file: {FileName} ({FileSize} bytes) from source: {Source}", 
                file.FileName, file.Length, source);

            // Save file using TempFilesService
            using var fileStream = file.OpenReadStream();
            var fileId = await tempFilesService.SaveFileAsync(
                file.FileName ?? "unknown",
                fileStream,
                file.ContentType,
                source,
                sourcePath
            );

            logger.LogInformation("File uploaded successfully with ID: {FileId}", fileId);

            return Results.Ok(new FileUploadResult
            {
                FileId = fileId.ToString(),
                FileName = file.FileName ?? "unknown",
                FileSize = file.Length
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file");
            return Results.Problem("Error uploading file: " + ex.Message);
        }
    }

    private static async Task<IResult> DeleteFile(
        string fileId,
        [FromServices] ITempFilesService tempFilesService,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            if (!Guid.TryParse(fileId, out var guidFileId))
            {
                return Results.BadRequest("Invalid file ID format");
            }

            await tempFilesService.DeleteFileAsync(guidFileId);
            logger.LogInformation("File deleted: {FileId}", fileId);
            
            return Results.NoContent();
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound($"File not found: {fileId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file: {FileId}", fileId);
            return Results.Problem("Error deleting file: " + ex.Message);
        }
    }

    private static async Task<IResult> GetFile(
        string fileId,
        [FromServices] ITempFilesService tempFilesService,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            if (!Guid.TryParse(fileId, out var guidFileId))
            {
                return Results.BadRequest("Invalid file ID format");
            }

            var fileRecord = await tempFilesService.GetFileAsync(guidFileId);
            if (fileRecord == null)
            {
                return Results.NotFound($"File not found: {fileId}");
            }

            return Results.Ok(fileRecord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file: {FileId}", fileId);
            return Results.Problem("Error getting file: " + ex.Message);
        }
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
