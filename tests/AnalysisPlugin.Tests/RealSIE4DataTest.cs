using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Components;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Tests;

/// <summary>
/// Test to verify that real SIE4 data is being parsed correctly and used in components
/// </summary>
public class RealSIE4DataTest
{
    private readonly ITestOutputHelper _output;

    public RealSIE4DataTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestRealSIE4DataParsing()
    {
        _output.WriteLine("🧪 Testing Real SIE4 Data Parsing...");
        
        // Read a real SIE4 file
        var sie4FilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "DemoEkonomiAB20251104_203034.se");
        
        if (!File.Exists(sie4FilePath))
        {
            _output.WriteLine($"❌ SIE4 file not found: {sie4FilePath}");
            Assert.Fail($"SIE4 test file not found: {sie4FilePath}");
            return;
        }
        
        var sie4Content = await File.ReadAllTextAsync(sie4FilePath);
        _output.WriteLine($"📄 Loaded SIE4 file: {sie4Content.Length} characters");
        
        // Test SIE4ProcessorService
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sie4Processor = new SIE4ProcessorService(loggerFactory.CreateLogger<SIE4ProcessorService>());
        
        var transactions = await sie4Processor.ParseAsync(sie4Content);
        _output.WriteLine($"💰 Parsed {transactions.Count} transactions");
        
        Assert.True(transactions.Count > 0, "Should parse transactions from real SIE4 data");
        
        _output.WriteLine("\n📊 Sample transactions:");
        foreach (var transaction in transactions.Take(5))
        {
            _output.WriteLine($"  - {transaction.Date:yyyy-MM-dd} | {transaction.AccountNumber} | {transaction.Amount:N2} SEK | {transaction.Description}");
        }
        
        // Test account balance calculation
        var account1930Balance = transactions.Where(t => t.AccountNumber == "1930").Sum(t => t.Amount);
        _output.WriteLine($"\n🏦 Account 1930 (Bank) balance: {account1930Balance:N2} SEK");
        
        var account3311Balance = transactions.Where(t => t.AccountNumber == "3311").Sum(t => t.Amount);
        _output.WriteLine($"💼 Account 3311 (Revenue) balance: {account3311Balance:N2} SEK");
        
        // Verify that we have real data (not zero)
        Assert.True(Math.Abs(account1930Balance) > 0 || Math.Abs(account3311Balance) > 0, 
            "Should have non-zero balances from real SIE4 data");
        
        // Test component with real data
        _output.WriteLine("\n🧩 Testing FrontPageComponent with real data...");
        var frontPageComponent = new FrontPageComponent();
        frontPageComponent.SIE4Data = sie4Content;
        
        // Parse the data
        await frontPageComponent.ParseSIE4DataAsync();
        
        // Check if parsed transactions are available
        var parsedCount = frontPageComponent.ParsedTransactions.Count;
        _output.WriteLine($"✅ FrontPageComponent parsed {parsedCount} transactions");
        
        Assert.True(parsedCount > 0, "Component should parse SIE4 data successfully");
        
        // Test GetAccountBalance method
        var bankBalance = frontPageComponent.GetAccountBalance("1930");
        _output.WriteLine($"🏦 Bank balance via component: {bankBalance:N2} SEK");
        
        var revenueBalance = frontPageComponent.GetAccountBalance("3311");
        _output.WriteLine($"💼 Revenue balance via component: {revenueBalance:N2} SEK");
        
        // Verify that component returns the same values as direct calculation
        Assert.Equal(account1930Balance, bankBalance);
        Assert.Equal(account3311Balance, revenueBalance);
        
        _output.WriteLine("\n🎉 SUCCESS: Real SIE4 data is being parsed and used correctly!");
        _output.WriteLine("🚀 Components will now use REAL financial data instead of dummy data!");
    }
}
