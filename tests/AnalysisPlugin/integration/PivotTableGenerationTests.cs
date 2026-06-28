using AnalysisPlugin.Components;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class PivotTableGenerationTests
{
    private readonly DynamicPivotComponent _pivotComponent;
    private readonly FinancialCalculatorService _calculatorService;
    private readonly FinancialDataValidator _dataValidator;
    private readonly AdvancedFilteringService _filteringService;
    private readonly ExcelRenderer _excelRenderer;
    
    private readonly Mock<ILogger<DynamicPivotComponent>> _mockPivotLogger;
    private readonly Mock<ILogger<FinancialCalculatorService>> _mockCalculatorLogger;
    private readonly Mock<ILogger<FinancialDataValidator>> _mockValidatorLogger;
    private readonly Mock<ILogger<AdvancedFilteringService>> _mockFilteringLogger;
    private readonly Mock<ILogger<ExcelRenderer>> _mockRendererLogger;

    public PivotTableGenerationTests()
    {
        _mockPivotLogger = new Mock<ILogger<DynamicPivotComponent>>();
        _mockCalculatorLogger = new Mock<ILogger<FinancialCalculatorService>>();
        _mockValidatorLogger = new Mock<ILogger<FinancialDataValidator>>();
        _mockFilteringLogger = new Mock<ILogger<AdvancedFilteringService>>();
        _mockRendererLogger = new Mock<ILogger<ExcelRenderer>>();
        
        _calculatorService = new FinancialCalculatorService(_mockCalculatorLogger.Object);
        _pivotComponent = new DynamicPivotComponent(_mockPivotLogger.Object, _calculatorService);
        _dataValidator = new FinancialDataValidator(_mockValidatorLogger.Object);
        _filteringService = new AdvancedFilteringService(_mockFilteringLogger.Object);
        _excelRenderer = new ExcelRenderer(_mockRendererLogger.Object);
    }

    [Fact]
    public async Task PivotTableGeneration_WithFiveYearRealDataset_GeneratesComprehensivePivot()
    {
        // Arrange
        var realDataset = CreateRealisticFiveYearDataset();
        var configuration = new ComponentConfiguration
        {
            ComponentId = Guid.NewGuid(),
            Title = "5-Year Comprehensive Financial Analysis",
            Language = "en",
            Settings = new Dictionary<string, object>
            {
                ["GroupBy"] = "AccountCategory",
                ["AggregateBy"] = "Amount",
                ["AggregateFunction"] = "Sum",
                ["HistoricalYears"] = 5,
                ["TimePeriod"] = "Yearly",
                ["EnableBASGrouping"] = true
            }
        };

        // Act
        var result = await _pivotComponent.GenerateExcelAsync(realDataset, configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("5-Year Comprehensive Financial Analysis", result.Title);
        Assert.True(result.Headers.Count >= 8); // Group + 5 years + Total + Average + Trend
        Assert.True(result.Rows.Count > 0);
        
        // Verify all years are present
        for (int year = 2020; year <= 2024; year++)
        {
            Assert.Contains(year.ToString(), result.Headers);
        }
        
        // Verify BAS categories are used
        var categoryColumn = result.Rows.Select(row => row[0].ToString()).ToList();
        Assert.Contains("Assets", categoryColumn);
        Assert.Contains("Revenue", categoryColumn);
        Assert.Contains("External Costs", categoryColumn);
    }

    [Fact]
    public async Task PivotTableGeneration_WithDataValidation_EnsuresDataQuality()
    {
        // Arrange
        var dataset = CreateRealisticFiveYearDataset();
        var validationOptions = new FinancialValidationOptions
        {
            StrictBASValidation = true,
            ValidateTemporalConsistency = true,
            EnableAnomalyDetection = true
        };

        // Act
        var validationResult = await _dataValidator.ValidateMultiYearDataAsync(dataset, validationOptions);

        // Assert
        Assert.True(validationResult.IsValid);
        Assert.True(validationResult.ValidationScore >= 80); // High quality data
        Assert.Empty(validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical));
        Assert.True(validationResult.TotalTransactions > 1000); // Substantial dataset
    }

    [Fact]
    public async Task PivotTableGeneration_WithAdvancedFiltering_FiltersCorrectly()
    {
        // Arrange
        var dataset = CreateRealisticFiveYearDataset();
        var filterCriteria = new AdvancedFilterCriteria
        {
            DateRanges = new List<DateRangeFilter>
            {
                new DateRangeFilter
                {
                    FilterType = DateFilterType.Between,
                    StartDate = new DateTime(2022, 1, 1),
                    EndDate = new DateTime(2024, 12, 31)
                }
            },
            AmountFilters = new List<AmountFilter>
            {
                new AmountFilter
                {
                    FilterType = AmountFilterType.AbsoluteGreaterThan,
                    Value = 1000m
                }
            },
            AccountFilters = new List<AccountFilter>
            {
                new AccountFilter
                {
                    FilterType = AccountFilterType.BASClassification,
                    BASClasses = new List<string> { "3", "4", "5" } // Revenue and costs
                }
            }
        };

        // Act
        var filteringResult = await _filteringService.ApplyAdvancedFiltersAsync(dataset, filterCriteria);

        // Assert
        Assert.True(filteringResult.IsSuccessful);
        Assert.True(filteringResult.FilteredCount < filteringResult.OriginalCount);
        Assert.True(filteringResult.Metrics.ReductionRatio > 0);
        Assert.All(filteringResult.FilteredTransactions, t => 
        {
            Assert.True(t.Date >= new DateTime(2022, 1, 1));
            Assert.True(t.Date <= new DateTime(2024, 12, 31));
            Assert.True(Math.Abs(t.Amount) > 1000m);
        });
    }

    [Fact]
    public async Task PivotTableGeneration_WithLargeDatasetOptimization_ProcessesWithinTimeout()
    {
        // Arrange
        var largeDataset = CreateLargeRealisticDataset(50000); // 50k transactions
        var timeoutSeconds = 60; // 1 minute timeout

        // Act
        var result = await _calculatorService.ProcessLargeDatasetWithTimeoutAsync(
            largeDataset, PeriodType.Quarterly, 3, timeoutSeconds);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.False(result.IsTimeout);
        Assert.True(result.ProcessingTimeMs < timeoutSeconds * 1000);
        Assert.True(result.ProcessedTransactions > 0);
        Assert.NotEmpty(result.AggregatedData);
        
        // Verify performance metrics
        Assert.True(result.Metrics.TransactionsPerSecond > 100);
        Assert.True(result.Metrics.EfficiencyScore > 50);
    }

    [Fact]
    public async Task PivotTableGeneration_WithStreamingProcessing_HandlesMemoryConstraints()
    {
        // Arrange
        var largeDataset = CreateLargeRealisticDataset(25000);
        var maxMemoryMB = 64; // 64MB limit
        var batchSize = 1000;

        // Act
        var batches = new List<HistoricalDataBatch>();
        await foreach (var batch in _calculatorService.ProcessHistoricalDataStreamAsync(
            largeDataset, PeriodType.Monthly, 2, batchSize, maxMemoryMB))
        {
            batches.Add(batch);
        }

        // Assert
        Assert.NotEmpty(batches);
        Assert.All(batches, batch => 
        {
            Assert.True(batch.TransactionCount <= batchSize);
            Assert.True(batch.MemoryUsedMB <= maxMemoryMB * 2); // Allow some overhead
            Assert.NotEmpty(batch.Aggregation);
        });
        
        var totalProcessed = batches.Sum(b => b.TransactionCount);
        Assert.True(totalProcessed > 0);
    }

    [Fact]
    public void PivotTableGeneration_WithProfessionalStyling_CreatesStyledExcel()
    {
        // Arrange
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("5-Year Analysis");
        
        // Create realistic pivot data structure
        var headers = new[] { "Category", "2020", "2021", "2022", "2023", "2024", "Total", "Average", "Trend %" };
        var data = new[]
        {
            new object[] { "Revenue", -500000, -550000, -600000, -650000, -700000, -3000000, -600000, 8.8 },
            new object[] { "External Costs", 200000, 220000, 240000, 260000, 280000, 1200000, 240000, 8.8 },
            new object[] { "Personnel Costs", 150000, 165000, 180000, 195000, 210000, 900000, 180000, 8.8 }
        };

        // Populate worksheet
        for (int col = 0; col < headers.Length; col++)
        {
            worksheet.Cell(1, col + 1).Value = headers[col];
        }
        
        for (int row = 0; row < data.Length; row++)
        {
            for (int col = 0; col < data[row].Length; col++)
            {
                worksheet.Cell(row + 2, col + 1).Value = data[row][col];
            }
        }

        var dataRange = worksheet.Range(1, 1, data.Length + 1, headers.Length);
        var template = _excelRenderer.CreateFinancialReportTemplate(false);

        // Act
        _excelRenderer.ApplyProfessionalStyling(worksheet, template, dataRange);

        // Assert
        // Verify styling was applied
        var headerRow = worksheet.Row(1);
        Assert.Equal(template.HeaderStyle.BackgroundColor, headerRow.Style.Fill.BackgroundColor);
        Assert.True(headerRow.Style.Font.Bold);
        
        // Verify numeric formatting
        var amountCell = worksheet.Cell(2, 2); // First amount cell
        Assert.Contains("#,##0.00", amountCell.Style.NumberFormat.Format);
        
        // Verify borders
        Assert.NotEqual(ClosedXML.Excel.XLBorderStyleValues.None, dataRange.Style.Border.OutsideBorder);
    }

    /// <summary>
    /// Creates a realistic 5-year financial dataset with proper BAS classification
    /// </summary>
    private List<FinancialTransaction> CreateRealisticFiveYearDataset()
    {
        var transactions = new List<FinancialTransaction>();
        var random = new Random(42); // Fixed seed for reproducible tests

        // Define realistic BAS account structure
        var basAccounts = new[]
        {
            ("1930", "Assets", "Bank Account", 0.0), // No growth for assets
            ("3001", "Revenue", "Sales Revenue", -0.08), // 8% annual growth (negative amounts)
            ("3010", "Revenue", "Service Revenue", -0.06), // 6% annual growth
            ("4010", "Cost of Goods Sold", "Material Costs", 0.07), // 7% annual growth
            ("5410", "External Costs", "Rent", 0.03), // 3% annual growth
            ("5420", "External Costs", "Utilities", 0.05), // 5% annual growth
            ("5610", "External Costs", "Marketing", 0.10), // 10% annual growth
            ("6210", "Personnel Costs", "Salaries", 0.04), // 4% annual growth
            ("6220", "Personnel Costs", "Social Costs", 0.04), // 4% annual growth
            ("7010", "Depreciation & Financial", "Depreciation", 0.02), // 2% annual growth
            ("7510", "Depreciation & Financial", "Interest Expense", 0.01) // 1% annual growth
        };

        for (int year = 2020; year <= 2024; year++)
        {
            foreach (var (accountCode, category, description, growthRate) in basAccounts)
            {
                // Generate monthly transactions for each account
                for (int month = 1; month <= 12; month++)
                {
                    var baseAmount = GetBaseAmount(accountCode, category);
                    var yearMultiplier = Math.Pow(1 + growthRate, year - 2020);
                    var monthlyAmount = baseAmount * (decimal)yearMultiplier;

                    // Add some randomness (±10%)
                    var variance = (decimal)(random.NextDouble() * 0.2 - 0.1);
                    monthlyAmount *= (1 + variance);

                    // Create multiple transactions per month for realism
                    var transactionsPerMonth = random.Next(3, 8);
                    for (int i = 0; i < transactionsPerMonth; i++)
                    {
                        var transactionAmount = monthlyAmount / transactionsPerMonth;
                        var day = random.Next(1, DateTime.DaysInMonth(year, month) + 1);

                        transactions.Add(new FinancialTransaction
                        {
                            TransactionId = $"{year}{month:D2}{accountCode}{i:D2}",
                            Date = new DateTime(year, month, day),
                            AccountCode = accountCode,
                            AccountCategory = category,
                            AccountSubCategory = description,
                            Amount = Math.Round(transactionAmount, 2),
                            Currency = "SEK",
                            Description = $"{description} - {year}-{month:D2}",
                            VoucherNumber = $"V{year}{month:D2}{accountCode}{i:D2}",
                            ProjectId = random.Next(1, 5) == 1 ? $"PROJ{random.Next(1, 6):D3}" : null
                        });
                    }
                }
            }
        }

        return transactions;
    }

    /// <summary>
    /// Creates a large realistic dataset for performance testing
    /// </summary>
    private List<FinancialTransaction> CreateLargeRealisticDataset(int targetTransactionCount)
    {
        var baseDataset = CreateRealisticFiveYearDataset();
        var transactions = new List<FinancialTransaction>();
        var random = new Random(42);

        // Replicate and vary the base dataset to reach target count
        var replicationFactor = Math.Max(1, targetTransactionCount / baseDataset.Count);

        for (int rep = 0; rep < replicationFactor; rep++)
        {
            foreach (var baseTransaction in baseDataset)
            {
                if (transactions.Count >= targetTransactionCount) break;

                // Create variation of the base transaction
                var variance = (decimal)(random.NextDouble() * 0.3 - 0.15); // ±15% variance
                var newAmount = baseTransaction.Amount * (1 + variance);

                transactions.Add(new FinancialTransaction
                {
                    TransactionId = $"LARGE{rep:D3}{baseTransaction.TransactionId}",
                    Date = baseTransaction.Date.AddDays(random.Next(-5, 6)), // ±5 days variance
                    AccountCode = baseTransaction.AccountCode,
                    AccountCategory = baseTransaction.AccountCategory,
                    AccountSubCategory = baseTransaction.AccountSubCategory,
                    Amount = Math.Round(newAmount, 2),
                    Currency = baseTransaction.Currency,
                    Description = $"Large Dataset - {baseTransaction.Description}",
                    VoucherNumber = $"VLARGE{rep:D3}{baseTransaction.VoucherNumber}",
                    ProjectId = baseTransaction.ProjectId
                });
            }
        }

        return transactions.Take(targetTransactionCount).ToList();
    }

    /// <summary>
    /// Gets base monthly amount for different account types
    /// </summary>
    private decimal GetBaseAmount(string accountCode, string category)
    {
        return accountCode[0] switch
        {
            '1' => 50000m,   // Assets - moderate amounts
            '2' => 30000m,   // Liabilities - moderate amounts
            '3' => -80000m,  // Revenue - large negative amounts
            '4' => 25000m,   // Cost of Goods Sold - moderate amounts
            '5' => 15000m,   // External Costs - smaller amounts
            '6' => 45000m,   // Personnel Costs - large amounts
            '7' => 8000m,    // Depreciation & Financial - smaller amounts
            '8' => 2000m,    // Extraordinary Items - small amounts
            _ => 10000m      // Default
        };
    }
}
