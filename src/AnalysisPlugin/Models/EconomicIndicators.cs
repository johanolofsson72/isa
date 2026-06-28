namespace AnalysisPlugin.Models;

/// <summary>
/// Economic indicators data structure for E2E test compatibility
/// </summary>
public class EconomicIndicators
{
    /// <summary>
    /// GDP growth rate as percentage
    /// </summary>
    public decimal GDP_GrowthRate { get; set; }

    /// <summary>
    /// Inflation rate as percentage
    /// </summary>
    public decimal InflationRate { get; set; }

    /// <summary>
    /// Unemployment rate as percentage
    /// </summary>
    public decimal UnemploymentRate { get; set; }

    /// <summary>
    /// Interest rate as percentage
    /// </summary>
    public decimal InterestRate { get; set; }

    /// <summary>
    /// SEK/EUR exchange rate
    /// </summary>
    public decimal ExchangeRate_SEK_EUR { get; set; }

    /// <summary>
    /// SEK/USD exchange rate
    /// </summary>
    public decimal ExchangeRate_SEK_USD { get; set; }

    /// <summary>
    /// Consumer confidence index
    /// </summary>
    public decimal ConsumerConfidenceIndex { get; set; }

    /// <summary>
    /// Industrial production index
    /// </summary>
    public decimal IndustrialProductionIndex { get; set; }

    /// <summary>
    /// Data source indicator (1 = Alpha Vantage, 2 = Fallback)
    /// </summary>
    public decimal Source { get; set; }

    /// <summary>
    /// Creates EconomicIndicators from Dictionary
    /// </summary>
    /// <param name="indicators">Dictionary of economic indicators</param>
    /// <returns>EconomicIndicators object</returns>
    public static EconomicIndicators FromDictionary(Dictionary<string, decimal> indicators)
    {
        return new EconomicIndicators
        {
            GDP_GrowthRate = indicators.GetValueOrDefault("GDP_GrowthRate", indicators.GetValueOrDefault("gdp_growth", 0)),
            InflationRate = indicators.GetValueOrDefault("InflationRate", indicators.GetValueOrDefault("inflation_rate", 0)),
            UnemploymentRate = indicators.GetValueOrDefault("UnemploymentRate", indicators.GetValueOrDefault("unemployment_rate", 0)),
            InterestRate = indicators.GetValueOrDefault("InterestRate", indicators.GetValueOrDefault("interest_rate", 0)),
            ExchangeRate_SEK_EUR = indicators.GetValueOrDefault("ExchangeRate_SEK_EUR", indicators.GetValueOrDefault("currency_strength", 0)),
            ExchangeRate_SEK_USD = indicators.GetValueOrDefault("ExchangeRate_SEK_USD", 0),
            ConsumerConfidenceIndex = indicators.GetValueOrDefault("ConsumerConfidenceIndex", 0),
            IndustrialProductionIndex = indicators.GetValueOrDefault("IndustrialProductionIndex", 0),
            Source = indicators.GetValueOrDefault("Source", 1)
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
            ["GDP_GrowthRate"] = GDP_GrowthRate,
            ["gdp_growth"] = GDP_GrowthRate,
            ["InflationRate"] = InflationRate,
            ["inflation_rate"] = InflationRate,
            ["UnemploymentRate"] = UnemploymentRate,
            ["unemployment_rate"] = UnemploymentRate,
            ["InterestRate"] = InterestRate,
            ["interest_rate"] = InterestRate,
            ["ExchangeRate_SEK_EUR"] = ExchangeRate_SEK_EUR,
            ["currency_strength"] = ExchangeRate_SEK_EUR,
            ["ExchangeRate_SEK_USD"] = ExchangeRate_SEK_USD,
            ["ConsumerConfidenceIndex"] = ConsumerConfidenceIndex,
            ["IndustrialProductionIndex"] = IndustrialProductionIndex,
            ["Source"] = Source
        };
    }
}
