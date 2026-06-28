using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using AnalysisPlugin.Components;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Text;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class PdfReportServiceTests
{
    private readonly PdfGeneratorService _service;
    private readonly Mock<ILogger<PdfGeneratorService>> _mockLogger;
    private readonly Mock<RiksbankService> _mockRiksbankService;

    public PdfReportServiceTests()
    {
        _mockLogger = new Mock<ILogger<PdfGeneratorService>>();
        _mockRiksbankService = new Mock<RiksbankService>(Mock.Of<HttpClient>(), Mock.Of<ILogger<RiksbankService>>());
        _service = new PdfGeneratorService(_mockLogger.Object, _mockRiksbankService.Object);
    }

    [Fact]
    public async Task GeneratePdfAsync_WithRealFinancialData_CreatesValidPdfFile()
    {
        // Arrange
        var composedReport = CreateRealComposedReport();

        // Act
        var pdfBytes = await _service.GeneratePdfAsync(composedReport);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);

        // Validate PDF header (PDF files start with %PDF-)
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        Assert.Equal("%PDF", pdfHeader);

        // Check minimum size for a valid PDF with content
        Assert.True(pdfBytes.Length > 1000); // Should be substantial with real data
    }

    [Fact]
    public async Task GeneratePdfAsync_WithChartsAndTables_IncludesAllComponents()
    {
        // Arrange
        var composedReport = CreateComposedReportWithMultipleComponents();

        // Act
        var pdfBytes = await _service.GeneratePdfAsync(composedReport);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);

        // Convert to string to check content (simplified validation)
        var pdfContent = Encoding.UTF8.GetString(pdfBytes);
        
        // Should contain component titles
        Assert.Contains("Revenue Trend Chart", pdfContent);
        Assert.Contains("Transaction Summary Table", pdfContent);
        Assert.Contains("Project Analysis", pdfContent);
    }

    [Fact]
    public async Task GeneratePdfAsync_WithForeignCurrency_IncludesCurrencyInformation()
    {
        // Arrange
        var composedReport = CreateComposedReportWithForeignCurrency();
        
        var mockExchangeRate = new ExchangeRate
        {
            Date = DateTime.UtcNow.Date,
            FromCurrency = "USD",
            ToCurrency = "SEK",
            Rate = 10.50m,
            Source = "Riksbank",
            RetrievedAt = DateTime.UtcNow
        };
        _mockRiksbankService.Setup(s => s.GetExchangeRateAsync("USD", It.IsAny<DateTime?>()))
                          .ReturnsAsync(mockExchangeRate);

        // Act
        var pdfBytes = await _service.GeneratePdfAsync(composedReport);

        // Assert
        Assert.NotNull(pdfBytes);
        var pdfContent = Encoding.UTF8.GetString(pdfBytes);
        
        // Should contain currency information
        Assert.Contains("Foreign Currency Information", pdfContent);
        Assert.Contains("USD", pdfContent);
        Assert.Contains("10.50", pdfContent); // Exchange rate
    }

    [Fact]
    public async Task FormatCurrencyWithExchangeRateAsync_WithValidCurrency_ReturnsHtmlFormattedString()
    {
        // Arrange
        var amount = 2500m;
        var currency = "EUR";
        var mockExchangeRate = new ExchangeRate
        {
            Date = DateTime.UtcNow.Date,
            FromCurrency = "EUR",
            ToCurrency = "SEK",
            Rate = 11.25m,
            Source = "Riksbank",
            RetrievedAt = DateTime.UtcNow
        };
        _mockRiksbankService.Setup(s => s.GetExchangeRateAsync("EUR", It.IsAny<DateTime?>()))
                          .ReturnsAsync(mockExchangeRate);

        // Act
        var result = await _service.FormatCurrencyWithExchangeRateAsync(amount, currency);

        // Assert
        Assert.Contains("2,500.00 EUR", result);
        Assert.Contains("28,125.00 SEK", result); // 2500 * 11.25
        Assert.Contains("<span", result); // Should contain HTML formatting
    }

    [Fact]
    public async Task GenerateCurrencyInformationHtmlAsync_WithMultipleCurrencies_ReturnsCompleteTable()
    {
        // Arrange
        var transactions = CreateTransactionsWithMultipleCurrencies();
        var reportDate = DateTime.UtcNow.Date;
        
        // Mock exchange rates
        _mockRiksbankService.Setup(s => s.GetExchangeRateAsync("USD", It.IsAny<DateTime?>()))
                          .ReturnsAsync(new ExchangeRate { FromCurrency = "USD", Rate = 10.50m, Date = reportDate, Source = "Riksbank" });
        _mockRiksbankService.Setup(s => s.GetExchangeRateAsync("EUR", It.IsAny<DateTime?>()))
                          .ReturnsAsync(new ExchangeRate { FromCurrency = "EUR", Rate = 11.25m, Date = reportDate, Source = "Riksbank" });

        // Act
        var html = await _service.GenerateCurrencyInformationHtmlAsync(transactions, reportDate);

        // Assert
        Assert.NotEmpty(html);
        Assert.Contains("<table", html);
        Assert.Contains("USD", html);
        Assert.Contains("EUR", html);
        Assert.Contains("10.50", html);
        Assert.Contains("11.25", html);
        Assert.Contains("Current", html); // Status for current rates
    }

    [Fact]
    public async Task GeneratePdfAsync_WithLargeDataSet_CompletesWithoutError()
    {
        // Arrange
        var composedReport = CreateLargeComposedReport(500); // 500 transactions

        // Act
        var pdfBytes = await _service.GeneratePdfAsync(composedReport);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
        
        // Should handle large datasets without throwing
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        Assert.Equal("%PDF", pdfHeader);
    }

    [Fact]
    public void GetCurrencyInformationStyles_ReturnsValidCss()
    {
        // Act
        var css = _service.GetCurrencyInformationStyles();

        // Assert
        Assert.NotEmpty(css);
        Assert.Contains(".currency-table", css);
        Assert.Contains("border-collapse", css);
        Assert.Contains("background-color", css);
    }

    private ComposedReport CreateRealComposedReport()
    {
        var transactions = CreateSampleTransactions();
        
        var chartComponent = new ChartComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Monthly Profit Analysis",
            ChartType = ChartType.Bar,
            Data = new ChartData
            {
                Labels = new[] { "Q1", "Q2", "Q3", "Q4" },
                Datasets = new[]
                {
                    new ChartDataset
                    {
                        Label = "Profit",
                        Data = new decimal[] { 75000, 85000, 92000, 88000 },
                        BackgroundColor = "#2196F3"
                    }
                }
            }
        };

        var tableComponent = new TableComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Top Transactions",
            Headers = new[] { "Date", "Account", "Amount (SEK)", "Description" },
            Rows = transactions.OrderByDescending(t => Math.Abs(t.Amount)).Take(5).Select(t => new object[]
            {
                t.Date.ToString("yyyy-MM-dd"),
                $"{t.AccountCode} - {t.AccountCategory}",
                $"{t.Amount:N2}",
                t.Description
            }).ToArray()
        };

        return new ComposedReport
        {
            ReportId = Guid.NewGuid(),
            Title = "Quarterly Financial Analysis",
            GeneratedAt = DateTime.UtcNow,
            ExcelComponents = new List<IReportComponent> { chartComponent, tableComponent },
            PdfComponents = new List<IReportComponent> { chartComponent, tableComponent },
            Transactions = transactions
        };
    }

    private ComposedReport CreateComposedReportWithMultipleComponents()
    {
        var transactions = CreateSampleTransactions();

        var chartComponent = new ChartComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Revenue Trend Chart",
            ChartType = ChartType.Line,
            Data = new ChartData
            {
                Labels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" },
                Datasets = new[]
                {
                    new ChartDataset
                    {
                        Label = "Revenue",
                        Data = new decimal[] { 120000, 135000, 128000, 142000, 155000, 148000 },
                        BackgroundColor = "#4CAF50"
                    }
                }
            }
        };

        var tableComponent = new TableComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Transaction Summary Table",
            Headers = new[] { "Account Category", "Count", "Total Amount" },
            Rows = transactions
                .GroupBy(t => t.AccountCategory ?? "Unknown")
                .Select(g => new object[]
                {
                    g.Key,
                    g.Count(),
                    g.Sum(t => t.Amount)
                }).ToArray()
        };

        var pivotComponent = new PivotTableComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Project Analysis",
            RowFields = new[] { "ProjectId" },
            ColumnFields = new[] { "AccountCategory" },
            ValueFields = new[] { "Amount" },
            Data = transactions.Where(t => !string.IsNullOrEmpty(t.ProjectId)).ToList()
        };

        return new ComposedReport
        {
            ReportId = Guid.NewGuid(),
            Title = "Comprehensive Financial Report",
            GeneratedAt = DateTime.UtcNow,
            ExcelComponents = new List<IReportComponent> { chartComponent, tableComponent, pivotComponent },
            PdfComponents = new List<IReportComponent> { chartComponent, tableComponent, pivotComponent },
            Transactions = transactions
        };
    }

    private ComposedReport CreateComposedReportWithForeignCurrency()
    {
        var transactions = CreateTransactionsWithForeignCurrency();

        var tableComponent = new TableComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "International Transactions",
            Headers = new[] { "Date", "Currency", "Amount", "SEK Equivalent", "Description" },
            Rows = transactions.Select(t => new object[]
            {
                t.Date.ToString("yyyy-MM-dd"),
                t.Currency,
                $"{t.Amount:N2}",
                "TBD", // Will be calculated with exchange rates
                t.Description
            }).ToArray()
        };

        return new ComposedReport
        {
            ReportId = Guid.NewGuid(),
            Title = "International Financial Report",
            GeneratedAt = DateTime.UtcNow,
            ExcelComponents = new List<IReportComponent> { tableComponent },
            PdfComponents = new List<IReportComponent> { tableComponent },
            Transactions = transactions
        };
    }

    private ComposedReport CreateLargeComposedReport(int transactionCount)
    {
        var transactions = CreateLargeTransactionSet(transactionCount);

        var summaryComponent = new TableComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Large Dataset Summary",
            Headers = new[] { "Month", "Transaction Count", "Total Revenue", "Total Expenses" },
            Rows = transactions
                .GroupBy(t => t.Date.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .Select(g => new object[]
                {
                    g.Key,
                    g.Count(),
                    g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                    g.Where(t => t.Amount > 0).Sum(t => t.Amount)
                }).ToArray()
        };

        return new ComposedReport
        {
            ReportId = Guid.NewGuid(),
            Title = "Large Dataset Analysis",
            GeneratedAt = DateTime.UtcNow,
            ExcelComponents = new List<IReportComponent> { summaryComponent },
            PdfComponents = new List<IReportComponent> { summaryComponent },
            Transactions = transactions
        };
    }

    private List<FinancialTransaction> CreateSampleTransactions()
    {
        return new List<FinancialTransaction>
        {
            new FinancialTransaction
            {
                TransactionId = "T001",
                Date = DateTime.UtcNow.AddDays(-60),
                AccountCode = "1930",
                AccountCategory = "Assets",
                AccountSubCategory = "Current Assets",
                Amount = 150000m,
                Currency = "SEK",
                Description = "Major client payment",
                VoucherNumber = "V001",
                ProjectId = "PRJ001"
            },
            new FinancialTransaction
            {
                TransactionId = "T002",
                Date = DateTime.UtcNow.AddDays(-45),
                AccountCode = "3001",
                AccountCategory = "Revenue",
                AccountSubCategory = "Sales Revenue",
                Amount = -125000m,
                Currency = "SEK",
                Description = "Software license sales",
                VoucherNumber = "V002",
                ProjectId = "PRJ001"
            },
            new FinancialTransaction
            {
                TransactionId = "T003",
                Date = DateTime.UtcNow.AddDays(-30),
                AccountCode = "5410",
                AccountCategory = "Expenses",
                AccountSubCategory = "External Costs",
                Amount = 25000m,
                Currency = "SEK",
                Description = "Marketing campaign",
                VoucherNumber = "V003",
                ProjectId = "PRJ002"
            },
            new FinancialTransaction
            {
                TransactionId = "T004",
                Date = DateTime.UtcNow.AddDays(-15),
                AccountCode = "6210",
                AccountCategory = "Personnel",
                AccountSubCategory = "Salaries",
                Amount = 85000m,
                Currency = "SEK",
                Description = "Monthly salaries",
                VoucherNumber = "V004"
            }
        };
    }

    private List<FinancialTransaction> CreateTransactionsWithForeignCurrency()
    {
        return new List<FinancialTransaction>
        {
            new FinancialTransaction
            {
                TransactionId = "T001",
                Date = DateTime.UtcNow.AddDays(-20),
                AccountCode = "1930",
                AccountCategory = "Assets",
                Amount = 10000m,
                Currency = "USD",
                Description = "US client payment",
                VoucherNumber = "V001"
            },
            new FinancialTransaction
            {
                TransactionId = "T002",
                Date = DateTime.UtcNow.AddDays(-15),
                AccountCode = "3001",
                AccountCategory = "Revenue",
                Amount = -7500m,
                Currency = "EUR",
                Description = "European sales",
                VoucherNumber = "V002"
            },
            new FinancialTransaction
            {
                TransactionId = "T003",
                Date = DateTime.UtcNow.AddDays(-10),
                AccountCode = "5410",
                AccountCategory = "Expenses",
                Amount = 2500m,
                Currency = "USD",
                Description = "US marketing expense",
                VoucherNumber = "V003"
            }
        };
    }

    private List<FinancialTransaction> CreateTransactionsWithMultipleCurrencies()
    {
        return new List<FinancialTransaction>
        {
            new FinancialTransaction { Currency = "USD", Amount = 5000m, Date = DateTime.UtcNow },
            new FinancialTransaction { Currency = "EUR", Amount = 3000m, Date = DateTime.UtcNow },
            new FinancialTransaction { Currency = "SEK", Amount = 50000m, Date = DateTime.UtcNow },
            new FinancialTransaction { Currency = "USD", Amount = 2000m, Date = DateTime.UtcNow },
            new FinancialTransaction { Currency = "EUR", Amount = 1500m, Date = DateTime.UtcNow }
        };
    }

    private List<FinancialTransaction> CreateLargeTransactionSet(int count)
    {
        var transactions = new List<FinancialTransaction>();
        var random = new Random(42); // Fixed seed for reproducible tests
        var accountCodes = new[] { "1930", "3001", "5410", "6210", "7510" };
        var categories = new[] { "Assets", "Revenue", "Expenses", "Personnel", "Financial" };
        var currencies = new[] { "SEK", "USD", "EUR" };

        for (int i = 0; i < count; i++)
        {
            var accountIndex = i % accountCodes.Length;
            var currencyIndex = i % currencies.Length;

            transactions.Add(new FinancialTransaction
            {
                TransactionId = $"T{i:D6}",
                Date = DateTime.UtcNow.AddDays(-random.Next(180)),
                AccountCode = accountCodes[accountIndex],
                AccountCategory = categories[accountIndex],
                Amount = (decimal)(random.NextDouble() * 200000 - 100000),
                Currency = currencies[currencyIndex],
                Description = $"Large dataset transaction {i}",
                VoucherNumber = $"V{i:D6}",
                ProjectId = i % 10 == 0 ? $"PRJ{i / 10:D3}" : null
            });
        }

        return transactions;
    }
}
