using AnalysisPlugin.Functions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;
using System.Text.Json;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class AIAgentIntegrationTests
{
    private readonly Mock<ILogger<GenerateReportFunction>> _mockLogger;
    private readonly Mock<ReportGenerationService> _mockReportService;
    private readonly GenerateReportFunction _function;
    private readonly Mock<Kernel> _mockKernel;

    public AIAgentIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<GenerateReportFunction>>();
        _mockReportService = new Mock<ReportGenerationService>(
            Mock.Of<ILogger<ReportGenerationService>>(),
            Mock.Of<SIE4ProcessorService>(),
            Mock.Of<ReportCompositionEngine>(),
            Mock.Of<ExcelGeneratorService>(),
            Mock.Of<PdfGeneratorService>(),
            Mock.Of<ReportArchiveService>(),
            Mock.Of<AnalysisPluginOptions>());

        _function = new GenerateReportFunction(_mockLogger.Object, _mockReportService.Object);
        _mockKernel = new Mock<Kernel>();
    }

    [Fact]
    public async Task AIAgent_DailyReportOrchestration_ExecutesSuccessfully()
    {
        // Arrange - Simulate AI agent orchestrating daily report
        var sie4Data = CreateMockSIE4Data();
        var expectedResult = CreateSuccessfulReportResult();
        
        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.Is<ReportRequest>(r => r.RequestType == ReportType.Financial && r.Format == ReportFormat.Excel),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(expectedResult);

        // Act - AI agent calls AnalysisPlugin function
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data: sie4Data,
            reportType: "Financial",
            format: "Excel",
            startDate: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
            endDate: DateTime.UtcNow.ToString("yyyy-MM-dd"),
            language: "en"
        );

        // Assert
        Assert.NotNull(result);
        Assert.Contains("successfully generated", result);
        Assert.Contains("25 transactions", result);
        Assert.Contains("4 components", result);
        
        // Verify the service was called with correct parameters
        _mockReportService.Verify(s => s.GenerateReportAsync(
            It.Is<ReportRequest>(r => 
                r.RequestType == ReportType.Financial &&
                r.Format == ReportFormat.Excel &&
                r.Language == "en"),
            sie4Data,
            It.IsAny<IEnumerable<ComponentConfiguration>>()), Times.Once);
    }

    [Fact]
    public async Task AIAgent_WeeklyReportWithProjectFilter_HandlesProjectFiltering()
    {
        // Arrange - AI agent requesting weekly report for specific projects
        var sie4Data = CreateMockSIE4DataWithProjects();
        var expectedResult = CreateSuccessfulReportResult();
        expectedResult.TransactionCount = 15; // Filtered count
        
        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.Is<ReportRequest>(r => r.ProjectIds != null && r.ProjectIds.Contains("PRJ001")),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(expectedResult);

        // Act - AI agent calls with project filter
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data: sie4Data,
            reportType: "Project",
            format: "PDF",
            startDate: DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd"),
            endDate: DateTime.UtcNow.ToString("yyyy-MM-dd"),
            projectIds: "PRJ001,PRJ002",
            language: "sv"
        );

        // Assert
        Assert.NotNull(result);
        Assert.Contains("successfully generated", result);
        Assert.Contains("15 transactions", result);
        
        // Verify project filtering was applied
        _mockReportService.Verify(s => s.GenerateReportAsync(
            It.Is<ReportRequest>(r => 
                r.ProjectIds != null && 
                r.ProjectIds.Count == 2 &&
                r.ProjectIds.Contains("PRJ001") &&
                r.ProjectIds.Contains("PRJ002")),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()), Times.Once);
    }

    [Fact]
    public async Task AIAgent_ErrorHandling_ReceivesStructuredErrorInformation()
    {
        // Arrange - Simulate error scenario
        var sie4Data = "Invalid SIE4 data";
        var errorResult = new ReportGenerationResult
        {
            IsSuccessful = false,
            ErrorCode = "NO_TRANSACTIONS_FOUND",
            ErrorMessage = "No valid transactions found in the provided SIE4 data",
            ErrorDetails = new Dictionary<string, object>
            {
                ["sie4DataLength"] = sie4Data.Length,
                ["requestId"] = Guid.NewGuid(),
                ["suggestion"] = "Check if the SIE4 data contains valid #VER and #TRANS entries"
            },
            GenerationTimeMs = 150
        };

        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(errorResult);

        // Act - AI agent receives structured error
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data: sie4Data,
            reportType: "Financial",
            format: "Excel"
        );

        // Assert - AI agent can parse structured error information
        Assert.NotNull(result);
        Assert.Contains("ERROR", result);
        Assert.Contains("NO_TRANSACTIONS_FOUND", result);
        Assert.Contains("RECOVERABLE", result); // Should indicate recoverable error
        Assert.Contains("Check if the SIE4 data contains valid #VER and #TRANS entries", result);
        
        // Verify error details are included for AI agent processing
        var errorInfo = ExtractErrorInformation(result);
        Assert.Equal("NO_TRANSACTIONS_FOUND", errorInfo.ErrorCode);
        Assert.True(errorInfo.IsRecoverable);
        Assert.NotEmpty(errorInfo.SuggestedActions);
    }

    [Fact]
    public async Task AIAgent_MonthlyReportOrchestration_WithCustomComponents_ExecutesCorrectly()
    {
        // Arrange - AI agent with custom component configuration
        var sie4Data = CreateMockSIE4Data();
        var customComponents = CreateCustomComponentConfiguration();
        var expectedResult = CreateSuccessfulReportResult();
        expectedResult.ComponentCount = 3; // Custom component count
        
        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.Is<IEnumerable<ComponentConfiguration>>(configs => configs.Count() == 2)))
            .ReturnsAsync(expectedResult);

        // Act - AI agent provides custom component configuration
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data: sie4Data,
            reportType: "Custom",
            format: "Excel",
            startDate: DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd"),
            endDate: DateTime.UtcNow.ToString("yyyy-MM-dd"),
            componentConfigurations: customComponents,
            language: "en"
        );

        // Assert
        Assert.NotNull(result);
        Assert.Contains("successfully generated", result);
        Assert.Contains("3 components", result);
        
        // Verify custom components were used
        _mockReportService.Verify(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.Is<IEnumerable<ComponentConfiguration>>(configs => configs.Any())), Times.Once);
    }

    [Fact]
    public async Task AIAgent_PerformanceMonitoring_ReceivesDetailedMetrics()
    {
        // Arrange - AI agent monitoring performance
        var sie4Data = CreateLargeMockSIE4Data();
        var expectedResult = CreateSuccessfulReportResult();
        expectedResult.GenerationTimeMs = 5500; // Longer processing time
        expectedResult.TransactionCount = 1000; // Large dataset
        
        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data: sie4Data,
            reportType: "Financial",
            format: "Excel"
        );

        // Assert - AI agent receives performance metrics
        Assert.NotNull(result);
        Assert.Contains("successfully generated", result);
        Assert.Contains("1000 transactions", result);
        Assert.Contains("5500ms", result); // Processing time included
        
        // Verify AI agent can extract performance metrics for monitoring
        var metrics = ExtractPerformanceMetrics(result);
        Assert.Equal(5500, metrics.GenerationTimeMs);
        Assert.Equal(1000, metrics.TransactionCount);
        Assert.True(metrics.GenerationTimeMs > 5000); // AI agent can detect slow processing
    }

    [Fact]
    public async Task AIAgent_RetryLogic_HandlesRecoverableErrors()
    {
        // Arrange - Simulate recoverable error followed by success
        var sie4Data = CreateMockSIE4Data();
        var errorResult = new ReportGenerationResult
        {
            IsSuccessful = false,
            ErrorCode = "REQUEST_VALIDATION_FAILED",
            ErrorMessage = "Invalid date range: start date must be before end date",
            ErrorDetails = new Dictionary<string, object>
            {
                ["validationErrors"] = new[] { "Invalid date range" },
                ["suggestion"] = "Review and correct the request parameters"
            }
        };
        
        var successResult = CreateSuccessfulReportResult();
        
        _mockReportService.SetupSequence(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(errorResult)  // First call fails
            .ReturnsAsync(successResult); // Second call succeeds

        // Act - AI agent first call (should fail)
        var firstResult = await _function.GenerateFinancialReportAsync(
            sie4Data: sie4Data,
            reportType: "Financial",
            format: "Excel",
            startDate: "2024-12-31", // Invalid: after end date
            endDate: "2024-01-01"
        );

        // Assert first call failed with recoverable error
        Assert.Contains("ERROR", firstResult);
        Assert.Contains("RECOVERABLE", firstResult);
        
        // Act - AI agent retry with corrected parameters
        var retryResult = await _function.GenerateFinancialReportAsync(
            sie4Data: sie4Data,
            reportType: "Financial",
            format: "Excel",
            startDate: "2024-01-01", // Corrected
            endDate: "2024-12-31"
        );

        // Assert retry succeeded
        Assert.Contains("successfully generated", retryResult);
        
        // Verify both calls were made
        _mockReportService.Verify(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()), Times.Exactly(2));
    }

    private string CreateMockSIE4Data()
    {
        return @"
#FLAGGA 0
#PROGRAM ""Fortnox"" ""4.0""
#FORMAT PC8
#KONTO ""1930"" ""Bank""
#KONTO ""3001"" ""Sales Revenue""
#KONTO ""5410"" ""External Costs""
#VER ""A"" ""1"" 20241101 ""Daily sales""
#TRANS ""1930"" 25000.00
#TRANS ""3001"" -25000.00
#VER ""A"" ""2"" 20241102 ""Office expenses""
#TRANS ""5410"" 5000.00
#TRANS ""1930"" -5000.00
#VER ""A"" ""3"" 20241103 ""Client payment""
#TRANS ""1930"" 15000.00
#TRANS ""3001"" -15000.00
";
    }

    private string CreateMockSIE4DataWithProjects()
    {
        return @"
#FLAGGA 0
#PROGRAM ""Fortnox"" ""4.0""
#FORMAT PC8
#KONTO ""1930"" ""Bank""
#KONTO ""3001"" ""Sales Revenue""
#VER ""A"" ""1"" 20241101 ""Project A revenue""
#TRANS ""1930"" 30000.00 {PRJ001}
#TRANS ""3001"" -30000.00 {PRJ001}
#VER ""A"" ""2"" 20241102 ""Project B revenue""
#TRANS ""1930"" 20000.00 {PRJ002}
#TRANS ""3001"" -20000.00 {PRJ002}
#VER ""A"" ""3"" 20241103 ""Project C revenue""
#TRANS ""1930"" 10000.00 {PRJ003}
#TRANS ""3001"" -10000.00 {PRJ003}
";
    }

    private string CreateLargeMockSIE4Data()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#FLAGGA 0");
        sb.AppendLine("#PROGRAM \"Fortnox\" \"4.0\"");
        sb.AppendLine("#FORMAT PC8");
        sb.AppendLine("#KONTO \"1930\" \"Bank\"");
        sb.AppendLine("#KONTO \"3001\" \"Sales Revenue\"");

        // Generate 100 transactions for performance testing
        for (int i = 1; i <= 100; i++)
        {
            var date = DateTime.UtcNow.AddDays(-i).ToString("yyyyMMdd");
            sb.AppendLine($"#VER \"A\" \"{i}\" {date} \"Transaction {i}\"");
            sb.AppendLine($"#TRANS \"1930\" {i * 100}.00");
            sb.AppendLine($"#TRANS \"3001\" -{i * 100}.00");
        }

        return sb.ToString();
    }

    private ReportGenerationResult CreateSuccessfulReportResult()
    {
        return new ReportGenerationResult
        {
            IsSuccessful = true,
            TransactionCount = 25,
            ComponentCount = 4,
            GenerationTimeMs = 1200,
            Timestamp = DateTime.UtcNow,
            ReportFiles = new List<ReportFile>
            {
                new ReportFile
                {
                    Format = ReportFormat.Excel,
                    Content = System.Text.Encoding.UTF8.GetBytes("Mock Excel Content"),
                    FileName = "Financial_Report.xlsx"
                }
            }
        };
    }

    private string CreateCustomComponentConfiguration()
    {
        return @"[
  {
    ""componentId"": ""custom-revenue-chart"",
    ""type"": ""Chart"",
    ""title"": ""Monthly Revenue Trend"",
    ""chartType"": ""Line"",
    ""dataSource"": ""Transactions"",
    ""filters"": {
      ""accountCategories"": [""Revenue""]
    }
  },
  {
    ""componentId"": ""custom-expense-table"",
    ""type"": ""Table"",
    ""title"": ""Top Expenses"",
    ""dataSource"": ""Transactions"",
    ""filters"": {
      ""accountCategories"": [""Expenses""]
    },
    ""limit"": 10
  }
]";
    }

    private ErrorInformation ExtractErrorInformation(string result)
    {
        // Simulate AI agent parsing structured error information from result
        var errorInfo = new ErrorInformation();

        if (result.Contains("NO_TRANSACTIONS_FOUND"))
        {
            errorInfo.ErrorCode = "NO_TRANSACTIONS_FOUND";
            errorInfo.IsRecoverable = true;
            errorInfo.SuggestedActions = new[] { "Check SIE4 data format", "Adjust date range" };
        }
        else if (result.Contains("REQUEST_VALIDATION_FAILED"))
        {
            errorInfo.ErrorCode = "REQUEST_VALIDATION_FAILED";
            errorInfo.IsRecoverable = true;
            errorInfo.SuggestedActions = new[] { "Review request parameters", "Check date ranges" };
        }

        return errorInfo;
    }

    private PerformanceMetrics ExtractPerformanceMetrics(string result)
    {
        // Simulate AI agent parsing performance metrics from result
        var metrics = new PerformanceMetrics();

        // Extract generation time
        var timeMatch = System.Text.RegularExpressions.Regex.Match(result, @"(\d+)ms");
        if (timeMatch.Success)
        {
            metrics.GenerationTimeMs = long.Parse(timeMatch.Groups[1].Value);
        }

        // Extract transaction count
        var transactionMatch = System.Text.RegularExpressions.Regex.Match(result, @"(\d+) transactions");
        if (transactionMatch.Success)
        {
            metrics.TransactionCount = int.Parse(transactionMatch.Groups[1].Value);
        }

        return metrics;
    }

    private class ErrorInformation
    {
        public string ErrorCode { get; set; } = "";
        public bool IsRecoverable { get; set; }
        public string[] SuggestedActions { get; set; } = Array.Empty<string>();
    }

    private class PerformanceMetrics
    {
        public long GenerationTimeMs { get; set; }
        public int TransactionCount { get; set; }
    }
}
