using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using AnalysisPlugin.Models;
using System.Text.Json;

namespace AnalysisPlugin.Services;

/// <summary>
/// Natural language processing service with Microsoft.SemanticKernel integration for financial analysis
/// </summary>
public class NaturalLanguageService
{
    private readonly ILogger<NaturalLanguageService> _logger;
    private readonly Kernel _kernel;

    public NaturalLanguageService(ILogger<NaturalLanguageService> logger)
    {
        _logger = logger;
        
        // Initialize Semantic Kernel (in production, this would be configured with actual AI models)
        var builder = Kernel.CreateBuilder();
        // Note: In production, you would add actual AI services here:
        // builder.AddOpenAIChatCompletion("gpt-4", "your-api-key");
        // builder.AddAzureOpenAIChatCompletion("deployment", "endpoint", "api-key");
        
        _kernel = builder.Build();
    }

    /// <summary>
    /// Generates natural language insights from financial data analysis
    /// </summary>
    /// <param name="analysisResult">ML analysis result to interpret</param>
    /// <param name="language">Output language (sv/en)</param>
    /// <returns>Natural language insights</returns>
    public async Task<string> GenerateInsightsAsync(MLAnalysisResult analysisResult, string language = "sv")
    {
        try
        {
            _logger.LogInformation("Generating natural language insights for {AnalysisType} in {Language}", 
                analysisResult.AnalysisType, language);

            // Stub implementation - in production would use actual AI models
            var insights = analysisResult.AnalysisType.ToLowerInvariant() switch
            {
                "trend_analysis" => await GenerateTrendInsights(analysisResult, language),
                "forecast" => await GenerateForecastInsights(analysisResult, language),
                "anomaly_detection" => await GenerateAnomalyInsights(analysisResult, language),
                _ => await GenerateGenericInsights(analysisResult, language)
            };

            _logger.LogDebug("Generated insights: {InsightsLength} characters", insights.Length);
            return insights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate natural language insights");
            return language == "sv" 
                ? "Kunde inte generera insikter från analysresultatet."
                : "Could not generate insights from the analysis result.";
        }
    }

    /// <summary>
    /// Interprets financial metrics and provides business context
    /// </summary>
    /// <param name="metrics">Financial metrics to interpret</param>
    /// <param name="language">Output language</param>
    /// <returns>Business interpretation</returns>
    public async Task<string> InterpretMetricsAsync(Dictionary<string, decimal> metrics, string language = "sv")
    {
        try
        {
            _logger.LogInformation("Interpreting {MetricCount} financial metrics", metrics.Count);

            var interpretations = new List<string>();

            foreach (var metric in metrics)
            {
                var interpretation = await InterpretSingleMetric(metric.Key, metric.Value, language);
                if (!string.IsNullOrEmpty(interpretation))
                {
                    interpretations.Add(interpretation);
                }
            }

            var result = language == "sv" 
                ? string.Join(" ", interpretations)
                : string.Join(" ", interpretations);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to interpret financial metrics");
            return language == "sv" 
                ? "Kunde inte tolka de finansiella måtten."
                : "Could not interpret the financial metrics.";
        }
    }

    /// <summary>
    /// Generates executive summary from multiple analysis results
    /// </summary>
    /// <param name="analysisResults">Multiple analysis results</param>
    /// <param name="language">Output language</param>
    /// <returns>Executive summary</returns>
    public async Task<string> GenerateExecutiveSummaryAsync(List<MLAnalysisResult> analysisResults, string language = "sv")
    {
        try
        {
            _logger.LogInformation("Generating executive summary from {ResultCount} analysis results", analysisResults.Count);

            // Stub implementation - would use AI to synthesize insights
            var summaryParts = new List<string>();

            if (language == "sv")
            {
                summaryParts.Add("## Sammanfattning");
                summaryParts.Add($"Denna rapport baseras på analys av {analysisResults.Count} olika finansiella aspekter.");

                foreach (var result in analysisResults)
                {
                    var insight = await GenerateInsightsAsync(result, language);
                    summaryParts.Add($"**{GetSwedishAnalysisTypeName(result.AnalysisType)}**: {insight}");
                }

                summaryParts.Add("Rekommendationer kommer att utvecklas baserat på dessa insikter.");
            }
            else
            {
                summaryParts.Add("## Executive Summary");
                summaryParts.Add($"This report is based on analysis of {analysisResults.Count} different financial aspects.");

                foreach (var result in analysisResults)
                {
                    var insight = await GenerateInsightsAsync(result, language);
                    summaryParts.Add($"**{result.AnalysisType}**: {insight}");
                }

                summaryParts.Add("Recommendations will be developed based on these insights.");
            }

            return string.Join("\n\n", summaryParts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate executive summary");
            return language == "sv" 
                ? "Kunde inte generera sammanfattning."
                : "Could not generate executive summary.";
        }
    }

    private async Task<string> GenerateTrendInsights(MLAnalysisResult result, string language)
    {
        await Task.CompletedTask; // Simulate async processing

        if (language == "sv")
        {
            var growthRate = result.Metrics.GetValueOrDefault("revenue_growth_rate", 0);
            return growthRate > 10 
                ? $"Stark tillväxttrend observerad med {growthRate:F1}% ökning. Detta indikerar en positiv affärsutveckling."
                : $"Måttlig tillväxt på {growthRate:F1}%. Överväg strategier för att accelerera tillväxten.";
        }
        else
        {
            var growthRate = result.Metrics.GetValueOrDefault("revenue_growth_rate", 0);
            return growthRate > 10 
                ? $"Strong growth trend observed with {growthRate:F1}% increase. This indicates positive business development."
                : $"Moderate growth at {growthRate:F1}%. Consider strategies to accelerate growth.";
        }
    }

    private async Task<string> GenerateForecastInsights(MLAnalysisResult result, string language)
    {
        await Task.CompletedTask;

        var forecastPoints = result.Forecast.Count;
        if (language == "sv")
        {
            return $"Prognosen visar utveckling över {forecastPoints} perioder med {result.ConfidenceLevel:P0} säkerhet. " +
                   "Trenden indikerar fortsatt tillväxt med säsongsvariation.";
        }
        else
        {
            return $"Forecast shows development over {forecastPoints} periods with {result.ConfidenceLevel:P0} confidence. " +
                   "The trend indicates continued growth with seasonal variation.";
        }
    }

    private async Task<string> GenerateAnomalyInsights(MLAnalysisResult result, string language)
    {
        await Task.CompletedTask;

        var anomalyCount = result.Anomalies.Count;
        if (language == "sv")
        {
            return anomalyCount > 0 
                ? $"Identifierade {anomalyCount} avvikelser som kräver uppmärksamhet. Granska dessa transaktioner för potentiella fel eller ovanliga händelser."
                : "Inga betydande avvikelser upptäcktes. Finansiella mönster följer förväntade trender.";
        }
        else
        {
            return anomalyCount > 0 
                ? $"Identified {anomalyCount} anomalies requiring attention. Review these transactions for potential errors or unusual events."
                : "No significant anomalies detected. Financial patterns follow expected trends.";
        }
    }

    private async Task<string> GenerateGenericInsights(MLAnalysisResult result, string language)
    {
        await Task.CompletedTask;

        return language == "sv" 
            ? $"Analys av typ {result.AnalysisType} slutförd med {result.ConfidenceLevel:P0} säkerhet."
            : $"Analysis of type {result.AnalysisType} completed with {result.ConfidenceLevel:P0} confidence.";
    }

    private async Task<string> InterpretSingleMetric(string metricName, decimal value, string language)
    {
        await Task.CompletedTask;

        return metricName.ToLowerInvariant() switch
        {
            "revenue_growth_rate" when language == "sv" => $"Intäktstillväxt: {value:F1}%",
            "revenue_growth_rate" => $"Revenue growth: {value:F1}%",
            "profit_margin" when language == "sv" => $"Vinstmarginal: {value:F1}%",
            "profit_margin" => $"Profit margin: {value:F1}%",
            _ => ""
        };
    }

    private string GetSwedishAnalysisTypeName(string analysisType)
    {
        return analysisType.ToLowerInvariant() switch
        {
            "trend_analysis" => "Trendanalys",
            "forecast" => "Prognos",
            "anomaly_detection" => "Avvikelsedetektering",
            _ => analysisType
        };
    }

    /// <summary>
    /// Generates executive summary from financial transactions
    /// </summary>
    /// <param name="transactions">Financial transactions</param>
    /// <param name="language">Output language (sv/en)</param>
    /// <returns>Executive summary</returns>
    public async Task<string> GenerateExecutiveSummaryAsync(List<FinancialTransaction> transactions, string language = "sv")
    {
        try
        {
            _logger.LogInformation("Generating executive summary for {Count} transactions in {Language}",
                transactions.Count, language);

            // Calculate key metrics
            var totalRevenue = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
            var totalExpenses = transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
            var netResult = totalRevenue - totalExpenses;
            var transactionCount = transactions.Count;

            // Generate summary based on language
            var summary = language.ToLower() switch
            {
                "sv" => GenerateSwedishExecutiveSummary(totalRevenue, totalExpenses, netResult, transactionCount),
                "en" => GenerateEnglishExecutiveSummary(totalRevenue, totalExpenses, netResult, transactionCount),
                _ => GenerateSwedishExecutiveSummary(totalRevenue, totalExpenses, netResult, transactionCount)
            };

            return await Task.FromResult(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate executive summary");
            return language.ToLower() == "sv" ?
                "Kunde inte generera sammanfattning på grund av ett fel." :
                "Could not generate summary due to an error.";
        }
    }

    /// <summary>
    /// Generates Swedish executive summary
    /// </summary>
    private string GenerateSwedishExecutiveSummary(decimal revenue, decimal expenses, decimal netResult, int transactionCount)
    {
        var profitability = netResult >= 0 ? "lönsam" : "förlustbringande";
        var trend = netResult > 0 ? "positiv utveckling" : "negativ utveckling";

        return $@"Sammanfattning av Finansiell Prestanda

Översikt:
Denna rapport analyserar {transactionCount:N0} finansiella transaktioner och visar en {profitability} verksamhet med {trend}.

Nyckeltal:
• Totala intäkter: {revenue:N0} SEK
• Totala kostnader: {expenses:N0} SEK
• Nettoresultat: {netResult:N0} SEK
• Vinstmarginal: {(netResult / Math.Max(revenue, 1) * 100):F1}%

Analys:
Verksamheten visar {(netResult >= 0 ? "en stark finansiell position med positiva kassaflöden" : "utmaningar med negativa kassaflöden som kräver uppmärksamhet")}.

Rekommendationer:
{(netResult >= 0 ?
    "• Fortsätt fokusera på lönsamhet\n• Överväg investeringar för tillväxt\n• Behåll kostnadskontroll" :
    "• Granska och minska onödiga kostnader\n• Fokusera på intäktsökning\n• Implementera striktare budgetkontroll")}";
    }

    /// <summary>
    /// Generates English executive summary
    /// </summary>
    private string GenerateEnglishExecutiveSummary(decimal revenue, decimal expenses, decimal netResult, int transactionCount)
    {
        var profitability = netResult >= 0 ? "profitable" : "loss-making";
        var trend = netResult > 0 ? "positive development" : "negative development";

        return $@"Financial Performance Summary

Overview:
This report analyzes {transactionCount:N0} financial transactions showing a {profitability} operation with {trend}.

Key Metrics:
• Total Revenue: {revenue:N0} SEK
• Total Expenses: {expenses:N0} SEK
• Net Result: {netResult:N0} SEK
• Profit Margin: {(netResult / Math.Max(revenue, 1) * 100):F1}%

Analysis:
The business shows {(netResult >= 0 ? "a strong financial position with positive cash flows" : "challenges with negative cash flows requiring attention")}.

Recommendations:
{(netResult >= 0 ?
    "• Continue focusing on profitability\n• Consider investments for growth\n• Maintain cost control" :
    "• Review and reduce unnecessary costs\n• Focus on revenue increase\n• Implement stricter budget control")}";
    }
}
