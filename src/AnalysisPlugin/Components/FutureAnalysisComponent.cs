using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// 🔮 FRAMTIDSANALYS - Inbjudande prognoser och trendanalys för framtida perioder
/// </summary>
public class FutureAnalysisComponent : ReportComponentBase
{
    private readonly ILogger<FutureAnalysisComponent> _logger;
    private readonly string _targetPeriod;
    private readonly int _targetYear;
    private readonly int _targetQuarter;

    public FutureAnalysisComponent(string targetPeriod = "2026Q1")
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<FutureAnalysisComponent>();
        
        _targetPeriod = targetPeriod;
        
        // Parse target period (e.g., "2026Q1")
        var match = System.Text.RegularExpressions.Regex.Match(targetPeriod, @"(\d{4})Q(\d)");
        if (match.Success)
        {
            _targetYear = int.Parse(match.Groups[1].Value);
            _targetQuarter = int.Parse(match.Groups[2].Value);
        }
        else
        {
            _targetYear = DateTime.Now.Year + 1;
            _targetQuarter = 1;
        }
        
        _logger.LogInformation("🔮 FutureAnalysisComponent skapad för {Period} (År: {Year}, Kvartal: {Quarter})", 
            _targetPeriod, _targetYear, _targetQuarter);
    }

    public override string ComponentType => "FutureAnalysis";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            // Parse SIE4-data för historisk analys
            var chatMessage = Environment.GetEnvironmentVariable("CHAT_MESSAGE") ?? "";
            var timePeriod = "Year"; // Använd all historisk data för prognoser
            await ParseSIE4DataAsync(timePeriod);
            
            var currentRow = startRow;
            
            // Ta bort rutnätslinjer för professionell look
            worksheet.View.ShowGridLines = false;
            
            // FRAMTIDSANALYS HEADER
            currentRow = await RenderFutureAnalysisHeader(worksheet, currentRow);
            
            // HISTORISK TRENDANALYS
            currentRow = await RenderHistoricalTrends(worksheet, currentRow);
            
            // PROGNOSER FÖR MÅLPERIODEN
            currentRow = await RenderForecasts(worksheet, currentRow);
            
            // REKOMMENDATIONER
            currentRow = await RenderRecommendations(worksheet, currentRow);
            
            // RISKANALYS
            currentRow = await RenderRiskAnalysis(worksheet, currentRow);
            
            // Optimera kolumnbredder
            OptimizeFutureAnalysisColumns(worksheet);

            return ComponentResult.CreateSuccess(currentRow - startRow + 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render future analysis component");
            return ComponentResult.CreateFailure($"Failed to render future analysis: {ex.Message}");
        }
    }

    private async Task<int> RenderFutureAnalysisHeader(ExcelWorksheet worksheet, int currentRow)
    {
        // HEADER BAKGRUND
        var headerRange = worksheet.Cells[currentRow, 1, currentRow + 2, 8];
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 125, 50)); // Grön för framtid
        
        currentRow += 1;
        
        // HUVUDTITEL
        var titleRange = worksheet.Cells[currentRow, 2, currentRow, 7];
        titleRange.Merge = true;
        titleRange.Value = $"🔮 FRAMTIDSANALYS {_targetPeriod}";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 24;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
        titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        
        currentRow += 1;
        
        // UNDERTITEL
        var subtitleRange = worksheet.Cells[currentRow, 2, currentRow, 7];
        subtitleRange.Merge = true;
        subtitleRange.Value = "Inbjudande prognoser baserade på historisk data och trendanalys";
        subtitleRange.Style.Font.Name = "Segoe UI";
        subtitleRange.Style.Font.Size = 12;
        subtitleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(200, 230, 201));
        subtitleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        
        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    /// <summary>
    /// Detekterar tidsperiod från användarens fråga (CHAT_MESSAGE)
    /// </summary>
    private string DetectTimePeriodFromUserQuery(string userQuery)
    {
        var lowerQuery = userQuery.ToLowerInvariant();
        
        // Specifikt kvartal och år (t.ex. "2026 Q1")
        var quarterYearMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"(\d{4})\s*q(\d)");
        if (quarterYearMatch.Success)
        {
            var year = quarterYearMatch.Groups[1].Value;
            var quarter = quarterYearMatch.Groups[2].Value;
            return $"Target{quarter}_{year}"; // t.ex. "Target1_2026"
        }
        
        // År (default för historisk data)
        return "Year";
    }

    private async Task<int> RenderHistoricalTrends(ExcelWorksheet worksheet, int currentRow)
    {
        // HISTORISK TRENDANALYS SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "📈 HISTORISK TRENDANALYS";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
        titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        currentRow += 2;

        // Beräkna historiska trender från SIE4-data
        var totalRevenue = Math.Abs(ParsedTransactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount));
        var totalExpenses = Math.Abs(ParsedTransactions.Where(t => t.AccountNumber.StartsWith("5") || t.AccountNumber.StartsWith("6") || t.AccountNumber.StartsWith("7")).Sum(t => t.Amount));
        var netResult = totalRevenue - totalExpenses;

        // Historiska nyckeltal
        worksheet.Cells[currentRow, 1].Value = "Historisk omsättning (totalt)";
        worksheet.Cells[currentRow, 2].Value = totalRevenue;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Historiska kostnader (totalt)";
        worksheet.Cells[currentRow, 2].Value = totalExpenses;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Historiskt resultat";
        worksheet.Cells[currentRow, 2].Value = netResult;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 2].Style.Font.Bold = true;

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderForecasts(ExcelWorksheet worksheet, int currentRow)
    {
        // PROGNOSER SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = $"🔮 PROGNOSER FÖR {_targetPeriod}";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
        titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        currentRow += 2;

        // Enkla prognoser baserade på historisk data (kan förbättras med ML)
        var historicalRevenue = Math.Abs(ParsedTransactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount));
        var forecastRevenue = historicalRevenue * 1.15m; // 15% tillväxt
        var forecastExpenses = historicalRevenue * 0.85m; // 85% av omsättning
        var forecastProfit = forecastRevenue - forecastExpenses;

        worksheet.Cells[currentRow, 1].Value = $"Prognostiserad omsättning {_targetPeriod}";
        worksheet.Cells[currentRow, 2].Value = forecastRevenue;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        worksheet.Cells[currentRow, 3].Value = "↗️ +15% tillväxt";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = $"Prognostiserade kostnader {_targetPeriod}";
        worksheet.Cells[currentRow, 2].Value = forecastExpenses;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        worksheet.Cells[currentRow, 3].Value = "📊 85% av omsättning";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = $"Prognostiserat resultat {_targetPeriod}";
        worksheet.Cells[currentRow, 2].Value = forecastProfit;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        worksheet.Cells[currentRow, 3].Value = "🎯 Stark tillväxt";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 2].Style.Font.Bold = true;

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderRecommendations(ExcelWorksheet worksheet, int currentRow)
    {
        // REKOMMENDATIONER SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "💡 STRATEGISKA REKOMMENDATIONER";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(46, 125, 50));
        titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        currentRow += 2;

        var recommendations = new[]
        {
            "🚀 Fokusera på digitalisering för att öka effektiviteten",
            "💰 Investera i kundlojalitetsprogram för återkommande intäkter",
            "📊 Implementera avancerad dataanalys för bättre beslutsfattande",
            "🌱 Utforska hållbara affärsmodeller för framtida tillväxt",
            "🤝 Stärk partnerskap och strategiska allianser"
        };

        foreach (var recommendation in recommendations)
        {
            var recRange = worksheet.Cells[currentRow, 1, currentRow, 8];
            recRange.Merge = true;
            recRange.Value = recommendation;
            recRange.Style.Font.Name = "Segoe UI";
            recRange.Style.Font.Size = 11;
            recRange.Style.WrapText = true;
            currentRow++;
        }

        currentRow += 2;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderRiskAnalysis(ExcelWorksheet worksheet, int currentRow)
    {
        // RISKANALYS SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "⚠️ RISKANALYS OCH MÖJLIGHETER";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 152, 0)); // Orange för varning
        titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        currentRow += 2;

        worksheet.Cells[currentRow, 1].Value = "🔴 Risker";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 3].Value = "🟢 Möjligheter";
        worksheet.Cells[currentRow, 3].Style.Font.Bold = true;
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "• Marknadsvolatilitet";
        worksheet.Cells[currentRow, 3].Value = "• Ny marknadssegment";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "• Ökade kostnader";
        worksheet.Cells[currentRow, 3].Value = "• Teknologisk innovation";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "• Konkurrenstryck";
        worksheet.Cells[currentRow, 3].Value = "• Internationell expansion";

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private void OptimizeFutureAnalysisColumns(ExcelWorksheet worksheet)
    {
        worksheet.Column(1).Width = 25; // Beskrivning
        worksheet.Column(2).Width = 15; // Värden
        worksheet.Column(3).Width = 15; // Prognos
        worksheet.Column(4).Width = 20; // Kommentarer
    }
}
