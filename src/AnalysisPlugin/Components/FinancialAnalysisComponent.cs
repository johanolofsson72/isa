using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Components;

/// <summary>
/// 💰 Financial Analysis Component - Specialiserade ekonomianalys-komponenter!
/// Cash Flow Timeline, Budget vs Actual, Break-even, ROI, Financial Ratios, Variance Analysis
/// </summary>
public class FinancialAnalysisComponent : ReportComponentBase
{
    private readonly ILogger<FinancialAnalysisComponent> _logger;

    public FinancialAnalysisComponent()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<FinancialAnalysisComponent>();
    }

    public override string ComponentType => "FinancialAnalysis";

    public override async Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        try
        {
            var currentRow = startRow;
            var analysisType = Configuration.GetValueOrDefault("analysisType", "cashflow").ToString()!;
            var data = Configuration.GetValueOrDefault("data", new List<Dictionary<string, object>>()) as List<Dictionary<string, object>>;

            // Component title
            worksheet.Cells[currentRow, 1].Value = Title;
            worksheet.Cells[currentRow, 1].Style.Font.Size = 16;
            worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
            currentRow += 2;

            // Render financial analysis based on type
            var rowsUsed = analysisType.ToLowerInvariant() switch
            {
                "cashflow" => await RenderCashFlowTimeline(worksheet, currentRow, data!),
                "budgetvsactual" => await RenderBudgetVsActual(worksheet, currentRow, data!),
                "breakeven" => await RenderBreakEvenAnalysis(worksheet, currentRow, data!),
                "roi" => await RenderROIDashboard(worksheet, currentRow, data!),
                "ratios" => await RenderFinancialRatios(worksheet, currentRow, data!),
                "variance" => await RenderVarianceAnalysis(worksheet, currentRow, data!),
                "trend" => await RenderTrendAnalysis(worksheet, currentRow, data!),
                _ => await RenderCashFlowTimeline(worksheet, currentRow, data!)
            };

            return ComponentResult.CreateSuccess(rowsUsed + 4);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render financial analysis component");
            return ComponentResult.CreateFailure($"Failed to render financial analysis: {ex.Message}");
        }
    }

    /// <summary>
    /// 💧 Cash Flow Timeline - Kassaflödesvisualisering över tid
    /// </summary>
    private async Task<int> RenderCashFlowTimeline(ExcelWorksheet worksheet, int startRow, List<Dictionary<string, object>> data)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = "💧 KASSAFLÖDESANALYS - Timeline";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        var cashFlowData = data?.Any() == true ? data : GetSampleCashFlowData();
        var dataStartRow = currentRow;

        // Headers
        worksheet.Cells[currentRow, 1].Value = "Period";
        worksheet.Cells[currentRow, 2].Value = "Inbetalningar";
        worksheet.Cells[currentRow, 3].Value = "Utbetalningar";
        worksheet.Cells[currentRow, 4].Value = "Netto Kassaflöde";
        worksheet.Cells[currentRow, 5].Value = "Ackumulerat";
        currentRow++;

        var accumulated = 0.0;
        foreach (var item in cashFlowData)
        {
            var inflow = Convert.ToDouble(item.GetValueOrDefault("inflow", 0));
            var outflow = Convert.ToDouble(item.GetValueOrDefault("outflow", 0));
            var net = inflow - outflow;
            accumulated += net;

            worksheet.Cells[currentRow, 1].Value = item.GetValueOrDefault("period", "").ToString();
            worksheet.Cells[currentRow, 2].Value = inflow;
            worksheet.Cells[currentRow, 3].Value = outflow;
            worksheet.Cells[currentRow, 4].Value = net;
            worksheet.Cells[currentRow, 5].Value = accumulated;

            // Färgkoda netto kassaflöde
            if (net >= 0)
                worksheet.Cells[currentRow, 4].Style.Font.Color.SetColor(System.Drawing.Color.Green);
            else
                worksheet.Cells[currentRow, 4].Style.Font.Color.SetColor(System.Drawing.Color.Red);

            currentRow++;
        }

        // Skapa kombinerat diagram för kassaflöde
        var chart = worksheet.Drawings.AddChart($"CashFlowChart_{Guid.NewGuid():N}", eChartType.ColumnClustered);
        chart.Title.Text = "Kassaflödesanalys över tid";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(800, 400);

        // Inbetalningar
        var inflowSeries = chart.Series.Add(
            worksheet.Cells[dataStartRow + 1, 2, dataStartRow + cashFlowData.Count, 2],
            worksheet.Cells[dataStartRow + 1, 1, dataStartRow + cashFlowData.Count, 1]);
        inflowSeries.Header = "Inbetalningar";

        // Utbetalningar
        var outflowSeries = chart.Series.Add(
            worksheet.Cells[dataStartRow + 1, 3, dataStartRow + cashFlowData.Count, 3],
            worksheet.Cells[dataStartRow + 1, 1, dataStartRow + cashFlowData.Count, 1]);
        outflowSeries.Header = "Utbetalningar";

        await Task.CompletedTask;
        return currentRow - startRow + 25;
    }

    /// <summary>
    /// 📊 Budget vs Actual - Budgetjämförelse med avvikelser
    /// </summary>
    private async Task<int> RenderBudgetVsActual(ExcelWorksheet worksheet, int startRow, List<Dictionary<string, object>> data)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = "📊 BUDGET VS ACTUAL - Avvikelseanalys";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        var budgetData = data?.Any() == true ? data : GetSampleBudgetData();
        var dataStartRow = currentRow;

        // Headers
        worksheet.Cells[currentRow, 1].Value = "Kategori";
        worksheet.Cells[currentRow, 2].Value = "Budget";
        worksheet.Cells[currentRow, 3].Value = "Actual";
        worksheet.Cells[currentRow, 4].Value = "Avvikelse";
        worksheet.Cells[currentRow, 5].Value = "Avvikelse %";
        currentRow++;

        foreach (var item in budgetData)
        {
            var budget = Convert.ToDouble(item.GetValueOrDefault("budget", 0));
            var actual = Convert.ToDouble(item.GetValueOrDefault("actual", 0));
            var variance = actual - budget;
            var variancePercent = budget != 0 ? variance / budget : 0;

            worksheet.Cells[currentRow, 1].Value = item.GetValueOrDefault("category", "").ToString();
            worksheet.Cells[currentRow, 2].Value = budget;
            worksheet.Cells[currentRow, 3].Value = actual;
            worksheet.Cells[currentRow, 4].Value = variance;
            worksheet.Cells[currentRow, 5].Value = variancePercent;

            // Färgkoda avvikelser
            var varianceColor = variance >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red;
            worksheet.Cells[currentRow, 4].Style.Font.Color.SetColor(varianceColor);
            worksheet.Cells[currentRow, 5].Style.Font.Color.SetColor(varianceColor);
            worksheet.Cells[currentRow, 5].Style.Numberformat.Format = "0.0%";

            currentRow++;
        }

        // Skapa jämförelsediagram
        var chart = worksheet.Drawings.AddChart($"BudgetChart_{Guid.NewGuid():N}", eChartType.ColumnClustered);
        chart.Title.Text = "Budget vs Actual Jämförelse";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(700, 400);

        // Budget serie
        var budgetSeries = chart.Series.Add(
            worksheet.Cells[dataStartRow + 1, 2, dataStartRow + budgetData.Count, 2],
            worksheet.Cells[dataStartRow + 1, 1, dataStartRow + budgetData.Count, 1]);
        budgetSeries.Header = "Budget";

        // Actual serie
        var actualSeries = chart.Series.Add(
            worksheet.Cells[dataStartRow + 1, 3, dataStartRow + budgetData.Count, 3],
            worksheet.Cells[dataStartRow + 1, 1, dataStartRow + budgetData.Count, 1]);
        actualSeries.Header = "Actual";

        await Task.CompletedTask;
        return currentRow - startRow + 25;
    }

    /// <summary>
    /// ⚖️ Break-even Analysis - Nollpunktsanalys
    /// </summary>
    private async Task<int> RenderBreakEvenAnalysis(ExcelWorksheet worksheet, int startRow, List<Dictionary<string, object>> data)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = "⚖️ BREAK-EVEN ANALYS - Nollpunktsanalys";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        // Break-even beräkningar
        var fixedCosts = 150000; // Fast kostnad
        var variableCostPerUnit = 45; // Rörlig kostnad per enhet
        var pricePerUnit = 75; // Pris per enhet
        var contributionMargin = pricePerUnit - variableCostPerUnit;
        var breakEvenUnits = fixedCosts / contributionMargin;

        // Visa nyckeltal
        worksheet.Cells[currentRow, 1].Value = "Nyckeltal";
        worksheet.Cells[currentRow, 2].Value = "Värde";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        worksheet.Cells[currentRow, 2].Style.Font.Bold = true;
        currentRow++;

        var keyMetrics = new[]
        {
            ("Fasta kostnader", fixedCosts),
            ("Pris per enhet", pricePerUnit),
            ("Rörlig kostnad per enhet", variableCostPerUnit),
            ("Täckningsbidrag per enhet", contributionMargin),
            ("Break-even volym (enheter)", breakEvenUnits),
            ("Break-even omsättning", breakEvenUnits * pricePerUnit)
        };

        foreach (var (metric, value) in keyMetrics)
        {
            worksheet.Cells[currentRow, 1].Value = metric;
            worksheet.Cells[currentRow, 2].Value = value;
            if (metric.Contains("Break-even"))
            {
                worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                worksheet.Cells[currentRow, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                worksheet.Cells[currentRow, 2].Style.Font.Bold = true;
            }
            currentRow++;
        }

        // Skapa break-even diagram
        currentRow += 2;
        var chartStartRow = currentRow;
        
        // Skapa data för break-even graf
        worksheet.Cells[currentRow, 1].Value = "Volym";
        worksheet.Cells[currentRow, 2].Value = "Totala kostnader";
        worksheet.Cells[currentRow, 3].Value = "Totala intäkter";
        currentRow++;

        for (int volume = 0; volume <= breakEvenUnits * 2; volume += (int)(breakEvenUnits / 10))
        {
            var totalCosts = fixedCosts + (volume * variableCostPerUnit);
            var totalRevenue = volume * pricePerUnit;
            
            worksheet.Cells[currentRow, 1].Value = volume;
            worksheet.Cells[currentRow, 2].Value = totalCosts;
            worksheet.Cells[currentRow, 3].Value = totalRevenue;
            currentRow++;
        }

        var chart = worksheet.Drawings.AddChart($"BreakEvenChart_{Guid.NewGuid():N}", eChartType.LineMarkers);
        chart.Title.Text = "Break-even Analys";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(700, 400);

        // Kostnadslinje
        var costSeries = chart.Series.Add(
            worksheet.Cells[chartStartRow + 1, 2, currentRow - 1, 2],
            worksheet.Cells[chartStartRow + 1, 1, currentRow - 1, 1]);
        costSeries.Header = "Totala kostnader";

        // Intäktslinje
        var revenueSeries = chart.Series.Add(
            worksheet.Cells[chartStartRow + 1, 3, currentRow - 1, 3],
            worksheet.Cells[chartStartRow + 1, 1, currentRow - 1, 1]);
        revenueSeries.Header = "Totala intäkter";

        await Task.CompletedTask;
        return currentRow - startRow + 25;
    }

    // Sample data methods
    private List<Dictionary<string, object>> GetSampleCashFlowData()
    {
        return new List<Dictionary<string, object>>
        {
            new() { ["period"] = "Jan", ["inflow"] = 250000, ["outflow"] = 180000 },
            new() { ["period"] = "Feb", ["inflow"] = 280000, ["outflow"] = 195000 },
            new() { ["period"] = "Mar", ["inflow"] = 320000, ["outflow"] = 210000 },
            new() { ["period"] = "Apr", ["inflow"] = 290000, ["outflow"] = 205000 },
            new() { ["period"] = "Maj", ["inflow"] = 350000, ["outflow"] = 225000 },
            new() { ["period"] = "Jun", ["inflow"] = 380000, ["outflow"] = 240000 }
        };
    }

    private List<Dictionary<string, object>> GetSampleBudgetData()
    {
        return new List<Dictionary<string, object>>
        {
            new() { ["category"] = "Intäkter", ["budget"] = 500000, ["actual"] = 520000 },
            new() { ["category"] = "Personal", ["budget"] = 200000, ["actual"] = 195000 },
            new() { ["category"] = "Lokaler", ["budget"] = 50000, ["actual"] = 52000 },
            new() { ["category"] = "Marknadsföring", ["budget"] = 30000, ["actual"] = 35000 },
            new() { ["category"] = "IT", ["budget"] = 25000, ["actual"] = 22000 }
        };
    }

    /// <summary>
    /// 💎 ROI Dashboard - Avkastningsanalys
    /// </summary>
    private async Task<int> RenderROIDashboard(ExcelWorksheet worksheet, int startRow, List<Dictionary<string, object>> data)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = "💎 ROI DASHBOARD - Avkastningsanalys";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        var roiData = data?.Any() == true ? data : GetSampleROIData();
        var dataStartRow = currentRow;

        // Headers
        worksheet.Cells[currentRow, 1].Value = "Investering";
        worksheet.Cells[currentRow, 2].Value = "Kostnad";
        worksheet.Cells[currentRow, 3].Value = "Avkastning";
        worksheet.Cells[currentRow, 4].Value = "ROI %";
        worksheet.Cells[currentRow, 5].Value = "Payback (månader)";
        currentRow++;

        foreach (var item in roiData)
        {
            var cost = Convert.ToDouble(item.GetValueOrDefault("cost", 0));
            var returns = Convert.ToDouble(item.GetValueOrDefault("returns", 0));
            var roi = cost != 0 ? (returns - cost) / cost : 0;
            var payback = Convert.ToDouble(item.GetValueOrDefault("payback", 0));

            worksheet.Cells[currentRow, 1].Value = item.GetValueOrDefault("investment", "").ToString();
            worksheet.Cells[currentRow, 2].Value = cost;
            worksheet.Cells[currentRow, 3].Value = returns;
            worksheet.Cells[currentRow, 4].Value = roi;
            worksheet.Cells[currentRow, 5].Value = payback;

            // Färgkoda ROI
            var roiColor = roi >= 0.15 ? System.Drawing.Color.Green :
                          roi >= 0 ? System.Drawing.Color.Orange : System.Drawing.Color.Red;
            worksheet.Cells[currentRow, 4].Style.Font.Color.SetColor(roiColor);
            worksheet.Cells[currentRow, 4].Style.Numberformat.Format = "0.0%";

            currentRow++;
        }

        // ROI diagram
        var chart = worksheet.Drawings.AddChart($"ROIChart_{Guid.NewGuid():N}", eChartType.ColumnClustered);
        chart.Title.Text = "ROI Jämförelse";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(600, 400);

        var series = chart.Series.Add(
            worksheet.Cells[dataStartRow + 1, 4, dataStartRow + roiData.Count, 4],
            worksheet.Cells[dataStartRow + 1, 1, dataStartRow + roiData.Count, 1]);
        series.Header = "ROI %";

        await Task.CompletedTask;
        return currentRow - startRow + 25;
    }

    /// <summary>
    /// 📈 Financial Ratios - Nyckeltal dashboard
    /// </summary>
    private async Task<int> RenderFinancialRatios(ExcelWorksheet worksheet, int startRow, List<Dictionary<string, object>> data)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = "📈 FINANCIAL RATIOS - Nyckeltal";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        // Beräkna nyckeltal från data eller använd sample
        var ratios = new[]
        {
            ("Likviditet (Current Ratio)", 2.1, "Bra", "> 2.0"),
            ("Skuldsättningsgrad", 0.35, "Bra", "< 0.5"),
            ("Räntabilitet på eget kapital", 0.18, "Utmärkt", "> 0.15"),
            ("Räntabilitet på totalt kapital", 0.12, "Bra", "> 0.10"),
            ("Omsättningshastighet", 4.2, "Bra", "> 3.0"),
            ("Vinstmarginal", 0.08, "Acceptabel", "> 0.05")
        };

        // Headers
        worksheet.Cells[currentRow, 1].Value = "Nyckeltal";
        worksheet.Cells[currentRow, 2].Value = "Värde";
        worksheet.Cells[currentRow, 3].Value = "Bedömning";
        worksheet.Cells[currentRow, 4].Value = "Målvärde";
        currentRow++;

        foreach (var (ratio, value, assessment, target) in ratios)
        {
            worksheet.Cells[currentRow, 1].Value = ratio;
            worksheet.Cells[currentRow, 2].Value = value;
            worksheet.Cells[currentRow, 3].Value = assessment;
            worksheet.Cells[currentRow, 4].Value = target;

            // Färgkoda bedömning
            var assessmentColor = assessment switch
            {
                "Utmärkt" => System.Drawing.Color.Green,
                "Bra" => System.Drawing.Color.Blue,
                "Acceptabel" => System.Drawing.Color.Orange,
                _ => System.Drawing.Color.Red
            };
            worksheet.Cells[currentRow, 3].Style.Font.Color.SetColor(assessmentColor);
            worksheet.Cells[currentRow, 3].Style.Font.Bold = true;

            currentRow++;
        }

        await Task.CompletedTask;
        return currentRow - startRow + 5;
    }

    /// <summary>
    /// 📊 Variance Analysis - Avvikelseanalys
    /// </summary>
    private async Task<int> RenderVarianceAnalysis(ExcelWorksheet worksheet, int startRow, List<Dictionary<string, object>> data)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = "📊 VARIANCE ANALYSIS - Avvikelseanalys";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        var varianceData = data?.Any() == true ? data : GetSampleVarianceData();
        var dataStartRow = currentRow;

        // Headers
        worksheet.Cells[currentRow, 1].Value = "KPI";
        worksheet.Cells[currentRow, 2].Value = "Föregående";
        worksheet.Cells[currentRow, 3].Value = "Aktuell";
        worksheet.Cells[currentRow, 4].Value = "Avvikelse";
        worksheet.Cells[currentRow, 5].Value = "Trend";
        currentRow++;

        foreach (var item in varianceData)
        {
            var previous = Convert.ToDouble(item.TryGetValue("previous", out var prevVal) ? prevVal : 0);
            var current = Convert.ToDouble(item.TryGetValue("current", out var currVal) ? currVal : 0);
            var variance = current - previous;
            var trend = variance >= 0 ? "↗️" : "↘️";

            worksheet.Cells[currentRow, 1].Value = item.TryGetValue("kpi", out var kpiVal) ? kpiVal?.ToString() ?? "" : "";
            worksheet.Cells[currentRow, 2].Value = previous;
            worksheet.Cells[currentRow, 3].Value = current;
            worksheet.Cells[currentRow, 4].Value = variance;
            worksheet.Cells[currentRow, 5].Value = trend;

            // Färgkoda avvikelse
            var varianceColor = variance >= 0 ? System.Drawing.Color.Green : System.Drawing.Color.Red;
            worksheet.Cells[currentRow, 4].Style.Font.Color.SetColor(varianceColor);
            worksheet.Cells[currentRow, 4].Style.Font.Bold = true;

            currentRow++;
        }

        await Task.CompletedTask;
        return currentRow - startRow + 5;
    }

    /// <summary>
    /// 📈 Trend Analysis - Trendprojektioner
    /// </summary>
    private async Task<int> RenderTrendAnalysis(ExcelWorksheet worksheet, int startRow, List<Dictionary<string, object>> data)
    {
        var currentRow = startRow;

        worksheet.Cells[currentRow, 1].Value = "📈 TREND ANALYSIS - Trendprojektioner";
        worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow += 2;

        var trendData = data?.Any() == true ? data : GetSampleTrendData();
        var dataStartRow = currentRow;

        // Headers
        worksheet.Cells[currentRow, 1].Value = "Period";
        worksheet.Cells[currentRow, 2].Value = "Historisk";
        worksheet.Cells[currentRow, 3].Value = "Prognos";
        worksheet.Cells[currentRow, 4].Value = "Trend";
        currentRow++;

        foreach (var item in trendData)
        {
            worksheet.Cells[currentRow, 1].Value = item.TryGetValue("period", out var periodVal) ? periodVal?.ToString() ?? "" : "";
            worksheet.Cells[currentRow, 2].Value = item.TryGetValue("historical", out var histVal) ? histVal : null;
            worksheet.Cells[currentRow, 3].Value = item.TryGetValue("forecast", out var forecastVal) ? forecastVal : null;
            worksheet.Cells[currentRow, 4].Value = item.TryGetValue("trend", out var trendVal) ? trendVal?.ToString() ?? "" : "";
            currentRow++;
        }

        // Trend diagram
        var chart = worksheet.Drawings.AddChart($"TrendChart_{Guid.NewGuid():N}", eChartType.LineMarkers);
        chart.Title.Text = "Trendanalys och Prognos";
        chart.SetPosition(currentRow + 1, 0, 1, 0);
        chart.SetSize(700, 400);

        // Historisk data
        var historicalSeries = chart.Series.Add(
            worksheet.Cells[dataStartRow + 1, 2, dataStartRow + trendData.Count, 2],
            worksheet.Cells[dataStartRow + 1, 1, dataStartRow + trendData.Count, 1]);
        historicalSeries.Header = "Historisk data";

        // Prognos data
        var forecastSeries = chart.Series.Add(
            worksheet.Cells[dataStartRow + 1, 3, dataStartRow + trendData.Count, 3],
            worksheet.Cells[dataStartRow + 1, 1, dataStartRow + trendData.Count, 1]);
        forecastSeries.Header = "Prognos";

        await Task.CompletedTask;
        return currentRow - startRow + 25;
    }

    private List<Dictionary<string, object>> GetSampleROIData()
    {
        return new List<Dictionary<string, object>>
        {
            new() { ["investment"] = "CRM System", ["cost"] = 150000, ["returns"] = 220000, ["payback"] = 8 },
            new() { ["investment"] = "Marknadsföring", ["cost"] = 80000, ["returns"] = 140000, ["payback"] = 6 },
            new() { ["investment"] = "Utbildning", ["cost"] = 45000, ["returns"] = 75000, ["payback"] = 12 },
            new() { ["investment"] = "Utrustning", ["cost"] = 120000, ["returns"] = 160000, ["payback"] = 18 }
        };
    }

    private List<Dictionary<string, object>> GetSampleVarianceData()
    {
        return new List<Dictionary<string, object>>
        {
            new() { ["kpi"] = "Omsättning", ["previous"] = 450000, ["current"] = 485000 },
            new() { ["kpi"] = "Bruttomarginal", ["previous"] = 0.35, ["current"] = 0.38 },
            new() { ["kpi"] = "Personalkostnader", ["previous"] = 180000, ["current"] = 175000 },
            new() { ["kpi"] = "Kundnöjdhet", ["previous"] = 4.2, ["current"] = 4.5 }
        };
    }

    private List<Dictionary<string, object>> GetSampleTrendData()
    {
        return new List<Dictionary<string, object>>
        {
            new() { ["period"] = "2022 Q1", ["historical"] = 380000, ["trend"] = "Historisk" },
            new() { ["period"] = "2022 Q2", ["historical"] = 420000, ["trend"] = "Historisk" },
            new() { ["period"] = "2022 Q3", ["historical"] = 465000, ["trend"] = "Historisk" },
            new() { ["period"] = "2022 Q4", ["historical"] = 510000, ["trend"] = "Historisk" },
            new() { ["period"] = "2023 Q1", ["forecast"] = 545000, ["trend"] = "Prognos" },
            new() { ["period"] = "2023 Q2", ["forecast"] = 580000, ["trend"] = "Prognos" }
        };
    }
}
