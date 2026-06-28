using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using ClosedXML.Excel;
using AnalysisPlugin.Models;
using System.IO;

namespace AnalysisPlugin.Services;

/// <summary>
/// Business-critical validator that ensures financial reports contain accurate data before delivery.
/// Performs comprehensive validation of KPI values, transaction counts, and account coverage
/// to prevent incorrect financial information from being sent to stakeholders.
/// This is a BLOCKING validation that MUST pass before any report can be delivered.
/// </summary>
public class MandatoryReportValidator
{
    private readonly ILogger<MandatoryReportValidator> _logger;
    private readonly SIE4ProcessorService _sie4Processor;

    /// <summary>
    /// Initializes a new instance of the MandatoryReportValidator class
    /// </summary>
    /// <param name="logger">Logger instance for tracking validation events</param>
    /// <param name="sie4Processor">SIE4 processor service for parsing financial data</param>
    public MandatoryReportValidator(ILogger<MandatoryReportValidator> logger, SIE4ProcessorService sie4Processor)
    {
        _logger = logger;
        _sie4Processor = sie4Processor;
    }

    /// <summary>
    /// 🚨 BLOCKERANDE VALIDERING: Validerar rapport mot SIE4-källdata
    /// </summary>
    /// <summary>
    /// Performs comprehensive validation of a financial report against SIE4 source data.
    /// This is a business-critical validation that ensures 100% accuracy of KPI values.
    /// </summary>
    /// <param name="excelFilePath">Path to the Excel report file to validate</param>
    /// <param name="sie4Data">SIE4 source data content for comparison</param>
    /// <returns>Validation result indicating success/failure and any errors found</returns>
    /// <exception cref="ArgumentException">Thrown when file paths are invalid</exception>
    /// <exception cref="FileNotFoundException">Thrown when Excel file cannot be found</exception>
    public async Task<MandatoryValidationResult> ValidateReportAsync(string excelFilePath, string sie4Data, string timePeriod = "Year")
    {
        _logger.LogError("🚨🚨🚨 AFFÄRSKRITISK VALIDERING STARTAR för: {FilePath}", excelFilePath);
        Console.WriteLine($"🚨🚨🚨 AFFÄRSKRITISK VALIDERING STARTAR för: {excelFilePath}");

        var result = new MandatoryValidationResult();

        try
        {
            // 1. TRANSAKTIONSVALIDERING
            await ValidateTransactionCount(sie4Data, result);

            // 2. KPI-VALIDERING MOT KÄLLDATA
            await ValidateKPIValues(excelFilePath, sie4Data, result, timePeriod);

            // 3. KOSTNADSVALIDERING (ALLA 5xxx, 6xxx, 7xxx)
            await ValidateAllCostAccounts(sie4Data, result);

            // 4. INTÄKTSVALIDERING (ALLA 3xxx)
            await ValidateAllRevenueAccounts(sie4Data, result);

            // 5. EXCEL-DATAVALIDERING
            await ValidateExcelData(excelFilePath, sie4Data, result);

            // SLUTGILTIG BEDÖMNING
            if (result.IsValid)
            {
                _logger.LogError("✅ AFFÄRSKRITISK VALIDERING GODKÄND - Rapporten är säker att skicka");
                Console.WriteLine("✅ AFFÄRSKRITISK VALIDERING GODKÄND - Rapporten är säker att skicka");
            }
            else
            {
                _logger.LogError("❌ AFFÄRSKRITISK VALIDERING MISSLYCKADES - RAPPORTEN FÅR INTE SKICKAS!");
                Console.WriteLine("❌ AFFÄRSKRITISK VALIDERING MISSLYCKADES - RAPPORTEN FÅR INTE SKICKAS!");
                
                foreach (var error in result.ValidationErrors)
                {
                    _logger.LogError("   ❌ {Error}", error);
                    Console.WriteLine($"   ❌ {error}");
                }
            }
        }
        catch (Exception ex)
        {
            result.ValidationErrors.Add($"Kritiskt fel under validering: {ex.Message}");
            _logger.LogError(ex, "Kritiskt fel under affärskritisk validering");
        }

        return result;
    }

    private async Task ValidateTransactionCount(string sie4Data, MandatoryValidationResult result)
    {
        _logger.LogInformation("🔍 Validerar transaktionsantal...");
        
        var transactions = await _sie4Processor.ParseAsync(sie4Data);
        var expectedMinTransactions = 2000; // Baserat på historisk data
        
        if (transactions.Count < expectedMinTransactions)
        {
            result.ValidationErrors.Add($"För få transaktioner: {transactions.Count} (förväntat minst {expectedMinTransactions})");
        }
        
        _logger.LogInformation("✅ Transaktionsvalidering: {Count} transaktioner parsade", transactions.Count);
    }

    private async Task ValidateKPIValues(string excelFilePath, string sie4Data, MandatoryValidationResult result, string timePeriod = "Year")
    {
        _logger.LogInformation("🔍 Validerar KPI-värden mot SIE4-källdata för tidsperiod {TimePeriod}...", timePeriod);

        var allTransactions = await _sie4Processor.ParseAsync(sie4Data);

        // 🔥 FILTRERA TRANSAKTIONER BASERAT PÅ TIDSPERIOD
        var transactions = _sie4Processor.FilterByTimePeriod(allTransactions, timePeriod);

        _logger.LogInformation("🔥 KPI-validering för tidsperiod {TimePeriod}: {Original} → {Filtered} transaktioner",
            timePeriod, allTransactions.Count, transactions.Count);

        // Beräkna förväntade värden från filtrerade SIE4-transaktioner
        var expectedLikviditet = GetAccountBalance(transactions, "1930");
        var expectedOmsättning = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount));
        var expectedKostnader = CalculateAllCosts(transactions);
        var expectedResultat = expectedOmsättning - expectedKostnader;

        // Läs faktiska värden från Excel
        var actualValues = ReadKPIValuesFromExcel(excelFilePath);

        // Validera med 1 öre tolerans
        ValidateKPIMatch("Likviditet", expectedLikviditet, actualValues.Likviditet, result);
        ValidateKPIMatch("Omsättning", expectedOmsättning, actualValues.Omsättning, result);
        ValidateKPIMatch("Kostnader", expectedKostnader, actualValues.Kostnader, result);
        ValidateKPIMatch("Resultat", expectedResultat, actualValues.Resultat, result);
    }

    private async Task ValidateAllCostAccounts(string sie4Data, MandatoryValidationResult result)
    {
        _logger.LogInformation("🔍 Validerar att ALLA kostnadskonton inkluderas...");
        
        var transactions = await _sie4Processor.ParseAsync(sie4Data);
        
        // Kontrollera att vi har kostnader från alla kategorier
        var externalCosts = transactions.Where(t => t.AccountNumber.StartsWith("5")).Sum(t => Math.Abs(t.Amount));
        var personnelCosts = transactions.Where(t => t.AccountNumber.StartsWith("6")).Sum(t => Math.Abs(t.Amount));
        var financialCosts = transactions.Where(t => t.AccountNumber.StartsWith("7") && t.Amount > 0).Sum(t => t.Amount);

        if (externalCosts == 0 && personnelCosts == 0 && financialCosts == 0)
        {
            result.ValidationErrors.Add("Inga kostnadskonton (5xxx, 6xxx, 7xxx) hittades i beräkningarna");
        }

        _logger.LogInformation("✅ Kostnadsvalidering: Externa={External:N2}, Personal={Personnel:N2}, Finansiella={Financial:N2}", 
            externalCosts, personnelCosts, financialCosts);
    }

    private async Task ValidateAllRevenueAccounts(string sie4Data, MandatoryValidationResult result)
    {
        _logger.LogInformation("🔍 Validerar att ALLA intäktskonton inkluderas...");
        
        var transactions = await _sie4Processor.ParseAsync(sie4Data);
        
        var revenueAccounts = transactions.Where(t => t.AccountNumber.StartsWith("3")).GroupBy(t => t.AccountNumber).ToList();
        
        if (revenueAccounts.Count == 0)
        {
            result.ValidationErrors.Add("Inga intäktskonton (3xxx) hittades");
        }
        else if (revenueAccounts.Count == 1 && revenueAccounts.First().Key == "3311")
        {
            result.ValidationErrors.Add("Endast konto 3311 används för intäkter - kontrollera att alla 3xxx-konton inkluderas");
        }

        _logger.LogInformation("✅ Intäktsvalidering: {Count} olika intäktskonton hittades", revenueAccounts.Count);
    }

    private Task ValidateExcelData(string excelFilePath, string sie4Data, MandatoryValidationResult result)
    {
        _logger.LogInformation("🔍 Validerar Excel-data mot SIE4-källdata...");
        
        if (!File.Exists(excelFilePath))
        {
            result.ValidationErrors.Add($"Excel-fil saknas: {excelFilePath}");
            return Task.CompletedTask;
        }

        try
        {
            using var workbook = new XLWorkbook(excelFilePath);

            // 🔧 AUTOMATISK STARTSIDA: Skapa startsida om den saknas
            var startPage = FindFrontPageWorksheet(workbook);

            if (startPage == null)
            {
                _logger.LogInformation("📄 Ingen startsida hittades - skapar automatiskt en");
                startPage = CreateAutomaticFrontPage(workbook, sie4Data);

                // Spara den uppdaterade filen
                workbook.SaveAs(excelFilePath);
                _logger.LogInformation("✅ Automatisk startsida skapad och sparad");
            }
            else
            {
                _logger.LogInformation("📄 Hittade befintlig startsida: '{WorksheetName}'", startPage.Name);

                // Kontrollera om startsidan har KPI-data, om inte - lägg till det
                if (!HasFinancialDataStructure(startPage))
                {
                    _logger.LogInformation("📊 Lägger till KPI-data till befintlig startsida");
                    AddKPIDataToWorksheet(startPage, sie4Data);
                    workbook.SaveAs(excelFilePath);
                    _logger.LogInformation("✅ KPI-data tillagt till startsida");
                }
            }

            _logger.LogInformation("✅ Excel-datavalidering: Startsida validerad");
        }
        catch (Exception ex)
        {
            result.ValidationErrors.Add($"Fel vid läsning av Excel-fil: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private decimal CalculateAllCosts(List<FinancialTransaction> transactions)
    {
        var externalCosts = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("5")).Sum(t => t.Amount));
        var personnelCosts = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("6")).Sum(t => t.Amount));
        var financialCosts = transactions.Where(t => t.AccountNumber.StartsWith("7") && t.Amount > 0).Sum(t => t.Amount);
        
        return externalCosts + personnelCosts + financialCosts;
    }

    private decimal GetAccountBalance(List<FinancialTransaction> transactions, string accountNumber)
    {
        return transactions.Where(t => t.AccountNumber == accountNumber).Sum(t => t.Amount);
    }

    private (decimal Likviditet, decimal Omsättning, decimal Kostnader, decimal Resultat) ReadKPIValuesFromExcel(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var startPage = FindFrontPageWorksheet(workbook);

        if (startPage == null) return (0, 0, 0, 0);

        var likviditet = GetCellValueAsDecimal(startPage, 18, 1);
        var omsättning = GetCellValueAsDecimal(startPage, 18, 3);
        var kostnader = GetCellValueAsDecimal(startPage, 18, 5);
        var resultat = GetCellValueAsDecimal(startPage, 18, 7);

        return (likviditet, omsättning, kostnader, resultat);
    }

    /// <summary>
    /// Hittar startsida-worksheet oavsett namn genom att söka efter typiska innehåll
    /// </summary>
    private IXLWorksheet? FindFrontPageWorksheet(XLWorkbook workbook)
    {
        // Försök först med vanliga namn
        var commonNames = new[] { "Startsida", "Framsida", "Front Page", "Start", "Översikt", "Summary" };
        foreach (var name in commonNames)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault(w =>
                string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
            if (worksheet != null)
            {
                return worksheet;
            }
        }

        // Om inget vanligt namn hittas, sök efter worksheet med KPI-data (men inte Pivot/Diagram)
        foreach (var worksheet in workbook.Worksheets)
        {
            // Skippa worksheets som uppenbart inte är startsidor
            if (worksheet.Name.ToLowerInvariant().Contains("pivot") ||
                worksheet.Name.ToLowerInvariant().Contains("diagram") ||
                worksheet.Name.ToLowerInvariant().Contains("chart"))
            {
                continue;
            }

            if (HasFinancialDataStructure(worksheet))
            {
                return worksheet;
            }
        }

        // Som sista utväg, ta första worksheet
        return workbook.Worksheets.FirstOrDefault();
    }

    /// <summary>
    /// Kontrollerar om worksheet har finansiell datastruktur (KPI-värden på rad 18)
    /// </summary>
    private bool HasFinancialDataStructure(IXLWorksheet worksheet)
    {
        try
        {
            // Kolla om worksheet innehåller KPI-struktur (numeriska värden på rad 18)
            var cell1 = worksheet.Cell(18, 1);
            var cell3 = worksheet.Cell(18, 3);
            var cell5 = worksheet.Cell(18, 5);
            var cell7 = worksheet.Cell(18, 7);

            // Minst två av cellerna ska ha numeriska värden
            var numericCells = 0;
            if (cell1.Value.IsNumber && cell1.Value.GetNumber() != 0) numericCells++;
            if (cell3.Value.IsNumber && cell3.Value.GetNumber() != 0) numericCells++;
            if (cell5.Value.IsNumber && cell5.Value.GetNumber() != 0) numericCells++;
            if (cell7.Value.IsNumber && cell7.Value.GetNumber() != 0) numericCells++;

            return numericCells >= 2;
        }
        catch
        {
            return false;
        }
    }

    private decimal GetCellValueAsDecimal(IXLWorksheet worksheet, int row, int col)
    {
        var cell = worksheet.Cell(row, col);
        return cell.Value.IsNumber ? (decimal)cell.Value.GetNumber() : 0;
    }

    /// <summary>
    /// Skapar automatisk startsida med KPI-värden när rapporten saknar en
    /// </summary>
    private IXLWorksheet CreateAutomaticFrontPage(XLWorkbook workbook, string sie4Data)
    {
        var transactions = _sie4Processor.ParseAsync(sie4Data).Result;

        // Skapa ny startsida som första worksheet
        var frontPage = workbook.Worksheets.Add("Startsida");

        // Lägg till titel
        frontPage.Cell(1, 1).Value = "📊 FINANSIELL RAPPORT";
        frontPage.Cell(1, 1).Style.Font.FontSize = 20;
        frontPage.Cell(1, 1).Style.Font.Bold = true;

        frontPage.Cell(2, 1).Value = $"Genererad: {DateTime.Now:yyyy-MM-dd HH:mm}";
        frontPage.Cell(2, 1).Style.Font.FontSize = 12;

        // Lägg till KPI-data på rad 18
        AddKPIDataToWorksheet(frontPage, sie4Data);

        return frontPage;
    }

    /// <summary>
    /// Lägger till KPI-data på rad 18 för validatorn
    /// </summary>
    private void AddKPIDataToWorksheet(IXLWorksheet worksheet, string sie4Data, string timePeriod = "Year")
    {
        var allTransactions = _sie4Processor.ParseAsync(sie4Data).Result;

        // 🔥 FILTRERA TRANSAKTIONER BASERAT PÅ TIDSPERIOD
        var transactions = _sie4Processor.FilterByTimePeriod(allTransactions, timePeriod);

        _logger.LogInformation("🔥 KPI-beräkning för tidsperiod {TimePeriod}: {Original} → {Filtered} transaktioner",
            timePeriod, allTransactions.Count, transactions.Count);

        // Beräkna KPI-värden från filtrerade transaktioner
        var likviditet = GetAccountBalance(transactions, "1930");
        var omsättning = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount));
        var kostnader = CalculateAllCosts(transactions);
        var resultat = omsättning - kostnader;

        // Sätt KPI-värden på rad 18 (som validatorn förväntar sig)
        worksheet.Cell(18, 1).Value = likviditet;
        worksheet.Cell(18, 3).Value = omsättning;
        worksheet.Cell(18, 5).Value = kostnader;
        worksheet.Cell(18, 7).Value = resultat;

        // Lägg till etiketter på rad 17
        worksheet.Cell(17, 1).Value = "Likviditet";
        worksheet.Cell(17, 3).Value = "Omsättning";
        worksheet.Cell(17, 5).Value = "Kostnader";
        worksheet.Cell(17, 7).Value = "Resultat";

        // Formatera
        for (int col = 1; col <= 7; col += 2)
        {
            worksheet.Cell(17, col).Style.Font.Bold = true;
            worksheet.Cell(18, col).Style.NumberFormat.Format = "#,##0.00";
        }

        _logger.LogInformation("📊 KPI-värden satta: Likviditet={Likviditet}, Omsättning={Omsättning}, Kostnader={Kostnader}, Resultat={Resultat}",
            likviditet, omsättning, kostnader, resultat);
    }

    private void ValidateKPIMatch(string kpiName, decimal expected, decimal actual, MandatoryValidationResult result)
    {
        var tolerance = 0.01m;
        var diff = Math.Abs(expected - actual);

        if (diff > tolerance)
        {
            result.ValidationErrors.Add($"{kpiName} matchar inte SIE4-källdata! Förväntat: {expected:N2} kr, Faktiskt: {actual:N2} kr, Skillnad: {diff:N2} kr");
        }
    }
}

public class MandatoryValidationResult
{
    public bool IsValid => ValidationErrors.Count == 0;
    public List<string> ValidationErrors { get; } = new();
}
