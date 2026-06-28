using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som analyserar den omfattande rapporten för att se vilka komponenter som fungerar
/// </summary>
public class AnalyzeComprehensiveReport
{
    private readonly ITestOutputHelper _output;

    public AnalyzeComprehensiveReport(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AnalyzeComprehensiveReport_ShouldShowAllWorkingComponents()
    {
        _output.WriteLine("🔍 ANALYSERAR OMFATTANDE RAPPORT");
        _output.WriteLine("================================");

        var reportPath = "/path/to/accountingagent/analysis_reports/financial_report_20251108_202452.xlsx";
        
        if (!File.Exists(reportPath))
        {
            _output.WriteLine("❌ Rapporten finns inte!");
            return;
        }

        var reportInfo = new FileInfo(reportPath);
        _output.WriteLine($"📄 Rapport: {Path.GetFileName(reportPath)}");
        _output.WriteLine($"📊 Filstorlek: {reportInfo.Length:N0} bytes");
        _output.WriteLine($"📅 Skapad: {reportInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");

        // Analysera Excel-filen
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(reportInfo);
        
        _output.WriteLine($"\n📋 WORKSHEETS ({package.Workbook.Worksheets.Count} st):");
        _output.WriteLine("=====================================");

        var expectedComponents = new Dictionary<string, string[]>
        {
            ["FrontPage"] = new[] { "startsida", "front", "page" },
            ["AdvancedChart"] = new[] { "avancerade diagram", "advanced chart", "waterfall", "gantt", "combo", "area", "funnel", "advanced", "avancerat" },
            ["FinancialAnalysis"] = new[] { "finansiell analys", "financial analysis", "cashflow", "ratios", "kassaflöde", "nyckeltal", "breakeven", "roi" },
            ["Chart"] = new[] { "diagram", "chart", "pie", "line", "column" },
            ["Table"] = new[] { "tabell", "table", "account", "monthly", "kontotabell", "månadstabell" },
            ["Pivot"] = new[] { "pivot", "breakdown", "revenue", "kontopivot", "intäktspivot" },
            ["MLAnalysis"] = new[] { "ml", "machine", "learning", "analys", "anomaly", "anomali", "ml-analys" }
        };

        var foundComponents = new HashSet<string>();
        var worksheetsWithData = 0;
        var totalCells = 0;

        foreach (var worksheet in package.Workbook.Worksheets)
        {
            var worksheetName = worksheet.Name;
            var hasData = worksheet.Dimension != null && worksheet.Dimension.Rows > 0;
            var cellCount = hasData ? worksheet.Dimension!.Rows * worksheet.Dimension.Columns : 0;
            totalCells += cellCount;
            
            _output.WriteLine($"   📊 {worksheetName}: {(hasData ? "✅ HAR DATA" : "❌ TOM")} ({cellCount} celler)");
            
            if (hasData) 
            {
                worksheetsWithData++;
                
                // Identifiera komponenttyp baserat på worksheet-namn
                var identified = false;
                foreach (var (componentType, keywords) in expectedComponents)
                {
                    if (keywords.Any(keyword => worksheetName.ToLowerInvariant().Contains(keyword.ToLowerInvariant())))
                    {
                        foundComponents.Add(componentType);
                        _output.WriteLine($"      ✅ Identifierad som: {componentType}");
                        identified = true;
                        break;
                    }
                }

                // Specialanalys för avancerade komponenter
                if (!identified)
                {
                    if (worksheetName.ToLowerInvariant().Contains("avancerade") ||
                        worksheetName.ToLowerInvariant().Contains("advanced"))
                    {
                        foundComponents.Add("AdvancedChart");
                        _output.WriteLine($"      ✅ Identifierad som: AdvancedChart (specialanalys)");
                        identified = true;
                    }
                    else if (worksheetName.ToLowerInvariant().Contains("finansiell") ||
                             worksheetName.ToLowerInvariant().Contains("financial"))
                    {
                        foundComponents.Add("FinancialAnalysis");
                        _output.WriteLine($"      ✅ Identifierad som: FinancialAnalysis (specialanalys)");
                        identified = true;
                    }
                }

                if (!identified)
                {
                    _output.WriteLine($"      ⚠️ Okänd komponenttyp");
                }

                // Visa första raderna för att förstå innehållet
                if (cellCount > 0)
                {
                    _output.WriteLine($"      📝 Första 3 rader:");
                    for (int row = 1; row <= Math.Min(3, worksheet.Dimension!.Rows); row++)
                    {
                        var rowData = "";
                        for (int col = 1; col <= Math.Min(4, worksheet.Dimension.Columns); col++)
                        {
                            var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(cellValue) && cellValue.Length > 50)
                            {
                                cellValue = cellValue.Substring(0, 47) + "...";
                            }
                            if (!string.IsNullOrEmpty(cellValue))
                            {
                                rowData += $" | {cellValue}";
                            }
                        }
                        if (!string.IsNullOrEmpty(rowData))
                        {
                            _output.WriteLine($"         Rad {row}:{rowData}");
                        }
                    }
                }
            }
        }

        var componentSuccessRate = expectedComponents.Count > 0 ? 
            (double)foundComponents.Count / expectedComponents.Count * 100 : 0;

        _output.WriteLine($"\n🏆 DETALJERAT RESULTAT:");
        _output.WriteLine($"   📊 Totala worksheets: {package.Workbook.Worksheets.Count}");
        _output.WriteLine($"   📊 Worksheets med data: {worksheetsWithData}");
        _output.WriteLine($"   📊 Totala celler: {totalCells:N0}");
        _output.WriteLine($"   📊 Identifierade komponenter: {foundComponents.Count}/{expectedComponents.Count}");
        _output.WriteLine($"   📊 Komponent-framgångsgrad: {componentSuccessRate:F1}%");

        _output.WriteLine($"\n✅ FUNGERANDE KOMPONENTER:");
        foreach (var component in foundComponents.OrderBy(c => c))
        {
            _output.WriteLine($"   🎯 {component}");
        }

        _output.WriteLine($"\n❌ SAKNADE KOMPONENTER:");
        foreach (var component in expectedComponents.Keys.Except(foundComponents).OrderBy(c => c))
        {
            _output.WriteLine($"   ⚠️ {component}");
        }

        // Kontrollera SIE4-underlag
        var sie4Path = reportPath.Replace(".xlsx", "_source.se");
        if (File.Exists(sie4Path))
        {
            var sie4Info = new FileInfo(sie4Path);
            _output.WriteLine($"\n📄 SIE4-UNDERLAG:");
            _output.WriteLine($"   📊 Fil: {Path.GetFileName(sie4Path)}");
            _output.WriteLine($"   📊 Storlek: {sie4Info.Length:N0} bytes");
            _output.WriteLine($"   ✅ SIE4-underlag finns och används");
        }

        // Slutbetyg
        _output.WriteLine($"\n🎯 SLUTBETYG:");
        if (componentSuccessRate >= 85)
        {
            _output.WriteLine($"   🏆 UTMÄRKT: Nästan alla komponenter fungerar perfekt!");
        }
        else if (componentSuccessRate >= 70)
        {
            _output.WriteLine($"   ✅ MYCKET BRA: De flesta komponenter fungerar");
        }
        else if (componentSuccessRate >= 50)
        {
            _output.WriteLine($"   ⚠️ BRA: Många komponenter fungerar");
        }
        else if (componentSuccessRate >= 30)
        {
            _output.WriteLine($"   ⚠️ ACCEPTABEL: Några komponenter fungerar");
        }
        else
        {
            _output.WriteLine($"   ❌ PROBLEM: Få komponenter fungerar korrekt");
        }

        await Task.CompletedTask;
    }
}
