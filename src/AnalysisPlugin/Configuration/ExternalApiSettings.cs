namespace AnalysisPlugin.Configuration;

/// <summary>
/// 🔧 Konfiguration för externa API:er
/// </summary>
public class ExternalApiSettings
{
    public const string SectionName = "ExternalAPIs";

    public OpenAISettings OpenAI { get; set; } = new();
    public SCBSettings SCB { get; set; } = new();
    public RiksbankSettings Riksbank { get; set; } = new();
    public YahooFinanceSettings YahooFinance { get; set; } = new();
    public GeneralSettings General { get; set; } = new();
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4";
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
    public bool Enabled { get; set; } = false;
}

public class SCBSettings
{
    public string BaseUrl { get; set; } = "https://api.scb.se/OV0104/v1/doris/sv/ssd";
    public int TimeoutSeconds { get; set; } = 30;
    public bool Enabled { get; set; } = true;
    public string[] DefaultIndicators { get; set; } = new[]
    {
        "START/PR/PR0101/PR0101E/BasbeloppTidsserie", // Basbelopp
        "START/NR/NR0103/NR0103A/NR0103ENS2010T03Ar", // BNP
        "START/AM/AM0207/AM0207A/AM0207C02" // Arbetslöshet
    };
}

public class RiksbankSettings
{
    public string BaseUrl { get; set; } = "https://api.riksbank.se/swea/v1";
    public int TimeoutSeconds { get; set; } = 30;
    public bool Enabled { get; set; } = true;
    public string[] DefaultSeries { get; set; } = new[]
    {
        "SEKEURPMI", // SEK/EUR
        "SEKUSDPMI", // SEK/USD
        "REPORÄNTA" // Reporänta
    };
}

public class YahooFinanceSettings
{
    public string BaseUrl { get; set; } = "https://query1.finance.yahoo.com/v8/finance";
    public int TimeoutSeconds { get; set; } = 30;
    public bool Enabled { get; set; } = true;
    public string[] DefaultSymbols { get; set; } = new[]
    {
        "^OMX", // OMX Stockholm
        "OMXSPI.ST", // OMX Stockholm PI
        "SEK=X" // SEK/USD
    };
}

public class GeneralSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public int MaxRetryAttempts { get; set; } = 3;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerTimeoutMinutes { get; set; } = 5;
    public bool EnableFallbackData { get; set; } = true;
    public bool LogApiCalls { get; set; } = true;
}

/// <summary>
/// 🔐 API-nycklar och känslig konfiguration (läses från miljövariabler)
/// </summary>
public static class ApiKeys
{
    public static string OpenAI => Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
    public static string AlphaVantage => Environment.GetEnvironmentVariable("ALPHAVANTAGE_API_KEY") ?? "";
    
    public static bool HasOpenAIKey => !string.IsNullOrEmpty(OpenAI);
    public static bool HasAlphaVantageKey => !string.IsNullOrEmpty(AlphaVantage);
}
