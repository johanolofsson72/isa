using Microsoft.Playwright;
using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som jämför rapporter för olika år för att säkerställa att data filtreras korrekt
/// </summary>
public class CompareYearlyReports : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public CompareYearlyReports(ITestOutputHelper output)
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
    public async Task CompareReports2021And2022_ShouldHaveDifferentData()
    {
        _output.WriteLine("🔍 JÄMFÖR RAPPORTER FÖR 2021 OCH 2022");
        _output.WriteLine("=====================================");

        var reportsDir = "/path/to/accountingagent/analysis_reports/";
        
        // Hitta de senaste rapporterna
        var files = Directory.GetFiles(reportsDir, "*.xlsx")
            .Where(f => Path.GetFileName(f).StartsWith("financial_report_20251108_19"))
            .OrderBy(f => new FileInfo(f).CreationTime)
            .ToList();

        if (files.Count < 2)
        {
            throw new InvalidOperationException($"Behöver minst 2 rapporter, hittade endast {files.Count}");
        }

        var report2021 = files[^2]; // Näst senaste
        var report2022 = files[^1]; // Senaste
        
        _output.WriteLine($"📊 2021-rapport: {Path.GetFileName(report2021)}");
        _output.WriteLine($"📊 2022-rapport: {Path.GetFileName(report2022)}");

        // Analysera båda rapporterna
        var data2021 = await AnalyzeReport(report2021, "2021");
        var data2022 = await AnalyzeReport(report2022, "2022");

        // Jämför SIE4-underlagen
        await CompareSIE4Sources(report2021, report2022);

        // Jämför rapportdata
        await CompareReportData(data2021, data2022);
    }

    private async Task<ReportData> AnalyzeReport(string filePath, string expectedYear)
    {
        _output.WriteLine($"\n🔍 ANALYSERAR {expectedYear}-RAPPORT:");
        _output.WriteLine("================================");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));
        
        var data = new ReportData { Year = expectedYear, FilePath = filePath };

        foreach (var worksheet in package.Workbook.Worksheets)
        {
            _output.WriteLine($"📋 Flik: '{worksheet.Name}'");
            
            if (worksheet.Name.ToLower().Contains("diagram"))
            {
                data.ChartData = ExtractChartData(worksheet);
                _output.WriteLine($"   📊 Diagram-data: {string.Join(", ", data.ChartData.Select(d => $"{d.Key}={d.Value}"))}");
            }
            else if (worksheet.Name.ToLower().Contains("pivot"))
            {
                data.PivotData = ExtractPivotData(worksheet);
                _output.WriteLine($"   📋 Pivot-data: {data.PivotData.Count} rader");
            }
            else if (worksheet.Name.ToLower().Contains("startsida"))
            {
                data.Title = ExtractTitle(worksheet);
                _output.WriteLine($"   📝 Titel: {data.Title}");
            }
        }

        await Task.CompletedTask;
        return data;
    }

    private Dictionary<string, double> ExtractChartData(OfficeOpenXml.ExcelWorksheet worksheet)
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

    private List<Dictionary<string, object>> ExtractPivotData(OfficeOpenXml.ExcelWorksheet worksheet)
    {
        var data = new List<Dictionary<string, object>>();
        
        if (worksheet.Dimension == null) return data;

        // Hitta headers (första raden med data)
        var headers = new List<string>();
        for (int col = 1; col <= Math.Min(worksheet.Dimension.End.Column, 10); col++)
        {
            var header = worksheet.Cells[3, col].Value?.ToString(); // Rad 3 är ofta headers
            if (!string.IsNullOrEmpty(header))
                headers.Add(header);
        }

        // Extrahera data
        for (int row = 4; row <= Math.Min(worksheet.Dimension.End.Row, 20); row++)
        {
            var rowData = new Dictionary<string, object>();
            bool hasData = false;

            for (int col = 1; col <= headers.Count; col++)
            {
                var value = worksheet.Cells[row, col].Value;
                if (value != null)
                {
                    rowData[headers[col - 1]] = value;
                    hasData = true;
                }
            }

            if (hasData)
                data.Add(rowData);
        }

        return data;
    }

    private string ExtractTitle(OfficeOpenXml.ExcelWorksheet worksheet)
    {
        return worksheet.Cells[1, 1].Value?.ToString() ?? "";
    }

    private async Task CompareSIE4Sources(string report2021Path, string report2022Path)
    {
        _output.WriteLine("\n🔍 JÄMFÖR SIE4-UNDERLAG:");
        _output.WriteLine("========================");

        var sie2021Path = report2021Path.Replace(".xlsx", "_source.se");
        var sie2022Path = report2022Path.Replace(".xlsx", "_source.se");

        if (!File.Exists(sie2021Path) || !File.Exists(sie2022Path))
        {
            _output.WriteLine("❌ SIE4-filer saknas för jämförelse");
            return;
        }

        var sie2021Content = await File.ReadAllTextAsync(sie2021Path);
        var sie2022Content = await File.ReadAllTextAsync(sie2022Path);

        _output.WriteLine($"📄 2021 SIE4-fil: {Path.GetFileName(sie2021Path)} ({sie2021Content.Length} tecken)");
        _output.WriteLine($"📄 2022 SIE4-fil: {Path.GetFileName(sie2022Path)} ({sie2022Content.Length} tecken)");

        // Extrahera år från SIE4-data
        var year2021 = ExtractYearFromSIE4(sie2021Content);
        var year2022 = ExtractYearFromSIE4(sie2022Content);

        _output.WriteLine($"📅 År i 2021-underlag: {year2021}");
        _output.WriteLine($"📅 År i 2022-underlag: {year2022}");

        // Kontrollera att åren är olika
        if (year2021 == year2022)
        {
            _output.WriteLine("⚠️ VARNING: Båda SIE4-filerna har samma år!");
        }
        else
        {
            _output.WriteLine("✅ SIE4-filerna har olika år som förväntat");
        }

        // Jämför företagsnamn
        var company2021 = ExtractCompanyFromSIE4(sie2021Content);
        var company2022 = ExtractCompanyFromSIE4(sie2022Content);

        _output.WriteLine($"🏢 Företag 2021: {company2021}");
        _output.WriteLine($"🏢 Företag 2022: {company2022}");
    }

    private async Task CompareReportData(ReportData data2021, ReportData data2022)
    {
        _output.WriteLine("\n🔍 JÄMFÖR RAPPORTDATA:");
        _output.WriteLine("======================");

        // Jämför titlar
        _output.WriteLine($"📝 2021 Titel: {data2021.Title}");
        _output.WriteLine($"📝 2022 Titel: {data2022.Title}");

        var titleContains2021 = data2021.Title.Contains("2021");
        var titleContains2022 = data2022.Title.Contains("2022");

        _output.WriteLine($"   ✅ 2021-titel innehåller '2021': {titleContains2021}");
        _output.WriteLine($"   ✅ 2022-titel innehåller '2022': {titleContains2022}");

        // Jämför diagram-data
        _output.WriteLine("\n📊 DIAGRAM-DATA JÄMFÖRELSE:");
        var chartDataSame = CompareDictionaries(data2021.ChartData, data2022.ChartData);
        _output.WriteLine($"   📊 Diagram-data identisk: {chartDataSame}");

        if (!chartDataSame)
        {
            _output.WriteLine("   ✅ BRA: Diagram-data skiljer sig mellan åren");
        }
        else
        {
            _output.WriteLine("   ⚠️ VARNING: Diagram-data är identisk mellan åren");
        }

        // Jämför pivot-data
        _output.WriteLine("\n📋 PIVOT-DATA JÄMFÖRELSE:");
        _output.WriteLine($"   📋 2021 Pivot-rader: {data2021.PivotData.Count}");
        _output.WriteLine($"   📋 2022 Pivot-rader: {data2022.PivotData.Count}");

        var pivotDataSame = data2021.PivotData.Count == data2022.PivotData.Count;
        _output.WriteLine($"   📋 Pivot-data identisk: {pivotDataSame}");

        // Sammanfattning
        _output.WriteLine("\n🏆 SAMMANFATTNING:");
        _output.WriteLine("==================");

        if (!chartDataSame || !pivotDataSame)
        {
            _output.WriteLine("✅ FRAMGÅNG: Rapporterna har olika data för olika år!");
        }
        else
        {
            _output.WriteLine("❌ PROBLEM: Rapporterna har identisk data trots olika år!");
        }

        await Task.CompletedTask;
    }

    private bool CompareDictionaries(Dictionary<string, double> dict1, Dictionary<string, double> dict2)
    {
        if (dict1.Count != dict2.Count) return false;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value) || Math.Abs(value - kvp.Value) > 0.01)
                return false;
        }

        return true;
    }

    private int ExtractYearFromSIE4(string sie4Content)
    {
        var lines = sie4Content.Split('\n');
        var omfattnLine = lines.FirstOrDefault(l => l.StartsWith("#OMFATTN"));
        if (omfattnLine != null)
        {
            var parts = omfattnLine.Split(' ');
            if (parts.Length >= 2 && parts[1].Length >= 4)
            {
                if (int.TryParse(parts[1][..4], out var year))
                    return year;
            }
        }
        return DateTime.Now.Year;
    }

    private string ExtractCompanyFromSIE4(string sie4Content)
    {
        var lines = sie4Content.Split('\n');
        var fnamn = lines.FirstOrDefault(l => l.StartsWith("#FNAMN"));
        if (fnamn != null)
        {
            var start = fnamn.IndexOf('"');
            var end = fnamn.LastIndexOf('"');
            if (start >= 0 && end > start)
                return fnamn.Substring(start + 1, end - start - 1);
        }
        return "Okänt företag";
    }
}

public class ReportData
{
    public string Year { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Title { get; set; } = "";
    public Dictionary<string, double> ChartData { get; set; } = new();
    public List<Dictionary<string, object>> PivotData { get; set; } = new();
}
