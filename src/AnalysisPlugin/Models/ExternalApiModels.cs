using System.Text.Json.Serialization;

namespace AnalysisPlugin.Models;

/// <summary>
/// 🌐 Externa API-modeller för finansiell analys
/// </summary>

// SCB (Statistiska Centralbyrån) API Models
public class SCBEconomicData
{
    [JsonPropertyName("data")]
    public SCBDataPoint[] Data { get; set; } = Array.Empty<SCBDataPoint>();
    
    [JsonPropertyName("columns")]
    public SCBColumn[] Columns { get; set; } = Array.Empty<SCBColumn>();
}

public class SCBDataPoint
{
    [JsonPropertyName("key")]
    public string[] Key { get; set; } = Array.Empty<string>();
    
    [JsonPropertyName("values")]
    public string[] Values { get; set; } = Array.Empty<string>();
}

public class SCBColumn
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

// Riksbanken API Models
public class RiksbankData
{
    [JsonPropertyName("observations")]
    public RiksbankObservation[] Observations { get; set; } = Array.Empty<RiksbankObservation>();
}

public class RiksbankObservation
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";
    
    [JsonPropertyName("value")]
    public decimal Value { get; set; }
    
    [JsonPropertyName("seriesname")]
    public string SeriesName { get; set; } = "";
}

// OpenAI API Models
public class OpenAIRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4";
    
    [JsonPropertyName("messages")]
    public OpenAIMessage[] Messages { get; set; } = Array.Empty<OpenAIMessage>();
    
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1000;
    
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;
}

public class OpenAIMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class OpenAIResponse
{
    [JsonPropertyName("choices")]
    public OpenAIChoice[] Choices { get; set; } = Array.Empty<OpenAIChoice>();
}

public class OpenAIChoice
{
    [JsonPropertyName("message")]
    public OpenAIMessage Message { get; set; } = new();
}

// Yahoo Finance API Models
public class YahooFinanceData
{
    [JsonPropertyName("chart")]
    public YahooChart Chart { get; set; } = new();
}

public class YahooChart
{
    [JsonPropertyName("result")]
    public YahooResult[] Result { get; set; } = Array.Empty<YahooResult>();
}

public class YahooResult
{
    [JsonPropertyName("meta")]
    public YahooMeta Meta { get; set; } = new();
    
    [JsonPropertyName("indicators")]
    public YahooIndicators Indicators { get; set; } = new();
}

public class YahooMeta
{
    [JsonPropertyName("regularMarketPrice")]
    public decimal RegularMarketPrice { get; set; }
    
    [JsonPropertyName("previousClose")]
    public decimal PreviousClose { get; set; }
}

public class YahooIndicators
{
    [JsonPropertyName("quote")]
    public YahooQuote[] Quote { get; set; } = Array.Empty<YahooQuote>();
}

public class YahooQuote
{
    [JsonPropertyName("close")]
    public decimal[] Close { get; set; } = Array.Empty<decimal>();
}

// Sammansatt analysmodell
public class ExternalMarketAnalysis
{
    public SCBEconomicData SwedishEconomicData { get; set; } = new();
    public RiksbankData InterestRates { get; set; } = new();
    public YahooFinanceData MarketData { get; set; } = new();
    public string AIInsights { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public bool IsDataComplete { get; set; }
    public string[] DataSources { get; set; } = Array.Empty<string>();
}
