using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Jämför de stora omfattande rapporterna för 2022 och 2023 i detalj
/// </summary>
public class CompareComprehensiveReports
{
    private readonly ITestOutputHelper _output;

    public CompareComprehensiveReports(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CompareComprehensiveReports_ShouldShowDetailedDifferences()
    {
        _output.WriteLine("🔍 DETALJERAD JÄMFÖRELSE AV STORA RAPPORTER");
        _output.WriteLine("==========================================");

        var report2022Path = "/path/to/accountingagent/analysis_reports/financial_report_20251108_201918.xlsx";
        var report2023Path = "/path/to/accountingagent/analysis_reports/financial_report_20251108_202033.xlsx";

        if (!File.Exists(report2022Path) || !File.Exists(report2023Path))
        {
            _output.WriteLine("❌ En eller båda rapporterna saknas!");
            return;
        }

        var report2022Info = new FileInfo(report2022Path);
        var report2023Info = new FileInfo(report2023Path);

        _output.WriteLine($"📄 2022-rapport: {Path.GetFileName(report2022Path)} ({report2022Info.Length:N0} bytes)");
        _output.WriteLine($"📄 2023-rapport: {Path.GetFileName(report2023Path)} ({report2023Info.Length:N0} bytes)");

        // Analysera båda rapporterna
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        using var package2022 = new ExcelPackage(report2022Info);
        using var package2023 = new ExcelPackage(report2023Info);

        await CompareWorksheets(package2022, package2023);
        await CompareSpecificData(package2022, package2023);
        await CompareSIE4Sources(report2022Path, report2023Path);

        await Task.CompletedTask;
    }

    private async Task CompareWorksheets(ExcelPackage package2022, ExcelPackage package2023)
    {
        _output.WriteLine($"\n📋 JÄMFÖR WORKSHEETS:");
        _output.WriteLine($"=====================");

        _output.WriteLine($"📊 2022: {package2022.Workbook.Worksheets.Count} worksheets");
        _output.WriteLine($"📊 2023: {package2023.Workbook.Worksheets.Count} worksheets");

        var worksheets2022 = package2022.Workbook.Worksheets.Select(w => w.Name).ToList();
        var worksheets2023 = package2023.Workbook.Worksheets.Select(w => w.Name).ToList();

        _output.WriteLine($"\n📋 2022 WORKSHEETS:");
        foreach (var ws in worksheets2022)
        {
            var worksheet = package2022.Workbook.Worksheets[ws];
            var cellCount = worksheet.Dimension?.Rows * worksheet.Dimension?.Columns ?? 0;
            _output.WriteLine($"   📊 {ws}: {cellCount} celler");
        }

        _output.WriteLine($"\n📋 2023 WORKSHEETS:");
        foreach (var ws in worksheets2023)
        {
            var worksheet = package2023.Workbook.Worksheets[ws];
            var cellCount = worksheet.Dimension?.Rows * worksheet.Dimension?.Columns ?? 0;
            _output.WriteLine($"   📊 {ws}: {cellCount} celler");
        }

        var commonWorksheets = worksheets2022.Intersect(worksheets2023).ToList();
        var only2022 = worksheets2022.Except(worksheets2023).ToList();
        var only2023 = worksheets2023.Except(worksheets2022).ToList();

        _output.WriteLine($"\n🔍 WORKSHEET-JÄMFÖRELSE:");
        _output.WriteLine($"   📊 Gemensamma: {commonWorksheets.Count}");
        _output.WriteLine($"   📊 Endast 2022: {only2022.Count}");
        _output.WriteLine($"   📊 Endast 2023: {only2023.Count}");

        if (only2022.Any())
        {
            _output.WriteLine($"   📋 Endast i 2022: {string.Join(", ", only2022)}");
        }
        if (only2023.Any())
        {
            _output.WriteLine($"   📋 Endast i 2023: {string.Join(", ", only2023)}");
        }

        await Task.CompletedTask;
    }

    private async Task CompareSpecificData(ExcelPackage package2022, ExcelPackage package2023)
    {
        _output.WriteLine($"\n💰 JÄMFÖR SPECIFIK DATA:");
        _output.WriteLine($"========================");

        // Jämför diagram-data
        await CompareWorksheetData(package2022, package2023, "Diagram");
        
        // Jämför pivot-data
        await CompareWorksheetData(package2022, package2023, "Pivot");
        
        // Jämför tabell-data
        await CompareWorksheetData(package2022, package2023, "Tabeller");

        await Task.CompletedTask;
    }

    private async Task CompareWorksheetData(ExcelPackage package2022, ExcelPackage package2023, string worksheetName)
    {
        var ws2022 = package2022.Workbook.Worksheets.FirstOrDefault(w => w.Name.Contains(worksheetName));
        var ws2023 = package2023.Workbook.Worksheets.FirstOrDefault(w => w.Name.Contains(worksheetName));

        if (ws2022 == null || ws2023 == null)
        {
            _output.WriteLine($"   ⚠️ {worksheetName}: Worksheet saknas i en av rapporterna");
            return;
        }

        _output.WriteLine($"\n📊 {worksheetName.ToUpper()}:");
        
        var cells2022 = ws2022.Dimension?.Rows * ws2022.Dimension?.Columns ?? 0;
        var cells2023 = ws2023.Dimension?.Rows * ws2023.Dimension?.Columns ?? 0;
        
        _output.WriteLine($"   📊 2022: {cells2022} celler");
        _output.WriteLine($"   📊 2023: {cells2023} celler");

        // Jämför första 5 raderna för att hitta skillnader
        var maxRows = Math.Min(5, Math.Min(ws2022.Dimension?.Rows ?? 0, ws2023.Dimension?.Rows ?? 0));
        var differences = 0;

        for (int row = 1; row <= maxRows; row++)
        {
            var maxCols = Math.Min(6, Math.Min(ws2022.Dimension?.Columns ?? 0, ws2023.Dimension?.Columns ?? 0));
            for (int col = 1; col <= maxCols; col++)
            {
                var value2022 = ws2022.Cells[row, col].Value?.ToString() ?? "";
                var value2023 = ws2023.Cells[row, col].Value?.ToString() ?? "";

                if (value2022 != value2023 && !string.IsNullOrEmpty(value2022) && !string.IsNullOrEmpty(value2023))
                {
                    differences++;
                    if (differences <= 3) // Visa bara första 3 skillnaderna
                    {
                        _output.WriteLine($"   🔍 Rad {row}, Kol {col}: 2022='{value2022}' vs 2023='{value2023}'");
                    }
                }
            }
        }

        if (differences == 0)
        {
            _output.WriteLine($"   ✅ Inga skillnader hittade i första 5 raderna");
        }
        else
        {
            _output.WriteLine($"   📊 Totalt {differences} skillnader hittade");
        }

        await Task.CompletedTask;
    }

    private async Task CompareSIE4Sources(string report2022Path, string report2023Path)
    {
        _output.WriteLine($"\n📄 JÄMFÖR SIE4-UNDERLAG:");
        _output.WriteLine($"========================");

        var sie2022Path = report2022Path.Replace(".xlsx", "_source.se");
        var sie2023Path = report2023Path.Replace(".xlsx", "_source.se");

        if (!File.Exists(sie2022Path) || !File.Exists(sie2023Path))
        {
            _output.WriteLine("❌ SIE4-filer saknas!");
            return;
        }

        var sie2022Content = await File.ReadAllTextAsync(sie2022Path);
        var sie2023Content = await File.ReadAllTextAsync(sie2023Path);

        _output.WriteLine($"📊 2022 SIE4: {sie2022Content.Length:N0} tecken");
        _output.WriteLine($"📊 2023 SIE4: {sie2023Content.Length:N0} tecken");

        // Räkna transaktioner med olika belopp
        var transactions2022 = sie2022Content.Split('\n').Where(line => line.StartsWith("#TRANS")).ToList();
        var transactions2023 = sie2023Content.Split('\n').Where(line => line.StartsWith("#TRANS")).ToList();

        _output.WriteLine($"📊 2022 transaktioner: {transactions2022.Count}");
        _output.WriteLine($"📊 2023 transaktioner: {transactions2023.Count}");

        // Hitta skillnader i transaktioner
        var differentTransactions = 0;
        for (int i = 0; i < Math.Min(10, Math.Min(transactions2022.Count, transactions2023.Count)); i++)
        {
            if (transactions2022[i] != transactions2023[i])
            {
                differentTransactions++;
                if (differentTransactions <= 3)
                {
                    _output.WriteLine($"   🔍 Trans {i+1}:");
                    _output.WriteLine($"      2022: {transactions2022[i].Substring(0, Math.Min(80, transactions2022[i].Length))}...");
                    _output.WriteLine($"      2023: {transactions2023[i].Substring(0, Math.Min(80, transactions2023[i].Length))}...");
                }
            }
        }

        _output.WriteLine($"📊 Olika transaktioner (första 10): {differentTransactions}");

        await Task.CompletedTask;
    }
}
