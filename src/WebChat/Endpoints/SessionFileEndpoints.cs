using Microsoft.AspNetCore.Mvc;
using WebChat.Services.Interfaces;

namespace WebChat.Endpoints;

/// <summary>
/// Session-based file upload endpoints for WebChat
/// </summary>
public static class SessionFileEndpoints
{
    public static void MapSessionFileEndpoints(this WebApplication app)
    {
        var fileGroup = app.MapGroup("/api/session-files")
            .WithTags("Session Files");

        fileGroup.MapPost("/upload/{sessionId}", UploadFile)
            .WithName("UploadSessionFile")
            .WithSummary("Upload a file for a specific session")
            .Produces<SessionFileUploadResponse>()
            .ProducesValidationProblem()
            .DisableAntiforgery();

        fileGroup.MapGet("/list/{sessionId}", ListFiles)
            .WithName("ListSessionFiles")
            .WithSummary("List files for a session")
            .Produces<List<SessionFileInfo>>();

        fileGroup.MapDelete("/delete/{sessionId}/{fileId}", DeleteFile)
            .WithName("DeleteSessionFile")
            .WithSummary("Delete a file from a session")
            .Produces<DeleteFileResponse>();

        fileGroup.MapDelete("/clear/{sessionId}", ClearFiles)
            .WithName("ClearSessionFiles")
            .WithSummary("Clear all files for a session")
            .Produces<ClearFilesResponse>();
    }

    private static async Task<IResult> UploadFile(
        string sessionId,
        HttpRequest request,
        [FromServices] ISessionFileService sessionFileService,
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

            logger.LogInformation("Uploading file for session {SessionId}: {FileName} ({FileSize} bytes)",
                sessionId, file.FileName, file.Length);

            var fileId = await sessionFileService.UploadFileAsync(sessionId, file);

            return Results.Ok(new SessionFileUploadResponse
            {
                FileId = fileId,
                FileName = file.FileName ?? "unknown",
                FileSize = file.Length,
                ContentType = file.ContentType,
                Status = "Success",
                Message = "File uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file for session {SessionId}", sessionId);
            return Results.Problem("Error uploading file: " + ex.Message);
        }
    }

    private static async Task<IResult> ListFiles(
        string sessionId,
        [FromServices] ISessionFileService sessionFileService)
    {
        var files = await sessionFileService.GetSessionFilesAsync(sessionId);
        return Results.Ok(files);
    }

    private static async Task<IResult> DeleteFile(
        string sessionId,
        string fileId,
        [FromServices] ISessionFileService sessionFileService,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            var deleted = await sessionFileService.DeleteFileAsync(sessionId, fileId);

            if (deleted)
            {
                logger.LogInformation("File deleted: {FileId} from session {SessionId}", fileId, sessionId);
                return Results.Ok(new DeleteFileResponse
                {
                    Success = true,
                    Message = "File deleted successfully"
                });
            }

            return Results.NotFound(new DeleteFileResponse
            {
                Success = false,
                Message = "File not found"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file {FileId} from session {SessionId}", fileId, sessionId);
            return Results.Problem("Error deleting file: " + ex.Message);
        }
    }

    private static async Task<IResult> ClearFiles(
        string sessionId,
        [FromServices] ISessionFileService sessionFileService,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            await sessionFileService.ClearSessionFilesAsync(sessionId);

            logger.LogInformation("Files cleared for session {SessionId}", sessionId);
            return Results.Ok(new ClearFilesResponse
            {
                Success = true,
                Message = "All files cleared successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing files for session {SessionId}", sessionId);
            return Results.Problem("Error clearing files: " + ex.Message);
        }
    }
}

/// <summary>
/// Response for session file upload
/// </summary>
public class SessionFileUploadResponse
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response for file deletion
/// </summary>
public class DeleteFileResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response for clearing files
/// </summary>
public class ClearFilesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}