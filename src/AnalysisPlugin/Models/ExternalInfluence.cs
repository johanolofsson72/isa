namespace AnalysisPlugin.Models;

/// <summary>
/// External influence factors data structure for E2E test compatibility
/// </summary>
public class ExternalInfluence
{
    /// <summary>
    /// Economic impact factor
    /// </summary>
    public decimal Economic_Impact { get; set; }

    /// <summary>
    /// Market volatility factor
    /// </summary>
    public decimal Market_Volatility { get; set; }

    /// <summary>
    /// Interest rate impact factor
    /// </summary>
    public decimal Interest_Rate_Impact { get; set; }

    /// <summary>
    /// Economic indicator weight
    /// </summary>
    public decimal EconomicIndicatorWeight { get; set; }

    /// <summary>
    /// Market sentiment weight
    /// </summary>
    public decimal MarketSentimentWeight { get; set; }

    /// <summary>
    /// Industry benchmark weight
    /// </summary>
    public decimal IndustryBenchmarkWeight { get; set; }

    /// <summary>
    /// Creates ExternalInfluence from Dictionary
    /// </summary>
    /// <param name="influence">Dictionary of external influence factors</param>
    /// <returns>ExternalInfluence object</returns>
    public static ExternalInfluence FromDictionary(Dictionary<string, decimal> influence)
    {
        return new ExternalInfluence
        {
            Economic_Impact = influence.GetValueOrDefault("Economic_Impact", 0),
            Market_Volatility = influence.GetValueOrDefault("Market_Volatility", 0),
            Interest_Rate_Impact = influence.GetValueOrDefault("Interest_Rate_Impact", 0),
            EconomicIndicatorWeight = influence.GetValueOrDefault("EconomicIndicatorWeight", 0),
            MarketSentimentWeight = influence.GetValueOrDefault("MarketSentimentWeight", 0),
            IndustryBenchmarkWeight = influence.GetValueOrDefault("IndustryBenchmarkWeight", 0)
        };
    }

    /// <summary>
    /// Converts to Dictionary for backward compatibility
    /// </summary>
    /// <returns>Dictionary representation</returns>
    public Dictionary<string, decimal> ToDictionary()
    {
        return new Dictionary<string, decimal>
        {
            ["Economic_Impact"] = Economic_Impact,
            ["Market_Volatility"] = Market_Volatility,
            ["Interest_Rate_Impact"] = Interest_Rate_Impact,
            ["EconomicIndicatorWeight"] = EconomicIndicatorWeight,
            ["MarketSentimentWeight"] = MarketSentimentWeight,
            ["IndustryBenchmarkWeight"] = IndustryBenchmarkWeight
        };
    }
}
