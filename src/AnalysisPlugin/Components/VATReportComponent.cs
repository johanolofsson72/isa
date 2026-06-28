using OfficeOpenXml;
using AnalysisPlugin.Models;

namespace AnalysisPlugin.Components;

/// <summary>
/// VAT (Moms) report component for Swedish VAT reporting
/// </summary>
public class VATReportComponent : ReportComponentBase
{
    /// <summary>
    /// Component type identifier
    /// </summary>
    public override string ComponentType => "VATReport";

    /// <summary>
    /// Initializes a new instance of the VATReportComponent
    /// </summary>
    /// <param name="quarter">VAT reporting quarter (e.g., "2025Q3")</param>
    public VATReportComponent(string quarter) : base()
    {
        Title = $"VAT Report {quarter}";
        Quarter = quarter;
    }

    /// <summary>
    /// Initializes a new instance of the VATReportComponent with date range
    /// </summary>
    /// <param name="startDate">Start date for VAT reporting period</param>
    /// <param name="endDate">End date for VAT reporting period</param>
    /// <param name="period">Reporting period type (e.g., "månad", "kvartal")</param>
    public VATReportComponent(DateTime startDate, DateTime endDate, string period) : base()
    {
        StartDate = startDate;
        EndDate = endDate;
        Period = period;
        Title = $"VAT Report {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}";
        Quarter = $"{startDate.Year}Q{(startDate.Month - 1) / 3 + 1}";
    }

    /// <summary>
    /// VAT reporting quarter
    /// </summary>
    public string Quarter { get; set; } = string.Empty;

    /// <summary>
    /// Start date for VAT reporting period
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for VAT reporting period
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Reporting period type
    /// </summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>
    /// Renders the VAT report component to Excel
    /// </summary>
    /// <param name="worksheet">Target Excel worksheet</param>
    /// <param name="startRow">Starting row for rendering</param>
    /// <returns>Result of the rendering operation</returns>
    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            // Parse SIE4-data med tidsperiodsfiltrering för momsrapporten
            var chatMessage = Environment.GetEnvironmentVariable("CHAT_MESSAGE") ?? "";
            var timePeriod = DetectTimePeriodFromUserQuery(chatMessage);
            await ParseSIE4DataAsync(timePeriod);

            var currentRow = startRow;

            // Ta bort rutnätslinjer för professionell look
            worksheet.View.ShowGridLines = false;

            // Extract VAT data from SIE4
            var vatData = await ExtractVATDataFromSIE4Async();

            // Header
            worksheet.Cells[currentRow, 1].Value = Title;
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 1].Style.Font.Size = 16;
            currentRow += 2;

            // VAT Summary med kolumnrubriker
            worksheet.Cells[currentRow, 1].Value = "Momsredovisning";
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            currentRow += 2;

            // Kolumnrubriker
            worksheet.Cells[currentRow, 1].Value = "Beskrivning";
            worksheet.Cells[currentRow, 2].Value = "Momsbas (SEK)";
            worksheet.Cells[currentRow, 3].Value = "Moms (SEK)";
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 2].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 3].Style.Font.Bold = true;
            currentRow++;

            // Use the VAT data we already extracted
                
                // Momsbaser och utgående moms
                worksheet.Cells[currentRow, 1].Value = "Momsbas 25%";
                worksheet.Cells[currentRow, 2].Value = vatData.TaxableBase25;
                worksheet.Cells[currentRow, 3].Value = vatData.OutgoingVAT25;
                worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00";
                currentRow++;

                worksheet.Cells[currentRow, 1].Value = "Momsbas 12%";
                worksheet.Cells[currentRow, 2].Value = vatData.TaxableBase12;
                worksheet.Cells[currentRow, 3].Value = vatData.OutgoingVAT12;
                worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00";
                currentRow++;

                worksheet.Cells[currentRow, 1].Value = "Momsbas 6%";
                worksheet.Cells[currentRow, 2].Value = vatData.TaxableBase6;
                worksheet.Cells[currentRow, 3].Value = vatData.OutgoingVAT6;
                worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00";
                currentRow++;

                worksheet.Cells[currentRow, 1].Value = "Incoming VAT";
                worksheet.Cells[currentRow, 2].Value = vatData.IncomingVAT;
                worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
                currentRow++;

                worksheet.Cells[currentRow, 1].Value = "Net VAT to Pay";
                worksheet.Cells[currentRow, 2].Value = vatData.NetVATToPay;
                worksheet.Cells[currentRow, 2].Style.Font.Bold = true;
                worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0.00";
                currentRow++;

            currentRow++; // Add spacing

            // Quarter information
            worksheet.Cells[currentRow, 1].Value = $"Reporting Period: {Quarter}";
            worksheet.Cells[currentRow, 1].Style.Font.Italic = true;
            currentRow++;

            worksheet.Cells[currentRow, 1].Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
            worksheet.Cells[currentRow, 1].Style.Font.Italic = true;
            currentRow++;

            var rowsUsed = currentRow - startRow;
            return ComponentResult.CreateSuccess(rowsUsed);
        }
        catch (Exception ex)
        {
            return ComponentResult.CreateFailure($"Failed to render VAT report: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts VAT data from SIE4 format using GetAccountBalance method
    /// </summary>
    private async Task<VATData> ExtractVATDataFromSIE4Async()
    {
        var vatData = new VATData();

        try
        {
            // SIE4-data är redan parsad och filtrerad i RenderAsync() - använd den datan
            await Task.CompletedTask;

            // Använd GetAccountBalance-metoden från basklassen för korrekt parsing
            // Svenska momskonton enligt BAS-kontoplanen (uppdaterad för verklig SIE4-data):
            // 2610/2611 = Utgående moms 25%
            // 2620/2621 = Utgående moms 12%
            // 2630/2631 = Utgående moms 6%
            // 2640/2641 = Ingående moms (avdragsgill)
            // 2650 = Redovisningskonto moms

            vatData.OutgoingVAT25 = Math.Abs(GetAccountBalance("2610")) + Math.Abs(GetAccountBalance("2611"));
            vatData.OutgoingVAT12 = Math.Abs(GetAccountBalance("2620")) + Math.Abs(GetAccountBalance("2621"));
            vatData.OutgoingVAT6 = Math.Abs(GetAccountBalance("2630")) + Math.Abs(GetAccountBalance("2631"));
            vatData.IncomingVAT = Math.Abs(GetAccountBalance("2640")) + Math.Abs(GetAccountBalance("2641"));

            // Lägg till redovisningskonto moms (2650) som kan innehålla netto momsskuld
            var redovisningskonto = GetAccountBalance("2650");
            if (redovisningskonto < 0) // Negativ = momsskuld
            {
                vatData.OutgoingVAT25 += Math.Abs(redovisningskonto); // Lägg till som utgående moms
            }

            // Beräkna netto moms att betala
            vatData.NetVATToPay = vatData.OutgoingVAT25 + vatData.OutgoingVAT12 + vatData.OutgoingVAT6 - vatData.IncomingVAT;

            // Beräkna momsbas (omsättning exklusive moms)
            if (vatData.OutgoingVAT25 > 0)
            {
                vatData.TaxableBase25 = vatData.OutgoingVAT25 / 0.25m; // 25% moms
            }
            if (vatData.OutgoingVAT12 > 0)
            {
                vatData.TaxableBase12 = vatData.OutgoingVAT12 / 0.12m; // 12% moms
            }
            if (vatData.OutgoingVAT6 > 0)
            {
                vatData.TaxableBase6 = vatData.OutgoingVAT6 / 0.06m; // 6% moms
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with empty data
            Console.WriteLine($"Error extracting VAT data: {ex.Message}");
        }

        return vatData;
    }

    /// <summary>
    /// VAT data structure
    /// </summary>
    private class VATData
    {
        public decimal OutgoingVAT25 { get; set; }
        public decimal OutgoingVAT12 { get; set; }
        public decimal OutgoingVAT6 { get; set; }
        public decimal IncomingVAT { get; set; }
        public decimal NetVATToPay { get; set; }

        // Momsbaser (omsättning exklusive moms)
        public decimal TaxableBase25 { get; set; }
        public decimal TaxableBase12 { get; set; }
        public decimal TaxableBase6 { get; set; }
    }

    /// <summary>
    /// Detekterar tidsperiod från användarens fråga (CHAT_MESSAGE)
    /// </summary>
    private string DetectTimePeriodFromUserQuery(string userQuery)
    {
        var lowerQuery = userQuery.ToLowerInvariant();

        // Specifikt kvartal och år (t.ex. "2025 Q3")
        var quarterYearMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"(\d{4})\s*q(\d)");
        if (quarterYearMatch.Success)
        {
            var year = quarterYearMatch.Groups[1].Value;
            var quarter = quarterYearMatch.Groups[2].Value;
            return $"Quarter{quarter}_{year}"; // t.ex. "Quarter3_2025"
        }

        // År (default)
        return "Year";
    }
}
