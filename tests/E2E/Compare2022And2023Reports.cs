using Microsoft.Playwright;
using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som jämför 2022 och 2023 rapporter för att verifiera att de har olika data
/// </summary>
public class Compare2022And2023Reports : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public Compare2022And2023Reports(ITestOutputHelper output)
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
    public async Task Compare2022And2023Reports_ShouldHaveDifferentData()
    {
        _output.WriteLine("🔍 JÄMFÖR 2022 OCH 2023 RAPPORTER");
        _output.WriteLine("=================================");

        var reportsDir = "/path/to/accountingagent/analysis_reports/";
        
        // Hitta de senaste stora rapporterna (från 20:19 och 20:20)
        var files = Directory.GetFiles(reportsDir, "*.xlsx")
            .Where(f => Path.GetFileName(f).StartsWith("financial_report_20251108_2019") ||
                       Path.GetFileName(f).StartsWith("financial_report_20251108_2020"))
            .OrderBy(f => new FileInfo(f).CreationTime)
            .TakeLast(2)
            .ToList();

        if (files.Count < 2)
        {
            throw new InvalidOperationException($"Behöver minst 2 rapporter, hittade endast {files.Count}");
        }

        var report2022 = files[0]; // Äldre
        var report2023 = files[1]; // Nyare
        
        _output.WriteLine($"📊 2022-rapport: {Path.GetFileName(report2022)}");
        _output.WriteLine($"📊 2023-rapport: {Path.GetFileName(report2023)}");

        // Analysera båda rapporterna
        var data2022 = await AnalyzeReport(report2022, "2022");
        var data2023 = await AnalyzeReport(report2023, "2023");

        // Jämför SIE4-underlag
        await CompareSIE4Data(report2022, report2023);

        // Jämför rapportdata
        await CompareReportData(data2022, data2023);
    }

    private async Task<ReportAnalysis> AnalyzeReport(string excelPath, string expectedYear)
    {
        _output.WriteLine($"\n🔍 ANALYSERAR {expectedYear}-RAPPORT:");
        _output.WriteLine("============================");

        var sie4Path = excelPath.Replace(".xlsx", "_source.se");
        
        if (!File.Exists(sie4Path))
        {
            throw new FileNotFoundException($"SIE4-fil saknas: {sie4Path}");
        }

        // Analysera SIE4-data
        var sie4Content = await File.ReadAllTextAsync(sie4Path);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AnalysisPlugin.Services.SIE4Parser>();
        var parser = new AnalysisPlugin.Services.SIE4Parser(logger);
        var parsedData = parser.ParseSIE4Data(sie4Content);

        // Analysera Excel-data
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(excelPath));
        var excelData = ExtractExcelData(package);

        var analysis = new ReportAnalysis
        {
            Year = expectedYear,
            SIE4Year = parsedData.Year,
            SIE4TransactionCount = parsedData.Transactions.Count,
            SIE4ExpenseCategories = parsedData.ExpensesByCategory.ToDictionary(kvp => kvp.Key, kvp => (double)kvp.Value),
            ExcelChartData = excelData,
            SIE4FileSize = new FileInfo(sie4Path).Length,
            ExcelFileSize = new FileInfo(excelPath).Length
        };

        _output.WriteLine($"📄 SIE4-fil: {Path.GetFileName(sie4Path)} ({analysis.SIE4FileSize:N0} bytes)");
        _output.WriteLine($"📊 SIE4-år: {analysis.SIE4Year}");
        _output.WriteLine($"📊 SIE4-transaktioner: {analysis.SIE4TransactionCount:N0}");
        _output.WriteLine($"📊 SIE4-kategorier: {analysis.SIE4ExpenseCategories.Count}");
        _output.WriteLine($"📊 Excel-kategorier: {analysis.ExcelChartData.Count}");

        return analysis;
    }

    private Dictionary<string, double> ExtractExcelData(ExcelPackage package)
    {
        var data = new Dictionary<string, double>();

        foreach (var worksheet in package.Workbook.Worksheets)
        {
            if (worksheet.Name.ToLower().Contains("diagram"))
            {
                if (worksheet.Dimension == null) continue;

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
                break;
            }
        }

        return data;
    }

    private async Task CompareSIE4Data(string report2022Path, string report2023Path)
    {
        _output.WriteLine("\n🔍 JÄMFÖR SIE4-UNDERLAG:");
        _output.WriteLine("========================");

        var sie4Path2022 = report2022Path.Replace(".xlsx", "_source.se");
        var sie4Path2023 = report2023Path.Replace(".xlsx", "_source.se");

        var sie4Content2022 = await File.ReadAllTextAsync(sie4Path2022);
        var sie4Content2023 = await File.ReadAllTextAsync(sie4Path2023);

        var size2022 = new FileInfo(sie4Path2022).Length;
        var size2023 = new FileInfo(sie4Path2023).Length;

        _output.WriteLine($"📄 2022 SIE4-fil: {size2022:N0} bytes");
        _output.WriteLine($"📄 2023 SIE4-fil: {size2023:N0} bytes");

        // Kontrollera att filerna är olika
        var contentsDifferent = !sie4Content2022.Equals(sie4Content2023);
        var sizesDifferent = size2022 != size2023;

        _output.WriteLine($"📊 Innehåll olika: {(contentsDifferent ? "✅ JA" : "❌ NEJ")}");
        _output.WriteLine($"📊 Storlek olika: {(sizesDifferent ? "✅ JA" : "❌ NEJ")}");

        // Extrahera år från SIE4-filerna
        var year2022 = ExtractYearFromSIE4(sie4Content2022);
        var year2023 = ExtractYearFromSIE4(sie4Content2023);

        _output.WriteLine($"📅 2022-fil innehåller år: {year2022}");
        _output.WriteLine($"📅 2023-fil innehåller år: {year2023}");

        if (year2022 != 2022 || year2023 != 2023)
        {
            _output.WriteLine("⚠️ VARNING: SIE4-filer innehåller inte förväntade år!");
        }
        else
        {
            _output.WriteLine("✅ SIE4-filer innehåller korrekta år!");
        }
    }

    private int ExtractYearFromSIE4(string sie4Content)
    {
        var lines = sie4Content.Split('\n');
        var rarLine = lines.FirstOrDefault(l => l.StartsWith("#RAR 0"));
        if (rarLine != null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(rarLine, @"#RAR 0 (\d{4})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
            {
                return year;
            }
        }
        return 0;
    }

    private async Task CompareReportData(ReportAnalysis data2022, ReportAnalysis data2023)
    {
        _output.WriteLine("\n🔍 JÄMFÖR RAPPORTDATA:");
        _output.WriteLine("======================");

        _output.WriteLine($"📊 2022 Excel-kategorier: {data2022.ExcelChartData.Count}");
        _output.WriteLine($"📊 2023 Excel-kategorier: {data2023.ExcelChartData.Count}");

        // Visa top 5 kategorier för varje år
        _output.WriteLine("\n📈 2022 TOP 5 KATEGORIER:");
        foreach (var kvp in data2022.ExcelChartData.OrderByDescending(x => x.Value).Take(5))
        {
            _output.WriteLine($"   💰 {kvp.Key}: {kvp.Value:N0} SEK");
        }

        _output.WriteLine("\n📈 2023 TOP 5 KATEGORIER:");
        foreach (var kvp in data2023.ExcelChartData.OrderByDescending(x => x.Value).Take(5))
        {
            _output.WriteLine($"   💰 {kvp.Key}: {kvp.Value:N0} SEK");
        }

        // Jämför gemensamma kategorier
        _output.WriteLine("\n🔍 JÄMFÖRELSE AV GEMENSAMMA KATEGORIER:");
        _output.WriteLine("=======================================");

        var commonCategories = data2022.ExcelChartData.Keys.Intersect(data2023.ExcelChartData.Keys).ToList();
        var differences = 0;
        var totalComparisons = 0;

        foreach (var category in commonCategories)
        {
            totalComparisons++;
            var value2022 = data2022.ExcelChartData[category];
            var value2023 = data2023.ExcelChartData[category];

            var difference = Math.Abs(value2023 - value2022);
            var percentDiff = value2022 > 0 ? (difference / value2022) * 100 : 0;

            if (percentDiff > 1) // Mer än 1% skillnad
            {
                differences++;
                _output.WriteLine($"   📊 {category}: 2022={value2022:N0}, 2023={value2023:N0} ({percentDiff:F1}% skillnad) ✅");
            }
            else
            {
                _output.WriteLine($"   📊 {category}: 2022={value2022:N0}, 2023={value2023:N0} ({percentDiff:F1}% skillnad) ⚠️");
            }
        }

        var differenceRate = totalComparisons > 0 ? (double)differences / totalComparisons * 100 : 0;

        _output.WriteLine($"\n🏆 RESULTAT:");
        _output.WriteLine($"   📊 Gemensamma kategorier: {commonCategories.Count}");
        _output.WriteLine($"   📊 Kategorier med skillnad: {differences}/{totalComparisons}");
        _output.WriteLine($"   📊 Skillnadsgrad: {differenceRate:F1}%");

        if (differenceRate >= 50)
        {
            _output.WriteLine($"   ✅ UTMÄRKT: Rapporterna har olika data för olika år!");
        }
        else if (differenceRate >= 25)
        {
            _output.WriteLine($"   ⚠️ BRA: Rapporterna har delvis olika data för olika år");
        }
        else
        {
            _output.WriteLine($"   ❌ PROBLEM: Rapporterna har för lika data för olika år");
        }

        await Task.CompletedTask;
    }
}

public class ReportAnalysis
{
    public string Year { get; set; } = "";
    public int SIE4Year { get; set; }
    public int SIE4TransactionCount { get; set; }
    public Dictionary<string, double> SIE4ExpenseCategories { get; set; } = new();
    public Dictionary<string, double> ExcelChartData { get; set; } = new();
    public long SIE4FileSize { get; set; }
    public long ExcelFileSize { get; set; }
}
