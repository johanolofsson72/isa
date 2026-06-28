using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AnalysisPlugin.Tests.Integration;

/// <summary>
/// Integration tests for the complete AnalysisPlugin workflow
/// </summary>
public class PluginIntegrationTests
{
    private readonly global::AnalysisPlugin.AnalysisPlugin _plugin;
    private readonly ILogger<global::AnalysisPlugin.AnalysisPlugin> _logger;

    public PluginIntegrationTests()
    {
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AnalysisPlugin:ReportArchiveDirectory"] = Path.GetTempPath(),
                ["AnalysisPlugin:DefaultHistoricalYears"] = "5",
                ["AnalysisPlugin:SimpleReportTimeoutSeconds"] = "60",
                ["AnalysisPlugin:ComplexReportTimeoutSeconds"] = "180",
                ["AnalysisPlugin:HistoricalReportTimeoutSeconds"] = "300",
                ["AnalysisPlugin:MLAnalysisTimeoutSeconds"] = "420",
                ["AnalysisPlugin:ReportJobTTLSeconds"] = "3600"
            })
            .Build();

        // Setup logger
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<global::AnalysisPlugin.AnalysisPlugin>();

        // Create plugin instance
        _plugin = new global::AnalysisPlugin.AnalysisPlugin(configuration, _logger);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task CreateReportJobAsync_ValidSIE4Data_ReturnsJobId()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act
        var result = await _plugin.CreateReportJobAsync(sie4Data, "sv");

        // Assert
        Assert.NotNull(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(response.TryGetProperty("reportJobId", out var jobIdElement));
        Assert.False(string.IsNullOrEmpty(jobIdElement.GetString()));
        
        Assert.True(response.TryGetProperty("status", out var statusElement));
        Assert.Equal("created", statusElement.GetString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task CreateReportJobAsync_InvalidSIE4Data_ReturnsError()
    {
        // Arrange
        var invalidSie4Data = "invalid data";

        // Act
        var result = await _plugin.CreateReportJobAsync(invalidSie4Data, "sv");

        // Assert
        Assert.NotNull(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(response.TryGetProperty("success", out var successElement));
        Assert.False(successElement.GetBoolean());
        
        Assert.True(response.TryGetProperty("error", out var errorElement));
        Assert.Equal("INVALID_SIE4_DATA", errorElement.GetString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task FullReportWorkflow_CompleteReport_GeneratesFile()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act - Create report job
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act - Add front page
        var frontPageResult = await _plugin.AddFrontPageAsync(
            reportJobId, 
            "Test Financial Report", 
            "Integration Test", 
            "Test Company AB");
        
        var frontPageResponse = JsonSerializer.Deserialize<JsonElement>(frontPageResult);
        Assert.True(frontPageResponse.GetProperty("success").GetBoolean());

        // Act - Add chart
        var chartResult = await _plugin.AddChartAsync(
            reportJobId,
            "revenue_trend",
            "Revenue Trend Chart",
            "historical");

        var chartResponse = JsonSerializer.Deserialize<JsonElement>(chartResult);
        Assert.True(chartResponse.GetProperty("success").GetBoolean());

        // Act - Add table
        var tableResult = await _plugin.AddTableAsync(
            reportJobId,
            "monthly_summary",
            "Monthly Summary Table",
            true);

        var tableResponse = JsonSerializer.Deserialize<JsonElement>(tableResult);
        Assert.True(tableResponse.GetProperty("success").GetBoolean());

        // Act - Finish report
        var finishResult = await _plugin.FinishReportAsync(reportJobId);
        var finishResponse = JsonSerializer.Deserialize<JsonElement>(finishResult);

        // Assert
        Assert.True(finishResponse.GetProperty("success").GetBoolean());
        Assert.True(finishResponse.TryGetProperty("filePath", out var filePathElement));
        
        var filePath = filePathElement.GetString();
        Assert.NotNull(filePath);
        
        // Verify file exists (should now create actual .xlsx file)
        Assert.True(File.Exists(filePath), $"Expected file to exist at: {filePath}");

        // Verify it's a valid Excel file by checking file size
        var fileInfo = new FileInfo(filePath);
        Assert.True(fileInfo.Length > 0, "Generated file should not be empty");

        // Cleanup
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static string GetTestSIE4Data()
    {
        return @"#FLAGGA 0
#PROGRAM ""Test Program"" 1.0
#FORMAT PC8
#GEN 20241107
#SIETYP 4
#KONTO 3000 ""Försäljning""
#KONTO 6000 ""Kontorsmaterial""
#VER ""A"" ""1"" 20241101 ""Test transaction""
#TRANS 3000 {} -10000.00 20241101 ""Revenue""
#TRANS 6000 {} 1000.00 20241101 ""Office supplies""
";
    }
}
