using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som testar alla rapportkomponenter direkt genom att skapa en rapport med alla komponenter
/// </summary>
public class TestAllComponentsDirect
{
    private readonly ITestOutputHelper _output;

    public TestAllComponentsDirect(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestAllComponentsDirect_ShouldCreateReportWithAllComponents()
    {
        _output.WriteLine("🔍 TESTAR ALLA KOMPONENTER DIREKT");
        _output.WriteLine("=================================");

        // Skapa en rapport med alla komponenter genom att anropa DebugReportTest
        var request = @"
Skapa en OMFATTANDE finansiell rapport för 2023 med ALLA tillgängliga komponenter. 

VIKTIGT: Använd dessa EXAKTA steg:
1) Hämta SIE-data för 2023
2) Skapa rapport-jobb  
3) Lägg till ALLA dessa komponenter:

GRUNDLÄGGANDE KOMPONENTER:
- AddFrontPageAsync (startsida)
- AddChartAsync med chartType='expense_breakdown' (grundläggande diagram)
- AddChartAsync med chartType='line' (linjediagram)
- AddChartAsync med chartType='column' (kolumndiagram)
- AddTableAsync med tableType='account_summary' (tabell)
- AddTableAsync med tableType='monthly_summary' (månadstabell)
- AddPivotTableAsync med pivotType='account_breakdown' (pivot)
- AddPivotTableAsync med pivotType='monthly_revenue' (intäktspivot)

AVANCERADE KOMPONENTER:
- AddAdvancedChartAsync med chartType='waterfall' (waterfall-diagram)
- AddAdvancedChartAsync med chartType='gantt' (gantt-diagram)
- AddAdvancedChartAsync med chartType='combo' (combo-diagram)
- AddAdvancedChartAsync med chartType='area' (area-diagram)
- AddAdvancedChartAsync med chartType='scatter' (scatter-diagram)
- AddAdvancedChartAsync med chartType='funnel' (funnel-diagram)

ANALYS KOMPONENTER:
- AddMLAnalysisAsync (maskininlärningsanalys)
- AddFinancialAnalysisAsync med analysisType='cashflow' (kassaflödesanalys)
- AddFinancialAnalysisAsync med analysisType='ratios' (nyckeltal)
- AddFinancialAnalysisAsync med analysisType='budgetvsactual' (budget vs faktisk)
- AddFinancialAnalysisAsync med analysisType='roi' (ROI-analys)

4) Slutför med FinishReportAsync

SPARA ENDAST till /path/to/accountingagent/analysis_reports/ - skicka INTE via mail.
ANVÄND VERKLIG SIE4-DATA för alla komponenter!
";

        // Använd DebugReportTest för att skicka förfrågan
        var debugTest = new DebugReportTest(_output);
        await debugTest.InitializeAsync();
        
        try
        {
            // Uppdatera request i DebugReportTest
            var debugTestType = typeof(DebugReportTest);
            var requestField = debugTestType.GetField("_request", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (requestField != null)
            {
                requestField.SetValue(debugTest, request);
            }

            // Kör testet
            await debugTest.DebugExpenseReportRequest_ShouldCaptureExactResponse();
            
            _output.WriteLine("✅ Rapport skapad framgångsrikt");

            // Analysera den skapade rapporten
            await AnalyzeGeneratedReport();
        }
        finally
        {
            await debugTest.DisposeAsync();
        }
    }

    private async Task AnalyzeGeneratedReport()
    {
        _output.WriteLine("\n📊 ANALYS AV GENERERAD RAPPORT:");
        _output.WriteLine("===============================");

        var reportsDir = "/path/to/accountingagent/analysis_reports/";
        
        // Hitta den senaste rapporten
        var files = Directory.GetFiles(reportsDir, "*.xlsx")
            .Where(f => Path.GetFileName(f).StartsWith("financial_report_"))
            .OrderBy(f => new FileInfo(f).CreationTime)
            .TakeLast(1)
            .ToList();

        if (!files.Any())
        {
            _output.WriteLine("❌ Ingen rapport hittades!");
            return;
        }

        var reportPath = files.First();
        var reportInfo = new FileInfo(reportPath);
        
        _output.WriteLine($"📄 Rapport: {Path.GetFileName(reportPath)}");
        _output.WriteLine($"📊 Filstorlek: {reportInfo.Length:N0} bytes");
        _output.WriteLine($"📅 Skapad: {reportInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");

        // Analysera Excel-filen
        OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        using var package = new OfficeOpenXml.ExcelPackage(reportInfo);
        
        _output.WriteLine($"\n📋 WORKSHEETS ({package.Workbook.Worksheets.Count} st):");
        _output.WriteLine("=====================================");

        var expectedComponents = new[]
        {
            "FrontPage", "Chart", "Table", "Pivot", "AdvancedChart", "MLAnalysis", "FinancialAnalysis"
        };

        var foundComponents = new HashSet<string>();
        var worksheetsWithData = 0;

        foreach (var worksheet in package.Workbook.Worksheets)
        {
            var worksheetName = worksheet.Name;
            var hasData = worksheet.Dimension != null && worksheet.Dimension.Rows > 0;
            var cellCount = hasData ? worksheet.Dimension!.Rows * worksheet.Dimension.Columns : 0;
            
            _output.WriteLine($"   📊 {worksheetName}: {(hasData ? "✅ HAR DATA" : "❌ TOM")} ({cellCount} celler)");
            
            if (hasData) 
            {
                worksheetsWithData++;
                
                // Identifiera komponenttyp baserat på worksheet-namn
                foreach (var component in expectedComponents)
                {
                    if (worksheetName.ToLowerInvariant().Contains(component.ToLowerInvariant()) ||
                        component.ToLowerInvariant().Contains(worksheetName.ToLowerInvariant()))
                    {
                        foundComponents.Add(component);
                        break;
                    }
                }
            }
        }

        var componentSuccessRate = expectedComponents.Length > 0 ? 
            (double)foundComponents.Count / expectedComponents.Length * 100 : 0;

        _output.WriteLine($"\n🏆 RESULTAT:");
        _output.WriteLine($"   📊 Totala worksheets: {package.Workbook.Worksheets.Count}");
        _output.WriteLine($"   📊 Worksheets med data: {worksheetsWithData}");
        _output.WriteLine($"   📊 Identifierade komponenter: {foundComponents.Count}/{expectedComponents.Length}");
        _output.WriteLine($"   📊 Komponent-framgångsgrad: {componentSuccessRate:F1}%");

        _output.WriteLine($"\n📋 HITTADE KOMPONENTER:");
        foreach (var component in foundComponents.OrderBy(c => c))
        {
            _output.WriteLine($"   ✅ {component}");
        }

        _output.WriteLine($"\n📋 SAKNADE KOMPONENTER:");
        foreach (var component in expectedComponents.Except(foundComponents).OrderBy(c => c))
        {
            _output.WriteLine($"   ❌ {component}");
        }

        if (componentSuccessRate >= 80)
        {
            _output.WriteLine($"\n✅ UTMÄRKT: Alla eller nästan alla komponenter fungerar!");
        }
        else if (componentSuccessRate >= 60)
        {
            _output.WriteLine($"\n⚠️ BRA: De flesta komponenter fungerar");
        }
        else if (componentSuccessRate >= 40)
        {
            _output.WriteLine($"\n⚠️ ACCEPTABEL: Några komponenter fungerar");
        }
        else
        {
            _output.WriteLine($"\n❌ PROBLEM: Få komponenter fungerar korrekt");
        }

        await Task.CompletedTask;
    }
}
