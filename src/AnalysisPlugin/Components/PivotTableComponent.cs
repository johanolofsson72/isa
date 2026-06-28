using OfficeOpenXml;
using OfficeOpenXml.Table.PivotTable;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// Professional pivot table component with REAL Excel PivotTable objects! 🎯
/// </summary>
public class PivotTableComponent : ReportComponentBase
{
    private readonly ILogger<PivotTableComponent> _logger;

    public PivotTableComponent()
    {
        // Create logger for pivot table component
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<PivotTableComponent>();
    }

    public override string ComponentType => "PivotTable";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            var currentRow = startRow;
            var pivotType = Configuration.GetValueOrDefault("pivotType", "expense_analysis").ToString()!;
            var groupBy = Configuration.GetValueOrDefault("groupBy", "category").ToString()!;
            var showTotals = Configuration.TryGetValue("showTotals", out var totalsObj) && 
                           bool.TryParse(totalsObj.ToString(), out var totals) && totals;

            // Component title
            var titleCell = worksheet.Cells[currentRow, 1];
            titleCell.Value = Title;
            titleCell.Style.Font.Size = 16;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(21, 101, 192));
            worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
            currentRow += 2;

            // Pivot metadata
            worksheet.Cells[currentRow, 1].Value = "Pivottyp:";
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 2].Value = GetSwedishPivotTypeName(pivotType);
            currentRow++;

            worksheet.Cells[currentRow, 1].Value = "Gruppering:";
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 2].Value = GetSwedishGroupByName(groupBy);
            currentRow += 2;

            // Render pivot table based on type
            var rowsUsed = pivotType.ToLowerInvariant() switch
            {
                "expense_analysis" => await RenderExpenseAnalysisPivot(worksheet, currentRow, groupBy, showTotals),
                "revenue_breakdown" => await RenderRevenueBreakdownPivot(worksheet, currentRow, groupBy, showTotals),
                "monthly_summary" => await RenderMonthlySummaryPivot(worksheet, currentRow, groupBy, showTotals),
                _ => await RenderGenericPivot(worksheet, currentRow, pivotType, groupBy, showTotals)
            };

            return ComponentResult.CreateSuccess(rowsUsed + 4); // +4 for title and metadata rows
        }
        catch (Exception ex)
        {
            return ComponentResult.CreateFailure($"Failed to render pivot table: {ex.Message}");
        }
    }

    private async Task<int> RenderExpenseAnalysisPivot(ExcelWorksheet worksheet, int startRow, string groupBy, bool showTotals)
    {
        var currentRow = startRow;

        // Create sample data for the pivot table
        worksheet.Cells[currentRow, 1].Value = "RIKTIGT EXCEL PIVOT-TABELL! 🎯";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 243, 224));
        worksheet.Cells[currentRow, 1, currentRow, 6].Merge = true;
        currentRow += 2;

        // Create source data for pivot table
        var dataStartRow = currentRow;
        
        // Headers
        worksheet.Cells[currentRow, 1].Value = "Datum";
        worksheet.Cells[currentRow, 2].Value = "Kategori";
        worksheet.Cells[currentRow, 3].Value = "Underkategori";
        worksheet.Cells[currentRow, 4].Value = "Belopp";
        worksheet.Cells[currentRow, 5].Value = "BAS-konto";
        worksheet.Cells[currentRow, 6].Value = "Beskrivning";
        ApplyEPPlusHeaderStyling(worksheet.Cells[currentRow, 1, currentRow, 6]);
        currentRow++;

        // Sample expense data
        var expenseData = new[]
        {
            new { Date = "2024-01-15", Category = "Personal", SubCategory = "Löner", Amount = 45000m, BAS = "6110", Description = "Månadslön" },
            new { Date = "2024-01-20", Category = "Personal", SubCategory = "Sociala avgifter", Amount = 14000m, BAS = "6210", Description = "Arbetsgivaravgifter" },
            new { Date = "2024-01-25", Category = "Lokaler", SubCategory = "Hyra", Amount = 18000m, BAS = "5010", Description = "Kontorshyra" },
            new { Date = "2024-01-30", Category = "IT", SubCategory = "Programvara", Amount = 3500m, BAS = "5410", Description = "Office 365" },
            new { Date = "2024-02-15", Category = "Personal", SubCategory = "Löner", Amount = 47000m, BAS = "6110", Description = "Månadslön" },
            new { Date = "2024-02-20", Category = "Personal", SubCategory = "Sociala avgifter", Amount = 14500m, BAS = "6210", Description = "Arbetsgivaravgifter" },
            new { Date = "2024-02-25", Category = "Lokaler", SubCategory = "Hyra", Amount = 18000m, BAS = "5010", Description = "Kontorshyra" },
            new { Date = "2024-02-28", Category = "Marknadsföring", SubCategory = "Annonsering", Amount = 8500m, BAS = "5090", Description = "Google Ads" }
        };

        foreach (var data in expenseData)
        {
            worksheet.Cells[currentRow, 1].Value = DateTime.Parse(data.Date);
            worksheet.Cells[currentRow, 2].Value = data.Category;
            worksheet.Cells[currentRow, 3].Value = data.SubCategory;
            worksheet.Cells[currentRow, 4].Value = data.Amount;
            worksheet.Cells[currentRow, 5].Value = data.BAS;
            worksheet.Cells[currentRow, 6].Value = data.Description;
            
            // Format date and currency
            worksheet.Cells[currentRow, 1].Style.Numberformat.Format = "yyyy-MM-dd";
            worksheet.Cells[currentRow, 4].Style.Numberformat.Format = "#,##0 \"kr\"";
            currentRow++;
        }

        var dataEndRow = currentRow - 1;

        // CREATE REAL EXCEL PIVOT TABLE! 🎯
        var dataRange = worksheet.Cells[dataStartRow, 1, dataEndRow, 6];
        var uniquePivotName = $"ExpenseAnalysisPivot_{Guid.NewGuid():N}";
        var pivotTable = worksheet.PivotTables.Add(worksheet.Cells[currentRow + 2, 1], dataRange, uniquePivotName);

        // Configure pivot table
        pivotTable.RowFields.Add(pivotTable.Fields["Kategori"]);
        if (groupBy == "subcategory")
        {
            pivotTable.RowFields.Add(pivotTable.Fields["Underkategori"]);
        }
        
        var amountField = pivotTable.DataFields.Add(pivotTable.Fields["Belopp"]);
        amountField.Name = "Summa Belopp";
        amountField.Function = DataFieldFunctions.Sum;

        // Style the pivot table (using new EPPlus API)
        pivotTable.ShowHeaders = true;
        pivotTable.ShowRowHeaders = true;
        pivotTable.ShowColumnHeaders = true;

        if (showTotals)
        {
            pivotTable.RowGrandTotals = true;
            pivotTable.ColumnGrandTotals = true;
        }

        await Task.CompletedTask;
        return currentRow - startRow + 15; // Extra space for pivot table
    }

    private async Task<int> RenderRevenueBreakdownPivot(ExcelWorksheet worksheet, int startRow, string groupBy, bool showTotals)
    {
        // Implementation for revenue breakdown pivot
        return await RenderGenericPivot(worksheet, startRow, "revenue_breakdown", groupBy, showTotals);
    }

    private async Task<int> RenderMonthlySummaryPivot(ExcelWorksheet worksheet, int startRow, string groupBy, bool showTotals)
    {
        // Implementation for monthly summary pivot
        return await RenderGenericPivot(worksheet, startRow, "monthly_summary", groupBy, showTotals);
    }

    private async Task<int> RenderGenericPivot(ExcelWorksheet worksheet, int startRow, string pivotType, string groupBy, bool showTotals)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = $"Generisk pivot-tabell: {pivotType} (RIKTIGT EXCEL PIVOT!)";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        // Simple sample data
        worksheet.Cells[currentRow, 1].Value = "Kategori";
        worksheet.Cells[currentRow, 2].Value = "Värde";
        ApplyEPPlusHeaderStyling(worksheet.Cells[currentRow, 1, currentRow, 2]);
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "A";
        worksheet.Cells[currentRow, 2].Value = 100;
        currentRow++;
        worksheet.Cells[currentRow, 1].Value = "B";
        worksheet.Cells[currentRow, 2].Value = 200;
        currentRow++;
        worksheet.Cells[currentRow, 1].Value = "C";
        worksheet.Cells[currentRow, 2].Value = 150;
        currentRow++;

        // Create simple pivot table
        var dataRange = worksheet.Cells[startRow + 2, 1, currentRow - 1, 2];
        var uniquePivotName = $"GenericPivot_{pivotType}_{Guid.NewGuid():N}";
        var pivotTable = worksheet.PivotTables.Add(worksheet.Cells[currentRow + 1, 1], dataRange, uniquePivotName);

        pivotTable.RowFields.Add(pivotTable.Fields["Kategori"]);
        var valueField = pivotTable.DataFields.Add(pivotTable.Fields["Värde"]);
        valueField.Name = "Summa";
        valueField.Function = DataFieldFunctions.Sum;

        // 🎨 PREMIUM PIVOT STYLING
        worksheet.View.ShowGridLines = false; // Ta bort rutnätslinjer

        // Premium header för pivot-sektion
        var pivotHeaderRange = worksheet.Cells[startRow - 2, 1, startRow - 2, 8];
        pivotHeaderRange.Merge = true;
        pivotHeaderRange.Value = $"📈 {Title}";
        pivotHeaderRange.Style.Font.Name = "Segoe UI";
        pivotHeaderRange.Style.Font.Size = 18;
        pivotHeaderRange.Style.Font.Bold = true;
        pivotHeaderRange.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
        pivotHeaderRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        pivotHeaderRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        pivotHeaderRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 248, 255));
        pivotHeaderRange.Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
        pivotHeaderRange.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));

        pivotTable.RowGrandTotals = showTotals;

        await Task.CompletedTask;
        return currentRow - startRow + 10;
    }

    private void ApplyEPPlusHeaderStyling(ExcelRange range)
    {
        // 🎨 PREMIUM PIVOT HEADER STYLING
        range.Style.Font.Name = "Segoe UI";
        range.Style.Font.Size = 12;
        range.Style.Font.Bold = true;
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(31, 78, 121)); // Premium blå
        range.Style.Font.Color.SetColor(System.Drawing.Color.White);
        range.Style.Border.BorderAround(ExcelBorderStyle.Medium, System.Drawing.Color.FromArgb(31, 78, 121));
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    }

    private string GetSwedishPivotTypeName(string pivotType)
    {
        return pivotType.ToLowerInvariant() switch
        {
            "expense_analysis" => "Kostnadsanalys",
            "revenue_breakdown" => "Intäktsfördelning",
            "monthly_summary" => "Månadssammanfattning",
            _ => pivotType
        };
    }

    private string GetSwedishGroupByName(string groupBy)
    {
        return groupBy.ToLowerInvariant() switch
        {
            "category" => "Kategori",
            "subcategory" => "Underkategori",
            "month" => "Månad",
            "bas_account" => "BAS-konto",
            _ => groupBy
        };
    }
}
