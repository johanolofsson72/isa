using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Components;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace AnalysisPlugin.Tests;

/// <summary>
/// Test to validate that real SIE4 data is used in actual Excel reports
/// </summary>
public class ValidateRealDataInReports
{
    private readonly ITestOutputHelper _output;

    public ValidateRealDataInReports(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestRealDataInExcelReport()
    {
        _output.WriteLine("🎯 VALIDERAR: Riktig SIE4-data används i Excel-rapporter");
        _output.WriteLine("=======================================================");
        
        // Read real SIE4 file
        var sie4FilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "DemoEkonomiAB20251104_203034.se");
        var sie4Content = await File.ReadAllTextAsync(sie4FilePath);
        
        _output.WriteLine($"📄 Laddat SIE4-fil: {sie4Content.Length} tecken");
        
        // Set EPPlus license
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        // Test FrontPageComponent with real data
        await TestFrontPageWithRealData(sie4Content);
        
        // Test IncomeStatementComponent with real data
        await TestIncomeStatementWithRealData(sie4Content);
        
        // Create actual Excel file to verify
        await CreateValidationExcelReport(sie4Content);
    }
    
    private async Task TestFrontPageWithRealData(string sie4Content)
    {
        _output.WriteLine("\n🏠 TESTAR STARTSIDA MED RIKTIG DATA:");
        _output.WriteLine("===================================");
        
        var component = new FrontPageComponent();
        component.SIE4Data = sie4Content;
        component.Title = "Demo Ekonomi AB - Validering av Riktig Data";
        
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Startsida Test");
        
        var result = await component.RenderAsync(worksheet, 1);
        
        _output.WriteLine($"✅ Rendering lyckades: {result.Success}");
        _output.WriteLine($"📊 Rader använda: {result.RowsUsed}");
        
        Assert.True(result.Success, "FrontPage rendering should succeed");
        Assert.True(result.RowsUsed > 10, "Should use multiple rows for content");
        
        // Verify that real data is used by checking parsed transactions
        Assert.True(component.ParsedTransactions.Count > 1000, $"Should have many transactions, got {component.ParsedTransactions.Count}");
        
        // Verify specific account balances
        var bankBalance = component.GetAccountBalance("1930");
        var revenueBalance = component.GetAccountBalance("3311");
        
        _output.WriteLine($"🏦 Bankkonto (1930): {bankBalance:N2} SEK");
        _output.WriteLine($"💼 Intäkter (3311): {revenueBalance:N2} SEK");
        
        // These should NOT be the old fallback values
        Assert.NotEqual(3437m, bankBalance); // Old fallback value
        Assert.NotEqual(0m, bankBalance); // Should have real data
        Assert.NotEqual(0m, Math.Abs(revenueBalance)); // Should have real revenue data
        
        _output.WriteLine("✅ Startsida använder RIKTIG data (inte fallback-värden)!");
    }
    
    private async Task TestIncomeStatementWithRealData(string sie4Content)
    {
        _output.WriteLine("\n📊 TESTAR RESULTATRAPPORT MED RIKTIG DATA:");
        _output.WriteLine("==========================================");
        
        var component = new IncomeStatementComponent(2020);
        component.SIE4Data = sie4Content;
        
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Resultatrapport Test");
        
        var result = await component.RenderAsync(worksheet, 1);
        
        _output.WriteLine($"✅ Rendering lyckades: {result.Success}");
        _output.WriteLine($"📊 Rader använda: {result.RowsUsed}");
        
        Assert.True(result.Success, "IncomeStatement rendering should succeed");
        
        // Verify real data for 2020
        var revenue3311 = component.GetAccountBalance("3311", 2020);
        var salaries7220 = component.GetAccountBalance("7220", 2020);
        
        _output.WriteLine($"💼 Intäkter 3311 (2020): {revenue3311:N2} SEK");
        _output.WriteLine($"👥 Löner 7220 (2020): {salaries7220:N2} SEK");
        
        // Should have real data, not fallback values
        Assert.NotEqual(0m, Math.Abs(revenue3311)); // Should have revenue data
        Assert.NotEqual(0m, Math.Abs(salaries7220)); // Should have salary data
        
        _output.WriteLine("✅ Resultatrapport använder RIKTIG data för 2020!");
    }
    
    private async Task CreateValidationExcelReport(string sie4Content)
    {
        _output.WriteLine("\n📋 SKAPAR VALIDERINGS-EXCEL MED RIKTIG DATA:");
        _output.WriteLine("============================================");
        
        using var package = new ExcelPackage();
        
        // Create FrontPage with real data
        var frontPage = new FrontPageComponent();
        frontPage.SIE4Data = sie4Content;
        frontPage.Title = "VALIDERING: Demo Ekonomi AB - Riktig SIE4 Data";
        
        var worksheet1 = package.Workbook.Worksheets.Add("Startsida - Riktig Data");
        var result1 = await frontPage.RenderAsync(worksheet1, 1);
        
        // Create IncomeStatement with real data
        var incomeStatement = new IncomeStatementComponent(2020);
        incomeStatement.SIE4Data = sie4Content;
        
        var worksheet2 = package.Workbook.Worksheets.Add("Resultat 2020 - Riktig Data");
        var result2 = await incomeStatement.RenderAsync(worksheet2, 1);
        
        // Save validation report
        var fileName = $"VALIDERING-Riktig-Data-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
        var reportsDir = "/path/to/accountingagent/analysis_reports";
        Directory.CreateDirectory(reportsDir);
        var filePath = Path.Combine(reportsDir, fileName);
        
        await package.SaveAsAsync(new FileInfo(filePath));
        
        _output.WriteLine($"✅ Validerings-rapport skapad: {fileName}");
        _output.WriteLine($"📁 Sökväg: {filePath}");
        _output.WriteLine($"📊 Startsida rader: {result1.RowsUsed}");
        _output.WriteLine($"📊 Resultatrapport rader: {result2.RowsUsed}");
        
        // Verify both reports succeeded
        Assert.True(result1.Success && result2.Success, "Both reports should render successfully");
        
        _output.WriteLine("\n🎉 VALIDERING KLAR!");
        _output.WriteLine("📋 Öppna Excel-filen för att se RIKTIG finansiell data!");
        _output.WriteLine("💡 Jämför med tidigare rapporter - nu ser du verkliga siffror från SIE4-filen!");
        _output.WriteLine($"🔍 Bankkonto: {frontPage.GetAccountBalance("1930"):N2} SEK (inte 3437 SEK)");
        _output.WriteLine($"🔍 Intäkter: {frontPage.GetAccountBalance("3311"):N2} SEK (inte fallback-värden)");
    }
}
