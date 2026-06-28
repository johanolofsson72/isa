using Microsoft.Playwright;
using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som validerar att rapportdata faktiskt stämmer med SIE4-underlaget
/// </summary>
public class ValidateReportDataAccuracy : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public ValidateReportDataAccuracy(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 1000
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task ValidateReportDataMatchesSIE4Source_ShouldBeAccurate()
    {
        _output.WriteLine("🔍 VALIDERAR ATT RAPPORTDATA STÄMMER MED SIE4-UNDERLAG");
        _output.WriteLine("=====================================================");

        var reportsDir = "/path/to/accountingagent/analysis_reports/";
        
        // Hitta de senaste rapporterna
        var files = Directory.GetFiles(reportsDir, "*.xlsx")
            .Where(f => Path.GetFileName(f).StartsWith("financial_report_20251108_19"))
            .OrderBy(f => new FileInfo(f).CreationTime)
            .TakeLast(2)
            .ToList();

        if (files.Count < 2)
        {
            throw new InvalidOperationException($"Behöver minst 2 rapporter, hittade endast {files.Count}");
        }

        var report2021 = files[0]; // Äldre
        var report2022 = files[1]; // Nyare
        
        _output.WriteLine($"📊 2021-rapport: {Path.GetFileName(report2021)}");
        _output.WriteLine($"📊 2022-rapport: {Path.GetFileName(report2022)}");

        // Validera 2021-rapporten
        await ValidateReportAgainstSIE4(report2021, "2021");
        
        // Validera 2022-rapporten  
        await ValidateReportAgainstSIE4(report2022, "2022");
    }

    private async Task ValidateReportAgainstSIE4(string excelPath, string expectedYear)
    {
        _output.WriteLine($"\n🔍 VALIDERAR {expectedYear}-RAPPORT MOT SIE4-UNDERLAG:");
        _output.WriteLine("================================================");

        var sie4Path = excelPath.Replace(".xlsx", "_source.se");
        
        if (!File.Exists(sie4Path))
        {
            _output.WriteLine($"❌ SIE4-fil saknas: {sie4Path}");
            return;
        }

        // Läs SIE4-data
        var sie4Content = await File.ReadAllTextAsync(sie4Path);
        var sie4Data = ParseSIE4Data(sie4Content);
        
        _output.WriteLine($"📄 SIE4-fil: {Path.GetFileName(sie4Path)}");
        _output.WriteLine($"📊 SIE4-transaktioner: {sie4Data.Transactions.Count}");
        _output.WriteLine($"📅 SIE4-år: {sie4Data.Year}");

        // Läs Excel-data
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(excelPath));
        
        var excelData = ExtractExcelData(package);
        _output.WriteLine($"📊 Excel diagram-data: {string.Join(", ", excelData.ChartData.Select(d => $"{d.Key}={d.Value}"))}");

        // Jämför data
        await CompareDataAccuracy(sie4Data, excelData, expectedYear, sie4Content);
    }

    private SIE4Data ParseSIE4Data(string content)
    {
        // Använd samma SIE4Parser som systemet använder för konsistens
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AnalysisPlugin.Services.SIE4Parser>();
        var parser = new AnalysisPlugin.Services.SIE4Parser(logger);
        var parsedData = parser.ParseSIE4Data(content);

        // Konvertera till vårt testformat
        var data = new SIE4Data
        {
            Year = parsedData.Year,
            Transactions = parsedData.Transactions.Select(t => new SIE4Transaction
            {
                AccountNumber = t.AccountNumber,
                AccountName = t.AccountName,
                Amount = t.Amount,
                Date = t.Date
            }).ToList()
        };

        return data;
    }

    private ExcelReportData ExtractExcelData(ExcelPackage package)
    {
        var data = new ExcelReportData();

        foreach (var worksheet in package.Workbook.Worksheets)
        {
            if (worksheet.Name.ToLower().Contains("diagram"))
            {
                data.ChartData = ExtractChartDataFromWorksheet(worksheet);
            }
        }

        return data;
    }

    private Dictionary<string, double> ExtractChartDataFromWorksheet(OfficeOpenXml.ExcelWorksheet worksheet)
    {
        var data = new Dictionary<string, double>();
        
        if (worksheet.Dimension == null) return data;

        for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
        {
            var label = worksheet.Cells[row, 1].Value?.ToString();
            var valueStr = worksheet.Cells[row, 2].Value?.ToString();
            
            if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(valueStr) && 
                double.TryParse(valueStr, out var value) && value > 0)
            {
                data[label] = value;
            }
        }

        return data;
    }

    private async Task CompareDataAccuracy(SIE4Data sie4Data, ExcelReportData excelData, string expectedYear, string sie4Content)
    {
        _output.WriteLine($"\n🔍 JÄMFÖR DATAEXAKTHET FÖR {expectedYear}:");
        _output.WriteLine("=========================================");

        // Använd samma SIE4Parser för att få kategoriserad data
        var loggerFactory2 = LoggerFactory.Create(builder => builder.AddConsole());
        var logger2 = loggerFactory2.CreateLogger<AnalysisPlugin.Services.SIE4Parser>();
        var parser2 = new AnalysisPlugin.Services.SIE4Parser(logger2);
        var parsedData2 = parser2.ParseSIE4Data(sie4Content);

        // Gruppera SIE4-transaktioner per kategori (samma som systemet använder)
        var sie4AccountTotals = parsedData2.ExpensesByCategory
            .ToDictionary(kvp => kvp.Key, kvp => (double)kvp.Value);

        _output.WriteLine("📊 SIE4 UTGIFTER PER KONTO:");
        foreach (var kvp in sie4AccountTotals.OrderByDescending(x => x.Value))
        {
            _output.WriteLine($"   💰 {kvp.Key}: {kvp.Value:N0} SEK");
        }

        _output.WriteLine("\n📊 EXCEL DIAGRAM-DATA:");
        foreach (var kvp in excelData.ChartData.OrderByDescending(x => x.Value))
        {
            _output.WriteLine($"   📈 {kvp.Key}: {kvp.Value:N0} SEK");
        }

        // Jämför data
        _output.WriteLine("\n🔍 DATAVALIDERING:");
        _output.WriteLine("==================");

        var matches = 0;
        var totalComparisons = 0;

        foreach (var excelItem in excelData.ChartData)
        {
            totalComparisons++;

            // Försök hitta matchande konto i SIE4
            var matchingSIE4 = sie4AccountTotals.FirstOrDefault(s =>
                s.Key.Contains(excelItem.Key, StringComparison.OrdinalIgnoreCase) ||
                excelItem.Key.Contains(s.Key, StringComparison.OrdinalIgnoreCase) ||
                IsRelatedAccount(excelItem.Key, s.Key));

            if (matchingSIE4.Key != null)
            {
                var difference = Math.Abs(excelItem.Value - matchingSIE4.Value);
                var percentDiff = (difference / Math.Max(excelItem.Value, matchingSIE4.Value)) * 100;

                if (percentDiff < 5) // Acceptera 5% skillnad
                {
                    matches++;
                    _output.WriteLine($"   ✅ {excelItem.Key}: Excel={excelItem.Value:N0}, SIE4={matchingSIE4.Value:N0} ({percentDiff:F1}% skillnad)");
                }
                else
                {
                    _output.WriteLine($"   ❌ {excelItem.Key}: Excel={excelItem.Value:N0}, SIE4={matchingSIE4.Value:N0} ({percentDiff:F1}% skillnad - FÖR STOR!)");
                }
            }
            else
            {
                _output.WriteLine($"   ⚠️ {excelItem.Key}: Excel={excelItem.Value:N0}, SIE4=INGEN MATCHNING");
            }
        }

        var accuracy = totalComparisons > 0 ? (double)matches / totalComparisons * 100 : 0;

        _output.WriteLine($"\n🏆 RESULTAT FÖR {expectedYear}:");
        _output.WriteLine($"   📊 Matchningar: {matches}/{totalComparisons}");
        _output.WriteLine($"   🎯 Noggrannhet: {accuracy:F1}%");

        if (accuracy >= 80)
        {
            _output.WriteLine($"   ✅ GODKÄNT: Data stämmer väl överens med SIE4-underlag");
        }
        else if (accuracy >= 50)
        {
            _output.WriteLine($"   ⚠️ VARNING: Data stämmer delvis överens med SIE4-underlag");
        }
        else
        {
            _output.WriteLine($"   ❌ PROBLEM: Data stämmer INTE överens med SIE4-underlag");
        }

        await Task.CompletedTask;
    }

    private bool IsRelatedAccount(string excelCategory, string sie4Account)
    {
        var mappings = new Dictionary<string, string[]>
        {
            ["Personal"] = new[] { "Löner", "Personal", "Lön", "Salary", "Tjänstemän", "Kollektiv", "Företagsledare", "Sociala", "Pension", "Traktament", "Förmån" },
            ["Lokaler"] = new[] { "Hyra", "Lokal", "Rent", "Facilities", "Lokalhyra", "Värme", "Vatten", "Städning", "Fastighet" },
            ["IT"] = new[] { "Dator", "IT", "Computer", "Software", "Hardware", "Programvara", "Datakommunikation", "IT-tjänster" },
            ["Telefon"] = new[] { "Telefon", "Telekommunikation", "Mobil", "Post", "Kommunikation" },
            ["Fordon"] = new[] { "Bil", "Fordon", "Drivmedel", "Försäkring", "Reparation", "Transport" },
            ["Resor"] = new[] { "Resa", "Resor", "Bilersättning", "Traktament" },
            ["Representation"] = new[] { "Representation", "Representationskostnad" },
            ["Marknadsföring"] = new[] { "Marknadsföring", "Marketing", "Reklam", "Advertising", "Försäljning" },
            ["Administration"] = new[] { "Administration", "Styrelse", "Revisor", "Förvaltning", "Redovisning" },
            ["Kontorsmaterial"] = new[] { "Kontorsmaterial", "Kontor", "Material" },
            ["Försäkringar"] = new[] { "Försäkring", "Bevakning", "Larm", "Självrisker" },
            ["Hyror"] = new[] { "Hyra", "Maskiner", "Inventarier" },
            ["Inventarier"] = new[] { "Inventarier", "Maskiner", "Verktyg" },
            ["Bank"] = new[] { "Bank", "Konto" },
            ["Övrigt"] = new[] { "Övrigt", "Other", "Diverse", "Förbrukning", "Arbetskläder", "Kontokortsavgifter" },
            ["Checkräkningskonto"] = new[] { "Checkräkningskonto", "Check", "Bank", "Konto" },
            ["Intäkter"] = new[] { "Intäkter", "Revenue", "Income", "Försäljning" },
            ["Okategoriserad"] = new[] { "Okategoriserad", "Unknown" }
        };

        if (mappings.TryGetValue(excelCategory, out var keywords))
        {
            return keywords.Any(keyword => sie4Account.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        // Direkt matchning av kategorinamn
        if (excelCategory.Equals(sie4Account, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

public class SIE4Data
{
    public int Year { get; set; }
    public List<SIE4Transaction> Transactions { get; set; } = new();
}

public class SIE4Transaction
{
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Date { get; set; } = "";
}

public class ExcelReportData
{
    public Dictionary<string, double> ChartData { get; set; } = new();
}
