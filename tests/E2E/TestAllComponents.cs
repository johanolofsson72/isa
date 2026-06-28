using Microsoft.Playwright;
using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som testar alla tillgängliga rapportkomponenter med verklig SIE4-data
/// </summary>
public class TestAllComponents : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public TestAllComponents(ITestOutputHelper output)
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
    public async Task TestAllComponents_ShouldWorkWithRealSIE4Data()
    {
        _output.WriteLine("🔍 TESTAR ALLA RAPPORTKOMPONENTER MED VERKLIG SIE4-DATA");
        _output.WriteLine("======================================================");

        var page = await _browser!.NewPageAsync();
        
        try
        {
            // Navigera till chat-sidan
            await page.GotoAsync("http://localhost:5000");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            _output.WriteLine("✅ Navigerat till chat-sidan");

            // Vänta på att sidan laddas
            await page.WaitForSelectorAsync(".chat-container", new PageWaitForSelectorOptions { Timeout = 10000 });
            _output.WriteLine("✅ Chat-container hittad");

            // Skicka en omfattande förfrågan som använder alla komponenter
            var comprehensiveRequest = @"
Skapa en OMFATTANDE finansiell rapport för 2023 med ALLA tillgängliga komponenter. 

VIKTIGT: Använd dessa EXAKTA steg:
1) Hämta SIE-data för 2023
2) Skapa rapport-jobb
3) Lägg till ALLA dessa komponenter:

GRUNDLÄGGANDE KOMPONENTER:
- AddFrontPageAsync (startsida)
- AddChartAsync med chartType='expense_breakdown' (grundläggande diagram)
- AddTableAsync med tableType='account_summary' (tabell)
- AddPivotTableAsync med pivotType='account_breakdown' (pivot)

AVANCERADE KOMPONENTER:
- AddAdvancedChartAsync med chartType='waterfall' (waterfall-diagram)
- AddAdvancedChartAsync med chartType='gantt' (gantt-diagram)
- AddAdvancedChartAsync med chartType='combo' (combo-diagram)
- AddMLAnalysisAsync (maskininlärningsanalys)
- AddFinancialAnalysisAsync med analysisType='cashflow' (kassaflödesanalys)
- AddFinancialAnalysisAsync med analysisType='ratios' (nyckeltal)

4) Slutför med FinishReportAsync

SPARA ENDAST till /path/to/accountingagent/analysis_reports/ - skicka INTE via mail.
ANVÄND VERKLIG SIE4-DATA för alla komponenter!
";

            await page.FillAsync("textarea[placeholder*='meddelande']", comprehensiveRequest);
            await page.ClickAsync("button[type='submit']");
            
            _output.WriteLine("✅ Omfattande förfrågan skickad");

            // Vänta på svar (längre tid för omfattande rapport)
            await page.WaitForSelectorAsync(".message:last-child .message-content", 
                new PageWaitForSelectorOptions { Timeout = 180000 }); // 3 minuter

            // Läs svaret
            var responseElement = await page.QuerySelectorAsync(".message:last-child .message-content");
            var response = await responseElement!.InnerTextAsync();
            
            _output.WriteLine("\n📝 SVAR FRÅN SYSTEMET:");
            _output.WriteLine("======================");
            _output.WriteLine(response);

            // Analysera svaret
            await AnalyzeResponse(response);

            // Hitta och analysera den skapade rapporten
            await AnalyzeGeneratedReport();

        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task AnalyzeResponse(string response)
    {
        _output.WriteLine("\n🔍 ANALYS AV SVAR:");
        _output.WriteLine("==================");

        var keywords = new[]
        {
            ("startsida", "AddFrontPageAsync"),
            ("diagram", "AddChartAsync"),
            ("tabell", "AddTableAsync"),
            ("pivot", "AddPivotTableAsync"),
            ("waterfall", "AddAdvancedChartAsync"),
            ("gantt", "AddAdvancedChartAsync"),
            ("combo", "AddAdvancedChartAsync"),
            ("maskininlärning", "AddMLAnalysisAsync"),
            ("kassaflöde", "AddFinancialAnalysisAsync"),
            ("nyckeltal", "AddFinancialAnalysisAsync"),
            ("spara", "FinishReportAsync"),
            ("fel", "error"),
            ("exception", "exception")
        };

        foreach (var (keyword, component) in keywords)
        {
            var found = response.ToLowerInvariant().Contains(keyword.ToLowerInvariant());
            var status = found ? "✅ HITTAD" : "❌ SAKNAS";
            _output.WriteLine($"   {status}: '{keyword}' ({component})");
        }

        await Task.CompletedTask;
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
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(reportInfo!);

        _output.WriteLine($"\n📋 WORKSHEETS ({package.Workbook.Worksheets.Count} st):");

        var foundWorksheets = 0;
        foreach (var worksheet in package.Workbook.Worksheets)
        {
            var worksheetName = worksheet.Name;
            var hasData = worksheet.Dimension != null && worksheet.Dimension.Rows > 0;
            var cellCount = hasData ? worksheet.Dimension!.Rows * worksheet.Dimension.Columns : 0;

            _output.WriteLine($"   📊 {worksheetName}: {(hasData ? "✅ HAR DATA" : "❌ TOM")} ({cellCount} celler)");

            if (hasData) foundWorksheets++;
        }

        _output.WriteLine($"\n🏆 RESULTAT:");
        _output.WriteLine($"   📊 Worksheets med data: {foundWorksheets}/{package.Workbook.Worksheets.Count}");

        await Task.CompletedTask;
    }
}
