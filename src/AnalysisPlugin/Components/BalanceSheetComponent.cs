using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// 🇸🇪 BALANSRAPPORT - Klassisk svensk balansräkning enligt K3/K2 standard
/// </summary>
public class BalanceSheetComponent : ReportComponentBase
{
    private readonly ILogger<BalanceSheetComponent> _logger;
    private readonly int _reportYear;
    private readonly DateTime _reportDate;

    public BalanceSheetComponent(int reportYear = 0, DateTime? reportDate = null)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<BalanceSheetComponent>();
        
        _reportYear = reportYear == 0 ? DateTime.Now.Year : reportYear;
        _reportDate = reportDate ?? new DateTime(_reportYear, 12, 31);
    }

    public override string ComponentType => "BalanceSheet";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            var currentRow = startRow;

            // 🔄 PARSE SIE4 DATA FIRST TO GET REAL FINANCIAL TRANSACTIONS MED TIDSPERIODSFILTRERING
            var chatMessage = Environment.GetEnvironmentVariable("CHAT_MESSAGE") ?? "";
            var timePeriod = DetectTimePeriodFromUserQuery(chatMessage);
            _logger.LogInformation("🔥 BalanceSheetComponent: DETEKTERAD TIDSPERIOD från '{ChatMessage}': {TimePeriod}", chatMessage, timePeriod);
            await ParseSIE4DataAsync(timePeriod);

            // 🎨 PREMIUM DESIGN SETUP
            worksheet.View.ShowGridLines = false;

            // 🏢 BALANSRAPPORT HEADER
            currentRow = await RenderReportHeader(worksheet, currentRow);
            
            // 📊 TILLGÅNGAR (ASSETS)
            currentRow = await RenderAssetsSection(worksheet, currentRow);
            
            // 💰 SKULDER OCH EGET KAPITAL (LIABILITIES & EQUITY)
            currentRow = await RenderLiabilitiesAndEquitySection(worksheet, currentRow);
            
            // 📈 BALANSVALIDERING
            currentRow = await RenderBalanceValidation(worksheet, currentRow);

            // 📏 OPTIMERA KOLUMNBREDDER FÖR BALANSRAPPORT
            OptimizeBalansrapportColumns(worksheet);

            return ComponentResult.CreateSuccess(currentRow - startRow + 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render balance sheet");
            return ComponentResult.CreateFailure($"Failed to render balance sheet: {ex.Message}");
        }
    }

    private async Task<int> RenderReportHeader(ExcelWorksheet worksheet, int currentRow)
    {
        // 🏢 FÖRETAGSNAMN OCH RAPPORTTITEL
        var companyName = "Företag AB"; // Default company name
        worksheet.Cells[currentRow, 1].Value = companyName;
        worksheet.Cells[currentRow, 1].Style.Font.Size = 18;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells[currentRow, 1, currentRow, 4].Merge = true;
        currentRow += 2;

        // 📊 RAPPORTTITEL
        worksheet.Cells[currentRow, 1].Value = $"BALANSRÄKNING per {_reportDate:yyyy-MM-dd}";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells[currentRow, 1, currentRow, 4].Merge = true;
        currentRow += 3;

        // 📋 KOLUMNRUBRIKER
        worksheet.Cells[currentRow, 1].Value = "KONTO";
        worksheet.Cells[currentRow, 2].Value = $"{_reportYear} (SEK)";
        worksheet.Cells[currentRow, 3].Value = $"{_reportYear - 1} (SEK)";
        
        var headerRange = worksheet.Cells[currentRow, 1, currentRow, 3];
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 230, 241));
        headerRange.Style.Border.BorderAround(ExcelBorderStyle.Medium);
        currentRow += 2;

        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderAssetsSection(ExcelWorksheet worksheet, int currentRow)
    {
        // 📊 TILLGÅNGAR ENLIGT SVENSK STANDARD
        currentRow = await RenderSectionHeader(worksheet, currentRow, "TILLGÅNGAR");
        
        // ANLÄGGNINGSTILLGÅNGAR (BAS-konton 10xx-12xx)
        currentRow = await RenderSubSectionHeader(worksheet, currentRow, "Anläggningstillgångar");

        var fixedAssets = new[]
        {
            ("Byggnader och mark", GetAccountBalance("1100") + GetAccountBalance("1110"), GetAccountBalance("1100", _reportYear - 1) + GetAccountBalance("1110", _reportYear - 1)),
            ("Maskiner och inventarier", GetAccountBalance("1200") + GetAccountBalance("1220"), GetAccountBalance("1200", _reportYear - 1) + GetAccountBalance("1220", _reportYear - 1)),
            ("Ackumulerade avskrivningar", -Math.Abs(GetAccountBalance("1299")), -Math.Abs(GetAccountBalance("1299", _reportYear - 1)))
        };

        decimal totalFixedAssets = 0;
        decimal totalFixedAssetsComparison = 0;

        foreach (var (label, current, comparison) in fixedAssets)
        {
            currentRow = await RenderAccountLine(worksheet, currentRow, label, current, comparison);
            totalFixedAssets += current;
            totalFixedAssetsComparison += comparison;
        }

        currentRow = await RenderTotalLine(worksheet, currentRow, "Summa anläggningstillgångar", totalFixedAssets, totalFixedAssetsComparison);
        currentRow++;

        // OMSÄTTNINGSTILLGÅNGAR (BAS-konton 13xx-19xx)
        currentRow = await RenderSubSectionHeader(worksheet, currentRow, "Omsättningstillgångar");

        var currentAssets = new[]
        {
            ("Varulager", GetAccountBalance("1400") + GetAccountBalance("1410"), GetAccountBalance("1400", _reportYear - 1) + GetAccountBalance("1410", _reportYear - 1)),
            ("Kundfordringar", GetAccountBalance("1510") + GetAccountBalance("1511"), GetAccountBalance("1510", _reportYear - 1) + GetAccountBalance("1511", _reportYear - 1)),
            ("Övriga fordringar", GetAccountBalance("1630") + GetAccountBalance("1650"), GetAccountBalance("1630", _reportYear - 1) + GetAccountBalance("1650", _reportYear - 1)),
            ("Förutbetalda kostnader", GetAccountBalance("1790"), GetAccountBalance("1790", _reportYear - 1)),
            ("Kassa och bank", GetAccountBalance("1930") + GetAccountBalance("1940"), GetAccountBalance("1930", _reportYear - 1) + GetAccountBalance("1940", _reportYear - 1))
        };

        decimal totalCurrentAssets = 0;
        decimal totalCurrentAssetsComparison = 0;

        foreach (var (label, current, comparison) in currentAssets)
        {
            currentRow = await RenderAccountLine(worksheet, currentRow, label, current, comparison);
            totalCurrentAssets += current;
            totalCurrentAssetsComparison += comparison;
        }

        currentRow = await RenderTotalLine(worksheet, currentRow, "Summa omsättningstillgångar", totalCurrentAssets, totalCurrentAssetsComparison);
        currentRow++;

        // TOTALA TILLGÅNGAR
        var totalAssets = totalFixedAssets + totalCurrentAssets;
        var totalAssetsComparison = totalFixedAssetsComparison + totalCurrentAssetsComparison;
        currentRow = await RenderGrandTotalLine(worksheet, currentRow, "SUMMA TILLGÅNGAR", totalAssets, totalAssetsComparison);
        currentRow += 2;

        return currentRow;
    }

    private async Task<int> RenderLiabilitiesAndEquitySection(ExcelWorksheet worksheet, int currentRow)
    {
        // 💰 SKULDER OCH EGET KAPITAL
        currentRow = await RenderSectionHeader(worksheet, currentRow, "SKULDER OCH EGET KAPITAL");
        
        // EGET KAPITAL
        currentRow = await RenderSubSectionHeader(worksheet, currentRow, "Eget kapital");
        
        var equity = new[]
        {
            ("Aktiekapital", GetAccountBalance("2010"), GetAccountBalance("2010", _reportYear - 1)),
            ("Balanserat resultat", GetAccountBalance("2091"), GetAccountBalance("2091", _reportYear - 1)),
            ("Årets resultat", GetYearResult(), GetYearResult(_reportYear - 1))
        };

        decimal totalEquity = 0;
        decimal totalEquityComparison = 0;

        foreach (var (label, current, comparison) in equity)
        {
            currentRow = await RenderAccountLine(worksheet, currentRow, label, current, comparison);
            totalEquity += current;
            totalEquityComparison += comparison;
        }

        currentRow = await RenderTotalLine(worksheet, currentRow, "Summa eget kapital", totalEquity, totalEquityComparison);
        currentRow++;

        // SKULDER (BAS-konton 23xx-29xx)
        currentRow = await RenderSubSectionHeader(worksheet, currentRow, "Skulder");

        var liabilities = new[]
        {
            ("Leverantörsskulder", Math.Abs(GetAccountBalance("2440")), Math.Abs(GetAccountBalance("2440", _reportYear - 1))),
            ("Skatteskulder", Math.Abs(GetAccountBalance("2510") + GetAccountBalance("2610") + GetAccountBalance("2620")), Math.Abs(GetAccountBalance("2510", _reportYear - 1) + GetAccountBalance("2610", _reportYear - 1) + GetAccountBalance("2620", _reportYear - 1))),
            ("Övriga skulder", Math.Abs(GetAccountBalance("2890")), Math.Abs(GetAccountBalance("2890", _reportYear - 1))),
            ("Upplupna kostnader", Math.Abs(GetAccountBalance("2990")), Math.Abs(GetAccountBalance("2990", _reportYear - 1)))
        };

        decimal totalLiabilities = 0;
        decimal totalLiabilitiesComparison = 0;

        foreach (var (label, current, comparison) in liabilities)
        {
            currentRow = await RenderAccountLine(worksheet, currentRow, label, current, comparison);
            totalLiabilities += current;
            totalLiabilitiesComparison += comparison;
        }

        currentRow = await RenderTotalLine(worksheet, currentRow, "Summa skulder", totalLiabilities, totalLiabilitiesComparison);
        currentRow++;

        // TOTALT EGET KAPITAL OCH SKULDER
        var totalEquityAndLiabilities = totalEquity + totalLiabilities;
        var totalEquityAndLiabilitiesComparison = totalEquityComparison + totalLiabilitiesComparison;
        currentRow = await RenderGrandTotalLine(worksheet, currentRow, "SUMMA EGET KAPITAL OCH SKULDER", totalEquityAndLiabilities, totalEquityAndLiabilitiesComparison);
        currentRow += 2;

        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderBalanceValidation(ExcelWorksheet worksheet, int currentRow)
    {
        // 📈 BALANSVALIDERING - Kontrollera att tillgångar = skulder + eget kapital
        currentRow = await RenderSectionHeader(worksheet, currentRow, "BALANSVALIDERING");

        // Beräkna totala tillgångar (anläggningstillgångar + omsättningstillgångar)
        var fixedAssets = GetAccountBalance("1100") + GetAccountBalance("1110") + GetAccountBalance("1200") + GetAccountBalance("1220") - Math.Abs(GetAccountBalance("1299"));
        var currentAssets = GetAccountBalance("1400") + GetAccountBalance("1410") + GetAccountBalance("1510") + GetAccountBalance("1511") +
                           GetAccountBalance("1630") + GetAccountBalance("1650") + GetAccountBalance("1790") +
                           GetAccountBalance("1930") + GetAccountBalance("1940");
        var totalAssets = fixedAssets + currentAssets;

        // Beräkna eget kapital och skulder
        var equity = GetAccountBalance("2010") + GetAccountBalance("2091") + GetYearResult();
        var liabilities = Math.Abs(GetAccountBalance("2440")) + Math.Abs(GetAccountBalance("2510")) +
                         Math.Abs(GetAccountBalance("2610")) + Math.Abs(GetAccountBalance("2620")) +
                         Math.Abs(GetAccountBalance("2890")) + Math.Abs(GetAccountBalance("2990"));
        var totalEquityAndLiabilities = equity + liabilities;

        worksheet.Cells[currentRow, 1].Value = "Summa tillgångar";
        worksheet.Cells[currentRow, 2].Value = totalAssets;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Summa skulder och eget kapital";
        worksheet.Cells[currentRow, 2].Value = totalEquityAndLiabilities;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
        currentRow++;

        var difference = totalAssets - totalEquityAndLiabilities;
        worksheet.Cells[currentRow, 1].Value = "Differens";
        worksheet.Cells[currentRow, 2].Value = difference;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";

        // Färgkoda baserat på om balansen stämmer
        if (Math.Abs(difference) < 0.01m)
        {
            worksheet.Cells[currentRow, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);
            worksheet.Cells[currentRow, 2].Style.Font.Bold = true;
        }
        else
        {
            worksheet.Cells[currentRow, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
            worksheet.Cells[currentRow, 2].Style.Font.Bold = true;
        }

        currentRow += 2;
        return currentRow;
    }

    private async Task<int> RenderSectionHeader(ExcelWorksheet worksheet, int currentRow, string title)
    {
        worksheet.Cells[currentRow, 1].Value = title;
        worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));

        var range = worksheet.Cells[currentRow, 1, currentRow, 3];
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 230, 241));
        range.Style.Border.BorderAround(ExcelBorderStyle.Medium);

        await Task.CompletedTask;
        return currentRow + 1;
    }

    private async Task<int> RenderSubSectionHeader(ExcelWorksheet worksheet, int currentRow, string title)
    {
        worksheet.Cells[currentRow, 1].Value = title;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 1].Style.Font.Italic = true;

        await Task.CompletedTask;
        return currentRow + 1;
    }

    private async Task<int> RenderAccountLine(ExcelWorksheet worksheet, int currentRow, string label, decimal current, decimal comparison)
    {
        worksheet.Cells[currentRow, 1].Value = $"  {label}";
        worksheet.Cells[currentRow, 2].Value = current;
        worksheet.Cells[currentRow, 3].Value = comparison;

        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
        worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00";

        await Task.CompletedTask;
        return currentRow + 1;
    }

    private async Task<int> RenderTotalLine(ExcelWorksheet worksheet, int currentRow, string label, decimal current, decimal comparison)
    {
        worksheet.Cells[currentRow, 1].Value = label;
        worksheet.Cells[currentRow, 2].Value = current;
        worksheet.Cells[currentRow, 3].Value = comparison;

        var range = worksheet.Cells[currentRow, 1, currentRow, 3];
        range.Style.Font.Bold = true;
        range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        range.Style.Numberformat.Format = "#,##0.00";

        await Task.CompletedTask;
        return currentRow + 1;
    }

    private async Task<int> RenderGrandTotalLine(ExcelWorksheet worksheet, int currentRow, string label, decimal current, decimal comparison)
    {
        worksheet.Cells[currentRow, 1].Value = label;
        worksheet.Cells[currentRow, 2].Value = current;
        worksheet.Cells[currentRow, 3].Value = comparison;

        var range = worksheet.Cells[currentRow, 1, currentRow, 3];
        range.Style.Font.Bold = true;
        range.Style.Font.Size = 12;
        range.Style.Border.Top.Style = ExcelBorderStyle.Double;
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Double;
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 248, 255));
        range.Style.Numberformat.Format = "#,##0.00";

        await Task.CompletedTask;
        return currentRow + 1;
    }

    private decimal GetYearResult(int? year = null)
    {
        var targetYear = year ?? _reportYear;
        // Beräkna årets resultat från intäkter minus kostnader
        var revenue = Math.Abs(GetAccountBalance("3010", targetYear));
        var expenses = Math.Abs(GetAccountBalance("5010", targetYear)) + Math.Abs(GetAccountBalance("7220", targetYear));
        return revenue - expenses;
    }

    private void OptimizeBalansrapportColumns(ExcelWorksheet worksheet)
    {
        worksheet.Column(1).Width = 35; // Kontonamn
        worksheet.Column(2).Width = 15; // Aktuellt år
        worksheet.Column(3).Width = 15; // Föregående år
        worksheet.Column(4).Width = 5;  // Marginal
    }

    /// <summary>
    /// Detekterar tidsperiod från användarens fråga (CHAT_MESSAGE)
    /// </summary>
    private string DetectTimePeriodFromUserQuery(string userQuery)
    {
        var lowerQuery = userQuery.ToLowerInvariant();

        // Specifikt år (t.ex. "2024")
        var yearMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"\b(\d{4})\b");
        if (yearMatch.Success)
        {
            var year = yearMatch.Groups[1].Value;
            _logger.LogInformation("🔥 BalanceSheetComponent: DETEKTERAT ÅR: {Year}", year);
            return $"Year{year}"; // t.ex. "Year2024"
        }

        // År (default)
        return "Year";
    }
}
