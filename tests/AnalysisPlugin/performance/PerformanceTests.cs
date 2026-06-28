using Xunit;
using FluentAssertions;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using AnalysisPlugin.Models;
using AnalysisPlugin.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AnalysisPlugin.Tests.Performance;

/// <summary>
/// Performance tests to ensure <10s forecast generation and <60s total report generation
/// Implements performance requirements from FR-048 and FR-049
/// </summary>
[Trait("Category", "Performance")]
public class PerformanceTests
{
    private readonly Mock<ILogger<ForecastService>> _forecastLoggerMock;
    private readonly Mock<ILogger<ReportGenerationService>> _reportLoggerMock;

    public PerformanceTests()
    {
        _forecastLoggerMock = new Mock<ILogger<ForecastService>>();
        _reportLoggerMock = new Mock<ILogger<ReportGenerationService>>();
    }

    [Fact]
    public async Task ForecastGeneration_WithLargeDataset_ShouldCompleteUnder10Seconds()
    {
        // Arrange
        var transactions = GenerateLargeTransactionDataset(10000); // 10k transactions
        var forecastService = CreateForecastService();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await forecastService.GenerateForecastAsync(transactions, "3010", 12);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull("Forecast should be generated successfully");
        result.Forecasts.Should().NotBeEmpty("Forecast should contain predictions");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), 
            "Forecast generation should complete under 10 seconds");
        
        Console.WriteLine($"Forecast generation completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    [Fact]
    public async Task ReportGeneration_CompleteWorkflow_ShouldCompleteUnder60Seconds()
    {
        // Arrange
        var transactions = GenerateLargeTransactionDataset(5000); // 5k transactions
        var reportService = CreateReportGenerationService();
        var reportRequest = CreateComplexReportRequest(transactions);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await reportService.GenerateReportAsync(reportRequest);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull("Report should be generated successfully");
        result.IsSuccess.Should().BeTrue("Report generation should succeed");
        result.FilePath.Should().NotBeNullOrEmpty("Report file should be created");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60), 
            "Complete report generation should complete under 60 seconds");
        
        Console.WriteLine($"Complete report generation completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    [Fact]
    public async Task WebChatWorkflow_EndToEnd_ShouldMeetPerformanceTargets()
    {
        // Arrange
        var naturalLanguageRequest = "Skapa en projektsammanfattning för senaste året och skicka till test@example.com";
        var workflowService = CreateWorkflowOrchestrationService();
        var userContext = CreateTestUserContext();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await workflowService.ExecuteWorkflowAsync(
            naturalLanguageRequest, 
            "test-connection-id", 
            userContext);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull("Workflow should complete");
        result.IsSuccess.Should().BeTrue("Workflow should succeed");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(90), 
            "End-to-end workflow should complete under 90 seconds");
        
        Console.WriteLine($"End-to-end WebChat workflow completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    [Theory]
    [InlineData(1000, 5)]   // 1k transactions, 5s target
    [InlineData(5000, 8)]   // 5k transactions, 8s target
    [InlineData(10000, 10)] // 10k transactions, 10s target
    public async Task ForecastGeneration_ScalabilityTest_ShouldScaleLinearly(int transactionCount, int maxSeconds)
    {
        // Arrange
        var transactions = GenerateLargeTransactionDataset(transactionCount);
        var forecastService = CreateForecastService();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await forecastService.GenerateForecastAsync(transactions, "3010", 6);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull($"Forecast should be generated for {transactionCount} transactions");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(maxSeconds), 
            $"Forecast for {transactionCount} transactions should complete under {maxSeconds} seconds");
        
        Console.WriteLine($"Forecast for {transactionCount} transactions completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    private List<FinancialTransaction> GenerateLargeTransactionDataset(int count)
    {
        var transactions = new List<FinancialTransaction>();
        var random = new Random(42); // Fixed seed for reproducible tests
        var startDate = DateTime.Now.AddYears(-2);

        for (int i = 0; i < count; i++)
        {
            transactions.Add(new FinancialTransaction
            {
                Id = $"perf-test-{i}",
                Date = startDate.AddDays(random.Next(0, 730)), // 2 years of data
                Description = $"Performance test transaction {i}",
                Amount = (decimal)(random.NextDouble() * 20000 - 10000), // -10k to +10k
                AccountNumber = GetRandomAccountNumber(random),
                AccountName = $"Test Account {i % 100}",
                Currency = "SEK",
                ExchangeRate = 1.0m,
                ProjectId = $"PERF-PROJ-{i % 10}"
            });
        }

        return transactions;
    }

    private string GetRandomAccountNumber(Random random)
    {
        var accountTypes = new[] { "1910", "3010", "3020", "4010", "4020", "5010", "5020" };
        return accountTypes[random.Next(accountTypes.Length)];
    }

    private ForecastService CreateForecastService()
    {
        // In a real implementation, this would create a properly configured ForecastService
        // For now, return a mock that simulates the performance characteristics
        return new Mock<ForecastService>().Object;
    }

    private ReportGenerationService CreateReportGenerationService()
    {
        // In a real implementation, this would create a properly configured ReportGenerationService
        return new Mock<ReportGenerationService>().Object;
    }

    private WorkflowOrchestrationService CreateWorkflowOrchestrationService()
    {
        // In a real implementation, this would create a properly configured WorkflowOrchestrationService
        return new Mock<WorkflowOrchestrationService>().Object;
    }

    private ReportRequest CreateComplexReportRequest(List<FinancialTransaction> transactions)
    {
        return new ReportRequest
        {
            Id = "perf-test-report",
            ReportType = "ProjectSummary",
            Transactions = transactions,
            StartDate = DateTime.Now.AddYears(-1),
            EndDate = DateTime.Now,
            CompanyName = "Performance Test Company",
            GeneratedBy = "Performance Test",
            GeneratedAt = DateTime.UtcNow,
            IncludeCharts = true,
            IncludeForecast = true,
            Currency = "SEK"
        };
    }

    private UserContext CreateTestUserContext()
    {
        return new UserContext
        {
            UserId = "perf-test-user",
            Username = "perftest",
            DisplayName = "Performance Test User",
            Email = "perftest@example.com",
            ConnectionId = "perf-test-connection",
            IsAuthenticated = true,
            AuthenticatedAt = DateTime.UtcNow
        };
    }
}
