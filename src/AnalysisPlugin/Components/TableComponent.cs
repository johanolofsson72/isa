using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// Table component using ONLY EPPlus - creates professional tables! 📋
/// </summary>
public class TableComponent : ReportComponentBase
{
    private readonly ILogger<TableComponent> _logger;

    public TableComponent()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<TableComponent>();
    }

    public override string ComponentType => "Table";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            var currentRow = startRow;

            // Component title
            worksheet.Cells[currentRow, 1].Value = Title;
            worksheet.Cells[currentRow, 1].Style.Font.Size = 16;
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            currentRow += 2;

            // Sample table data
            var headers = new[] { "Category", "Q1", "Q2", "Q3", "Q4", "Total" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[currentRow, i + 1].Value = headers[i];
                worksheet.Cells[currentRow, i + 1].Style.Font.Bold = true;
                worksheet.Cells[currentRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[currentRow, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }
            currentRow++;

            // Sample data rows
            var data = new[]
            {
                ("Revenue", 125000, 135000, 142000, 158000, 560000),
                ("Expenses", 95000, 102000, 98000, 105000, 400000),
                ("Profit", 30000, 33000, 44000, 53000, 160000)
            };

            foreach (var (category, q1, q2, q3, q4, total) in data)
            {
                worksheet.Cells[currentRow, 1].Value = category;
                worksheet.Cells[currentRow, 2].Value = q1;
                worksheet.Cells[currentRow, 3].Value = q2;
                worksheet.Cells[currentRow, 4].Value = q3;
                worksheet.Cells[currentRow, 5].Value = q4;
                worksheet.Cells[currentRow, 6].Value = total;

                // Format as currency
                for (int col = 2; col <= 6; col++)
                {
                    worksheet.Cells[currentRow, col].Style.Numberformat.Format = "#,##0 \"kr\"";
                }

                // Alternate row coloring
                if (currentRow % 2 == 0)
                {
                    for (int col = 1; col <= 6; col++)
                    {
                        worksheet.Cells[currentRow, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[currentRow, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(242, 242, 242));
                    }
                }

                currentRow++;
            }

            await Task.CompletedTask;
            return ComponentResult.CreateSuccess(currentRow - startRow + 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render table component");
            return ComponentResult.CreateFailure($"Failed to render table: {ex.Message}");
        }
    }
}
