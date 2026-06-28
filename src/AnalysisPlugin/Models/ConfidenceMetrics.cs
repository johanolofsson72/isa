namespace AnalysisPlugin.Models;

/// <summary>
/// Confidence metrics data structure for E2E test compatibility
/// </summary>
public class ConfidenceMetrics
{
    /// <summary>
    /// Overall confidence level
    /// </summary>
    public decimal Overall_Confidence { get; set; }

    /// <summary>
    /// Data quality score
    /// </summary>
    public decimal Data_Quality { get; set; }

    /// <summary>
    /// Model stability score
    /// </summary>
    public decimal Model_Stability { get; set; }

    /// <summary>
    /// Hybrid confidence score
    /// </summary>
    public decimal HybridConfidence { get; set; }

    /// <summary>
    /// Creates ConfidenceMetrics from Dictionary
    /// </summary>
    /// <param name="metrics">Dictionary of confidence metrics</param>
    /// <returns>ConfidenceMetrics object</returns>
    public static ConfidenceMetrics FromDictionary(Dictionary<string, decimal> metrics)
    {
        return new ConfidenceMetrics
        {
            Overall_Confidence = metrics.GetValueOrDefault("Overall_Confidence", 0),
            Data_Quality = metrics.GetValueOrDefault("Data_Quality", 0),
            Model_Stability = metrics.GetValueOrDefault("Model_Stability", 0),
            HybridConfidence = metrics.GetValueOrDefault("HybridConfidence", 0)
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
            ["Overall_Confidence"] = Overall_Confidence,
            ["Data_Quality"] = Data_Quality,
            ["Model_Stability"] = Model_Stability,
            ["HybridConfidence"] = HybridConfidence
        };
    }
}
