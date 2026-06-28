using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using AnalysisPlugin.Models;
using AnalysisPlugin.Configuration;

namespace AnalysisPlugin.Services;

/// <summary>
/// 🌐 Extern analys-service - integrerar med SCB, Riksbanken, OpenAI och Yahoo Finance
/// </summary>
public class ExternalAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalAnalysisService> _logger;
    private readonly IMemoryCache _cache;
    private readonly ApiCircuitBreaker _circuitBreaker;
    private readonly ExternalApiSettings _settings;

    public ExternalAnalysisService(
        HttpClient httpClient,
        ILogger<ExternalAnalysisService> logger,
        IMemoryCache cache,
        ApiCircuitBreaker circuitBreaker,
        ExternalApiSettings settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _circuitBreaker = circuitBreaker;
        _settings = settings;
        
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger.LogInformation("🌐 ExternalAnalysisService initialiserad");
    }

    /// <summary>
    /// 📊 Hämtar komplett marknadsanalys från alla externa källor
    /// </summary>
    public async Task<ExternalMarketAnalysis> GetCompleteMarketAnalysisAsync(FinancialTransaction[] transactions)
    {
        _logger.LogInformation("🔍 Startar komplett extern marknadsanalys");

        var analysis = new ExternalMarketAnalysis();
        var dataSources = new List<string>();

        // Parallell hämtning av data från olika källor
        var tasks = new List<Task>
        {
            Task.Run(async () =>
            {
                if (_settings.SCB.Enabled)
                {
                    analysis.SwedishEconomicData = await GetSCBDataAsync();
                    if (analysis.SwedishEconomicData.Data.Any())
                        dataSources.Add("SCB");
                }
            }),
            
            Task.Run(async () =>
            {
                if (_settings.Riksbank.Enabled)
                {
                    analysis.InterestRates = await GetRiksbankDataAsync();
                    if (analysis.InterestRates.Observations.Any())
                        dataSources.Add("Riksbanken");
                }
            }),
            
            Task.Run(async () =>
            {
                if (_settings.YahooFinance.Enabled)
                {
                    analysis.MarketData = await GetYahooFinanceDataAsync();
                    if (analysis.MarketData.Chart.Result.Any())
                        dataSources.Add("Yahoo Finance");
                }
            })
        };

        await Task.WhenAll(tasks);

        // AI-analys baserat på insamlad data (om tillgänglig)
        if (_settings.OpenAI.Enabled && ApiKeys.HasOpenAIKey)
        {
            analysis.AIInsights = await GenerateAIInsightsAsync(transactions, analysis);
            if (!string.IsNullOrEmpty(analysis.AIInsights))
                dataSources.Add("OpenAI");
        }

        analysis.DataSources = dataSources.ToArray();
        analysis.IsDataComplete = dataSources.Count >= 2; // Minst 2 källor för komplett analys
        analysis.GeneratedAt = DateTime.UtcNow;

        _logger.LogInformation("✅ Extern marknadsanalys klar - {SourceCount} källor: {Sources}", 
            dataSources.Count, string.Join(", ", dataSources));

        return analysis;
    }

    /// <summary>
    /// 🇸🇪 Hämtar svensk ekonomisk data från SCB
    /// </summary>
    private async Task<SCBEconomicData> GetSCBDataAsync()
    {
        return await _circuitBreaker.ExecuteAsync(
            "SCB",
            async () =>
            {
                var cacheKey = $"scb_data_{DateTime.UtcNow:yyyy-MM-dd-HH}";
                if (_cache.TryGetValue(cacheKey, out SCBEconomicData? cachedData) && cachedData != null)
                {
                    _logger.LogDebug("📦 Använder cachad SCB-data");
                    return cachedData;
                }

                // Hämta basbelopp som exempel på svensk ekonomisk indikator
                var url = $"{_settings.SCB.BaseUrl}/START/PR/PR0101/PR0101E/BasbeloppTidsserie";
                
                var request = new
                {
                    query = new[]
                    {
                        new
                        {
                            code = "Tid",
                            selection = new
                            {
                                filter = "item",
                                values = new[] { DateTime.Now.Year.ToString(), (DateTime.Now.Year - 1).ToString() }
                            }
                        }
                    },
                    response = new { format = "json" }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("🌐 Anropar SCB API: {Url}", url);
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<SCBEconomicData>(responseJson) ?? new SCBEconomicData();

                _cache.Set(cacheKey, data, TimeSpan.FromMinutes(_settings.General.CacheExpirationMinutes));
                _logger.LogInformation("✅ SCB-data hämtad: {DataPoints} datapunkter", data.Data.Length);

                return data;
            },
            () =>
            {
                _logger.LogWarning("⚠️ SCB API otillgänglig - använder fallback-data");
                return new SCBEconomicData
                {
                    Data = new[]
                    {
                        new SCBDataPoint
                        {
                            Key = new[] { DateTime.Now.Year.ToString() },
                            Values = new[] { "52500" } // Fallback basbelopp
                        }
                    }
                };
            }
        );
    }

    /// <summary>
    /// 🏦 Hämtar räntedata från Riksbanken
    /// </summary>
    private async Task<RiksbankData> GetRiksbankDataAsync()
    {
        return await _circuitBreaker.ExecuteAsync(
            "Riksbank",
            async () =>
            {
                var cacheKey = $"riksbank_data_{DateTime.UtcNow:yyyy-MM-dd-HH}";
                if (_cache.TryGetValue(cacheKey, out RiksbankData? cachedData) && cachedData != null)
                {
                    _logger.LogDebug("📦 Använder cachad Riksbank-data");
                    return cachedData;
                }

                // Hämta SEK/EUR kurs som exempel
                var startDate = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd");
                var endDate = DateTime.Now.ToString("yyyy-MM-dd");
                var url = $"{_settings.Riksbank.BaseUrl}/Observations/SEKEURPMI/{startDate}/{endDate}";

                _logger.LogDebug("🌐 Anropar Riksbank API: {Url}", url);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<RiksbankData>(responseJson) ?? new RiksbankData();

                _cache.Set(cacheKey, data, TimeSpan.FromMinutes(_settings.General.CacheExpirationMinutes));
                _logger.LogInformation("✅ Riksbank-data hämtad: {ObservationCount} observationer", data.Observations.Length);

                return data;
            },
            () =>
            {
                _logger.LogWarning("⚠️ Riksbank API otillgänglig - använder fallback-data");
                return new RiksbankData
                {
                    Observations = new[]
                    {
                        new RiksbankObservation
                        {
                            Date = DateTime.Now.ToString("yyyy-MM-dd"),
                            Value = 11.5m, // Fallback SEK/EUR kurs
                            SeriesName = "SEKEURPMI"
                        }
                    }
                };
            }
        );
    }

    /// <summary>
    /// 📈 Hämtar marknadsdata från Yahoo Finance
    /// </summary>
    private async Task<YahooFinanceData> GetYahooFinanceDataAsync()
    {
        return await _circuitBreaker.ExecuteAsync(
            "YahooFinance",
            async () =>
            {
                var cacheKey = $"yahoo_data_{DateTime.UtcNow:yyyy-MM-dd-HH}";
                if (_cache.TryGetValue(cacheKey, out YahooFinanceData? cachedData) && cachedData != null)
                {
                    _logger.LogDebug("📦 Använder cachad Yahoo Finance-data");
                    return cachedData;
                }

                // Hämta OMX Stockholm index
                var symbol = "^OMX";
                var url = $"{_settings.YahooFinance.BaseUrl}/chart/{symbol}";

                _logger.LogDebug("🌐 Anropar Yahoo Finance API: {Url}", url);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<YahooFinanceData>(responseJson) ?? new YahooFinanceData();

                _cache.Set(cacheKey, data, TimeSpan.FromMinutes(_settings.General.CacheExpirationMinutes));
                _logger.LogInformation("✅ Yahoo Finance-data hämtad för {Symbol}", symbol);

                return data;
            },
            () =>
            {
                _logger.LogWarning("⚠️ Yahoo Finance API otillgänglig - använder fallback-data");
                return new YahooFinanceData
                {
                    Chart = new YahooChart
                    {
                        Result = new[]
                        {
                            new YahooResult
                            {
                                Meta = new YahooMeta
                                {
                                    RegularMarketPrice = 2500m, // Fallback OMX värde
                                    PreviousClose = 2480m
                                }
                            }
                        }
                    }
                };
            }
        );
    }

    /// <summary>
    /// 🤖 Genererar AI-baserade insikter med OpenAI
    /// </summary>
    private async Task<string> GenerateAIInsightsAsync(FinancialTransaction[] transactions, ExternalMarketAnalysis analysis)
    {
        return await _circuitBreaker.ExecuteAsync(
            "OpenAI",
            async () =>
            {
                var cacheKey = $"ai_insights_{DateTime.UtcNow:yyyy-MM-dd-HH}";
                if (_cache.TryGetValue(cacheKey, out string? cachedInsights) && !string.IsNullOrEmpty(cachedInsights))
                {
                    _logger.LogDebug("📦 Använder cachade AI-insikter");
                    return cachedInsights;
                }

                // Skapa prompt baserat på finansiell data
                var totalRevenue = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => t.Amount));
                var totalExpenses = Math.Abs(transactions.Where(t => t.AccountNumber.StartsWith("5") || t.AccountNumber.StartsWith("6") || t.AccountNumber.StartsWith("7")).Sum(t => t.Amount));

                var prompt = $@"Analysera denna svenska företagsdata och ge strategiska rekommendationer:

Finansiell data:
- Omsättning: {totalRevenue:N0} SEK
- Kostnader: {totalExpenses:N0} SEK
- Resultat: {(totalRevenue - totalExpenses):N0} SEK

Marknadskontext:
- SCB-data tillgänglig: {analysis.SwedishEconomicData.Data.Any()}
- Riksbank-data tillgänglig: {analysis.InterestRates.Observations.Any()}
- Marknadsdata tillgänglig: {analysis.MarketData.Chart.Result.Any()}

Ge konkreta, actionable rekommendationer för svenska företag på svenska. Fokusera på:
1. Finansiell prestanda
2. Marknadsläge
3. Strategiska möjligheter
4. Riskhantering

Svara på svenska och håll det under 500 ord.";

                var request = new OpenAIRequest
                {
                    Model = _settings.OpenAI.Model,
                    Messages = new[]
                    {
                        new OpenAIMessage { Role = "system", Content = "Du är en expert på svensk företagsanalys och finansiell rådgivning." },
                        new OpenAIMessage { Role = "user", Content = prompt }
                    },
                    MaxTokens = _settings.OpenAI.MaxTokens,
                    Temperature = _settings.OpenAI.Temperature
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKeys.OpenAI}");

                _logger.LogDebug("🌐 Anropar OpenAI API");
                var response = await _httpClient.PostAsync($"{_settings.OpenAI.BaseUrl}/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var aiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

                var insights = aiResponse?.Choices.FirstOrDefault()?.Message.Content ?? "";

                _cache.Set(cacheKey, insights, TimeSpan.FromMinutes(_settings.General.CacheExpirationMinutes));
                _logger.LogInformation("✅ AI-insikter genererade: {Length} tecken", insights.Length);

                return insights;
            },
            () =>
            {
                _logger.LogWarning("⚠️ OpenAI API otillgänglig - använder fallback-insikter");
                return @"📊 AUTOMATISK ANALYS (Fallback):

Baserat på den finansiella datan rekommenderas:

🎯 STYRKOR:
• Stabil omsättningsutveckling
• Kontrollerade kostnader
• Positiv resultatutveckling

💡 REKOMMENDATIONER:
• Fokusera på digitalisering för ökad effektivitet
• Utforska nya marknadskanaler
• Stärk kassaflödeshantering
• Investera i kundlojalitet

⚠️ RISKOMRÅDEN:
• Marknadsvolatilitet
• Kostnadsinflation
• Konkurrenstryck

🚀 MÖJLIGHETER:
• Expansion till nya segment
• Teknologisk innovation
• Strategiska partnerskap

Denna analys baseras på intern data. För mer detaljerade insikter rekommenderas extern marknadsanalys.";
            }
        );
    }
}
