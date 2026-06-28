using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AnalysisPlugin.Tests.Integration;

/// <summary>
/// Integration tests for professional report components (Chart, Pivot, ML Analysis)
/// </summary>
public class ProfessionalComponentsTests
{
    private readonly global::AnalysisPlugin.AnalysisPlugin _plugin;
    private readonly ILogger<global::AnalysisPlugin.AnalysisPlugin> _logger;

    public ProfessionalComponentsTests()
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
    public async Task ComprehensiveReport_WithAllProfessionalComponents_GeneratesSuccessfully()
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
            "Komplett Finansiell Analysrapport", 
            "Omfattande analys med AI-insikter och professionella komponenter", 
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

        // Act - Add monthly revenue pivot table
        var pivotResult = await _plugin.AddPivotTableAsync(
            reportJobId,
            "monthly_revenue",
            "Månadsvis Intäktsanalys",
            "Month",
            "Revenue");
        
        var pivotResponse = JsonSerializer.Deserialize<JsonElement>(pivotResult);
        Assert.True(pivotResponse.GetProperty("success").GetBoolean());

        // Act - Add ML analysis component
        var mlResult = await _plugin.AddMLAnalysisAsync(
            reportJobId,
            "trend_analysis",
            "AI-driven Trendanalys",
            0.95);
        
        var mlResponse = JsonSerializer.Deserialize<JsonElement>(mlResult);
        Assert.True(mlResponse.GetProperty("success").GetBoolean());

        // Act - Add transaction table
        var tableResult = await _plugin.AddTableAsync(
            reportJobId,
            "monthly_summary",
            "Månadssammandrag",
            true);
        
        var tableResponse = JsonSerializer.Deserialize<JsonElement>(tableResult);
        Assert.True(tableResponse.GetProperty("success").GetBoolean());

        // Act - Finish comprehensive report
        var finishResult = await _plugin.FinishReportAsync(reportJobId);
        var finishResponse = JsonSerializer.Deserialize<JsonElement>(finishResult);

        // Assert
        Assert.True(finishResponse.GetProperty("success").GetBoolean());
        Assert.True(finishResponse.TryGetProperty("filePath", out var filePathElement));
        
        var filePath = filePathElement.GetString();
        Assert.NotNull(filePath);
        
        // Verify file exists and has substantial content
        Assert.True(File.Exists(filePath), $"Expected file to exist at: {filePath}");
        
        var fileInfo = new FileInfo(filePath);
        Assert.True(fileInfo.Length > 0, $"Generated file should not be empty, got {fileInfo.Length} bytes");
        
        // Verify it's a valid Excel file
        Assert.True(filePath.EndsWith(".xlsx"), "Generated file should be Excel format");
        
        // Cleanup
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AddChartAsync_DifferentChartTypes_AllSucceed()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act & Assert - Test different chart types
        var chartTypes = new[] { "revenue_trend", "expense_breakdown", "profit_margin", "forecast_chart", "weekly_gross_profit" };
        
        foreach (var chartType in chartTypes)
        {
            var chartResult = await _plugin.AddChartAsync(
                reportJobId,
                chartType,
                $"Test {chartType} Chart",
                "historical");
            
            var chartResponse = JsonSerializer.Deserialize<JsonElement>(chartResult);
            Assert.True(chartResponse.GetProperty("success").GetBoolean(), 
                $"Chart type {chartType} should succeed");
        }
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AddPivotTableAsync_DifferentPivotTypes_AllSucceed()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act & Assert - Test different pivot types
        var pivotTypes = new[] { "monthly_revenue", "project_costs", "account_breakdown", "quarterly_breakdown" };
        
        foreach (var pivotType in pivotTypes)
        {
            var pivotResult = await _plugin.AddPivotTableAsync(
                reportJobId,
                pivotType,
                $"Test {pivotType} Pivot",
                "Month",
                "Amount");
            
            var pivotResponse = JsonSerializer.Deserialize<JsonElement>(pivotResult);
            Assert.True(pivotResponse.GetProperty("success").GetBoolean(), 
                $"Pivot type {pivotType} should succeed");
        }
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AddMLAnalysisAsync_DifferentAnalysisTypes_AllSucceed()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act & Assert - Test different ML analysis types
        var analysisTypes = new[] { "trend_analysis", "forecast", "anomaly_detection" };
        
        foreach (var analysisType in analysisTypes)
        {
            var mlResult = await _plugin.AddMLAnalysisAsync(
                reportJobId,
                analysisType,
                $"Test {analysisType} Analysis",
                0.90);
            
            var mlResponse = JsonSerializer.Deserialize<JsonElement>(mlResult);
            Assert.True(mlResponse.GetProperty("success").GetBoolean(), 
                $"ML analysis type {analysisType} should succeed");
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
