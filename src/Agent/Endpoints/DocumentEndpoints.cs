using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.Text.Json;
using Agent.Data;
using Shared.Models;
using DocumentInterpreter;

namespace Agent.Endpoints;

/// <summary>
/// Document processing endpoints for Agent
/// </summary>
public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var docGroup = app.MapGroup("/api/documents")
            .WithTags("Documents")
            .WithOpenApi();

        docGroup.MapPost("/process", ProcessDocument)
            .WithName("ProcessDocument")
            .WithSummary("Process a document with DocumentInterpreterPlugin")
            .Produces<DocumentProcessResponse>()
            .ProducesValidationProblem();

        docGroup.MapGet("/status/{fileId}", GetProcessingStatus)
            .WithName("GetDocumentStatus")
            .WithSummary("Get processing status for a document")
            .Produces<DocumentStatusResponse>()
            .Produces(404);

        docGroup.MapGet("/analysis/{fileId}", GetDocumentAnalysis)
            .WithName("GetDocumentAnalysis")
            .WithSummary("Get analysis results for a document")
            .Produces<DocumentAnalysisResponse>()
            .Produces(404);
    }

    private static async Task<IResult> ProcessDocument(
        [FromBody] FileRecord fileRecord,
        [FromServices] FilesDbContext filesDbContext,
        [FromServices] Kernel kernel,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            logger.LogInformation("Processing document: {FileId} - {FileName}", fileRecord.Id, fileRecord.FileName);

            // Check if file record already exists, if not add it
            var existingFile = await filesDbContext.Files.FindAsync(fileRecord.Id);
            if (existingFile == null)
            {
                filesDbContext.Files.Add(fileRecord);
                await filesDbContext.SaveChangesAsync();
                logger.LogInformation("Added new file record to database: {FileId}", fileRecord.Id);
            }
            else
            {
                logger.LogInformation("File record already exists in database: {FileId}", fileRecord.Id);
                fileRecord = existingFile; // Use existing record
            }

            // Create initial processing result
            var processingResult = new ProcessingResult
            {
                FileId = fileRecord.Id,
                ProcessingType = ProcessingType.DocumentInterpreter,
                Status = ProcessingStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };

            filesDbContext.ProcessingResults.Add(processingResult);
            await filesDbContext.SaveChangesAsync();

            // Process document in background
            _ = Task.Run(async () => await ProcessDocumentAsync(fileRecord, processingResult.Id, filesDbContext, kernel, logger));

            return Results.Ok(new DocumentProcessResponse
            {
                FileId = fileRecord.Id.ToString(),
                Status = "Processing",
                Message = "Document processing started"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting document processing for file: {FileId}", fileRecord.Id);
            return Results.Problem("Error starting document processing: " + ex.Message);
        }
    }

    private static async Task ProcessDocumentAsync(
        FileRecord fileRecord,
        Guid processingResultId,
        FilesDbContext filesDbContext,
        Kernel kernel,
        ILogger<string> logger)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            logger.LogInformation("Starting background processing for file: {FileId}", fileRecord.Id);

            // Process with DocumentInterpreterPlugin using file ID (TempFilesService will resolve it)
            var result = await kernel.InvokeAsync("DocumentInterpreter", "InterpretDocumentFromFile",
                new KernelArguments
                {
                    ["filePath"] = fileRecord.Id.ToString(), // Pass file ID as string - plugin will resolve via TempFilesService
                    ["documentType"] = null, // Let plugin auto-detect document type
                    ["source"] = "WebChat" // Source
                });

            var jsonResult = result.GetValue<string>();

            // Update processing result
            using var scope = new ServiceCollection()
                .AddDbContext<FilesDbContext>(options => 
                    options.UseSqlite("Data Source=files.db"))
                .BuildServiceProvider()
                .CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
            var processingResult = await dbContext.ProcessingResults.FindAsync(processingResultId);
            
            if (processingResult != null)
            {
                processingResult.Status = ProcessingStatus.Completed;
                processingResult.Result = jsonResult ?? "{}";
                processingResult.ProcessedAt = DateTime.UtcNow;
                processingResult.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                processingResult.QualityScore = 85; // Default quality score
                processingResult.ConfidenceLevel = 80; // Default confidence

                await dbContext.SaveChangesAsync();
            }

            // Update file record
            var fileRecordToUpdate = await dbContext.Files.FindAsync(fileRecord.Id);
            if (fileRecordToUpdate != null)
            {
                fileRecordToUpdate.IsProcessed = true;
                fileRecordToUpdate.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            logger.LogInformation("Successfully processed document: {FileId}", fileRecord.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document: {FileId}", fileRecord.Id);

            // Update processing result with error
            try
            {
                using var scope = new ServiceCollection()
                    .AddDbContext<FilesDbContext>(options => 
                        options.UseSqlite("Data Source=files.db"))
                    .BuildServiceProvider()
                    .CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var processingResult = await dbContext.ProcessingResults.FindAsync(processingResultId);
                
                if (processingResult != null)
                {
                    processingResult.Status = ProcessingStatus.Failed;
                    processingResult.ErrorMessage = ex.Message;
                    processingResult.StackTrace = ex.StackTrace;
                    processingResult.ProcessedAt = DateTime.UtcNow;
                    processingResult.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx, "Error updating processing result with error for file: {FileId}", fileRecord.Id);
            }
        }
    }

    private static async Task<IResult> GetProcessingStatus(
        string fileId,
        [FromServices] FilesDbContext filesDbContext,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            if (!Guid.TryParse(fileId, out var guid))
            {
                return Results.BadRequest("Invalid file ID format");
            }

            var processingResult = await filesDbContext.ProcessingResults
                .Where(pr => pr.FileId == guid && pr.ProcessingType == ProcessingType.DocumentInterpreter)
                .OrderByDescending(pr => pr.ProcessedAt)
                .FirstOrDefaultAsync();

            if (processingResult == null)
            {
                return Results.NotFound($"No processing status found for file: {fileId}");
            }

            return Results.Ok(new DocumentStatusResponse
            {
                FileId = fileId,
                Status = processingResult.Status,
                ProcessedAt = processingResult.ProcessedAt,
                DurationMs = processingResult.DurationMs,
                ErrorMessage = processingResult.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting processing status for file: {FileId}", fileId);
            return Results.Problem("Error getting processing status: " + ex.Message);
        }
    }

    private static async Task<IResult> GetDocumentAnalysis(
        string fileId,
        [FromServices] FilesDbContext filesDbContext,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            if (!Guid.TryParse(fileId, out var guid))
            {
                return Results.BadRequest("Invalid file ID format");
            }

            var processingResult = await filesDbContext.ProcessingResults
                .Where(pr => pr.FileId == guid && pr.ProcessingType == ProcessingType.DocumentInterpreter)
                .OrderByDescending(pr => pr.ProcessedAt)
                .FirstOrDefaultAsync();

            if (processingResult == null)
            {
                return Results.NotFound($"No analysis found for file: {fileId}");
            }

            if (processingResult.Status != ProcessingStatus.Completed)
            {
                return Results.Ok(new DocumentAnalysisResponse
                {
                    FileId = fileId,
                    ProcessingStatus = processingResult.Status,
                    ErrorMessage = processingResult.ErrorMessage
                });
            }

            // Parse the result JSON
            var analysisResult = JsonSerializer.Deserialize<JsonElement>(processingResult.Result ?? "{}");

            // Extract OCR text from nested structure
            var ocrText = "No text extracted";
            if (analysisResult.TryGetProperty("extractedData", out var extractedData))
            {
                if (extractedData.TryGetProperty("rawText", out var rawTextElement))
                {
                    ocrText = rawTextElement.GetString() ?? "No text extracted";
                }
            }

            // Create AI analysis from the full result
            var aiAnalysisJson = JsonSerializer.Serialize(analysisResult, new JsonSerializerOptions { WriteIndented = true });

            return Results.Ok(new DocumentAnalysisResponse
            {
                FileId = fileId,
                DocumentType = GetJsonProperty(analysisResult, "documentType", "Unknown"),
                OcrResult = ocrText,
                AiAnalysis = aiAnalysisJson,
                ProcessingStatus = processingResult.Status,
                ProcessedAt = processingResult.ProcessedAt,
                ErrorMessage = processingResult.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting document analysis for file: {FileId}", fileId);
            return Results.Problem("Error getting document analysis: " + ex.Message);
        }
    }

    private static string GetJsonProperty(JsonElement element, string propertyName, string defaultValue)
    {
        try
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                return property.GetString() ?? defaultValue;
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }
        return defaultValue;
    }
}

/// <summary>
/// Response model for document processing
/// </summary>
public class DocumentProcessResponse
{
    public string FileId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response model for document status
/// </summary>
public class DocumentStatusResponse
{
    public string FileId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public long? DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response model for document analysis (shared with WebChat)
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
