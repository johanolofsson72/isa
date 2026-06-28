using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DocumentInterpreter.Models;
using Agent.Data;
using Agent.Services;
using Shared.Services;
using System.Text.Json;

namespace DocumentInterpreterPlugin.Tests;

/// <summary>
/// End-to-end tests for DocumentInterpreterPlugin using all 35 files from ekonomi directory
/// </summary>
public class E2EDocumentProcessingTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentInterpreter.Plugin _plugin;
    private readonly ITempFilesService _tempFilesService;
    private readonly ILogger<E2EDocumentProcessingTests> _logger;
    private readonly string _ekonomiPath;
    private readonly List<string> _testFiles;
    private readonly string _testDbPath;

    public E2EDocumentProcessingTests()
    {
        // Create a shared SQLite database file for testing
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_files_{Guid.NewGuid()}.db");

        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TempFiles:Path"] = Path.Combine(Directory.GetCurrentDirectory(), "TestTempFiles"),
                ["ConnectionStrings:FilesConnection"] = $"Data Source={_testDbPath}"
            })
            .Build();

        // Setup services
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Add database context with shared connection
        services.AddDbContext<FilesDbContext>(options =>
            options.UseSqlite($"Data Source={_testDbPath}"));

        // Add TempFilesService
        services.AddScoped<ITempFilesService, TempFilesService>();

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        context.Database.EnsureCreated();

        // Create plugin instance
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        _plugin = new DocumentInterpreter.Plugin(configuration, loggerFactory.CreateLogger<DocumentInterpreter.Plugin>());
        DocumentInterpreter.Plugin.ServiceProvider = _serviceProvider;

        _tempFilesService = _serviceProvider.GetRequiredService<ITempFilesService>();
        _logger = loggerFactory.CreateLogger<E2EDocumentProcessingTests>();

        // Setup test files path - use absolute path to ekonomi directory
        _ekonomiPath = "/path/to/accountingagent/ekonomi";
        
        // Get all test files
        _testFiles = Directory.GetFiles(_ekonomiPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("."))
            .OrderBy(f => f)
            .ToList();

        _logger.LogInformation($"Found {_testFiles.Count} test files in {_ekonomiPath}");
    }

    [Fact]
    public async Task ProcessAllEkonomiFiles_ShouldSucceed()
    {
        var results = new List<DocumentProcessingResult>();
        var successCount = 0;
        var failureCount = 0;

        _logger.LogInformation($"Starting E2E test with {_testFiles.Count} files");

        foreach (var filePath in _testFiles)
        {
            var fileName = Path.GetFileName(filePath);
            _logger.LogInformation($"Processing file: {fileName}");

            try
            {
                var result = await ProcessSingleFile(filePath);
                results.Add(result);

                if (result.Success)
                {
                    successCount++;
                    _logger.LogInformation($"✅ SUCCESS: {fileName} - {result.DocumentType}");
                }
                else
                {
                    failureCount++;
                    _logger.LogError($"❌ FAILED: {fileName} - {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                var errorResult = new DocumentProcessingResult
                {
                    FileName = fileName,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTimeMs = 0
                };
                results.Add(errorResult);
                _logger.LogError(ex, $"❌ EXCEPTION: {fileName}");
            }
        }

        // Generate summary report
        GenerateSummaryReport(results, successCount, failureCount);

        // Assert that at least 80% of files were processed successfully
        var successRate = (double)successCount / _testFiles.Count;
        Assert.True(successRate >= 0.8, 
            $"Success rate {successRate:P} is below 80%. {successCount}/{_testFiles.Count} files processed successfully.");
    }

    private async Task<DocumentProcessingResult> ProcessSingleFile(string filePath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var fileName = Path.GetFileName(filePath);

        try
        {
            // Upload file to TempFilesService
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var contentType = GetContentType(fileName);
            
            var fileId = await _tempFilesService.SaveFileAsync(
                fileName, 
                fileBytes, 
                contentType, 
                "E2ETest", 
                filePath);

            // Process with DocumentInterpreterPlugin
            var result = await _plugin.InterpretDocumentFromFileAsync(
                fileId.ToString(), 
                null, // Auto-detect document type
                $"E2ETest:{fileName}");

            stopwatch.Stop();

            // Parse result to validate structure
            var parsedResult = JsonSerializer.Deserialize<JsonElement>(result);
            var documentType = parsedResult.GetProperty("documentType").GetString();
            var confidence = parsedResult.GetProperty("confidence").GetDouble();

            return new DocumentProcessingResult
            {
                FileName = fileName,
                Success = true,
                DocumentType = documentType ?? "Unknown",
                ConfidenceScore = confidence,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                FileId = fileId,
                ResultJson = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new DocumentProcessingResult
            {
                FileName = fileName,
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private void GenerateSummaryReport(List<DocumentProcessingResult> results, int successCount, int failureCount)
    {
        _logger.LogInformation("=== E2E TEST SUMMARY REPORT ===");
        _logger.LogInformation($"Total files: {results.Count}");
        _logger.LogInformation($"Successful: {successCount} ({(double)successCount / results.Count:P})");
        _logger.LogInformation($"Failed: {failureCount} ({(double)failureCount / results.Count:P})");
        
        if (successCount > 0)
        {
            var avgProcessingTime = results.Where(r => r.Success).Average(r => r.ProcessingTimeMs);
            var avgConfidence = results.Where(r => r.Success).Average(r => r.ConfidenceScore);
            _logger.LogInformation($"Average processing time: {avgProcessingTime:F0}ms");
            _logger.LogInformation($"Average confidence: {avgConfidence:P}");
        }

        // Group by document type
        var documentTypes = results.Where(r => r.Success)
            .GroupBy(r => r.DocumentType)
            .OrderByDescending(g => g.Count());

        _logger.LogInformation("Document types detected:");
        foreach (var group in documentTypes)
        {
            _logger.LogInformation($"  {group.Key}: {group.Count()} files");
        }

        // List failed files
        if (failureCount > 0)
        {
            _logger.LogInformation("Failed files:");
            foreach (var failure in results.Where(r => !r.Success))
            {
                _logger.LogInformation($"  {failure.FileName}: {failure.ErrorMessage}");
            }
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tiff" or ".tif" => "image/tiff",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        // Clean up test database
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

public class DocumentProcessingResult
{
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Guid? FileId { get; set; }
    public string ResultJson { get; set; } = string.Empty;
}
