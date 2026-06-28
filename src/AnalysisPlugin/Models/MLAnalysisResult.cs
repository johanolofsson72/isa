namespace AnalysisPlugin.Models;

/// <summary>
/// Contains ML analysis output with forecast and anomaly point classes
/// </summary>
public class MLAnalysisResult
{
    /// <summary>
    /// Type of analysis performed (trend_analysis, forecast, anomaly_detection, etc.)
    /// </summary>
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the analysis was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Key metrics from the analysis
    /// </summary>
    public Dictionary<string, decimal> Metrics { get; set; } = new();

    /// <summary>
    /// Forecast data points with confidence intervals
    /// </summary>
    public List<ForecastPoint> Forecast { get; set; } = new();

    /// <summary>
    /// Detected anomalies in the data
    /// </summary>
    public List<AnomalyPoint> Anomalies { get; set; } = new();

    /// <summary>
    /// Confidence level of the analysis (0.0 to 1.0)
    /// </summary>
    public double ConfidenceLevel { get; set; } = 0.95;

    /// <summary>
    /// Human-readable summary of the analysis
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Model performance metrics (R², MAE, RMSE)
    /// </summary>
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// Hybrid forecast combining multiple models (E2E test compatibility)
    /// </summary>
    public List<ForecastPoint> HybridForecast { get; set; } = new();

    /// <summary>
    /// Confidence metrics for the analysis (E2E test compatibility)
    /// </summary>
    public ConfidenceMetrics ConfidenceMetrics { get; set; } = new();

    /// <summary>
    /// Type of forecast methodology used (E2E test compatibility)
    /// </summary>
    public string ForecastType { get; set; } = "ML_Enhanced";

    /// <summary>
    /// Methodology description (E2E test compatibility)
    /// </summary>
    public string Methodology { get; set; } = "Machine Learning with Economic Indicators";

    /// <summary>
    /// External influence factors (E2E test compatibility)
    /// </summary>
    public ExternalInfluence ExternalInfluence { get; set; } = new();

    /// <summary>
    /// Series name that was analyzed
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single forecast point with confidence intervals
/// </summary>
public class ForecastPoint
{
    /// <summary>
    /// Date of the forecast point
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Predicted value
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Lower bound of confidence interval
    /// </summary>
    public decimal LowerBound { get; set; }

    /// <summary>
    /// Upper bound of confidence interval
    /// </summary>
    public decimal UpperBound { get; set; }

    /// <summary>
    /// Confidence level for this point (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; } = 0.95;
}

/// <summary>
/// Represents a detected anomaly in the financial data
/// </summary>
public class AnomalyPoint
{
    /// <summary>
    /// Date when the anomaly occurred
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Actual value that was flagged as anomalous
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Expected value based on the model
    /// </summary>
    public decimal ExpectedValue { get; set; }

    /// <summary>
    /// Anomaly score (higher = more anomalous)
    /// </summary>
    public double AnomalyScore { get; set; }

    /// <summary>
    /// Human-readable description of the anomaly
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the anomaly
    /// </summary>
    public AnomalySeverity Severity { get; set; } = AnomalySeverity.Medium;

    /// <summary>
    /// Deviation from expected value as a percentage
    /// </summary>
    public double DeviationPercent => ExpectedValue != 0 
        ? Math.Abs((double)(Value - ExpectedValue) / (double)ExpectedValue) * 100 
        : 0;
}

/// <summary>
/// Severity levels for anomalies
/// </summary>
public enum AnomalySeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
