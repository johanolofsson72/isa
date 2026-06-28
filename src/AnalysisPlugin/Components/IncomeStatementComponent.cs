using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// 🇸🇪 RESULTATRAPPORT - Klassisk svensk företagsrapport enligt K3/K2 standard
/// </summary>
public class IncomeStatementComponent : ReportComponentBase
{
    private readonly ILogger<IncomeStatementComponent> _logger;
    private readonly int _reportYear;
    private readonly int _comparisonYear;

    public IncomeStatementComponent(int reportYear = 0)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<IncomeStatementComponent>();
        
        _reportYear = reportYear == 0 ? DateTime.Now.Year : reportYear;
        _comparisonYear = _reportYear - 1;
    }

    public override string ComponentType => "IncomeStatement";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            var currentRow = startRow;

            // 🔄 PARSE SIE4 DATA FIRST TO GET REAL FINANCIAL TRANSACTIONS
            await ParseSIE4DataAsync();

            // 🎨 PREMIUM DESIGN SETUP
            worksheet.View.ShowGridLines = false;

            // 🏢 RESULTATRAPPORT HEADER
            currentRow = await RenderReportHeader(worksheet, currentRow);
            
            // 📊 INTÄKTER SEKTION
            currentRow = await RenderRevenueSection(worksheet, currentRow);
            
            // 💰 KOSTNADER SEKTION
            currentRow = await RenderExpenseSection(worksheet, currentRow);
            
            // 📈 RÖRELSERESULTAT
            currentRow = await RenderOperatingResult(worksheet, currentRow);
            
            // 🏦 FINANSNETTO
            currentRow = await RenderFinancialResult(worksheet, currentRow);
            
            // 🎯 RESULTAT FÖRE/EFTER SKATT
            currentRow = await RenderTaxResult(worksheet, currentRow);
            
            // 📊 NYCKELTAL
            currentRow = await RenderKeyRatios(worksheet, currentRow);

            // 📏 OPTIMERA KOLUMNBREDDER FÖR RESULTATRAPPORT
            OptimizeResultatrapportColumns(worksheet);

            return ComponentResult.CreateSuccess(currentRow - startRow + 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render income statement");
            return ComponentResult.CreateFailure($"Failed to render income statement: {ex.Message}");
        }
    }

    private async Task<int> RenderReportHeader(ExcelWorksheet worksheet, int currentRow)
    {
        // 🏢 PREMIUM HEADER MED SVENSK STANDARD
        var headerRange = worksheet.Cells[currentRow, 1, currentRow + 3, 8];
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        
        currentRow += 1;

        // HUVUDTITEL
        var titleRange = worksheet.Cells[currentRow, 2, currentRow, 7];
        titleRange.Merge = true;
        titleRange.Value = "RESULTATRÄKNING";
        titleRange.Style.Font.Name = "Segoe UI";
        titleRange.Style.Font.Size = 24;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
        titleRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        
        currentRow += 1;

        // PERIOD OCH JÄMFÖRELSE
        var periodRange = worksheet.Cells[currentRow, 2, currentRow, 7];
        periodRange.Merge = true;
        periodRange.Value = $"För räkenskapsåret {_reportYear} (jämförelsetal {_comparisonYear})";
        periodRange.Style.Font.Name = "Segoe UI";
        periodRange.Style.Font.Size = 12;
        periodRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(200, 220, 240));
        periodRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        
        currentRow += 3;

        // KOLUMNRUBRIKER ENLIGT SVENSK STANDARD
        var headerLabels = new[] { "Belopp i kronor", $"{_reportYear}", $"{_comparisonYear}" };
        for (int i = 0; i < headerLabels.Length; i++)
        {
            var cell = worksheet.Cells[currentRow, 6 + i];
            cell.Value = headerLabels[i];
            cell.Style.Font.Name = "Segoe UI";
            cell.Style.Font.Size = 12;
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            cell.Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            cell.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        }
        
        currentRow += 2;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderRevenueSection(ExcelWorksheet worksheet, int currentRow)
    {
        // 📊 INTÄKTER ENLIGT SVENSK STANDARD
        currentRow = await RenderSectionHeader(worksheet, currentRow, "RÖRELSEINTÄKTER");
        
        var revenueItems = new[]
        {
            ("Nettoomsättning", GetAccountBalance("3010"), GetAccountBalance("3010", _comparisonYear)),
            ("Övriga rörelseintäkter", GetAccountBalance("3900"), GetAccountBalance("3900", _comparisonYear))
        };

        decimal totalRevenue = 0;
        decimal totalRevenueComparison = 0;

        foreach (var (label, current, comparison) in revenueItems)
        {
            currentRow = await RenderAccountLine(worksheet, currentRow, label, current, comparison);
            totalRevenue += Math.Abs(current);
            totalRevenueComparison += Math.Abs(comparison);
        }

        // SUMMA RÖRELSEINTÄKTER
        currentRow = await RenderTotalLine(worksheet, currentRow, "Summa rörelseintäkter", totalRevenue, totalRevenueComparison);
        currentRow += 1;
        
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderExpenseSection(ExcelWorksheet worksheet, int currentRow)
    {
        // 💰 RÖRELSEKOSTNADER ENLIGT SVENSK STANDARD
        currentRow = await RenderSectionHeader(worksheet, currentRow, "RÖRELSEKOSTNADER");
        
        var expenseItems = new[]
        {
            ("Handelsvaror", GetAccountBalance("4010"), GetAccountBalance("4010", _comparisonYear)),
            ("Övriga externa kostnader", GetAccountBalance("5010"), GetAccountBalance("5010", _comparisonYear)),
            ("Personalkostnader", GetAccountBalance("7010"), GetAccountBalance("7010", _comparisonYear)),
            ("Avskrivningar", GetAccountBalance("7810"), GetAccountBalance("7810", _comparisonYear)),
            ("Övriga rörelsekostnader", GetAccountBalance("6990"), GetAccountBalance("6990", _comparisonYear))
        };

        decimal totalExpenses = 0;
        decimal totalExpensesComparison = 0;

        foreach (var (label, current, comparison) in expenseItems)
        {
            currentRow = await RenderAccountLine(worksheet, currentRow, label, current, comparison, true);
            totalExpenses += Math.Abs(current);
            totalExpensesComparison += Math.Abs(comparison);
        }

        // SUMMA RÖRELSEKOSTNADER
        currentRow = await RenderTotalLine(worksheet, currentRow, "Summa rörelsekostnader", -totalExpenses, -totalExpensesComparison);
        currentRow += 1;
        
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderOperatingResult(ExcelWorksheet worksheet, int currentRow)
    {
        // 📈 RÖRELSERESULTAT
        var revenue = Math.Abs(GetAccountBalance("3010"));
        var expenses = Math.Abs(GetAccountBalance("4010")) + Math.Abs(GetAccountBalance("5010")) +
                      Math.Abs(GetAccountBalance("7010")) + Math.Abs(GetAccountBalance("7810"));
        var operatingResult = revenue - expenses;

        var revenueComparison = Math.Abs(GetAccountBalance("3010", _comparisonYear));
        var expensesComparison = Math.Abs(GetAccountBalance("4010", _comparisonYear)) +
                               Math.Abs(GetAccountBalance("5010", _comparisonYear));
        var operatingResultComparison = revenueComparison - expensesComparison;

        currentRow = await RenderTotalLine(worksheet, currentRow, "RÖRELSERESULTAT", operatingResult, operatingResultComparison, true);
        currentRow += 1;

        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderFinancialResult(ExcelWorksheet worksheet, int currentRow)
    {
        // 🏦 FINANSIELLA POSTER
        currentRow = await RenderSectionHeader(worksheet, currentRow, "FINANSIELLA POSTER");

        var financialItems = new[]
        {
            ("Ränteintäkter", GetAccountBalance("8310"), GetAccountBalance("8310", _comparisonYear)),
            ("Räntekostnader", GetAccountBalance("8410"), GetAccountBalance("8410", _comparisonYear))
        };

        decimal totalFinancial = 0;
        decimal totalFinancialComparison = 0;

        foreach (var (label, current, comparison) in financialItems)
        {
            var isExpense = label.Contains("kostnad");
            currentRow = await RenderAccountLine(worksheet, currentRow, label, current, comparison, isExpense);
            totalFinancial += isExpense ? -Math.Abs(current) : Math.Abs(current);
            totalFinancialComparison += isExpense ? -Math.Abs(comparison) : Math.Abs(comparison);
        }

        currentRow = await RenderTotalLine(worksheet, currentRow, "Summa finansiella poster", totalFinancial, totalFinancialComparison);
        currentRow += 1;

        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderTaxResult(ExcelWorksheet worksheet, int currentRow)
    {
        // 🎯 RESULTAT FÖRE OCH EFTER SKATT
        var operatingResult = Math.Abs(GetAccountBalance("3010")) - Math.Abs(GetAccountBalance("4010")) - Math.Abs(GetAccountBalance("5010"));
        var financialResult = Math.Abs(GetAccountBalance("8310")) - Math.Abs(GetAccountBalance("8410"));
        var resultBeforeTax = operatingResult + financialResult;

        var operatingResultComparison = Math.Abs(GetAccountBalance("3010", _comparisonYear)) - Math.Abs(GetAccountBalance("4010", _comparisonYear));
        var resultBeforeTaxComparison = operatingResultComparison;

        currentRow = await RenderTotalLine(worksheet, currentRow, "RESULTAT FÖRE SKATT", resultBeforeTax, resultBeforeTaxComparison, true);

        // Skatt (uppskattad som 20.6% för svenska företag)
        var tax = resultBeforeTax * 0.206m;
        var taxComparison = resultBeforeTaxComparison * 0.206m;

        currentRow = await RenderAccountLine(worksheet, currentRow, "Skatt på årets resultat", -tax, -taxComparison, true);

        var resultAfterTax = resultBeforeTax - tax;
        var resultAfterTaxComparison = resultBeforeTaxComparison - taxComparison;

        currentRow = await RenderTotalLine(worksheet, currentRow, "ÅRETS RESULTAT", resultAfterTax, resultAfterTaxComparison, true);
        currentRow += 2;

        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderKeyRatios(ExcelWorksheet worksheet, int currentRow)
    {
        // 📊 NYCKELTAL ENLIGT SVENSK STANDARD
        currentRow = await RenderSectionHeader(worksheet, currentRow, "NYCKELTAL");

        var revenue = Math.Abs(GetAccountBalance("3010"));
        var operatingResult = revenue - Math.Abs(GetAccountBalance("4010")) - Math.Abs(GetAccountBalance("5010"));
        var resultAfterTax = operatingResult * 0.794m; // Efter skatt

        var operatingMargin = revenue > 0 ? (operatingResult / revenue) * 100 : 0;
        var profitMargin = revenue > 0 ? (resultAfterTax / revenue) * 100 : 0;

        var revenueComparison = Math.Abs(GetAccountBalance("3010", _comparisonYear));
        var operatingResultComparison = revenueComparison - Math.Abs(GetAccountBalance("4010", _comparisonYear));
        var operatingMarginComparison = revenueComparison > 0 ? (operatingResultComparison / revenueComparison) * 100 : 0;

        // Nyckeltal med svensk formatering
        currentRow = await RenderRatioLine(worksheet, currentRow, "Rörelsemarginal", operatingMargin, operatingMarginComparison, "%");
        currentRow = await RenderRatioLine(worksheet, currentRow, "Vinstmarginal", profitMargin, 0, "%");
        currentRow = await RenderRatioLine(worksheet, currentRow, "Omsättningstillväxt",
            revenueComparison > 0 ? ((revenue - revenueComparison) / revenueComparison) * 100 : 0, 0, "%");

        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderSectionHeader(ExcelWorksheet worksheet, int currentRow, string title)
    {
        var headerRange = worksheet.Cells[currentRow, 1, currentRow, 8];
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 248, 255));

        var titleCell = worksheet.Cells[currentRow, 2];
        titleCell.Value = title;
        titleCell.Style.Font.Name = "Segoe UI";
        titleCell.Style.Font.Size = 14;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));

        currentRow += 1;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderAccountLine(ExcelWorksheet worksheet, int currentRow, string label, decimal current, decimal comparison, bool isExpense = false)
    {
        // Kontorad med svensk formatering
        var labelCell = worksheet.Cells[currentRow, 3];
        labelCell.Value = label;
        labelCell.Style.Font.Name = "Segoe UI";
        labelCell.Style.Font.Size = 11;

        var currentCell = worksheet.Cells[currentRow, 7];
        var displayCurrent = isExpense ? -Math.Abs(current) : Math.Abs(current);
        currentCell.Value = displayCurrent;
        currentCell.Style.Numberformat.Format = "#,##0 \"kr\"";
        currentCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

        var comparisonCell = worksheet.Cells[currentRow, 8];
        var displayComparison = isExpense ? -Math.Abs(comparison) : Math.Abs(comparison);
        comparisonCell.Value = displayComparison;
        comparisonCell.Style.Numberformat.Format = "#,##0 \"kr\"";
        comparisonCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

        currentRow += 1;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderTotalLine(ExcelWorksheet worksheet, int currentRow, string label, decimal current, decimal comparison, bool isMajorTotal = false)
    {
        var labelCell = worksheet.Cells[currentRow, 2];
        labelCell.Value = label;
        labelCell.Style.Font.Name = "Segoe UI";
        labelCell.Style.Font.Size = isMajorTotal ? 13 : 12;
        labelCell.Style.Font.Bold = true;
        labelCell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));

        var currentCell = worksheet.Cells[currentRow, 7];
        currentCell.Value = current;
        currentCell.Style.Numberformat.Format = "#,##0 \"kr\"";
        currentCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        currentCell.Style.Font.Bold = true;
        currentCell.Style.Border.Top.Style = ExcelBorderStyle.Thin;

        var comparisonCell = worksheet.Cells[currentRow, 8];
        comparisonCell.Value = comparison;
        comparisonCell.Style.Numberformat.Format = "#,##0 \"kr\"";
        comparisonCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        comparisonCell.Style.Font.Bold = true;
        comparisonCell.Style.Border.Top.Style = ExcelBorderStyle.Thin;

        if (isMajorTotal)
        {
            var range = worksheet.Cells[currentRow, 2, currentRow, 8];
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Double;
            range.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        }

        currentRow += 1;
        await Task.CompletedTask;
        return currentRow;
    }

    private async Task<int> RenderRatioLine(ExcelWorksheet worksheet, int currentRow, string label, decimal current, decimal comparison, string unit)
    {
        var labelCell = worksheet.Cells[currentRow, 3];
        labelCell.Value = label;
        labelCell.Style.Font.Name = "Segoe UI";
        labelCell.Style.Font.Size = 11;

        var currentCell = worksheet.Cells[currentRow, 7];
        currentCell.Value = current;
        currentCell.Style.Numberformat.Format = $"0.0 \"{unit}\"";
        currentCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

        if (comparison != 0)
        {
            var comparisonCell = worksheet.Cells[currentRow, 8];
            comparisonCell.Value = comparison;
            comparisonCell.Style.Numberformat.Format = $"0.0 \"{unit}\"";
            comparisonCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        }

        currentRow += 1;
        await Task.CompletedTask;
        return currentRow;
    }

    // GetAccountBalance method is now inherited from ReportComponentBase and uses parsed transactions

    private decimal GetFallbackValue(string accountNumber)
    {
        // Realistiska fallback-värden för svenska företag
        return accountNumber switch
        {
            "3010" => 12500000, // Nettoomsättning
            "4010" => 7500000,  // Handelsvaror
            "5010" => 2800000,  // Övriga externa kostnader
            "7010" => 1800000,  // Personalkostnader
            "7810" => 450000,   // Avskrivningar
            "8310" => 25000,    // Ränteintäkter
            "8410" => 180000,   // Räntekostnader
            _ => 0
        };
    }

    /// <summary>
    /// 📏 Optimerar kolumnbredder specifikt för resultatrapport
    /// </summary>
    private void OptimizeResultatrapportColumns(ExcelWorksheet worksheet)
    {
        // Kolumn 1: Minimal bredd (används sällan)
        worksheet.Column(1).Width = 3;

        // Kolumn 2: Huvudkategorier (RÖRELSEINTÄKTER, etc.)
        worksheet.Column(2).Width = 35;

        // Kolumn 3: Kontobeskrivningar (indenterade)
        worksheet.Column(3).Width = 30;

        // Kolumn 4-5: Mellankolumner
        worksheet.Column(4).Width = 8;
        worksheet.Column(5).Width = 8;

        // Kolumn 6: Rubrik "Belopp i kronor"
        worksheet.Column(6).Width = 18;

        // Kolumn 7: Aktuellt år (siffror)
        worksheet.Column(7).Width = 16;

        // Kolumn 8: Föregående år (siffror)
        worksheet.Column(8).Width = 16;

        _logger.LogInformation("📏 Kolumnbredder optimerade för resultatrapport");
    }
}
