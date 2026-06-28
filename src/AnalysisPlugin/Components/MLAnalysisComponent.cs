using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// ML Analysis component using ONLY EPPlus - creates ML analysis reports! 🤖
/// </summary>
public class MLAnalysisComponent : ReportComponentBase
{
    private readonly ILogger<MLAnalysisComponent> _logger;

    public MLAnalysisComponent()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<MLAnalysisComponent>();
    }

    public override string ComponentType => "MLAnalysis";

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

            // ML Analysis summary
            worksheet.Cells[currentRow, 1].Value = "Machine Learning Analysis Results";
            worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
            worksheet.Cells[currentRow, 1, currentRow, 4].Merge = true;
            currentRow += 2;

            // Sample ML results
            var mlResults = new[]
            {
                ("Revenue Prediction Accuracy", "94.2%", "High"),
                ("Expense Forecast Confidence", "87.8%", "Good"),
                ("Trend Analysis Score", "91.5%", "High"),
                ("Anomaly Detection Rate", "96.1%", "Excellent")
            };

            // Headers
            worksheet.Cells[currentRow, 1].Value = "Metric";
            worksheet.Cells[currentRow, 2].Value = "Score";
            worksheet.Cells[currentRow, 3].Value = "Rating";
            
            for (int col = 1; col <= 3; col++)
            {
                worksheet.Cells[currentRow, col].Style.Font.Bold = true;
                worksheet.Cells[currentRow, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[currentRow, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }
            currentRow++;

            // Data rows
            foreach (var (metric, score, rating) in mlResults)
            {
                worksheet.Cells[currentRow, 1].Value = metric;
                worksheet.Cells[currentRow, 2].Value = score;
                worksheet.Cells[currentRow, 3].Value = rating;

                // Color code ratings
                var ratingCell = worksheet.Cells[currentRow, 3];
                ratingCell.Style.Font.Bold = true;
                ratingCell.Style.Font.Color.SetColor(rating switch
                {
                    "Excellent" => System.Drawing.Color.DarkGreen,
                    "High" => System.Drawing.Color.Green,
                    "Good" => System.Drawing.Color.Orange,
                    _ => System.Drawing.Color.Red
                });

                currentRow++;
            }

            // Add insights section
            currentRow += 2;
            worksheet.Cells[currentRow, 1].Value = "Key Insights";
            worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            currentRow++;

            var insights = new[]
            {
                "• Revenue patterns show strong seasonal correlation",
                "• Expense forecasting model identifies cost optimization opportunities",
                "• Anomaly detection flagged 3 unusual transactions for review",
                "• Trend analysis suggests 12% growth potential next quarter"
            };

            foreach (var insight in insights)
            {
                worksheet.Cells[currentRow, 1].Value = insight;
                worksheet.Cells[currentRow, 1, currentRow, 4].Merge = true;
                currentRow++;
            }

            await Task.CompletedTask;
            return ComponentResult.CreateSuccess(currentRow - startRow + 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render ML analysis component");
            return ComponentResult.CreateFailure($"Failed to render ML analysis: {ex.Message}");
        }
    }
}
