using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Agent.Data;
using Agent.Services;
using Shared.Services;
using System.Text.Json;

namespace DocumentInterpreterPlugin.Tests;

/// <summary>
/// Debug test to understand the JSON structure returned by DocumentInterpreterPlugin
/// </summary>
public class DebugJsonStructureTest : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentInterpreter.Plugin _plugin;
    private readonly ITempFilesService _tempFilesService;
    private readonly ILogger<DebugJsonStructureTest> _logger;
    private readonly string _testDbPath;

    public DebugJsonStructureTest()
    {
        // Create a shared SQLite database file for testing
        _testDbPath = Path.Combine(Path.GetTempPath(), $"debug_test_{Guid.NewGuid()}.db");
        
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TempFiles:Path"] = Path.Combine(Directory.GetCurrentDirectory(), "DebugTempFiles"),
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
        _logger = loggerFactory.CreateLogger<DebugJsonStructureTest>();
    }

    [Fact]
    public async Task DebugJsonStructure_WithSampleFile()
    {
        // Use the first file from ekonomi directory
        var ekonomiPath = "/path/to/accountingagent/ekonomi";
        var testFiles = Directory.GetFiles(ekonomiPath, "*.pdf", SearchOption.TopDirectoryOnly)
            .Take(1)
            .ToList();

        if (!testFiles.Any())
        {
            _logger.LogWarning("No PDF files found in ekonomi directory");
            return;
        }

        var filePath = testFiles.First();
        var fileName = Path.GetFileName(filePath);
        
        _logger.LogInformation($"Testing with file: {fileName}");

        try
        {
            // Upload file to TempFilesService
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var contentType = "application/pdf";
            
            var fileId = await _tempFilesService.SaveFileAsync(
                fileName, 
                fileBytes, 
                contentType, 
                "DebugTest", 
                filePath);

            _logger.LogInformation($"File uploaded with ID: {fileId}");

            // Process with DocumentInterpreterPlugin
            var result = await _plugin.InterpretDocumentFromFileAsync(
                fileId.ToString(), 
                null, // Auto-detect document type
                $"DebugTest:{fileName}");

            _logger.LogInformation("=== RAW JSON RESULT ===");
            _logger.LogInformation(result);
            _logger.LogInformation("=== END RAW JSON ===");

            // Try to parse and show structure
            var parsedResult = JsonSerializer.Deserialize<JsonElement>(result);
            
            _logger.LogInformation("=== JSON PROPERTIES ===");
            foreach (var property in parsedResult.EnumerateObject())
            {
                _logger.LogInformation($"Property: {property.Name} = {property.Value}");
            }
            _logger.LogInformation("=== END PROPERTIES ===");

            // Test different access patterns
            _logger.LogInformation("=== TESTING ACCESS PATTERNS ===");
            
            // Pattern 1: Direct access
            if (parsedResult.TryGetProperty("documentType", out var docType1))
            {
                _logger.LogInformation($"✅ Direct documentType: {docType1.GetString()}");
            }
            else
            {
                _logger.LogInformation("❌ Direct documentType not found");
            }

            if (parsedResult.TryGetProperty("confidenceScore", out var confidence1))
            {
                _logger.LogInformation($"✅ Direct confidenceScore: {confidence1.GetDouble()}");
            }
            else
            {
                _logger.LogInformation("❌ Direct confidenceScore not found");
            }

            // Pattern 2: Nested access
            if (parsedResult.TryGetProperty("document", out var docElement))
            {
                _logger.LogInformation("✅ Found 'document' property");
                if (docElement.TryGetProperty("documentType", out var docType2))
                {
                    _logger.LogInformation($"✅ Nested document.documentType: {docType2.GetString()}");
                }
                else
                {
                    _logger.LogInformation("❌ Nested document.documentType not found");
                }
            }
            else
            {
                _logger.LogInformation("❌ 'document' property not found");
            }

            _logger.LogInformation("=== END ACCESS PATTERNS ===");

            // This should not fail - just for verification
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to process file: {fileName}");
            throw;
        }
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
