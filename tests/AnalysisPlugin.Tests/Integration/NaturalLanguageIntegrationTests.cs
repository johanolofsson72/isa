using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AnalysisPlugin.Tests.Integration;

/// <summary>
/// Integration tests for natural language processing functionality
/// </summary>
public class NaturalLanguageIntegrationTests
{
    private readonly global::AnalysisPlugin.AnalysisPlugin _plugin;
    private readonly ILogger<global::AnalysisPlugin.AnalysisPlugin> _logger;

    public NaturalLanguageIntegrationTests()
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
    public async Task AnalyzeFinancialDataAsync_TrendAnalysis_ReturnsNaturalLanguageInsights()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act
        var result = await _plugin.AnalyzeFinancialDataAsync(sie4Data, "trend_analysis", "sv");

        // Assert
        Assert.NotNull(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        if (!response.GetProperty("success").GetBoolean())
        {
            var errorMsg = response.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error";
            Assert.Fail($"Analysis failed: {errorMsg}");
        }
        
        Assert.True(response.TryGetProperty("insights", out var insightsElement));
        var insights = insightsElement.GetString();
        Assert.NotNull(insights);
        Assert.True(insights.Length > 50, "Insights should be substantial");
        
        Assert.True(response.TryGetProperty("metricsInterpretation", out var metricsElement));
        var metricsInterpretation = metricsElement.GetString();
        Assert.NotNull(metricsInterpretation);
        
        Assert.True(response.TryGetProperty("analysisType", out var typeElement));
        Assert.Equal("trend_analysis", typeElement.GetString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AnalyzeFinancialDataAsync_ForecastAnalysis_ReturnsValidForecast()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act
        var result = await _plugin.AnalyzeFinancialDataAsync(sie4Data, "forecast", "en");

        // Assert
        Assert.NotNull(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(response.GetProperty("success").GetBoolean());
        
        Assert.True(response.TryGetProperty("forecastPoints", out var forecastElement));
        var forecastPoints = forecastElement.GetInt32();
        Assert.True(forecastPoints > 0, "Should have forecast points");
        
        Assert.True(response.TryGetProperty("insights", out var insightsElement));
        var insights = insightsElement.GetString();
        Assert.NotNull(insights);
        Assert.Contains("forecast", insights.ToLowerInvariant());
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AnalyzeFinancialDataAsync_AnomalyDetection_ReturnsAnomalyInsights()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act
        var result = await _plugin.AnalyzeFinancialDataAsync(sie4Data, "anomaly_detection", "sv");

        // Assert
        Assert.NotNull(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(response.GetProperty("success").GetBoolean());
        
        Assert.True(response.TryGetProperty("anomaliesDetected", out var anomaliesElement));
        var anomaliesDetected = anomaliesElement.GetInt32();
        Assert.True(anomaliesDetected >= 0, "Should report anomaly count");
        
        Assert.True(response.TryGetProperty("insights", out var insightsElement));
        var insights = insightsElement.GetString();
        Assert.NotNull(insights);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GenerateExecutiveSummaryAsync_ValidData_ReturnsComprehensiveSummary()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();

        // Act
        var result = await _plugin.GenerateExecutiveSummaryAsync(sie4Data, "sv");

        // Assert
        Assert.NotNull(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(response.GetProperty("success").GetBoolean());
        
        Assert.True(response.TryGetProperty("executiveSummary", out var summaryElement));
        var summary = summaryElement.GetString();
        Assert.NotNull(summary);
        Assert.True(summary.Length > 100, "Executive summary should be comprehensive");
        Assert.Contains("Sammanfattning", summary); // Swedish summary header
        
        Assert.True(response.TryGetProperty("analysesPerformed", out var analysesElement));
        var analysesPerformed = analysesElement.GetInt32();
        Assert.True(analysesPerformed >= 3, "Should perform multiple analyses");
        
        Assert.True(response.TryGetProperty("totalTransactions", out var transactionsElement));
        var totalTransactions = transactionsElement.GetInt32();
        Assert.True(totalTransactions > 0, "Should process transactions");
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task AddMLAnalysisAsync_ToReport_AddsComponentSuccessfully()
    {
        // Arrange
        var sie4Data = GetTestSIE4Data();
        
        // Create report job
        var createResult = await _plugin.CreateReportJobAsync(sie4Data, "sv");
        var createResponse = JsonSerializer.Deserialize<JsonElement>(createResult);
        var reportJobId = createResponse.GetProperty("reportJobId").GetString()!;

        // Act
        var mlResult = await _plugin.AddMLAnalysisAsync(
            reportJobId,
            "trend_analysis",
            "Trendanalys med AI-insikter",
            0.95);

        // Assert
        var mlResponse = JsonSerializer.Deserialize<JsonElement>(mlResult);
        Assert.True(mlResponse.GetProperty("success").GetBoolean());
        
        // Verify we can finish the report with ML component
        var finishResult = await _plugin.FinishReportAsync(reportJobId);
        var finishResponse = JsonSerializer.Deserialize<JsonElement>(finishResult);
        Assert.True(finishResponse.GetProperty("success").GetBoolean());
        
        // Cleanup
        if (finishResponse.TryGetProperty("filePath", out var filePathElement))
        {
            var filePath = filePathElement.GetString();
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
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
