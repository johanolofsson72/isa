using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;

namespace AnalysisPlugin.Tests;

/// <summary>
/// 🔍 DETALJERAD ANALYS: Kontrollerar alla konton i SIE4-data för att förstå de exakta värdena
/// </summary>
public class DetailedSIE4Analysis
{
    private readonly ITestOutputHelper _output;

    public DetailedSIE4Analysis(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AnalyzeAllAccountsInDetail_ShouldShowExactBreakdown()
    {
        _output.WriteLine("🔍 DETALJERAD ANALYS AV ALLA KONTON");
        _output.WriteLine("==================================");

        // Hämta den senaste SIE4-filen
        var reportsDir = "/path/to/accountingagent/analysis_reports";
        var latestSie4File = Directory.GetFiles(reportsDir, "financial_report_*_source.se")
            .OrderByDescending(f => File.GetCreationTime(f))
            .FirstOrDefault();

        Assert.NotNull(latestSie4File);
        var sie4Data = await File.ReadAllTextAsync(latestSie4File);

        // Parsa med vår processor
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sie4Processor = new SIE4ProcessorService(loggerFactory.CreateLogger<SIE4ProcessorService>());
        var transactions = await sie4Processor.ParseAsync(sie4Data);

        _output.WriteLine($"📊 Totalt {transactions.Count} transaktioner");

        // Gruppera per kontokategori
        var accountGroups = transactions.GroupBy(t => t.AccountNumber[0])
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var group in accountGroups)
        {
            var categoryName = group.Key switch
            {
                '1' => "TILLGÅNGAR",
                '2' => "SKULDER & EGET KAPITAL", 
                '3' => "INTÄKTER",
                '4' => "KOSTNAD FÖR SÅLDA VAROR",
                '5' => "EXTERNA KOSTNADER",
                '6' => "PERSONALKOSTNADER",
                '7' => "FINANSIELLA POSTER",
                '8' => "EXTRAORDINÄRA POSTER",
                _ => "OKÄNT"
            };

            _output.WriteLine($"\n📋 {group.Key}xxx - {categoryName}:");
            
            var accounts = group.GroupBy(t => t.AccountNumber)
                .Select(g => new { 
                    Account = g.Key, 
                    Name = g.First().AccountName,
                    Count = g.Count(), 
                    Total = g.Sum(t => t.Amount) 
                })
                .OrderBy(a => a.Account)
                .ToList();

            decimal categoryTotal = 0;
            foreach (var account in accounts)
            {
                _output.WriteLine($"   {account.Account}: {account.Total:N2} kr ({account.Count} trans) - {account.Name}");
                categoryTotal += account.Total;
            }
            
            _output.WriteLine($"   📊 KATEGORI TOTALT: {categoryTotal:N2} kr");
        }

        // Beräkna KPI-värden enligt olika metoder
        _output.WriteLine("\n🧮 KPI-BERÄKNINGAR:");
        _output.WriteLine("==================");

        // Metod 1: Endast konto 3311 för intäkter
        var revenue3311 = Math.Abs(GetAccountBalance(transactions, "3311"));
        _output.WriteLine($"💰 Intäkter (endast 3311): {revenue3311:N2} kr");

        // Metod 2: Alla 3xxx-konton för intäkter
        var allRevenue = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount));
        _output.WriteLine($"💰 Intäkter (alla 3xxx): {allRevenue:N2} kr");

        // Metod 3: Endast löner + hyra för kostnader
        var expensesLimited = Math.Abs(GetAccountBalance(transactions, "7220")) + 
                             Math.Abs(GetAccountBalance(transactions, "5010"));
        _output.WriteLine($"💸 Kostnader (endast löner+hyra): {expensesLimited:N2} kr");

        // Metod 4: Alla 5xxx + 6xxx + 7xxx för kostnader
        var allExpenses = Math.Abs(transactions.Where(t => 
            t.AccountNumber.StartsWith("5") || 
            t.AccountNumber.StartsWith("6") || 
            t.AccountNumber.StartsWith("7")).Sum(t => t.Amount));
        _output.WriteLine($"💸 Kostnader (alla 5xxx+6xxx+7xxx): {allExpenses:N2} kr");

        // Metod 5: Endast externa kostnader (5xxx)
        var externalCosts = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("5")).Sum(t => t.Amount));
        _output.WriteLine($"💸 Externa kostnader (5xxx): {externalCosts:N2} kr");

        // Metod 6: Endast personalkostnader (6xxx + 7xxx)
        var personnelCosts = Math.Abs(transactions.Where(t => 
            t.AccountNumber.StartsWith("6") || 
            t.AccountNumber.StartsWith("7")).Sum(t => t.Amount));
        _output.WriteLine($"💸 Personal + finansiella kostnader (6xxx+7xxx): {personnelCosts:N2} kr");

        _output.WriteLine("\n🎯 SLUTSATS:");
        _output.WriteLine($"   Vår nuvarande beräkning använder: {revenue3311:N2} - {expensesLimited:N2} = {revenue3311 - expensesLimited:N2} kr");
        _output.WriteLine($"   En mer komplett beräkning skulle vara: {allRevenue:N2} - {allExpenses:N2} = {allRevenue - allExpenses:N2} kr");
    }

    private decimal GetAccountBalance(List<FinancialTransaction> transactions, string accountNumber)
    {
        return transactions.Where(t => t.AccountNumber == accountNumber).Sum(t => t.Amount);
    }
}
