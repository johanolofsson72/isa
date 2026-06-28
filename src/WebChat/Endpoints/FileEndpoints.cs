using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Shared.Models;

namespace WebChat.Endpoints;

/// <summary>
/// File upload and management endpoints for WebChat
/// </summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        var fileGroup = app.MapGroup("/api/files")
            .WithTags("Files");

        fileGroup.MapPost("/upload", UploadFile)
            .WithName("UploadFile")
            .WithSummary("Upload a file for processing")
            .Produces<FileUploadResponse>()
            .ProducesValidationProblem()
            .DisableAntiforgery(); // Disable antiforgery for file uploads

        fileGroup.MapGet("/analysis/{fileId}", GetAnalysis)
            .WithName("GetFileAnalysis")
            .WithSummary("Get analysis results for a file")
            .Produces<DocumentAnalysisResponse>()
            .Produces(404);
    }

    private static async Task<IResult> UploadFile(
        HttpRequest request,
        [FromServices] IHttpClientFactory httpClientFactory,
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

            var source = form["source"].FirstOrDefault() ?? "Upload";
            var fileId = Guid.NewGuid();

            logger.LogInformation("Uploading file: {FileName} ({FileSize} bytes) from source: {Source}", 
                file.FileName, file.Length, source);

            // Save file to TempFiles directory
            var tempFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Agent", "TempFiles");
            Directory.CreateDirectory(tempFilesPath);

            var fileName = $"{fileId}_{file.FileName}";
            var filePath = Path.Combine(tempFilesPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            logger.LogInformation("File saved to: {FilePath}", filePath);

            // Notify Agent about the new file via HTTP
            var agentClient = httpClientFactory.CreateClient("agent");
            var fileRecord = new FileRecord
            {
                Id = fileId,
                FileName = file.FileName ?? "unknown",
                FilePath = filePath,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Source = source,
                SourcePath = file.FileName,
                CreatedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(fileRecord);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await agentClient.PostAsync("/api/documents/process", content);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Successfully notified Agent about file: {FileId}", fileId);
                }
                else
                {
                    logger.LogWarning("Failed to notify Agent about file: {FileId}, Status: {StatusCode}", 
                        fileId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error notifying Agent about file: {FileId}", fileId);
            }

            return Results.Ok(new FileUploadResponse
            {
                FileId = fileId.ToString(),
                FileName = file.FileName ?? "unknown",
                FileSize = file.Length,
                ContentType = file.ContentType,
                Source = source,
                Status = "Uploaded",
                Message = "File uploaded successfully and queued for processing"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file");
            return Results.Problem("Error uploading file: " + ex.Message);
        }
    }

    private static async Task<IResult> GetAnalysis(
        string fileId,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            if (!Guid.TryParse(fileId, out var guid))
            {
                return Results.BadRequest("Invalid file ID format");
            }

            logger.LogInformation("Getting analysis for file: {FileId}", fileId);

            // Get analysis from Agent
            var agentClient = httpClientFactory.CreateClient("agent");
            var response = await agentClient.GetAsync($"/api/documents/analysis/{fileId}");

            if (response.IsSuccessStatusCode)
            {
                var analysisJson = await response.Content.ReadAsStringAsync();
                var analysis = JsonSerializer.Deserialize<DocumentAnalysisResponse>(analysisJson);
                return Results.Ok(analysis);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Results.NotFound($"Analysis not found for file: {fileId}");
            }
            else
            {
                logger.LogWarning("Failed to get analysis from Agent for file: {FileId}, Status: {StatusCode}", 
                    fileId, response.StatusCode);
                return Results.Problem($"Failed to get analysis: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting analysis for file: {FileId}", fileId);
            return Results.Problem("Error getting analysis: " + ex.Message);
        }
    }
}

/// <summary>
/// Response model for file upload
/// </summary>
public class FileUploadResponse
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    public string? Source { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response model for document analysis
/// </summary>
public class DocumentAnalysisResponse
{
    public string FileId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string OcrResult { get; set; } = string.Empty;
    public string AiAnalysis { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
