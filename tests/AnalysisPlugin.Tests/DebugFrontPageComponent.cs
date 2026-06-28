using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Components;
using AnalysisPlugin.Services;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Tests;

/// <summary>
/// Debug test to see what FrontPageComponent actually returns from GetAccountBalance
/// </summary>
public class DebugFrontPageComponent
{
    private readonly ITestOutputHelper _output;

    public DebugFrontPageComponent(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DebugFrontPageKPIValues()
    {
        _output.WriteLine("🔍 DEBUG: Vad returnerar FrontPageComponent från GetAccountBalance?");
        _output.WriteLine("=================================================================");
        
        // Find the latest SIE4 source file
        var reportsDir = "/path/to/accountingagent/analysis_reports";
        var sourceFiles = Directory.GetFiles(reportsDir, "financial_report_*_source.se")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToArray();
        
        Assert.True(sourceFiles.Length > 0, "Should have at least one SIE4 source file");
        
        var latestSource = sourceFiles[0];
        var sie4Content = await File.ReadAllTextAsync(latestSource);
        
        _output.WriteLine($"📄 Använder SIE4-källa: {Path.GetFileName(latestSource)}");
        _output.WriteLine($"📊 SIE4-innehåll: {sie4Content.Length} tecken");
        
        // Create FrontPageComponent and set SIE4 data
        var component = new FrontPageComponent();
        component.SIE4Data = sie4Content;
        
        // Parse SIE4 data
        await component.ParseSIE4DataAsync();
        
        _output.WriteLine($"💰 Parsade transaktioner: {component.ParsedTransactions.Count}");
        
        // Test the specific accounts used in GetKPIDataFromSIE4
        var accounts = new[] { "1930", "3311", "7220", "5010" };
        var accountNames = new[] { "Bankkonto", "Intäkter", "Löner", "Hyra" };
        
        _output.WriteLine("\n🏦 KONTOSALDON FRÅN GetAccountBalance:");
        _output.WriteLine("=====================================");
        
        for (int i = 0; i < accounts.Length; i++)
        {
            var account = accounts[i];
            var name = accountNames[i];
            var balance = component.GetAccountBalance(account);
            
            _output.WriteLine($"{name} ({account}): {balance:N2} SEK");
            
            // Show transactions for this account from ParsedTransactions
            var transactions = component.ParsedTransactions.Where(t => t.AccountNumber == account).ToList();
            _output.WriteLine($"  - Antal transaktioner: {transactions.Count}");
            if (transactions.Any())
            {
                var sampleTransactions = transactions.Take(3);
                foreach (var trans in sampleTransactions)
                {
                    _output.WriteLine($"    {trans.Date:yyyy-MM-dd}: {trans.Amount:N2} SEK - {trans.Description}");
                }
                if (transactions.Count > 3)
                {
                    _output.WriteLine($"    ... och {transactions.Count - 3} till");
                }
            }
        }
        
        // Now simulate what GetKPIDataFromSIE4 would calculate
        _output.WriteLine("\n📊 SIMULERAR GetKPIDataFromSIE4:");
        _output.WriteLine("=================================");
        
        var checkingAccount = component.GetAccountBalance("1930");
        var revenue = Math.Abs(component.GetAccountBalance("3311"));
        var expenses = Math.Abs(component.GetAccountBalance("7220")) + Math.Abs(component.GetAccountBalance("5010"));
        var profit = revenue - expenses;
        
        _output.WriteLine($"LIKVIDITET: {checkingAccount:N2} SEK");
        _output.WriteLine($"OMSÄTTNING: {revenue:N2} SEK");
        _output.WriteLine($"KOSTNADER: {expenses:N2} SEK");
        _output.WriteLine($"RESULTAT: {profit:N2} SEK");
        
        // Check if these are the values that should appear in Excel
        _output.WriteLine("\n🎯 FÖRVÄNTADE EXCEL-VÄRDEN:");
        _output.WriteLine("===========================");
        _output.WriteLine($"LIKVIDITET cell should contain: {checkingAccount} (decimal)");
        _output.WriteLine($"OMSÄTTNING cell should contain: {revenue} (decimal)");
        _output.WriteLine($"KOSTNADER cell should contain: {expenses} (decimal)");
        _output.WriteLine($"RESULTAT cell should contain: {profit} (decimal)");
        
        // Verify that we have real data
        var hasRealData = checkingAccount != 0 || revenue != 0 || expenses != 0;
        _output.WriteLine($"\n✅ Har riktig data: {hasRealData}");
        
        if (!hasRealData)
        {
            _output.WriteLine("❌ PROBLEM: Alla värden är 0 - GetAccountBalance returnerar inte riktig data!");
        }
        else
        {
            _output.WriteLine("✅ SUCCESS: GetAccountBalance returnerar riktig data!");
        }
    }
}
