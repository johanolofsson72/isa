using OfficeOpenXml;
using AnalysisPlugin.Models;

namespace AnalysisPlugin.Components;

/// <summary>
/// Component for generating annual report sections
/// </summary>
public class AnnualReportComponent : ReportComponentBase
{
    /// <summary>
    /// Initializes a new instance of the AnnualReportComponent class
    /// </summary>
    /// <param name="year">Report year</param>
    public AnnualReportComponent(int year) : base()
    {
        Title = $"Annual Report {year}";
        ReportYear = year;
    }

    /// <summary>
    /// Report year for the annual report
    /// </summary>
    public int ReportYear { get; set; } = DateTime.Now.Year;

    /// <summary>
    /// Component type identifier
    /// </summary>
    public override string ComponentType => "AnnualReport";

    /// <summary>
    /// Renders the annual report component to an Excel worksheet
    /// </summary>
    /// <param name="worksheet">Target Excel worksheet</param>
    /// <param name="startRow">Starting row for the component</param>
    /// <returns>Component result with success status and rows used</returns>
    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            // Parse SIE4-data med tidsperiodsfiltrering för årsrapporten
            var chatMessage = Environment.GetEnvironmentVariable("CHAT_MESSAGE") ?? "";
            var timePeriod = DetectTimePeriodFromUserQuery(chatMessage);
            await ParseSIE4DataAsync(timePeriod);

            var currentRow = startRow;

            // Ta bort rutnätslinjer för professionell look
            worksheet.View.ShowGridLines = false;

            // ÅRSRAPPORT HEADER
            currentRow = await RenderAnnualReportHeader(worksheet, currentRow);

            // FÖRVALTNINGSBERÄTTELSE
            currentRow = await RenderManagementReport(worksheet, currentRow);

            // RESULTATRÄKNING
            currentRow = await RenderIncomeStatement(worksheet, currentRow);

            // BALANSRÄKNING
            currentRow = await RenderBalanceSheet(worksheet, currentRow);

            // KASSAFLÖDESANALYS
            currentRow = await RenderCashFlowStatement(worksheet, currentRow);

            // NOTER
            currentRow = await RenderNotes(worksheet, currentRow);

            // Optimera kolumnbredder
            OptimizeAnnualReportColumns(worksheet);

            return ComponentResult.CreateSuccess(currentRow - startRow + 5);
        }
        catch (Exception ex)
        {
            return ComponentResult.CreateFailure($"Failed to render annual report component: {ex.Message}");
        }
    }

    private async Task<int> RenderAnnualReportHeader(ExcelWorksheet worksheet, int currentRow)
    {
        // HEADER BAKGRUND
        var headerRange = worksheet.Cells[currentRow, 1, currentRow + 2, 8];
        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));

        currentRow += 1;

        // HUVUDTITEL
        var titleRange = worksheet.Cells[currentRow, 2, currentRow, 7];
        titleRange.Merge = true;
        titleRange.Value = $"📊 ÅRSRAPPORT {ReportYear}";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 24;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
        titleRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        currentRow += 1;

        // DATUM OCH PERIOD
        var dateRange = worksheet.Cells[currentRow, 2, currentRow, 7];
        dateRange.Merge = true;
        dateRange.Value = $"Räkenskapsår {ReportYear}-01-01 till {ReportYear}-12-31";
        dateRange.Style.Font.Name = "Segoe UI";
        dateRange.Style.Font.Size = 12;
        dateRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(200, 220, 240));
        dateRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderManagementReport(ExcelWorksheet worksheet, int currentRow)
    {
        // FÖRVALTNINGSBERÄTTELSE SEKTION
        var sectionHeaderRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        sectionHeaderRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        sectionHeaderRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 248, 255));

        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "📋 FÖRVALTNINGSBERÄTTELSE";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        titleRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        currentRow += 2;

        // Verksamhetsbeskrivning
        var descRange = worksheet.Cells[currentRow, 1, currentRow + 2, 8];
        descRange.Merge = true;
        descRange.Value = $"Bolaget bedriver verksamhet inom redovisning och ekonomisk rådgivning. Under {ReportYear} har verksamheten utvecklats positivt med fokus på digitalisering och automatisering av redovisningsprocesser.";
        descRange.Style.Font.Name = "Segoe UI";
        descRange.Style.Font.Size = 11;
        descRange.Style.WrapText = true;

        currentRow += 4;
        await Task.CompletedTask;
        return currentRow;
    }

    /// <summary>
    /// Detekterar tidsperiod från användarens fråga (CHAT_MESSAGE)
    /// </summary>
    private string DetectTimePeriodFromUserQuery(string userQuery)
    {
        var lowerQuery = userQuery.ToLowerInvariant();

        // Specifikt år (t.ex. "2023")
        var yearMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"\b(\d{4})\b");
        if (yearMatch.Success)
        {
            var year = yearMatch.Groups[1].Value;
            return $"Year{year}"; // t.ex. "Year2023"
        }

        // År (default)
        return "Year";
    }

    private async Task<int> RenderIncomeStatement(ExcelWorksheet worksheet, int currentRow)
    {
        // RESULTATRÄKNING SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "💰 RESULTATRÄKNING";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        titleRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        currentRow += 2;

        // Beräkna verkliga värden från SIE4-data
        var revenue = Math.Abs(ParsedTransactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount));
        var expenses = Math.Abs(ParsedTransactions.Where(t => t.AccountNumber.StartsWith("5") || t.AccountNumber.StartsWith("6") || t.AccountNumber.StartsWith("7")).Sum(t => t.Amount));
        var netResult = revenue - expenses;

        // Resultaträkning tabell
        worksheet.Cells[currentRow, 1].Value = "Nettoomsättning";
        worksheet.Cells[currentRow, 2].Value = revenue;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Rörelsekostnader";
        worksheet.Cells[currentRow, 2].Value = -expenses;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Årets resultat";
        worksheet.Cells[currentRow, 2].Value = netResult;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 2].Style.Font.Bold = true;

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderBalanceSheet(ExcelWorksheet worksheet, int currentRow)
    {
        // BALANSRÄKNING SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "📊 BALANSRÄKNING";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        titleRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        currentRow += 2;

        // Beräkna verkliga värden från SIE4-data
        var bankBalance = GetAccountBalance("1930");
        var totalAssets = Math.Abs(ParsedTransactions.Where(t => t.AccountNumber.StartsWith("1") || t.AccountNumber.StartsWith("2")).Sum(t => t.Amount));
        var totalLiabilities = Math.Abs(ParsedTransactions.Where(t => t.AccountNumber.StartsWith("2")).Sum(t => t.Amount));

        // TILLGÅNGAR
        worksheet.Cells[currentRow, 1].Value = "TILLGÅNGAR";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Banktillgodohavanden";
        worksheet.Cells[currentRow, 2].Value = bankBalance;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Summa tillgångar";
        worksheet.Cells[currentRow, 2].Value = totalAssets;
        worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 2].Style.Font.Bold = true;

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderCashFlowStatement(ExcelWorksheet worksheet, int currentRow)
    {
        // KASSAFLÖDESANALYS SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "💸 KASSAFLÖDESANALYS";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        titleRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        currentRow += 2;

        // Enkel kassaflödesanalys
        worksheet.Cells[currentRow, 1].Value = "Kassaflöde från den löpande verksamheten";
        worksheet.Cells[currentRow, 2].Value = "Beräknas från verksamhetsresultat";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Kassaflöde från investeringsverksamheten";
        worksheet.Cells[currentRow, 2].Value = "Inga större investeringar under året";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Kassaflöde från finansieringsverksamheten";
        worksheet.Cells[currentRow, 2].Value = "Inga större finansieringsförändringar";

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderNotes(ExcelWorksheet worksheet, int currentRow)
    {
        // NOTER SEKTION
        var titleRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        titleRange.Merge = true;
        titleRange.Value = "📝 NOTER";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 16;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        titleRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        currentRow += 2;

        worksheet.Cells[currentRow, 1].Value = "Not 1 - Redovisningsprinciper";
        worksheet.Cells[currentRow + 1, 1].Value = "Årsredovisningen har upprättats enligt årsredovisningslagen och BFNAR 2012:1.";
        currentRow += 3;

        worksheet.Cells[currentRow, 1].Value = "Not 2 - Väsentliga händelser";
        worksheet.Cells[currentRow + 1, 1].Value = $"Inga väsentliga händelser har inträffat under {ReportYear}.";

        currentRow += 3;
        await Task.CompletedTask;
        return currentRow;
    }

    private void OptimizeAnnualReportColumns(ExcelWorksheet worksheet)
    {
        worksheet.Column(1).Width = 30; // Beskrivning
        worksheet.Column(2).Width = 15; // Värden
        worksheet.Column(3).Width = 15; // Föregående år
        worksheet.Column(4).Width = 5;  // Marginal
    }
}
