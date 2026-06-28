using AnalysisPlugin.Components;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ClosedXML.Excel;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class HistoricalPivotTests
{
    private readonly DynamicPivotComponent _pivotComponent;
    private readonly FinancialCalculatorService _calculatorService;
    private readonly ExcelRenderer _excelRenderer;
    private readonly Mock<ILogger<DynamicPivotComponent>> _mockPivotLogger;
    private readonly Mock<ILogger<FinancialCalculatorService>> _mockCalculatorLogger;
    private readonly Mock<ILogger<ExcelRenderer>> _mockRendererLogger;

    public HistoricalPivotTests()
    {
        _mockPivotLogger = new Mock<ILogger<DynamicPivotComponent>>();
        _mockCalculatorLogger = new Mock<ILogger<FinancialCalculatorService>>();
        _mockRendererLogger = new Mock<ILogger<ExcelRenderer>>();
        
        _calculatorService = new FinancialCalculatorService(_mockCalculatorLogger.Object);
        _pivotComponent = new DynamicPivotComponent(_mockPivotLogger.Object, _calculatorService);
        _excelRenderer = new ExcelRenderer(_mockRendererLogger.Object);
    }

    [Fact]
    public async Task DynamicPivotComponent_WithThreeYearHistoricalData_GeneratesCorrectPivot()
    {
        // Arrange
        var transactions = CreateThreeYearHistoricalData();
        var configuration = new ComponentConfiguration
        {
            ComponentId = Guid.NewGuid(),
            Title = "3-Year Historical Analysis",
            Language = "en",
            Settings = new Dictionary<string, object>
            {
                ["GroupBy"] = "AccountCategory",
                ["AggregateBy"] = "Amount",
                ["AggregateFunction"] = "Sum",
                ["HistoricalYears"] = 3,
                ["TimePeriod"] = "Yearly",
                ["EnableBASGrouping"] = false
            }
        };

        // Act
        var result = await _pivotComponent.GenerateExcelAsync(transactions, configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("3-Year Historical Analysis", result.Title);
        Assert.True(result.Headers.Count >= 5); // Group + 3 years + Total + Average + Trend
        Assert.True(result.Rows.Count > 0);
        
        // Verify historical structure
        Assert.Contains("2022", result.Headers);
        Assert.Contains("2023", result.Headers);
        Assert.Contains("2024", result.Headers);
        Assert.Contains("Total", result.Headers);
        Assert.Contains("Trend %", result.Headers);
    }

    [Fact]
    public async Task DynamicPivotComponent_WithQuarterlyPeriods_GeneratesQuarterlyPivot()
    {
        // Arrange
        var transactions = CreateTwoYearQuarterlyData();
        var configuration = new ComponentConfiguration
        {
            ComponentId = Guid.NewGuid(),
            Title = "Quarterly Historical Analysis",
            Language = "sv",
            Settings = new Dictionary<string, object>
            {
                ["GroupBy"] = "AccountCategory",
                ["AggregateBy"] = "Amount",
                ["AggregateFunction"] = "Sum",
                ["HistoricalYears"] = 2,
                ["TimePeriod"] = "Quarterly",
                ["EnableBASGrouping"] = false
            }
        };

        // Act
        var result = await _pivotComponent.GenerateExcelAsync(transactions, configuration);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Headers.Count >= 11); // Group + 8 quarters + Total + Average + Trend
        
        // Verify quarterly structure
        Assert.Contains("2023-Q1", result.Headers);
        Assert.Contains("2023-Q4", result.Headers);
        Assert.Contains("2024-Q1", result.Headers);
        Assert.Contains("2024-Q4", result.Headers);
    }

    [Fact]
    public async Task DynamicPivotComponent_WithBASGrouping_UsesBASCategories()
    {
        // Arrange
        var transactions = CreateBASClassifiedData();
        var configuration = new ComponentConfiguration
        {
            ComponentId = Guid.NewGuid(),
            Title = "BAS Historical Analysis",
            Language = "sv",
            Settings = new Dictionary<string, object>
            {
                ["GroupBy"] = "AccountCategory",
                ["AggregateBy"] = "Amount",
                ["AggregateFunction"] = "Sum",
                ["HistoricalYears"] = 2,
                ["TimePeriod"] = "Yearly",
                ["EnableBASGrouping"] = true
            }
        };

        // Act
        var result = await _pivotComponent.GenerateExcelAsync(transactions, configuration);

        // Assert
        Assert.NotNull(result);
        
        // Verify BAS categories are used
        var categoryColumn = result.Rows.Select(row => row[0].ToString()).ToList();
        Assert.Contains("Tillgångar", categoryColumn); // Assets in Swedish
        Assert.Contains("Intäkter", categoryColumn);   // Revenue in Swedish
        Assert.Contains("Externa kostnader", categoryColumn); // External costs in Swedish
    }

    [Fact]
    public async Task FinancialCalculatorService_ProcessHistoricalDataStream_HandlesLargeDataset()
    {
        // Arrange
        var largeDataset = CreateLargeHistoricalDataset(5000); // 5000 transactions
        var batchSize = 500;
        var maxMemoryMB = 100;

        // Act
        var batches = new List<HistoricalDataBatch>();
        await foreach (var batch in _calculatorService.ProcessHistoricalDataStreamAsync(
            largeDataset, PeriodType.Monthly, 2, batchSize, maxMemoryMB))
        {
            batches.Add(batch);
        }

        // Assert
        Assert.NotEmpty(batches);
        Assert.All(batches, batch => Assert.True(batch.TransactionCount <= batchSize));
        Assert.All(batches, batch => Assert.True(batch.MemoryUsedMB <= maxMemoryMB * 2)); // Allow some overhead
        
        var totalTransactionsProcessed = batches.Sum(b => b.TransactionCount);
        Assert.True(totalTransactionsProcessed > 0);
    }

    [Fact]
    public async Task FinancialCalculatorService_ProcessHistoricalDataPaginated_ReturnsCorrectPages()
    {
        // Arrange
        var transactions = CreateThreeYearHistoricalData();
        var pageSize = 6; // 6 periods per page
        var pageNumber = 1;

        // Act
        var result = await _calculatorService.ProcessHistoricalDataPaginatedAsync(
            transactions, PeriodType.Monthly, 3, pageSize, pageNumber);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pageNumber, result.PageNumber);
        Assert.Equal(pageSize, result.PageSize);
        Assert.True(result.TotalPages > 1); // 3 years * 12 months = 36 periods, should be multiple pages
        Assert.Equal(PeriodType.Monthly, result.PeriodType);
        Assert.Equal(3, result.HistoricalYears);
        Assert.True(result.Results.Count <= pageSize);
    }

    [Fact]
    public void ExcelRenderer_ApplyHistoricalPivotTemplate_CreatesStyledWorksheet()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Historical Pivot Test");
        
        // Create sample data
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "2022";
        worksheet.Cell(1, 3).Value = "2023";
        worksheet.Cell(1, 4).Value = "2024";
        worksheet.Cell(1, 5).Value = "Total";
        worksheet.Cell(1, 6).Value = "Trend %";
        
        worksheet.Cell(2, 1).Value = "Revenue";
        worksheet.Cell(2, 2).Value = 100000;
        worksheet.Cell(2, 3).Value = 120000;
        worksheet.Cell(2, 4).Value = 140000;
        worksheet.Cell(2, 5).Value = 360000;
        worksheet.Cell(2, 6).Value = 20.0;
        
        var dataRange = worksheet.Range("A1:F2");
        var template = _excelRenderer.CreateHistoricalPivotTemplate(false);

        // Act
        _excelRenderer.ApplyProfessionalStyling(worksheet, template, dataRange);

        // Assert
        // Verify header styling
        var headerRow = worksheet.Row(1);
        Assert.Equal(template.HeaderStyle.BackgroundColor, headerRow.Style.Fill.BackgroundColor);
        Assert.Equal(template.HeaderStyle.FontColor, headerRow.Style.Font.FontColor);
        Assert.True(headerRow.Style.Font.Bold);
        
        // Verify freeze panes
        Assert.True(worksheet.SheetView.FreezeRows > 0);
        Assert.True(worksheet.SheetView.FreezeColumns > 0);
    }

    [Fact]
    public void ExcelRenderer_CreateBASClassificationTemplate_HasCorrectCategoryColors()
    {
        // Act
        var template = _excelRenderer.CreateBASClassificationTemplate(true);

        // Assert
        Assert.NotNull(template.CategoryColors);
        Assert.True(template.CategoryColors.ContainsKey("Tillgångar"));
        Assert.True(template.CategoryColors.ContainsKey("Intäkter"));
        Assert.True(template.CategoryColors.ContainsKey("Kostnader"));
        
        // Verify colors are different
        var assetColor = template.CategoryColors["Tillgångar"];
        var revenueColor = template.CategoryColors["Intäkter"];
        Assert.NotEqual(assetColor, revenueColor);
    }

    private List<FinancialTransaction> CreateThreeYearHistoricalData()
    {
        var transactions = new List<FinancialTransaction>();
        var random = new Random(42); // Fixed seed for reproducible tests
        var categories = new[] { "Revenue", "Expenses", "Assets", "Liabilities" };
        var accountCodes = new[] { "3001", "5410", "1930", "2440" };

        for (int year = 2022; year <= 2024; year++)
        {
            for (int month = 1; month <= 12; month++)
            {
                for (int i = 0; i < categories.Length; i++)
                {
                    var baseAmount = (year - 2021) * 10000 + month * 1000; // Trending amounts
                    var amount = baseAmount + random.Next(-2000, 2000);

                    transactions.Add(new FinancialTransaction
                    {
                        TransactionId = $"T{year}{month:D2}{i:D2}",
                        Date = new DateTime(year, month, 15), // Mid-month
                        AccountCode = accountCodes[i],
                        AccountCategory = categories[i],
                        Amount = categories[i] == "Revenue" ? -amount : amount, // Revenue is negative
                        Currency = "SEK",
                        Description = $"{categories[i]} transaction for {year}-{month:D2}",
                        VoucherNumber = $"V{year}{month:D2}{i:D2}"
                    });
                }
            }
        }

        return transactions;
    }

    private List<FinancialTransaction> CreateTwoYearQuarterlyData()
    {
        var transactions = new List<FinancialTransaction>();
        var categories = new[] { "Revenue", "Expenses", "Personnel" };
        var accountCodes = new[] { "3001", "5410", "6210" };

        for (int year = 2023; year <= 2024; year++)
        {
            for (int quarter = 1; quarter <= 4; quarter++)
            {
                var startMonth = (quarter - 1) * 3 + 1;
                for (int month = 0; month < 3; month++)
                {
                    var currentMonth = startMonth + month;
                    if (currentMonth > 12) continue;

                    for (int i = 0; i < categories.Length; i++)
                    {
                        var amount = quarter * 25000 + (i + 1) * 5000;

                        transactions.Add(new FinancialTransaction
                        {
                            TransactionId = $"Q{year}{quarter}{currentMonth}{i}",
                            Date = new DateTime(year, currentMonth, 10),
                            AccountCode = accountCodes[i],
                            AccountCategory = categories[i],
                            Amount = categories[i] == "Revenue" ? -amount : amount,
                            Currency = "SEK",
                            Description = $"{categories[i]} Q{quarter} {year}",
                            VoucherNumber = $"VQ{year}{quarter}{i}"
                        });
                    }
                }
            }
        }

        return transactions;
    }

    private List<FinancialTransaction> CreateBASClassifiedData()
    {
        var transactions = new List<FinancialTransaction>();
        var basAccounts = new[]
        {
            ("1930", "Assets", "Tillgångar"),
            ("3001", "Revenue", "Intäkter"),
            ("5410", "External Costs", "Externa kostnader"),
            ("6210", "Personnel Costs", "Personalkostnader")
        };

        for (int year = 2023; year <= 2024; year++)
        {
            foreach (var (accountCode, categoryEn, categorySv) in basAccounts)
            {
                for (int month = 1; month <= 12; month++)
                {
                    var amount = year * 1000 + month * 100;

                    transactions.Add(new FinancialTransaction
                    {
                        TransactionId = $"BAS{year}{month}{accountCode}",
                        Date = new DateTime(year, month, 20),
                        AccountCode = accountCode,
                        AccountCategory = categoryEn,
                        Amount = categoryEn == "Revenue" ? -amount : amount,
                        Currency = "SEK",
                        Description = $"BAS {categorySv} {year}-{month:D2}",
                        VoucherNumber = $"VBAS{year}{month}"
                    });
                }
            }
        }

        return transactions;
    }

    private List<FinancialTransaction> CreateLargeHistoricalDataset(int transactionCount)
    {
        var transactions = new List<FinancialTransaction>();
        var random = new Random(42);
        var categories = new[] { "Revenue", "Expenses", "Assets", "Liabilities", "Personnel" };
        var accountCodes = new[] { "3001", "5410", "1930", "2440", "6210" };

        for (int i = 0; i < transactionCount; i++)
        {
            var categoryIndex = i % categories.Length;
            var year = 2022 + (i % 3); // Spread across 3 years
            var month = (i % 12) + 1;
            var day = (i % 28) + 1;

            transactions.Add(new FinancialTransaction
            {
                TransactionId = $"LARGE{i:D6}",
                Date = new DateTime(year, month, day),
                AccountCode = accountCodes[categoryIndex],
                AccountCategory = categories[categoryIndex],
                Amount = random.Next(-100000, 100000),
                Currency = "SEK",
                Description = $"Large dataset transaction {i}",
                VoucherNumber = $"VLARGE{i:D6}"
            });
        }

        return transactions;
    }
}
