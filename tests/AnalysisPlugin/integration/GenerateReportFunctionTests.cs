using AnalysisPlugin.Functions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using AnalysisPlugin.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;
using System.Text;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class GenerateReportFunctionTests
{
    private readonly GenerateReportFunction _function;
    private readonly Mock<ILogger<GenerateReportFunction>> _mockLogger;
    private readonly Mock<ReportGenerationService> _mockReportService;
    private readonly Mock<SIE4ProcessorService> _mockSIE4Service;

    public GenerateReportFunctionTests()
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
        
        _mockSIE4Service = new Mock<SIE4ProcessorService>(
            Mock.Of<ILogger<SIE4ProcessorService>>(),
            Mock.Of<Kernel>());

        _function = new GenerateReportFunction(_mockLogger.Object, _mockReportService.Object);
    }

    [Fact]
    public async Task GenerateFinancialReportAsync_WithValidSIE4Data_ReturnsSuccessfulResult()
    {
        // Arrange
        var sie4Data = CreateValidSIE4Data();
        var reportType = "Financial";
        var format = "Excel";
        var startDate = "2024-01-01";
        var endDate = "2024-12-31";
        var language = "en";

        var expectedResult = new ReportGenerationResult
        {
            IsSuccessful = true,
            TransactionCount = 10,
            ComponentCount = 3,
            GenerationTimeMs = 1500,
            ReportFiles = new List<ReportFile>
            {
                new ReportFile
                {
                    Format = ReportFormat.Excel,
                    Content = Encoding.UTF8.GetBytes("Mock Excel Content"),
                    FileName = "Financial_Report_2024.xlsx"
                }
            }
        };

        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data, reportType, format, startDate, endDate, language);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("successfully generated", result);
        Assert.Contains("10 transactions", result);
        Assert.Contains("3 components", result);
        Assert.Contains("1500ms", result);
    }

    [Fact]
    public async Task GenerateFinancialReportAsync_WithInvalidSIE4Data_ReturnsErrorDetails()
    {
        // Arrange
        var invalidSie4Data = "Invalid SIE4 data";
        var reportType = "Financial";
        var format = "PDF";

        var expectedResult = new ReportGenerationResult
        {
            IsSuccessful = false,
            ErrorCode = "NO_TRANSACTIONS_FOUND",
            ErrorMessage = "No valid transactions found in the provided SIE4 data",
            ErrorDetails = new Dictionary<string, object>
            {
                ["sie4DataLength"] = invalidSie4Data.Length,
                ["suggestion"] = "Check if the SIE4 data contains valid #VER and #TRANS entries"
            },
            GenerationTimeMs = 250
        };

        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _function.GenerateFinancialReportAsync(
            invalidSie4Data, reportType, format);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ERROR", result);
        Assert.Contains("NO_TRANSACTIONS_FOUND", result);
        Assert.Contains("No valid transactions found", result);
        Assert.Contains("Check if the SIE4 data contains valid #VER and #TRANS entries", result);
    }

    [Fact]
    public async Task GenerateFinancialReportAsync_WithProjectFilter_FiltersCorrectly()
    {
        // Arrange
        var sie4Data = CreateSIE4DataWithProjects();
        var reportType = "Project";
        var format = "Excel";
        var projectIds = "PRJ001,PRJ002";

        var expectedResult = new ReportGenerationResult
        {
            IsSuccessful = true,
            TransactionCount = 5,
            ComponentCount = 2,
            GenerationTimeMs = 800
        };

        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.Is<ReportRequest>(r => r.ProjectIds != null && r.ProjectIds.Contains("PRJ001") && r.ProjectIds.Contains("PRJ002")),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data, reportType, format, projectIds: projectIds);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("successfully generated", result);
        Assert.Contains("5 transactions", result);
        
        // Verify the service was called with correct project filters
        _mockReportService.Verify(s => s.GenerateReportAsync(
            It.Is<ReportRequest>(r => r.ProjectIds != null && r.ProjectIds.Count == 2),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()), Times.Once);
    }

    [Fact]
    public async Task GenerateFinancialReportAsync_WithCustomComponents_UsesProvidedConfiguration()
    {
        // Arrange
        var sie4Data = CreateValidSIE4Data();
        var reportType = "Custom";
        var format = "PDF";
        var componentConfigJson = CreateCustomComponentConfiguration();

        var expectedResult = new ReportGenerationResult
        {
            IsSuccessful = true,
            TransactionCount = 8,
            ComponentCount = 4,
            GenerationTimeMs = 2000
        };

        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.Is<IEnumerable<ComponentConfiguration>>(configs => configs.Count() == 2)))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data, reportType, format, componentConfigurations: componentConfigJson);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("successfully generated", result);
        Assert.Contains("4 components", result);
        
        // Verify custom configurations were used
        _mockReportService.Verify(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.Is<IEnumerable<ComponentConfiguration>>(configs => configs.Any())), Times.Once);
    }

    [Fact]
    public async Task GenerateFinancialReportAsync_WithRecoverableError_ProvidesActionableGuidance()
    {
        // Arrange
        var sie4Data = CreateValidSIE4Data();
        var reportType = "Financial";
        var format = "Excel";

        var expectedResult = new ReportGenerationResult
        {
            IsSuccessful = false,
            ErrorCode = "REQUEST_VALIDATION_FAILED",
            ErrorMessage = "Invalid request: End date must be after start date",
            ErrorDetails = new Dictionary<string, object>
            {
                ["validationErrors"] = new[] { "End date must be after start date" },
                ["suggestion"] = "Review and correct the request parameters"
            },
            GenerationTimeMs = 100
        };

        _mockReportService.Setup(s => s.GenerateReportAsync(
            It.IsAny<ReportRequest>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<ComponentConfiguration>>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _function.GenerateFinancialReportAsync(
            sie4Data, reportType, format, "2024-12-31", "2024-01-01"); // Invalid date range

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ERROR", result);
        Assert.Contains("REQUEST_VALIDATION_FAILED", result);
        Assert.Contains("RECOVERABLE", result); // Should indicate it's recoverable
        Assert.Contains("Review and correct the request parameters", result);
        Assert.Contains("Check date ranges and format specifications", result);
    }

    private string CreateValidSIE4Data()
    {
        return @"
#FLAGGA 0
#PROGRAM ""Fortnox"" ""4.0""
#FORMAT PC8
#KONTO ""1930"" ""Bank""
#KONTO ""3001"" ""Sales Revenue""
#KONTO ""5410"" ""External Costs""
#KONTO ""6210"" ""Salaries""
#VER ""A"" ""1"" 20240115 ""Opening balance""
#TRANS ""1930"" 100000.00
#TRANS ""3001"" -100000.00
#VER ""A"" ""2"" 20240201 ""Sales transaction""
#TRANS ""1930"" 50000.00
#TRANS ""3001"" -50000.00
#VER ""A"" ""3"" 20240215 ""Office rent""
#TRANS ""5410"" 15000.00
#TRANS ""1930"" -15000.00
#VER ""A"" ""4"" 20240301 ""Salary payment""
#TRANS ""6210"" 45000.00
#TRANS ""1930"" -45000.00
#VER ""A"" ""5"" 20240315 ""Client payment""
#TRANS ""1930"" 75000.00
#TRANS ""3001"" -75000.00
";
    }

    private string CreateSIE4DataWithProjects()
    {
        return @"
#FLAGGA 0
#PROGRAM ""Fortnox"" ""4.0""
#FORMAT PC8
#KONTO ""1930"" ""Bank""
#KONTO ""3001"" ""Sales Revenue""
#KONTO ""5410"" ""Project Costs""
#VER ""A"" ""1"" 20240115 ""Project A revenue""
#TRANS ""1930"" 80000.00 {PRJ001}
#TRANS ""3001"" -80000.00 {PRJ001}
#VER ""A"" ""2"" 20240201 ""Project B revenue""
#TRANS ""1930"" 60000.00 {PRJ002}
#TRANS ""3001"" -60000.00 {PRJ002}
#VER ""A"" ""3"" 20240215 ""Project A costs""
#TRANS ""5410"" 20000.00 {PRJ001}
#TRANS ""1930"" -20000.00 {PRJ001}
#VER ""A"" ""4"" 20240301 ""Project C revenue""
#TRANS ""1930"" 40000.00 {PRJ003}
#TRANS ""3001"" -40000.00 {PRJ003}
#VER ""A"" ""5"" 20240315 ""Project B costs""
#TRANS ""5410"" 15000.00 {PRJ002}
#TRANS ""1930"" -15000.00 {PRJ002}
";
    }

    private string CreateCustomComponentConfiguration()
    {
        return @"[
  {
    ""componentId"": ""custom-chart-1"",
    ""type"": ""Chart"",
    ""title"": ""Custom Revenue Analysis"",
    ""chartType"": ""Bar"",
    ""dataSource"": ""Transactions"",
    ""groupBy"": ""AccountCategory"",
    ""aggregation"": ""Sum"",
    ""filters"": {
      ""accountCodes"": [""3001"", ""3002"", ""3003""]
    }
  },
  {
    ""componentId"": ""custom-table-1"",
    ""type"": ""Table"",
    ""title"": ""Top Expense Accounts"",
    ""dataSource"": ""Transactions"",
    ""columns"": [
      { ""field"": ""AccountCode"", ""title"": ""Account"" },
      { ""field"": ""Description"", ""title"": ""Description"" },
      { ""field"": ""Amount"", ""title"": ""Amount"", ""format"": ""Currency"" }
    ],
    ""filters"": {
      ""amountRange"": { ""min"": 1000 }
    },
    ""sorting"": [
      { ""field"": ""Amount"", ""direction"": ""Desc"" }
    ],
    ""limit"": 10
  }
]";
    }
}
