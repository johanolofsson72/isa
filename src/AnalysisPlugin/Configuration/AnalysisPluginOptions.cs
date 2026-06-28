using System.ComponentModel.DataAnnotations;

namespace AnalysisPlugin.Configuration;

/// <summary>
/// Configuration options for the AnalysisPlugin
/// </summary>
public class AnalysisPluginOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "AnalysisPlugin";

    /// <summary>
    /// Directory path for storing generated reports
    /// </summary>
    [Required]
    public string ReportArchiveDirectory { get; set; } = "reportarchive";

    /// <summary>
    /// Default number of years of historical data to include in reports
    /// </summary>
    [Range(1, 10)]
    public int DefaultHistoricalYears { get; set; } = 5;

    /// <summary>
    /// Timeout for simple report generation (60 seconds) per FR-022
    /// </summary>
    [Range(30, 600)]
    public int SimpleReportTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout for complex report generation (180 seconds) per FR-022
    /// </summary>
    [Range(60, 1200)]
    public int ComplexReportTimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// Timeout for historical report generation (300 seconds) per FR-022
    /// </summary>
    [Range(120, 1800)]
    public int HistoricalReportTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Timeout for ML model training and analysis (420 seconds) per FR-022
    /// </summary>
    [Range(60, 1800)]
    public int MLAnalysisTimeoutSeconds { get; set; } = 420;

    /// <summary>
    /// Report retention period in years (Swedish law compliance minimum: 7 years)
    /// </summary>
    [Range(7, 50)]
    public int ReportRetentionYears { get; set; } = 7;

    /// <summary>
    /// Default scheduling interval for automated reports
    /// </summary>
    public string DefaultSchedulingInterval { get; set; } = "weekly";

    /// <summary>
    /// ML model training timeout in seconds
    /// </summary>
    [Range(30, 300)]
    public int MLTrainingTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// ML prediction timeout per series in seconds
    /// </summary>
    [Range(5, 60)]
    public int MLPredictionTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Default ML retraining threshold percentage
    /// </summary>
    [Range(1, 20)]
    public double MLRetrainingThresholdPercent { get; set; } = 5.0;

    /// <summary>
    /// Default forecast horizon in months
    /// </summary>
    [Range(1, 12)]
    public int DefaultForecastHorizonMonths { get; set; } = 3;

    /// <summary>
    /// Default confidence level for ML predictions
    /// </summary>
    [Range(0.8, 0.99)]
    public double DefaultConfidenceLevel { get; set; } = 0.95;

    /// <summary>
    /// SQLite database path for ML models and forecast metadata
    /// </summary>
    public string AnalysisDatabasePath { get; set; } = "analysis.db";

    /// <summary>
    /// SQLite database connection string for ML models and forecast metadata
    /// </summary>
    public string DatabaseConnectionString => $"Data Source={AnalysisDatabasePath}";

    /// <summary>
    /// GDPR-compliant data retention period for ML metadata in days
    /// </summary>
    [Range(30, 3650)]
    public int MLMetadataRetentionDays { get; set; } = 2555; // 7 years

    /// <summary>
    /// Exchange rate cache duration in hours
    /// </summary>
    [Range(1, 48)]
    public int ExchangeRateCacheHours { get; set; } = 24;

    /// <summary>
    /// Primary exchange rate provider (Riksbank, ECB)
    /// </summary>
    public string PrimaryExchangeRateProvider { get; set; } = "Riksbank";

    /// <summary>
    /// Fallback exchange rate provider
    /// </summary>
    public string FallbackExchangeRateProvider { get; set; } = "ECB";

    /// <summary>
    /// Email delivery settings for multi-sheet reports
    /// </summary>
    public EmailSettings? EmailSettings { get; set; }
}

/// <summary>
/// Email delivery configuration settings
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// SMTP server hostname
    /// </summary>
    [Required]
    public string SmtpHost { get; set; } = "localhost";

    /// <summary>
    /// SMTP server port
    /// </summary>
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Enable SSL/TLS encryption
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// SMTP authentication username
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// SMTP authentication password
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// From email address
    /// </summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "noreply@isa.accounting";

    /// <summary>
    /// From display name
    /// </summary>
    public string FromName { get; set; } = "ISA Accounting";

    /// <summary>
    /// Default email subject template
    /// </summary>
    public string DefaultSubjectTemplate { get; set; } = "Financial Report - {ReportTitle} ({Date})";

    /// <summary>
    /// Maximum attachment size in MB
    /// </summary>
    [Range(1, 100)]
    public int MaxAttachmentSizeMB { get; set; } = 25;

    /// <summary>
    /// Default link expiration time in hours
    /// </summary>
    [Range(1, 168)] // 1 hour to 1 week
    public int DefaultLinkExpirationHours { get; set; } = 72;

    /// <summary>
    /// Enable email delivery by default
    /// </summary>
    public bool EnableEmailDeliveryByDefault { get; set; } = false;

    /// <summary>
    /// Default recipients for automated reports
    /// </summary>
    public List<string> DefaultRecipients { get; set; } = new();

    /// <summary>
    /// Email template directory path
    /// </summary>
    public string EmailTemplateDirectory { get; set; } = "EmailTemplates";

    /// <summary>
    /// Enable email delivery logging
    /// </summary>
    public bool EnableDeliveryLogging { get; set; } = true;
}
