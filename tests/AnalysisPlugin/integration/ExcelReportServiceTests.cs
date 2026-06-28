using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using AnalysisPlugin.Components;
using Microsoft.Extensions.Logging;
using Moq;
using ClosedXML.Excel;
using Xunit;
using System.Text;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class ExcelReportServiceTests
{
    private readonly ExcelGeneratorService _service;
    private readonly Mock<ILogger<ExcelGeneratorService>> _mockLogger;
    private readonly Mock<RiksbankService> _mockRiksbankService;

    public ExcelReportServiceTests()
    {
        _mockLogger = new Mock<ILogger<ExcelGeneratorService>>();
        _mockRiksbankService = new Mock<RiksbankService>(Mock.Of<HttpClient>(), Mock.Of<ILogger<RiksbankService>>());
        _service = new ExcelGeneratorService(_mockLogger.Object, _mockRiksbankService.Object);
    }

    [Fact]
    public async Task GenerateExcelAsync_WithRealFinancialData_CreatesValidExcelFile()
    {
        // Arrange
        var composedReport = CreateRealComposedReport();
        
        // Mock exchange rate for foreign currency test
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
        var excelBytes = await _service.GenerateExcelAsync(composedReport);

        // Assert
        Assert.NotNull(excelBytes);
        Assert.True(excelBytes.Length > 0);

        // Validate Excel file structure
        using var stream = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(stream);
        
        // Should have summary worksheet
        Assert.True(workbook.Worksheets.Any(w => w.Name.Contains("Summary")));
        
        // Should have component worksheets
        Assert.True(workbook.Worksheets.Count >= composedReport.ExcelComponents.Count);
        
        // Validate chart component worksheet
        var chartWorksheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Chart"));
        Assert.NotNull(chartWorksheet);
        
        // Check that data is present
        Assert.True(chartWorksheet.CellsUsed().Any());
        
        // Validate table component worksheet
        var tableWorksheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Table"));
        Assert.NotNull(tableWorksheet);
        
        // Check table headers and data
        var usedCells = tableWorksheet.CellsUsed().ToList();
        Assert.True(usedCells.Count > 10); // Should have headers + data rows
    }

    [Fact]
    public async Task GenerateExcelAsync_WithForeignCurrency_IncludesCurrencyInformation()
    {
        // Arrange
        var composedReport = CreateComposedReportWithForeignCurrency();
        
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
        var excelBytes = await _service.GenerateExcelAsync(composedReport);

        // Assert
        using var stream = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(stream);
        
        // Should have currency information worksheet
        var currencyWorksheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("Currency"));
        Assert.NotNull(currencyWorksheet);
        
        // Check currency data
        var currencyCell = currencyWorksheet.CellsUsed().FirstOrDefault(c => c.GetString().Contains("EUR"));
        Assert.NotNull(currencyCell);
    }

    [Fact]
    public async Task FormatCurrencyWithExchangeRateAsync_WithValidCurrency_ReturnsFormattedString()
    {
        // Arrange
        var amount = 1000m;
        var currency = "USD";
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
        var result = await _service.FormatCurrencyWithExchangeRateAsync(amount, currency);

        // Assert
        Assert.Contains("1,000.00 USD", result);
        Assert.Contains("10,500.00 SEK", result); // 1000 * 10.50
    }

    [Fact]
    public async Task GenerateExcelAsync_WithLargeDataSet_CompletesWithoutError()
    {
        // Arrange
        var composedReport = CreateLargeComposedReport(1000); // 1000 transactions

        // Act
        var excelBytes = await _service.GenerateExcelAsync(composedReport);

        // Assert
        Assert.NotNull(excelBytes);
        Assert.True(excelBytes.Length > 0);
        
        // Validate file can be opened
        using var stream = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(stream);
        Assert.True(workbook.Worksheets.Any());
    }

    private ComposedReport CreateRealComposedReport()
    {
        var transactions = CreateSampleTransactions();
        
        var chartComponent = new ChartComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Monthly Revenue Trend",
            ChartType = ChartType.Line,
            Data = new ChartData
            {
                Labels = new[] { "Jan", "Feb", "Mar", "Apr" },
                Datasets = new[]
                {
                    new ChartDataset
                    {
                        Label = "Revenue",
                        Data = new decimal[] { 100000, 120000, 110000, 130000 },
                        BackgroundColor = "#4CAF50"
                    }
                }
            }
        };

        var tableComponent = new TableComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Transaction Summary",
            Headers = new[] { "Date", "Account", "Amount", "Description" },
            Rows = transactions.Take(10).Select(t => new object[]
            {
                t.Date.ToString("yyyy-MM-dd"),
                t.AccountCode,
                t.Amount,
                t.Description
            }).ToArray()
        };

        return new ComposedReport
        {
            ReportId = Guid.NewGuid(),
            Title = "Financial Report Test",
            GeneratedAt = DateTime.UtcNow,
            ExcelComponents = new List<IReportComponent> { chartComponent, tableComponent },
            PdfComponents = new List<IReportComponent> { chartComponent, tableComponent },
            Transactions = transactions
        };
    }

    private ComposedReport CreateComposedReportWithForeignCurrency()
    {
        var transactions = CreateSampleTransactionsWithForeignCurrency();

        var tableComponent = new TableComponent
        {
            ComponentId = Guid.NewGuid(),
            Title = "Foreign Currency Transactions",
            Headers = new[] { "Date", "Account", "Amount", "Currency", "Description" },
            Rows = transactions.Take(5).Select(t => new object[]
            {
                t.Date.ToString("yyyy-MM-dd"),
                t.AccountCode,
                t.Amount,
                t.Currency,
                t.Description
            }).ToArray()
        };

        return new ComposedReport
        {
            ReportId = Guid.NewGuid(),
            Title = "Foreign Currency Report Test",
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
            Headers = new[] { "Account Category", "Transaction Count", "Total Amount" },
            Rows = transactions
                .GroupBy(t => t.AccountCategory ?? "Unknown")
                .Select(g => new object[]
                {
                    g.Key,
                    g.Count(),
                    g.Sum(t => t.Amount)
                }).ToArray()
        };

        return new ComposedReport
        {
            ReportId = Guid.NewGuid(),
            Title = "Large Dataset Report Test",
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
                Date = DateTime.UtcNow.AddDays(-30),
                AccountCode = "1930",
                AccountCategory = "Assets",
                AccountSubCategory = "Current Assets",
                Amount = 50000m,
                Currency = "SEK",
                Description = "Bank deposit",
                VoucherNumber = "V001"
            },
            new FinancialTransaction
            {
                TransactionId = "T002",
                Date = DateTime.UtcNow.AddDays(-25),
                AccountCode = "3001",
                AccountCategory = "Revenue",
                AccountSubCategory = "Sales Revenue",
                Amount = -45000m,
                Currency = "SEK",
                Description = "Sales revenue",
                VoucherNumber = "V002"
            },
            new FinancialTransaction
            {
                TransactionId = "T003",
                Date = DateTime.UtcNow.AddDays(-20),
                AccountCode = "5410",
                AccountCategory = "Expenses",
                AccountSubCategory = "External Costs",
                Amount = 15000m,
                Currency = "SEK",
                Description = "Office rent",
                VoucherNumber = "V003"
            }
        };
    }

    private List<FinancialTransaction> CreateSampleTransactionsWithForeignCurrency()
    {
        return new List<FinancialTransaction>
        {
            new FinancialTransaction
            {
                TransactionId = "T001",
                Date = DateTime.UtcNow.AddDays(-10),
                AccountCode = "1930",
                AccountCategory = "Assets",
                Amount = 5000m,
                Currency = "EUR",
                Description = "European client payment",
                VoucherNumber = "V001"
            },
            new FinancialTransaction
            {
                TransactionId = "T002",
                Date = DateTime.UtcNow.AddDays(-5),
                AccountCode = "3001",
                AccountCategory = "Revenue",
                Amount = -3000m,
                Currency = "USD",
                Description = "US sales",
                VoucherNumber = "V002"
            }
        };
    }

    private List<FinancialTransaction> CreateLargeTransactionSet(int count)
    {
        var transactions = new List<FinancialTransaction>();
        var random = new Random(42); // Fixed seed for reproducible tests
        var accountCodes = new[] { "1930", "3001", "5410", "6210", "7510" };
        var categories = new[] { "Assets", "Revenue", "Expenses", "Personnel", "Financial" };

        for (int i = 0; i < count; i++)
        {
            var accountIndex = i % accountCodes.Length;
            transactions.Add(new FinancialTransaction
            {
                TransactionId = $"T{i:D6}",
                Date = DateTime.UtcNow.AddDays(-random.Next(365)),
                AccountCode = accountCodes[accountIndex],
                AccountCategory = categories[accountIndex],
                Amount = (decimal)(random.NextDouble() * 100000 - 50000),
                Currency = "SEK",
                Description = $"Transaction {i}",
                VoucherNumber = $"V{i:D6}"
            });
        }

        return transactions;
    }
}
