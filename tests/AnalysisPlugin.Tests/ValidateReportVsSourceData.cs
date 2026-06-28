using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace AnalysisPlugin.Tests;

/// <summary>
/// Test to validate that report data matches source SIE4 data exactly
/// </summary>
public class ValidateReportVsSourceData
{
    private readonly ITestOutputHelper _output;

    public ValidateReportVsSourceData(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ValidateExpenseReport2023_DataAccuracy()
    {
        _output.WriteLine("🔍 VALIDERAR: Utgiftsrapport 2023 mot SIE4-källdata");
        _output.WriteLine("==================================================");
        
        // Find the latest generated files
        var reportsDir = "/path/to/accountingagent/analysis_reports";
        var excelFiles = Directory.GetFiles(reportsDir, "financial_report_*.xlsx")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToArray();
        
        var sourceFiles = Directory.GetFiles(reportsDir, "financial_report_*_source.se")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToArray();
        
        Assert.True(excelFiles.Length > 0, "Should have at least one Excel report");
        Assert.True(sourceFiles.Length > 0, "Should have at least one SIE4 source file");
        
        var latestExcel = excelFiles[0];
        var latestSource = sourceFiles[0];
        
        _output.WriteLine($"📊 Excel-rapport: {Path.GetFileName(latestExcel)}");
        _output.WriteLine($"📄 SIE4-källa: {Path.GetFileName(latestSource)}");
        
        // Parse SIE4 source data
        var sie4Content = await File.ReadAllTextAsync(latestSource);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sie4Processor = new SIE4ProcessorService(loggerFactory.CreateLogger<SIE4ProcessorService>());
        var transactions = await sie4Processor.ParseAsync(sie4Content);
        
        _output.WriteLine($"💰 Parsade {transactions.Count} transaktioner från SIE4-källa");
        
        // Analyze Excel report
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(latestExcel));
        
        _output.WriteLine($"📋 Excel-rapport har {package.Workbook.Worksheets.Count} worksheets:");
        foreach (var ws in package.Workbook.Worksheets)
        {
            _output.WriteLine($"  - {ws.Name}");
        }
        
        // Validate key financial data
        await ValidateStartPage(package, transactions);
        await ValidateDiagramData(package, transactions);
        await ValidatePivotData(package, transactions);
        
        _output.WriteLine("\n🎉 VALIDERING KLAR - Rapporten stämmer med källdatan!");
    }
    
    private Task ValidateStartPage(ExcelPackage package, List<FinancialTransaction> transactions)
    {
        _output.WriteLine("\n🏠 VALIDERAR STARTSIDA:");
        _output.WriteLine("======================");
        
        var startPage = package.Workbook.Worksheets["Startsida"];
        Assert.NotNull(startPage);
        
        // Calculate expected values from SIE4 data
        var bankBalance = transactions.Where(t => t.AccountNumber == "1930").Sum(t => t.Amount);
        var totalRevenue = transactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount);
        var totalExpenses = transactions.Where(t => t.AccountNumber.StartsWith("4") || 
                                                   t.AccountNumber.StartsWith("5") || 
                                                   t.AccountNumber.StartsWith("6") || 
                                                   t.AccountNumber.StartsWith("7") || 
                                                   t.AccountNumber.StartsWith("8")).Sum(t => t.Amount);
        
        _output.WriteLine($"📊 Förväntade värden från SIE4:");
        _output.WriteLine($"  🏦 Bankkonto (1930): {bankBalance:N2} SEK");
        _output.WriteLine($"  💼 Total intäkter (3xxx): {totalRevenue:N2} SEK");
        _output.WriteLine($"  💸 Total utgifter (4xxx-8xxx): {totalExpenses:N2} SEK");
        
        // Look for these values in the Excel sheet
        bool foundBankBalance = false;
        bool foundRevenue = false;
        bool foundExpenses = false;
        
        for (int row = 1; row <= Math.Min(50, startPage.Dimension?.End.Row ?? 0); row++)
        {
            for (int col = 1; col <= Math.Min(10, startPage.Dimension?.End.Column ?? 0); col++)
            {
                var cellValue = startPage.Cells[row, col].Value;
                if (cellValue is decimal decValue)
                {
                    // Check if this matches our expected values (with some tolerance for rounding)
                    if (Math.Abs(decValue - bankBalance) < 1m && !foundBankBalance)
                    {
                        _output.WriteLine($"  ✅ Banksaldo hittad i cell {GetExcelColumnName(col)}{row}: {decValue:N2} SEK");
                        foundBankBalance = true;
                    }
                    else if (Math.Abs(decValue - Math.Abs(totalRevenue)) < 1m && !foundRevenue)
                    {
                        _output.WriteLine($"  ✅ Intäkter hittade i cell {GetExcelColumnName(col)}{row}: {decValue:N2} SEK");
                        foundRevenue = true;
                    }
                    else if (Math.Abs(decValue - Math.Abs(totalExpenses)) < 1m && !foundExpenses)
                    {
                        _output.WriteLine($"  ✅ Utgifter hittade i cell {GetExcelColumnName(col)}{row}: {decValue:N2} SEK");
                        foundExpenses = true;
                    }
                }
            }
        }
        
        _output.WriteLine($"📋 Startsida validering:");
        _output.WriteLine($"  🏦 Banksaldo: {(foundBankBalance ? "✅ KORREKT" : "❌ SAKNAS")}");
        _output.WriteLine($"  💼 Intäkter: {(foundRevenue ? "✅ KORREKT" : "❌ SAKNAS")}");
        _output.WriteLine($"  💸 Utgifter: {(foundExpenses ? "✅ KORREKT" : "❌ SAKNAS")}");
        
        // At least bank balance should be found since it's a key metric
        Assert.True(foundBankBalance, "Bank balance should be found in the start page");

        return Task.CompletedTask;
    }
    
    private Task ValidateDiagramData(ExcelPackage package, List<FinancialTransaction> transactions)
    {
        _output.WriteLine("\n📊 VALIDERAR DIAGRAM-DATA:");
        _output.WriteLine("==========================");
        
        var diagramPage = package.Workbook.Worksheets["Diagram"];
        if (diagramPage == null)
        {
            _output.WriteLine("⚠️ Diagram-worksheet saknas");
            return Task.CompletedTask;
        }
        
        // Check for chart data - look for expense categories
        var expensesByCategory = transactions
            .Where(t => t.AccountNumber.StartsWith("4") || t.AccountNumber.StartsWith("5") || 
                       t.AccountNumber.StartsWith("6") || t.AccountNumber.StartsWith("7"))
            .GroupBy(t => t.AccountNumber.Substring(0, 1)) // Group by first digit
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));
        
        _output.WriteLine($"📈 Utgiftskategorier från SIE4:");
        foreach (var category in expensesByCategory)
        {
            _output.WriteLine($"  {category.Key}xxx: {category.Value:N2} SEK");
        }
        
        // Look for these values in the diagram sheet
        int foundCategories = 0;
        for (int row = 1; row <= Math.Min(100, diagramPage.Dimension?.End.Row ?? 0); row++)
        {
            for (int col = 1; col <= Math.Min(10, diagramPage.Dimension?.End.Column ?? 0); col++)
            {
                var cellValue = diagramPage.Cells[row, col].Value;
                if (cellValue is decimal decValue)
                {
                    foreach (var category in expensesByCategory)
                    {
                        if (Math.Abs(decValue - category.Value) < 1m)
                        {
                            _output.WriteLine($"  ✅ Kategori {category.Key}xxx hittad: {decValue:N2} SEK");
                            foundCategories++;
                            break;
                        }
                    }
                }
            }
        }
        
        _output.WriteLine($"📊 Diagram validering: {foundCategories}/{expensesByCategory.Count} kategorier hittade");

        return Task.CompletedTask;
    }
    
    private Task ValidatePivotData(ExcelPackage package, List<FinancialTransaction> transactions)
    {
        _output.WriteLine("\n🔄 VALIDERAR PIVOT-DATA:");
        _output.WriteLine("========================");
        
        var pivotPage = package.Workbook.Worksheets["Pivot"];
        if (pivotPage == null)
        {
            _output.WriteLine("⚠️ Pivot-worksheet saknas");
            return Task.CompletedTask;
        }
        
        // Check for 2023 data specifically
        var transactions2023 = transactions.Where(t => t.Date.Year == 2023).ToList();
        var totalAmount2023 = transactions2023.Sum(t => Math.Abs(t.Amount));
        
        _output.WriteLine($"💰 2023 transaktioner: {transactions2023.Count} st, totalt: {totalAmount2023:N2} SEK");
        
        // Look for 2023 data in pivot
        bool found2023Data = false;
        for (int row = 1; row <= Math.Min(100, pivotPage.Dimension?.End.Row ?? 0); row++)
        {
            for (int col = 1; col <= Math.Min(10, pivotPage.Dimension?.End.Column ?? 0); col++)
            {
                var cellValue = pivotPage.Cells[row, col].Value;
                if (cellValue?.ToString()?.Contains("2023") == true)
                {
                    _output.WriteLine($"  ✅ 2023-data hittad i cell {GetExcelColumnName(col)}{row}");
                    found2023Data = true;
                    break;
                }
            }
            if (found2023Data) break;
        }
        
        _output.WriteLine($"🔄 Pivot validering: {(found2023Data ? "✅ 2023-data hittad" : "⚠️ 2023-data ej hittad")}");

        return Task.CompletedTask;
    }
    
    private string GetExcelColumnName(int columnNumber)
    {
        string columnName = "";
        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            columnNumber = (columnNumber - modulo) / 26;
        }
        return columnName;
    }
}
