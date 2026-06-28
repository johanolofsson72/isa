using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// Simple chart component using ONLY EPPlus - no ClosedXML dependencies! 🎯
/// </summary>
public class SimpleChartComponent : ReportComponentBase
{
    private readonly ILogger<SimpleChartComponent> _logger;

    public SimpleChartComponent()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SimpleChartComponent>();
    }

    public override string ComponentType => "SimpleChart";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            var currentRow = startRow;
            var chartType = Configuration.GetValueOrDefault("chartType", "pie").ToString()!;
            var showLegend = Configuration.TryGetValue("showLegend", out var legendObj) && 
                           bool.TryParse(legendObj.ToString(), out var legend) && legend;

            // Component title
            worksheet.Cells[currentRow, 1].Value = Title;
            worksheet.Cells[currentRow, 1].Style.Font.Size = 16;
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(21, 101, 192));
            worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
            currentRow += 2;

            // Chart metadata
            worksheet.Cells[currentRow, 1].Value = "Diagramtyp:";
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            worksheet.Cells[currentRow, 2].Value = GetSwedishChartTypeName(chartType);
            currentRow += 2;

            // Render chart based on type
            var rowsUsed = chartType.ToLowerInvariant() switch
            {
                "pie" => await RenderPieChart(worksheet, currentRow, showLegend),
                "line" => await RenderLineChart(worksheet, currentRow, showLegend),
                "column" => await RenderColumnChart(worksheet, currentRow, showLegend),
                _ => await RenderPieChart(worksheet, currentRow, showLegend) // Default to pie
            };

            return ComponentResult.CreateSuccess(rowsUsed + 4);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render simple chart component");
            return ComponentResult.CreateFailure($"Failed to render chart: {ex.Message}");
        }
    }

    private async Task<int> RenderPieChart(ExcelWorksheet worksheet, int startRow, bool showLegend)
    {
        var currentRow = startRow;

        // Add sample data
        worksheet.Cells[currentRow, 1].Value = "Kategori";
        worksheet.Cells[currentRow, 2].Value = "Värde";
        ApplyHeaderStyling(worksheet.Cells[currentRow, 1, currentRow, 2]);
        currentRow++;

        var data = new[] { ("Personal", 85000), ("Lokaler", 35000), ("IT", 28000), ("Övrigt", 37000) };
        var dataStartRow = currentRow;

        foreach (var (category, value) in data)
        {
            worksheet.Cells[currentRow, 1].Value = category;
            worksheet.Cells[currentRow, 2].Value = value;
            worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
            currentRow++;
        }

        // CREATE REAL PIE CHART! 🥧
        var chart = worksheet.Drawings.AddChart("PieChart", eChartType.Pie);
        chart.Title.Text = "Fördelning - RIKTIGT CIRKELDIAGRAM!";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(500, 400);

        var series = chart.Series.Add(worksheet.Cells[dataStartRow, 2, dataStartRow + data.Length - 1, 2],
                                     worksheet.Cells[dataStartRow, 1, dataStartRow + data.Length - 1, 1]);
        series.Header = "Värden";
        chart.Legend.Position = showLegend ? eLegendPosition.Right : eLegendPosition.TopRight;

        await Task.CompletedTask;
        return currentRow - startRow + 25; // Extra space for chart
    }

    private async Task<int> RenderLineChart(ExcelWorksheet worksheet, int startRow, bool showLegend)
    {
        var currentRow = startRow;

        // Add sample data
        worksheet.Cells[currentRow, 1].Value = "Månad";
        worksheet.Cells[currentRow, 2].Value = "Värde";
        ApplyHeaderStyling(worksheet.Cells[currentRow, 1, currentRow, 2]);
        currentRow++;

        var data = new[] { ("Jan", 100), ("Feb", 120), ("Mar", 95), ("Apr", 140), ("Maj", 160), ("Jun", 180) };
        var dataStartRow = currentRow;

        foreach (var (month, value) in data)
        {
            worksheet.Cells[currentRow, 1].Value = month;
            worksheet.Cells[currentRow, 2].Value = value;
            currentRow++;
        }

        // CREATE REAL LINE CHART! 📈
        var chart = worksheet.Drawings.AddChart("LineChart", eChartType.Line);
        chart.Title.Text = "Utveckling - RIKTIGT LINJEDIAGRAM!";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(600, 400);

        var series = chart.Series.Add(worksheet.Cells[dataStartRow, 2, dataStartRow + data.Length - 1, 2],
                                     worksheet.Cells[dataStartRow, 1, dataStartRow + data.Length - 1, 1]);
        series.Header = "Värden";
        chart.Legend.Position = showLegend ? eLegendPosition.Bottom : eLegendPosition.TopRight;

        await Task.CompletedTask;
        return currentRow - startRow + 25;
    }

    private async Task<int> RenderColumnChart(ExcelWorksheet worksheet, int startRow, bool showLegend)
    {
        var currentRow = startRow;

        // Add sample data
        worksheet.Cells[currentRow, 1].Value = "Kvartal";
        worksheet.Cells[currentRow, 2].Value = "Försäljning";
        ApplyHeaderStyling(worksheet.Cells[currentRow, 1, currentRow, 2]);
        currentRow++;

        var data = new[] { ("Q1", 402000), ("Q2", 501000), ("Q3", 445000), ("Q4", 523000) };
        var dataStartRow = currentRow;

        foreach (var (quarter, sales) in data)
        {
            worksheet.Cells[currentRow, 1].Value = quarter;
            worksheet.Cells[currentRow, 2].Value = sales;
            worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "#,##0 \"kr\"";
            currentRow++;
        }

        // CREATE REAL COLUMN CHART! 📊
        var chart = worksheet.Drawings.AddChart("ColumnChart", eChartType.ColumnClustered);
        chart.Title.Text = "Försäljning - RIKTIGT STAPELDIAGRAM!";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(600, 400);

        var series = chart.Series.Add(worksheet.Cells[dataStartRow, 2, dataStartRow + data.Length - 1, 2],
                                     worksheet.Cells[dataStartRow, 1, dataStartRow + data.Length - 1, 1]);
        series.Header = "Försäljning";
        chart.Legend.Position = showLegend ? eLegendPosition.Bottom : eLegendPosition.TopRight;

        await Task.CompletedTask;
        return currentRow - startRow + 25;
    }

    private void ApplyHeaderStyling(ExcelRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
        range.Style.Font.Color.SetColor(System.Drawing.Color.White);
        range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
    }

    private string GetSwedishChartTypeName(string chartType)
    {
        return chartType.ToLowerInvariant() switch
        {
            "pie" => "Cirkeldiagram",
            "line" => "Linjediagram",
            "column" => "Stapeldiagram",
            _ => chartType
        };
    }
}
