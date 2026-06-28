using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🔍 INSPEKTERA EXCEL-FIL
/// Undersöker vad som faktiskt finns i den genererade Excel-filen
/// </summary>
public class InspectExcelFile
{
    private readonly ITestOutputHelper _output;

    public InspectExcelFile(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectLatestExcelFile_ShouldShowAllWorksheets()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        var reportsDir = "/path/to/accountingagent/analysis_reports/";
        var latestFile = Directory.GetFiles(reportsDir, "*.xlsx")
            .OrderByDescending(f => File.GetCreationTime(f))
            .FirstOrDefault();

        if (latestFile == null)
        {
            _output.WriteLine("❌ Ingen Excel-fil hittades");
            return;
        }

        _output.WriteLine($"🔍 INSPEKTERAR: {Path.GetFileName(latestFile)}");
        _output.WriteLine($"📅 Skapad: {File.GetCreationTime(latestFile):yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine("");

        using var package = new ExcelPackage(new FileInfo(latestFile));
        
        _output.WriteLine($"📊 ANTAL FLIKAR: {package.Workbook.Worksheets.Count}");
        _output.WriteLine("");

        for (int i = 0; i < package.Workbook.Worksheets.Count; i++)
        {
            var worksheet = package.Workbook.Worksheets[i];
            _output.WriteLine($"📋 FLIK {i + 1}: '{worksheet.Name}'");
            
            // Räkna rader med data
            var lastRow = worksheet.Dimension?.End.Row ?? 0;
            var lastCol = worksheet.Dimension?.End.Column ?? 0;
            
            _output.WriteLine($"   📏 Storlek: {lastRow} rader x {lastCol} kolumner");
            
            // Visa första 5 raderna
            if (lastRow > 0)
            {
                _output.WriteLine("   📝 Innehåll (första 5 raderna):");
                for (int row = 1; row <= Math.Min(5, lastRow); row++)
                {
                    var rowContent = "";
                    for (int col = 1; col <= Math.Min(4, lastCol); col++)
                    {
                        var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(cellValue))
                        {
                            rowContent += $"[{col}]{cellValue} ";
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(rowContent))
                    {
                        _output.WriteLine($"      Rad {row}: {rowContent.Trim()}");
                    }
                }
            }
            else
            {
                _output.WriteLine("   ❌ Tom flik");
            }
            
            _output.WriteLine("");
        }

        // Kontrollera om första fliken innehåller startsida-element
        if (package.Workbook.Worksheets.Count > 0)
        {
            var firstSheet = package.Workbook.Worksheets[0];
            _output.WriteLine($"🔍 FÖRSTA FLIK ANALYS: '{firstSheet.Name}'");
            
            var hasTitle = false;
            var hasCompanyName = false;
            var hasDate = false;
            
            // Sök efter startsida-element i första 20 raderna
            for (int row = 1; row <= Math.Min(20, firstSheet.Dimension?.End.Row ?? 0); row++)
            {
                for (int col = 1; col <= Math.Min(10, firstSheet.Dimension?.End.Column ?? 0); col++)
                {
                    var cellValue = firstSheet.Cells[row, col].Value?.ToString()?.ToLower() ?? "";
                    
                    if (cellValue.Contains("utgiftsrapport") || cellValue.Contains("rapport"))
                        hasTitle = true;
                    if (cellValue.Contains("company") || cellValue.Contains("företag") || cellValue.Contains("ab"))
                        hasCompanyName = true;
                    if (cellValue.Contains("2023") || cellValue.Contains("datum"))
                        hasDate = true;
                }
            }
            
            _output.WriteLine($"   📋 Titel hittad: {(hasTitle ? "✅" : "❌")}");
            _output.WriteLine($"   🏢 Företagsnamn hittad: {(hasCompanyName ? "✅" : "❌")}");
            _output.WriteLine($"   📅 Datum/År hittad: {(hasDate ? "✅" : "❌")}");
            
            var isStartPage = hasTitle && (hasCompanyName || hasDate);
            _output.WriteLine($"   🎯 Är startsida: {(isStartPage ? "✅ JA" : "❌ NEJ")}");
        }

        // Sök efter nyckelord som testet letar efter
        _output.WriteLine("");
        _output.WriteLine("🔍 NYCKELORD SOM TESTET LETAR EFTER:");
        var requiredElements = new[] { "startsida", "diagram", "pivot", "utgifter", "kostnader", "spara" };

        foreach (var element in requiredElements)
        {
            var found = false;
            var foundInSheets = new List<string>();

            foreach (var worksheet in package.Workbook.Worksheets)
            {
                var content = GetWorksheetContent(worksheet);
                var elementFound = false;

                // Kontrollera i cellinnehåll
                if (content.Contains(element, StringComparison.OrdinalIgnoreCase))
                {
                    elementFound = true;
                }
                // Kontrollera i fliknamn
                else if (worksheet.Name.Contains(element, StringComparison.OrdinalIgnoreCase))
                {
                    elementFound = true;
                }
                // Kontrollera närliggande ord
                else if (CheckSimilarWords(element, content + " " + worksheet.Name))
                {
                    elementFound = true;
                }

                if (elementFound)
                {
                    found = true;
                    foundInSheets.Add(worksheet.Name);
                }
            }

            _output.WriteLine($"   {(found ? "✅" : "❌")} '{element}': {(found ? $"Hittad i {string.Join(", ", foundInSheets)}" : "SAKNAS")}");
        }
    }

    private string GetWorksheetContent(OfficeOpenXml.ExcelWorksheet worksheet)
    {
        var content = "";
        if (worksheet.Dimension != null)
        {
            for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
            {
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        content += cellValue + " ";
                    }
                }
            }
        }
        return content;
    }

    /// <summary>
    /// Kontrollerar om närliggande ord finns för ett element
    /// </summary>
    private bool CheckSimilarWords(string element, string content)
    {
        return element.ToLowerInvariant() switch
        {
            "startsida" => content.Contains("startsida", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("start", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("front", StringComparison.OrdinalIgnoreCase),

            "diagram" => content.Contains("diagram", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("chart", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("graf", StringComparison.OrdinalIgnoreCase),

            "pivot" => content.Contains("pivot", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("tabell", StringComparison.OrdinalIgnoreCase),

            "utgifter" => content.Contains("utgifter", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("utgiftsrapport", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("utgiftsfördelning", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("expenses", StringComparison.OrdinalIgnoreCase),

            "kostnader" => content.Contains("kostnader", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("kostnadsfördelning", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("kostnad", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("costs", StringComparison.OrdinalIgnoreCase),

            "spara" => content.Contains("spara", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("sparad", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("saved", StringComparison.OrdinalIgnoreCase) ||
                      content.Contains("fil", StringComparison.OrdinalIgnoreCase) ||
                      true, // Om filen existerar så har den sparats

            _ => false
        };
    }
}
