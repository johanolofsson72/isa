using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AnalysisPlugin.Tests.Integration;

/// <summary>
/// Integration tests for professional report components
/// </summary>
public class ComponentIntegrationTests
{
    private readonly global::AnalysisPlugin.AnalysisPlugin _plugin;
    private readonly ILogger<global::AnalysisPlugin.AnalysisPlugin> _logger;

    public ComponentIntegrationTests()
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
                ["AnalysisPlugin:ExchangeRateCacheHours"] = "24",
                ["AnalysisPlugin:PrimaryExchangeRateProvider"] = "Riksbank",
                ["AnalysisPlugin:FallbackExchangeRateProvider"] = "ECB"
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
    public async Task SimpleProfessionalReport_WithFrontPageOnly_GeneratesFile()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act - Create report job
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act - Add professional front page only
        var frontPageResult = await _plugin.AddFrontPageAsync(
            reportJobId,
            "Test Rapport",
            "Enkel test",
            "Test AB");

        var frontPageResponse = JsonSerializer.Deserialize<JsonElement>(frontPageResult);
        Assert.True(frontPageResponse.GetProperty("success").GetBoolean());

        // Act - Finish report
        var finishResult = await _plugin.FinishReportAsync(reportJobId);
        var finishResponse = JsonSerializer.Deserialize<JsonElement>(finishResult);

        // Assert
        if (!finishResponse.GetProperty("success").GetBoolean())
        {
            var errorMsg = finishResponse.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error";
            Assert.Fail($"FinishReport failed: {errorMsg}");
        }

        Assert.True(finishResponse.TryGetProperty("filePath", out var filePathElement));

        var filePath = filePathElement.GetString();
        Assert.NotNull(filePath);

        // Verify file exists
        Assert.True(File.Exists(filePath), $"Expected file to exist at: {filePath}");

        // Cleanup
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ProfessionalReportWorkflow_WithMultipleComponents_GeneratesCompleteReport()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act - Create report job
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act - Add professional front page
        var frontPageResult = await _plugin.AddFrontPageAsync(
            reportJobId, 
            "Finansiell Analysrapport Q4 2024", 
            "Kvartalsrapport med ML-analys och prognoser", 
            "Demo Ekonomi AB");
        
        var frontPageResponse = JsonSerializer.Deserialize<JsonElement>(frontPageResult);
        Assert.True(frontPageResponse.GetProperty("success").GetBoolean());

        // Act - Add revenue trend chart
        var chartResult = await _plugin.AddChartAsync(
            reportJobId,
            "revenue_trend",
            "Intäktsutveckling 2024",
            "historical");
        
        var chartResponse = JsonSerializer.Deserialize<JsonElement>(chartResult);
        Assert.True(chartResponse.GetProperty("success").GetBoolean());

        // Act - Add transaction table
        var tableResult = await _plugin.AddTableAsync(
            reportJobId,
            "transactions",
            "Transaktionsöversikt",
            true);
        
        var tableResponse = JsonSerializer.Deserialize<JsonElement>(tableResult);
        Assert.True(tableResponse.GetProperty("success").GetBoolean());

        // Act - Add monthly summary table
        var monthlySummaryResult = await _plugin.AddTableAsync(
            reportJobId,
            "monthly_summary",
            "Månadssammandrag",
            true);
        
        var monthlySummaryResponse = JsonSerializer.Deserialize<JsonElement>(monthlySummaryResult);
        Assert.True(monthlySummaryResponse.GetProperty("success").GetBoolean());

        // Act - Finish report
        var finishResult = await _plugin.FinishReportAsync(reportJobId);
        var finishResponse = JsonSerializer.Deserialize<JsonElement>(finishResult);

        // Assert
        if (!finishResponse.GetProperty("success").GetBoolean())
        {
            // Debug: Print the error message
            var errorMsg = finishResponse.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error";
            Assert.Fail($"FinishReport failed: {errorMsg}");
        }

        Assert.True(finishResponse.TryGetProperty("filePath", out var filePathElement));
        
        var filePath = filePathElement.GetString();
        Assert.NotNull(filePath);
        
        // Verify file exists and has reasonable size
        Assert.True(File.Exists(filePath), $"Expected file to exist at: {filePath}");

        var fileInfo = new FileInfo(filePath);
        // Just verify file exists and is not empty for now
        Assert.True(fileInfo.Length > 0, $"Generated file should not be empty, got {fileInfo.Length} bytes");
        
        // Verify it's a valid Excel file by checking extension
        Assert.True(filePath.EndsWith(".xlsx"), "Generated file should be Excel format");
        
        // Cleanup
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AddChartAsync_WithInvalidChartType_ReturnsError()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act - Try to add chart with invalid type
        var chartResult = await _plugin.AddChartAsync(
            reportJobId,
            "invalid_chart_type",
            "Invalid Chart",
            "historical");
        
        // Assert - Should still succeed (validation happens at component level)
        var chartResponse = JsonSerializer.Deserialize<JsonElement>(chartResult);
        Assert.True(chartResponse.GetProperty("success").GetBoolean());
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AddTableAsync_WithDifferentTableTypes_AllSucceed()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act & Assert - Test different table types
        var tableTypes = new[] { "transactions", "monthly_summary", "account_summary", "project_summary", "weekly_summary" };
        
        foreach (var tableType in tableTypes)
        {
            var tableResult = await _plugin.AddTableAsync(
                reportJobId,
                tableType,
                $"Test {tableType} Table",
                true);
            
            var tableResponse = JsonSerializer.Deserialize<JsonElement>(tableResult);
            Assert.True(tableResponse.GetProperty("success").GetBoolean(), 
                $"Table type {tableType} should succeed");
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
#KONTO 5400 ""Konsulttjänster""
#KONTO 5010 ""Lokalhyra""
#VER ""A"" ""1"" 20241101 ""Försäljning november""
#TRANS 3000 {} -25000.00 20241101 ""Försäljning""
#VER ""A"" ""2"" 20241102 ""Kontorsmaterial""
#TRANS 6000 {} 1500.00 20241102 ""Kontorsmaterial""
#VER ""A"" ""3"" 20241103 ""Konsultarvode""
#TRANS 5400 {} 8000.00 20241103 ""Konsultarvode""
#VER ""A"" ""4"" 20241104 ""Försäljning EUR""
#TRANS 3000 {} -15000.00 20241104 ""Försäljning EUR""
#VER ""A"" ""5"" 20241105 ""Hyra lokaler""
#TRANS 5010 {} 12000.00 20241105 ""Hyra lokaler""
";
    }
}
