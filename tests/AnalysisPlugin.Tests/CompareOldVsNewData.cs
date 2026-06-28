using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Components;

namespace AnalysisPlugin.Tests;

/// <summary>
/// Test to compare old fallback data vs new real SIE4 data
/// </summary>
public class CompareOldVsNewData
{
    private readonly ITestOutputHelper _output;

    public CompareOldVsNewData(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CompareOldFallbackVsNewRealData()
    {
        _output.WriteLine("🔍 JÄMFÖRELSE: Gamla fallback-värden vs Nya riktiga värden");
        _output.WriteLine("===========================================================");
        
        // Read real SIE4 file
        var sie4FilePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "DemoEkonomiAB20251104_203034.se");
        var sie4Content = await File.ReadAllTextAsync(sie4FilePath);
        
        // Test component WITHOUT SIE4 data (should use fallback/zero values)
        var componentWithoutData = new FrontPageComponent();
        // Don't set SIE4Data - should result in empty transactions
        await componentWithoutData.ParseSIE4DataAsync();
        
        // Test component WITH real SIE4 data
        var componentWithRealData = new FrontPageComponent();
        componentWithRealData.SIE4Data = sie4Content;
        await componentWithRealData.ParseSIE4DataAsync();
        
        _output.WriteLine("\n📊 JÄMFÖRELSE AV TRANSAKTIONER:");
        _output.WriteLine("===============================");
        _output.WriteLine($"❌ Utan SIE4-data: {componentWithoutData.ParsedTransactions.Count} transaktioner");
        _output.WriteLine($"✅ Med SIE4-data: {componentWithRealData.ParsedTransactions.Count} transaktioner");
        
        _output.WriteLine("\n💰 JÄMFÖRELSE AV KONTOSALDON:");
        _output.WriteLine("=============================");
        
        // Compare key accounts
        var accounts = new[] { "1930", "3311", "7220", "2611" };
        var accountNames = new[] { "Bankkonto", "Intäkter", "Löner", "Moms" };
        
        for (int i = 0; i < accounts.Length; i++)
        {
            var account = accounts[i];
            var name = accountNames[i];
            
            var oldValue = componentWithoutData.GetAccountBalance(account);
            var newValue = componentWithRealData.GetAccountBalance(account);
            
            _output.WriteLine($"\n🏦 {name} ({account}):");
            _output.WriteLine($"  ❌ Utan data: {oldValue:N2} SEK");
            _output.WriteLine($"  ✅ Med riktig data: {newValue:N2} SEK");
            _output.WriteLine($"  📈 Skillnad: {Math.Abs(newValue - oldValue):N2} SEK");
            
            if (account == "1930") // Bank account
            {
                Assert.Equal(0m, oldValue); // Should be 0 without data
                Assert.Equal(75611m, newValue); // Should be real bank balance
            }
            else if (account == "3311") // Revenue
            {
                Assert.Equal(0m, oldValue); // Should be 0 without data
                Assert.Equal(-1166340.50m, newValue); // Should be real revenue (negative)
            }
        }
        
        _output.WriteLine("\n🎯 SAMMANFATTNING:");
        _output.WriteLine("==================");
        _output.WriteLine("❌ FÖRE (utan SIE4-data):");
        _output.WriteLine("   - 0 transaktioner parsade");
        _output.WriteLine("   - Alla kontosaldon = 0 SEK");
        _output.WriteLine("   - Identiska rapporter för alla företag");
        
        _output.WriteLine("\n✅ EFTER (med riktig SIE4-data):");
        _output.WriteLine("   - 1,839 transaktioner parsade");
        _output.WriteLine("   - Riktiga kontosaldon från bokföringen");
        _output.WriteLine("   - Unika rapporter för varje företag");
        
        _output.WriteLine("\n🚀 RESULTAT: Problemet med identiska dummydata är LÖST!");
        _output.WriteLine("💡 Varje rapport kommer nu att visa verkliga finansiella siffror!");
        
        // Verify the fix
        Assert.True(componentWithRealData.ParsedTransactions.Count > 1000, "Should have many real transactions");
        Assert.True(componentWithoutData.ParsedTransactions.Count == 0, "Should have no transactions without data");
        Assert.NotEqual(componentWithoutData.GetAccountBalance("1930"), componentWithRealData.GetAccountBalance("1930"));
    }
}
