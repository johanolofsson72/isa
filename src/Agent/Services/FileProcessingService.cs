using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Shared.Services;
using Shared.Models;
using DocumentInterpreter;

namespace Agent.Services;

/// <summary>
/// Service for processing file references in messages and interpreting documents
/// </summary>
public class FileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;
    private readonly ITempFilesService _tempFilesService;
    private readonly IConfiguration _configuration;

    public FileProcessingService(
        ILogger<FileProcessingService> logger,
        ITempFilesService tempFilesService,
        IConfiguration configuration)
    {
        _logger = logger;
        _tempFilesService = tempFilesService;
        _configuration = configuration;
    }

    /// <summary>
    /// Process file references in user message and replace with document analysis results
    /// </summary>
    public async Task<string> ProcessFileReferencesInMessageAsync(string userMessage)
    {
        try
        {
            _logger.LogInformation("🔍 FILE_PROC: Processing message for file references: {Message}", userMessage);

            // Look for file ID pattern: [FILER: {fileId1}, {fileId2}, ...]
            var filePattern = @"\[FILER:\s*([^\]]+)\]";
            var match = Regex.Match(userMessage, filePattern);

            _logger.LogInformation("🔍 FILE_PROC: Regex match success: {Success}", match.Success);

            if (!match.Success)
            {
                _logger.LogInformation("🔍 FILE_PROC: No file references found in message");
                return userMessage; // No file references found
            }

            var fileIdsString = match.Groups[1].Value;
            var fileIdStrings = fileIdsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            _logger.LogInformation("🔍 FILE_PROC: Found {Count} file references in pattern '{Pattern}': {FileIds}",
                fileIdStrings.Count, fileIdsString, string.Join(", ", fileIdStrings));

            var processedResults = new List<string>();

            foreach (var fileIdString in fileIdStrings)
            {
                if (Guid.TryParse(fileIdString, out var fileId))
                {
                    _logger.LogInformation("🔍 FILE_PROC: Processing file ID: {FileId}", fileId);

                    // Get file record from TempFilesService with retry logic for timing issues
                    var fileRecord = await GetFileWithRetryAsync(fileId);
                    if (fileRecord == null)
                    {
                        _logger.LogWarning("❌ FILE_PROC: File not found after retries: {FileId}", fileId);
                        processedResults.Add($"[FIL INTE HITTAD: {fileId}]");
                        continue;
                    }

                    _logger.LogInformation("✅ FILE_PROC: Found file: {FileName} ({FileId})", fileRecord.FileName, fileId);

                    // Process file via DocumentInterpreter
                    _logger.LogInformation("🔄 FILE_PROC: Starting DocumentInterpreter processing for: {FileName}", fileRecord.FileName);
                    var analysisResult = await ProcessFileViaDocumentInterpreterAsync(fileRecord);

                    if (!string.IsNullOrEmpty(analysisResult))
                    {
                        _logger.LogInformation("✅ FILE_PROC: Successfully processed file: {FileName} (result length: {Length})",
                            fileRecord.FileName, analysisResult.Length);
                        processedResults.Add($"[DOKUMENT: {fileRecord.FileName}]\n{analysisResult}");
                    }
                    else
                    {
                        _logger.LogWarning("❌ FILE_PROC: DocumentInterpreter returned empty result for file: {FileName}", fileRecord.FileName);
                        processedResults.Add($"[KUNDE INTE TOLKA: {fileRecord.FileName}]");
                    }
                }
                else
                {
                    _logger.LogWarning("❌ FILE_PROC: Invalid file ID format: {FileIdString}", fileIdString);
                    processedResults.Add($"[OGILTIGT FIL-ID: {fileIdString}]");
                }
            }

            // Replace file reference with processed results
            var processedContent = string.Join("\n\n", processedResults);
            var processedMessage = Regex.Replace(userMessage, filePattern, processedContent);

            _logger.LogInformation("🔍 FILE_PROC: Processed message length: {OriginalLength} -> {ProcessedLength}",
                userMessage.Length, processedMessage.Length);

            return processedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FILE_PROC: Error processing file references in message");
            return userMessage; // Return original message on error
        }
    }

    /// <summary>
    /// Get file with retry logic to handle timing issues between WebChat upload and database sync
    /// </summary>
    private async Task<FileRecord?> GetFileWithRetryAsync(Guid fileId, int maxRetries = 3, int delayMs = 1000)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            _logger.LogInformation("🔍 FILE_PROC: Attempt {Attempt}/{MaxRetries} to get file: {FileId}", attempt, maxRetries, fileId);

            var fileRecord = await _tempFilesService.GetFileAsync(fileId);
            if (fileRecord != null)
            {
                _logger.LogInformation("✅ FILE_PROC: File found on attempt {Attempt}: {FileId}", attempt, fileId);
                return fileRecord;
            }

            if (attempt < maxRetries)
            {
                _logger.LogInformation("⏳ FILE_PROC: File not found, waiting {DelayMs}ms before retry {NextAttempt}/{MaxRetries}",
                    delayMs, attempt + 1, maxRetries);
                await Task.Delay(delayMs);
            }
        }

        _logger.LogWarning("❌ FILE_PROC: File not found after {MaxRetries} attempts: {FileId}", maxRetries, fileId);
        return null;
    }

    /// <summary>
    /// Process file via DocumentInterpreter plugin directly
    /// </summary>
    private async Task<string?> ProcessFileViaDocumentInterpreterAsync(FileRecord file)
    {
        try
        {
            _logger.LogInformation("🔍 FILE_PROC: Processing file {FileName} (ID: {FileId}) via DocumentInterpreter",
                file.FileName, file.Id);

            // Validate file path
            if (string.IsNullOrEmpty(file.FilePath))
            {
                _logger.LogError("❌ FILE_PROC: File path is null or empty for file: {FileName} (ID: {FileId})",
                    file.FileName, file.Id);
                return null;
            }

            if (!File.Exists(file.FilePath))
            {
                _logger.LogError("❌ FILE_PROC: File does not exist at path: {FilePath} for file: {FileName} (ID: {FileId})",
                    file.FilePath, file.FileName, file.Id);
                return null;
            }

            var fileInfo = new FileInfo(file.FilePath);
            _logger.LogInformation("🔍 FILE_PROC: File validated - Path: {FilePath}, Size: {Size} bytes, LastModified: {LastModified}",
                file.FilePath, fileInfo.Length, fileInfo.LastWriteTime);

            // First, log which extraction techniques are available
            _logger.LogInformation("📄 FILE_PROC: Starting DocumentInterpreter processing for: {FileName}", file.FileName);
            _logger.LogInformation("🔍 FILE_PROC: Checking available extraction techniques...");

            var jsonResult = await CallDocumentInterpreterWithDiagnosticsAsync(
                file.FilePath,
                file.FileName
            );

            if (!string.IsNullOrEmpty(jsonResult))
            {
                _logger.LogInformation("✅ FILE_PROC: DocumentInterpreter success - File: {FileName}, Result length: {Length} chars",
                    file.FileName, jsonResult.Length);

                // Post-process result to improve quality for known good extractions
                var improvedResult = PostProcessDocumentResult(jsonResult, file.FileName);

                if (improvedResult != jsonResult)
                {
                    _logger.LogInformation("🔧 FILE_PROC: Applied post-processing improvements to result");
                }

                return improvedResult;
            }
            else
            {
                _logger.LogWarning("❌ FILE_PROC: DocumentInterpreter returned null or empty result for file: {FileName}", file.FileName);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FILE_PROC: Failed to process file {FileName} (ID: {FileId}) via DocumentInterpreter",
                file.FileName, file.Id);
            return null;
        }
    }

    /// <summary>
    /// Call DocumentInterpreter with diagnostics to understand which techniques are being used
    /// </summary>
    private async Task<string?> CallDocumentInterpreterWithDiagnosticsAsync(
        string filePath,
        string fileName)
    {
        try
        {
            _logger.LogInformation("🔍 FILE_PROC: Creating DocumentInterpreter instance with diagnostics");

            // Create DocumentInterpreter instance with enhanced logging
            var logger = new LoggerWrapper<Plugin>(_logger);
            var documentInterpreterPlugin = new Plugin(_configuration, logger);

            // Log configuration status for Azure services
            var azureDocEndpoint = _configuration["Azure:DocumentIntelligence:Endpoint"];
            var azureDocEnabled = _configuration.GetValue<bool>("Azure:DocumentIntelligence:EnableDocumentOcr", false);
            var azureVisionEndpoint = _configuration["Azure:ComputerVision:Endpoint"];
            var azureVisionEnabled = _configuration.GetValue<bool>("Azure:ComputerVision:EnableAdvancedOcr", false);

            _logger.LogInformation("🔧 FILE_PROC: Azure Document Intelligence - Endpoint: {Endpoint}, Enabled: {Enabled}",
                string.IsNullOrEmpty(azureDocEndpoint) ? "NOT_CONFIGURED" : "CONFIGURED", azureDocEnabled);
            _logger.LogInformation("🔧 FILE_PROC: Azure Computer Vision - Endpoint: {Endpoint}, Enabled: {Enabled}",
                string.IsNullOrEmpty(azureVisionEndpoint) ? "NOT_CONFIGURED" : "CONFIGURED", azureVisionEnabled);

            _logger.LogInformation("📄 FILE_PROC: Calling DocumentInterpreter for: {FileName}", fileName);

            var result = await documentInterpreterPlugin.InterpretDocumentFromFileAsync(
                filePath,
                null, // documentType - let plugin auto-detect
                $"WebChat-Diagnostics-{DateTime.UtcNow:HHmmssfff}"
            );

            if (!string.IsNullOrEmpty(result))
            {
                _logger.LogInformation("✅ FILE_PROC: DocumentInterpreter returned result - Length: {Length}", result.Length);

                // Analyze result quality
                var quality = AnalyzeResultQuality(result);
                _logger.LogInformation("📊 FILE_PROC: Result quality score: {Quality:F2}", quality);

                // Log general quality indicators (NO HARDCODING)
                var lowerResult = result.ToLower();
                var hasNumbers = System.Text.RegularExpressions.Regex.IsMatch(result, @"\d+[,\.]\d+");
                var hasAmounts = lowerResult.Contains("sek") || lowerResult.Contains("kr") || lowerResult.Contains("€") || lowerResult.Contains("$");
                var hasDates = System.Text.RegularExpressions.Regex.IsMatch(result, @"\d{1,2}[\s\-\.\/]\w+[\s\-\.\/]\d{4}");
                var hasMissingFields = lowerResult.Contains("ej angivet") || lowerResult.Contains("saknas") || lowerResult.Contains("0,00");

                _logger.LogInformation("🔍 FILE_PROC: Content indicators - Numbers: {Numbers}, Amounts: {Amounts}, Dates: {Dates}, Missing: {Missing}",
                    hasNumbers, hasAmounts, hasDates, hasMissingFields);

                return result;
            }
            else
            {
                _logger.LogWarning("⚠️ FILE_PROC: DocumentInterpreter returned empty result");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FILE_PROC: DocumentInterpreter failed for: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Analyze the quality of DocumentInterpreter result
    /// </summary>
    private double AnalyzeResultQuality(string result)
    {
        if (string.IsNullOrEmpty(result))
            return 0.0;

        double quality = 0.0;

        // Check for key indicators of successful extraction
        var lowerResult = result.ToLower();

        // Positive indicators
        if (lowerResult.Contains("fakturanummer") || lowerResult.Contains("ordernummer") || lowerResult.Contains("invoice"))
            quality += 0.2;
        if (lowerResult.Contains("datum") || lowerResult.Contains("date"))
            quality += 0.2;
        if (lowerResult.Contains("belopp") || lowerResult.Contains("total") || lowerResult.Contains("amount"))
            quality += 0.2;
        if (lowerResult.Contains("moms") || lowerResult.Contains("vat") || lowerResult.Contains("tax"))
            quality += 0.1;
        if (lowerResult.Contains("leverantör") || lowerResult.Contains("vendor") || lowerResult.Contains("supplier"))
            quality += 0.1;

        // General quality indicators (NO HARDCODING)
        if (System.Text.RegularExpressions.Regex.IsMatch(result, @"\d+[,\.]\d+"))
            quality += 0.2; // Contains numeric amounts
        if (System.Text.RegularExpressions.Regex.IsMatch(result, @"\d{1,2}[\s\-\.\/]\w+[\s\-\.\/]\d{4}"))
            quality += 0.2; // Contains proper dates
        if (System.Text.RegularExpressions.Regex.IsMatch(result, @"#?\d{6,}"))
            quality += 0.2; // Contains order/invoice numbers

        // Negative indicators (more aggressive penalties)
        if (lowerResult.Contains("ej angivet") || lowerResult.Contains("saknas"))
            quality -= 0.5; // Increased penalty
        if (lowerResult.Contains("0,00") || lowerResult.Contains("0.00"))
            quality -= 0.4; // Increased penalty
        if (lowerResult.Contains("kritiska fält saknas") || lowerResult.Contains("critical fields missing"))
            quality -= 0.6; // Increased penalty
        if (lowerResult.Contains("too many critical fields missing"))
            quality -= 0.7; // Heavy penalty for this specific error

        // Length bonus (longer results are generally better)
        if (result.Length > 1000)
            quality += 0.2;
        else if (result.Length > 500)
            quality += 0.1;

        return Math.Max(0.0, Math.Min(1.0, quality)); // Clamp between 0 and 1
    }

    /// <summary>
    /// Post-process DocumentInterpreter result to improve quality and fix common issues
    /// </summary>
    private string PostProcessDocumentResult(string originalResult, string fileName)
    {
        try
        {
            var result = originalResult;

            // For Paddle.com files specifically, improve the result if we have good data
            if (fileName.ToLower().Contains("paddle"))
            {
                var lowerResult = result.ToLower();

                // If we have the key Paddle.com data, improve the presentation
                bool hasOrderNumber = lowerResult.Contains("6502658-34956036");
                bool hasCorrectAmount = lowerResult.Contains("368.41") || lowerResult.Contains("368,41");
                bool hasPaddle = lowerResult.Contains("paddle");
                bool hasProduct = lowerResult.Contains("cleanmymac");

                // NO HARDCODING - This was wrong approach
                // The real problem needs to be fixed in DocumentInterpreter itself
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post-process document result, returning original");
            return originalResult;
        }
    }
}

/// <summary>
/// Simple logger wrapper that forwards to the main logger
/// </summary>
public class LoggerWrapper<T> : ILogger<T>
{
    private readonly ILogger _logger;

    public LoggerWrapper(ILogger logger)
    {
        _logger = logger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);
}
