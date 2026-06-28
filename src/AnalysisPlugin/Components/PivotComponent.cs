using OfficeOpenXml;
using OfficeOpenXml.Table.PivotTable;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using AnalysisPlugin.Services;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// Pivot component using ONLY EPPlus - creates real Excel pivot tables! 🎯
/// </summary>
public class PivotComponent : ReportComponentBase
{
    private readonly ILogger<PivotComponent> _logger;

    public PivotComponent()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<PivotComponent>();
    }

    public override string ComponentType => "Pivot";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        _logger.LogError("🔥🔥🔥 PivotComponent.RenderAsync ANROPAD!");
        Console.WriteLine("🔥🔥🔥 PivotComponent.RenderAsync ANROPAD!");
        try
        {
            var currentRow = startRow;

            // Component title
            worksheet.Cells[currentRow, 1].Value = Title;
            worksheet.Cells[currentRow, 1].Style.Font.Size = 16;
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            currentRow += 2;

            // Create sample data for pivot table
            var dataStartRow = currentRow;
            
            // Headers
            worksheet.Cells[currentRow, 1].Value = "Category";
            worksheet.Cells[currentRow, 2].Value = "Month";
            worksheet.Cells[currentRow, 3].Value = "Amount";
            worksheet.Cells[currentRow, 4].Value = "Type";
            
            // Style headers
            for (int col = 1; col <= 4; col++)
            {
                worksheet.Cells[currentRow, col].Style.Font.Bold = true;
                worksheet.Cells[currentRow, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[currentRow, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }
            currentRow++;

            // 🔥 KRITISK FIX: Använd ENDAST riktig SIE4-data, ALDRIG fallback
            var pivotData = await GetPivotDataFromSIE4();
            if (pivotData.Length == 0)
            {
                _logger.LogError("KRITISKT FEL: Ingen SIE4-data tillgänglig för pivot-tabell!");
                worksheet.Cells[currentRow, 1].Value = "FEL: Ingen SIE4-data tillgänglig för pivot-analys";
                return ComponentResult.CreateFailure("Ingen SIE4-data för pivot");
            }
            var sampleData = pivotData;

            foreach (var (category, month, amount, type) in sampleData)
            {
                worksheet.Cells[currentRow, 1].Value = category;
                worksheet.Cells[currentRow, 2].Value = month;
                worksheet.Cells[currentRow, 3].Value = amount;
                worksheet.Cells[currentRow, 4].Value = type;
                currentRow++;
            }

            var dataEndRow = currentRow - 1;

            // Create pivot table with unique name
            var pivotStartRow = currentRow + 2;
            var dataRange = worksheet.Cells[dataStartRow, 1, dataEndRow, 4];
            var uniquePivotName = $"PivotTable_{Guid.NewGuid():N}";
            var pivotTable = worksheet.PivotTables.Add(worksheet.Cells[pivotStartRow, 1], dataRange, uniquePivotName);

            // Configure pivot table
            pivotTable.RowFields.Add(pivotTable.Fields["Category"]);
            pivotTable.ColumnFields.Add(pivotTable.Fields["Month"]);
            pivotTable.DataFields.Add(pivotTable.Fields["Amount"]);
            
            // Set data field properties
            var dataField = pivotTable.DataFields[0];
            dataField.Name = "Sum of Amount";
            dataField.Function = DataFieldFunctions.Sum;

            await Task.CompletedTask;
            return ComponentResult.CreateSuccess(pivotStartRow - startRow + 15);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render pivot component");
            return ComponentResult.CreateFailure($"Failed to render pivot table: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets pivot data from SIE4 based on configuration - ENDAST RIKTIG DATA!
    /// </summary>
    private async Task<(string Category, string Month, int Amount, string Type)[]> GetPivotDataFromSIE4()
    {
        _logger.LogError("🔥🔥🔥 GetPivotDataFromSIE4 ANROPAD!");
        Console.WriteLine("🔥🔥🔥 GetPivotDataFromSIE4 ANROPAD!");
        try
        {
            // Get pivot type from configuration
            var pivotType = Configuration.GetValueOrDefault("pivotType", "account_breakdown").ToString()!;

            _logger.LogInformation("PivotComponent: pivotType='{PivotType}', SIE4Data length={SIE4Length}",
                pivotType, SIE4Data?.Length ?? 0);

            if (string.IsNullOrEmpty(SIE4Data))
            {
                _logger.LogError("KRITISKT: SIE4Data är null eller tom i PivotComponent!");
                return Array.Empty<(string, string, int, string)>();
            }

            // Parse SIE4 data to get real financial transactions
            await ParseSIE4DataAsync();

            // Skapa pivot-data baserat på riktiga SIE4-transaktioner
            var pivotData = new List<(string, string, int, string)>();

            // Använd GetAccountBalance för att få exakta värden från olika kontokategorier
            var personalCosts = Math.Abs(GetAccountBalance("6000")); // Personalkostnader
            var rentCosts = Math.Abs(GetAccountBalance("5010"));     // Lokalhyra
            var itCosts = Math.Abs(GetAccountBalance("5400"));       // IT-kostnader
            var revenue3000 = Math.Abs(GetAccountBalance("3000"));   // Försäljning
            var revenue3100 = Math.Abs(GetAccountBalance("3100"));   // Tjänster

            // 🔥 RIKTIG MÅNADSDATA: Använd faktiska transaktionsdatum istället för simulering
            _logger.LogError("🔥 FÖRE GetRealMonthlyDataFromTransactions: ParsedTransactions.Count = {Count}", ParsedTransactions.Count);
            var monthlyData = GetRealMonthlyDataFromTransactions();
            _logger.LogError("🔥 EFTER GetRealMonthlyDataFromTransactions: monthlyData.Count = {Count}", monthlyData.Count);

            foreach (var (accountName, monthlyAmounts) in monthlyData)
            {
                _logger.LogError("🔥 Bearbetar konto: {Account} med {MonthCount} månader", accountName, monthlyAmounts.Count);
                foreach (var (month, amount) in monthlyAmounts)
                {
                    if (amount != 0) // Skippa månader utan transaktioner
                    {
                        var type = DetermineTransactionType(accountName);
                        pivotData.Add((accountName, month, (int)Math.Abs(amount), type));
                        _logger.LogError("🔥 Lade till pivot-data: {Account}, {Month}, {Amount}, {Type}", accountName, month, Math.Abs(amount), type);
                    }
                }
            }

            if (pivotData.Any())
            {
                _logger.LogInformation("Successfully created {Count} pivot data points from real SIE4 data", pivotData.Count);
                return pivotData.ToArray();
            }
            else
            {
                _logger.LogError("KRITISKT: Kunde inte skapa pivot-data från SIE4 - inga konton hade värden!");
                return Array.Empty<(string, string, int, string)>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get pivot data from SIE4, using fallback data");
            return Array.Empty<(string, string, int, string)>();
        }
    }

    /// <summary>
    /// Hämtar riktig månadsdata från SIE4-transaktioner baserat på faktiska datum
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> GetRealMonthlyDataFromTransactions()
    {
        var monthlyData = new Dictionary<string, Dictionary<string, decimal>>();

        _logger.LogError("🔥 GetRealMonthlyDataFromTransactions: Totalt {Count} transaktioner att bearbeta", ParsedTransactions.Count);

        foreach (var transaction in ParsedTransactions)
        {
            // Extrahera månad från transaktionsdatum
            var month = transaction.Date.ToString("MMM", System.Globalization.CultureInfo.CreateSpecificCulture("sv-SE"));

            // Använd kontonamn som nyckel
            var accountKey = transaction.AccountName;

            _logger.LogError("🔥 Transaktion: {Account} ({AccountNumber}), Datum: {Date}, Månad: {Month}, Belopp: {Amount}",
                accountKey, transaction.AccountNumber, transaction.Date, month, transaction.Amount);

            if (!monthlyData.ContainsKey(accountKey))
                monthlyData[accountKey] = new Dictionary<string, decimal>();

            if (!monthlyData[accountKey].ContainsKey(month))
                monthlyData[accountKey][month] = 0;

            monthlyData[accountKey][month] += transaction.Amount;
        }

        _logger.LogError("🔥 Slutresultat: {Count} konton med månadsdata", monthlyData.Count);
        foreach (var (account, months) in monthlyData)
        {
            _logger.LogError("🔥 Konto: {Account}, Månader: {Months}", account, string.Join(", ", months.Select(m => $"{m.Key}:{m.Value:N0}")));
        }

        return monthlyData;
    }

    /// <summary>
    /// Bestämmer transaktionstyp baserat på kontonamn/nummer
    /// </summary>
    private string DetermineTransactionType(string accountName)
    {
        // Hitta motsvarande transaktion för att få kontonummer
        var transaction = ParsedTransactions.FirstOrDefault(t => t.AccountName == accountName);
        if (transaction != null)
        {
            var accountNumber = transaction.AccountNumber;
            if (accountNumber.StartsWith("3")) return "Intäkt";
            if (accountNumber.StartsWith("4") || accountNumber.StartsWith("5") ||
                accountNumber.StartsWith("6") || accountNumber.StartsWith("7")) return "Kostnad";
        }

        // Fallback baserat på kontonamn
        if (accountName.ToLowerInvariant().Contains("försäljning") ||
            accountName.ToLowerInvariant().Contains("intäkt") ||
            accountName.ToLowerInvariant().Contains("tjänster"))
            return "Intäkt";

        return "Kostnad";
    }

    /// <summary>
    /// Rensar strängar från ogiltiga XML-tecken som kan orsaka pivot table fel
    /// </summary>
    private static string CleanXmlString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Ta bort ogiltiga XML-tecken
        return new string(input.Where(c =>
            c == 0x09 || c == 0x0A || c == 0x0D ||
            (c >= 0x20 && c <= 0xD7FF) ||
            (c >= 0xE000 && c <= 0xFFFD)).ToArray());
    }
}
