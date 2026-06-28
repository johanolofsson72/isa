using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Testar förbättrad prognosfunktionalitet med extern marknadsdata
/// </summary>
public class TestEnhancedForecasting
{
    private readonly ITestOutputHelper _output;
    private readonly MLAnalysisService _mlService;

    public TestEnhancedForecasting(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<MLAnalysisService>();
        _mlService = new MLAnalysisService(logger);
    }

    [Fact]
    public async Task TestEnhancedRevenueForecast_ShouldIncludeExternalData()
    {
        _output.WriteLine("🔮 TESTAR FÖRBÄTTRAD INTÄKTSPROGNOS");
        _output.WriteLine("===================================");

        try
        {
            // Skapa realistisk testdata (24 månader)
            var transactions = GenerateRevenueTransactions(24);
            _output.WriteLine($"📊 Skapad testdata: {transactions.Count} transaktioner");

            // Kör förbättrad prognos
            var result = await _mlService.ForecastRevenueAsync(transactions, 6);

            // Validera resultat
            Assert.NotNull(result);
            Assert.True(result.AnalysisType.Contains("forecast"), $"Unexpected analysis type: {result.AnalysisType}");
            Assert.True(result.Forecast.Count == 6, $"Expected 6 forecast points, got {result.Forecast.Count}");
            Assert.True(result.ConfidenceLevel >= 0.7, $"Confidence too low: {result.ConfidenceLevel}");

            _output.WriteLine($"✅ Prognos skapad: {result.AnalysisType}");
            _output.WriteLine($"📈 Prognosperiod: {result.Forecast.Count} månader");
            _output.WriteLine($"🎯 Konfidensgrad: {result.ConfidenceLevel:P1}");

            // Kontrollera om extern data användes
            if (result.AnalysisType == "enhanced_revenue_forecast")
            {
                _output.WriteLine("🌍 Extern marknadsdata integrerad!");
                _output.WriteLine($"📊 Intern konfidensgrad: {result.Metrics.GetValueOrDefault("internal_confidence", 0):P1}");
                _output.WriteLine($"🌐 Extern konfidensgrad: {result.Metrics.GetValueOrDefault("external_confidence", 0):P1}");
                _output.WriteLine($"🔄 Hybrid konfidensgrad: {result.Metrics.GetValueOrDefault("hybrid_confidence", 0):P1}");
            }
            else
            {
                _output.WriteLine("ℹ️ Använder intern analys (extern data ej tillgänglig)");
            }

            // Visa prognosdata
            _output.WriteLine("\n📈 PROGNOSDATA:");
            foreach (var point in result.Forecast.Take(3))
            {
                _output.WriteLine($"  {point.Date:yyyy-MM}: {point.Value:N0} kr (±{(point.UpperBound - point.LowerBound)/2:N0})");
            }

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestFutureQuarterAnalysis_ShouldWork()
    {
        _output.WriteLine("🔮 TESTAR FRAMTIDA KVARTALSANALYS");
        _output.WriteLine("=================================");

        try
        {
            // Skapa testdata
            var transactions = GenerateRevenueTransactions(36); // 3 år
            _output.WriteLine($"📊 Skapad testdata: {transactions.Count} transaktioner");

            // Analysera Q3 2026
            var targetQuarter = new DateTime(2026, 7, 1); // Q3 2026
            var result = await _mlService.CreateFutureQuarterAnalysisAsync(transactions, targetQuarter, "TECH");

            // Validera resultat
            Assert.NotNull(result);
            Assert.True(result.HybridForecast.Count > 0, "No forecast points generated");
            Assert.True(result.ConfidenceMetrics.HybridConfidence > 0, "No confidence calculated");

            _output.WriteLine($"✅ Kvartalsanalys skapad för: {targetQuarter:yyyy-MM}");
            _output.WriteLine($"🎯 Hybrid konfidensgrad: {result.ConfidenceMetrics.HybridConfidence:P1}");
            _output.WriteLine($"📊 Prognostyp: {result.ForecastType}");
            _output.WriteLine($"🔧 Metodologi: {result.Methodology}");

            // Visa extern påverkan
            if (result.ExternalInfluence.EconomicIndicatorWeight > 0)
            {
                _output.WriteLine("\n🌍 EXTERN DATAPÅVERKAN:");
                _output.WriteLine($"  Ekonomiska indikatorer: {result.ExternalInfluence.EconomicIndicatorWeight:P1}");
                _output.WriteLine($"  Marknadssentiment: {result.ExternalInfluence.MarketSentimentWeight:P1}");
                _output.WriteLine($"  Branschbenchmarks: {result.ExternalInfluence.IndustryBenchmarkWeight:P1}");
            }

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestEconomicIndicators_ShouldReturnData()
    {
        _output.WriteLine("🇸🇪 TESTAR SVENSKA EKONOMISKA INDIKATORER");
        _output.WriteLine("==========================================");

        try
        {
            var indicators = await _mlService.GetCurrentEconomicIndicatorsAsync();

            Assert.NotNull(indicators);

            // Validera att vi har fått NÅGON riktig data från API:erna
            bool hasRealData = indicators.ExchangeRate_SEK_EUR > 0 ||
                              indicators.ExchangeRate_SEK_USD > 0 ||
                              indicators.InterestRate > 0 ||
                              indicators.GDP_GrowthRate != 0;

            Assert.True(hasRealData, "Should have received some real data from APIs");
            Assert.True(indicators.InflationRate >= 0, "Inflation rate should be non-negative");
            Assert.True(indicators.InterestRate >= 0, "Interest rate should be non-negative");

            _output.WriteLine($"✅ Ekonomiska indikatorer hämtade");
            _output.WriteLine($"📈 BNP-tillväxt: {indicators.GDP_GrowthRate:F1}%");
            _output.WriteLine($"💰 Inflation: {indicators.InflationRate:F1}%");
            _output.WriteLine($"🏦 Ränta: {indicators.InterestRate:F1}%");
            _output.WriteLine($"💱 SEK/EUR: {indicators.ExchangeRate_SEK_EUR:F2}");
            _output.WriteLine($"💱 SEK/USD: {indicators.ExchangeRate_SEK_USD:F2}");
            _output.WriteLine($"📊 Konsumentförtroende: {indicators.ConsumerConfidenceIndex:F1}");
            _output.WriteLine($"🏭 Industriproduktion: {indicators.IndustrialProductionIndex:F1}");
            _output.WriteLine($"🔄 Källa: {indicators.Source}");

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestIndustryBenchmarks_ShouldReturnData()
    {
        _output.WriteLine("🏭 TESTAR BRANSCHBENCHMARKS");
        _output.WriteLine("===========================");

        try
        {
            var benchmarks = await _mlService.GetIndustryBenchmarksAsync("TECH");

            Assert.NotNull(benchmarks);
            Assert.Equal("TECH", benchmarks.IndustryCode);
            Assert.True(benchmarks.AverageGrowthRate > 0, "Growth rate should be positive for tech");

            _output.WriteLine($"✅ Branschbenchmarks hämtade för: {benchmarks.IndustryCode}");
            _output.WriteLine($"📈 Genomsnittlig tillväxt: {benchmarks.AverageGrowthRate:F1}%");
            _output.WriteLine($"💰 Genomsnittlig marginal: {benchmarks.AverageMargin:F1}%");
            _output.WriteLine($"🌊 Säsongsfaktor: {benchmarks.SeasonalityFactor:F2}");
            _output.WriteLine($"📊 Volatilitetsindex: {benchmarks.VolatilityIndex:F2}");
            _output.WriteLine($"🎯 Marknadssentiment: {benchmarks.MarketSentiment:P1}");
            _output.WriteLine($"🔄 Källa: {benchmarks.Source}");

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    private List<FinancialTransaction> GenerateRevenueTransactions(int months)
    {
        var transactions = new List<FinancialTransaction>();
        var baseAmount = 100000m;
        var startDate = DateTime.Now.AddMonths(-months);

        for (int i = 0; i < months; i++)
        {
            var date = startDate.AddMonths(i);
            var seasonalFactor = 1 + (decimal)(Math.Sin(i * Math.PI / 6) * 0.1); // Säsongsmönster
            var trendFactor = 1 + (i * 0.01m); // 1% månadsvis tillväxt
            var amount = baseAmount * seasonalFactor * trendFactor;

            transactions.Add(new FinancialTransaction
            {
                Date = date,
                Amount = -amount, // Negativ för intäkt
                AccountNumber = "3010",
                Description = $"Försäljning månad {i + 1}"
            });
        }

        return transactions;
    }
}
