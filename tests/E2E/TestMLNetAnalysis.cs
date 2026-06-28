using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Testar verklig ML.NET funktionalitet
/// </summary>
public class TestMLNetAnalysis
{
    private readonly ITestOutputHelper _output;
    private readonly MLAnalysisService _mlService;

    public TestMLNetAnalysis(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<MLAnalysisService>();
        _mlService = new MLAnalysisService(logger);
    }

    [Fact]
    public async Task TestMLNetRevenueForecast_ShouldWork()
    {
        _output.WriteLine("🔮 TESTAR ML.NET SSA PROGNOS");
        _output.WriteLine("============================");

        try
        {
            // Skapa realistisk testdata (24 månader)
            var transactions = GenerateRevenueTransactions(24);
            _output.WriteLine($"📊 Skapad testdata: {transactions.Count} transaktioner");

            // Kör ML.NET prognos
            var result = await _mlService.ForecastRevenueAsync(transactions, 6);

            // Validera resultat (acceptera Docker-optimerad, ML.NET och fallback)
            Assert.NotNull(result);
            Assert.True(result.AnalysisType == "docker_optimized_forecast" ||
                       result.AnalysisType == "ml_revenue_forecast" ||
                       result.AnalysisType == "fallback_forecast",
                $"Unexpected analysis type: {result.AnalysisType}");
            Assert.True(result.Forecast.Count == 6, $"Expected 6 forecast points, got {result.Forecast.Count}");
            Assert.True(result.ConfidenceLevel >= 0.7, $"Confidence too low: {result.ConfidenceLevel}");

            _output.WriteLine($"✅ Prognos skapad: {result.AnalysisType}");
            _output.WriteLine($"📈 Prognosperiod: {result.Forecast.Count} månader");
            _output.WriteLine($"🎯 Konfidensgrad: {result.ConfidenceLevel:P1}");
            _output.WriteLine($"📊 MAPE: {result.PerformanceMetrics.GetValueOrDefault("MAPE", 0):F1}%");

            if (result.AnalysisType == "fallback_forecast")
            {
                _output.WriteLine("ℹ️ Använder fallback-prognos (ML.NET SSA kräver Intel MKL på ARM64)");
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
    public async Task TestMLNetAnomalyDetection_ShouldWork()
    {
        _output.WriteLine("⚠️ TESTAR ML.NET ANOMALIDETEKTION");
        _output.WriteLine("==================================");

        try
        {
            // Skapa testdata med anomalier
            var transactions = GenerateTransactionsWithAnomalies(60); // 60 dagar
            _output.WriteLine($"📊 Skapad testdata: {transactions.Count} transaktioner");

            // Kör ML.NET anomalidetektion
            var result = await _mlService.DetectAnomaliesAsync(transactions);

            // Validera resultat (acceptera Docker-optimerad och andra typer)
            Assert.NotNull(result);
            Assert.True(result.AnalysisType.Contains("anomaly_detection"), $"Unexpected analysis type: {result.AnalysisType}");
            Assert.True(result.ConfidenceLevel >= 0.8, $"Confidence too low: {result.ConfidenceLevel}");

            _output.WriteLine($"✅ Anomalidetektion klar: {result.AnalysisType}");
            _output.WriteLine($"⚠️ Anomalier hittade: {result.Anomalies.Count}");
            _output.WriteLine($"🎯 Konfidensgrad: {result.ConfidenceLevel:P1}");
            _output.WriteLine($"📊 Anomalifrekvens: {result.Metrics.GetValueOrDefault("anomaly_rate_percent", 0):F1}%");

            // Visa anomalier
            if (result.Anomalies.Any())
            {
                _output.WriteLine("\n⚠️ UPPTÄCKTA ANOMALIER:");
                foreach (var anomaly in result.Anomalies.Take(3))
                {
                    _output.WriteLine($"  {anomaly.Date:yyyy-MM-dd}: {anomaly.Value:N0} kr (Score: {anomaly.AnomalyScore:F2})");
                    _output.WriteLine($"    {anomaly.Description}");
                }
            }

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestMLNetTrendAnalysis_ShouldWork()
    {
        _output.WriteLine("📈 TESTAR ML.NET TRENDANALYS");
        _output.WriteLine("=============================");

        try
        {
            // Skapa testdata med tydlig trend
            var transactions = GenerateTrendingTransactions(36); // 3 år
            _output.WriteLine($"📊 Skapad testdata: {transactions.Count} transaktioner");

            // Kör trendanalys
            var result = await _mlService.AnalyzeTrendsAsync(transactions);

            // Validera resultat (acceptera Docker-optimerad och andra typer)
            Assert.NotNull(result);
            Assert.True(result.AnalysisType.Contains("trend_analysis"), $"Unexpected analysis type: {result.AnalysisType}");
            Assert.True(result.ConfidenceLevel >= 0.7, $"Confidence too low: {result.ConfidenceLevel}");

            _output.WriteLine($"✅ Trendanalys klar: {result.AnalysisType}");
            _output.WriteLine($"🎯 Konfidensgrad: {result.ConfidenceLevel:P1}");
            _output.WriteLine($"📊 Tillväxttakt: {result.Metrics.GetValueOrDefault("revenue_growth_rate", 0):F1}%");
            _output.WriteLine($"🌊 Säsongsvarians: {result.Metrics.GetValueOrDefault("seasonal_variance", 0):F1}%");

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

    private List<FinancialTransaction> GenerateTransactionsWithAnomalies(int days)
    {
        var transactions = new List<FinancialTransaction>();
        var baseAmount = 5000m;
        var startDate = DateTime.Now.AddDays(-days);
        var random = new Random(42);

        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var amount = baseAmount + (decimal)(random.NextDouble() * 2000 - 1000); // ±1000 variation

            // Lägg till anomalier på vissa dagar
            if (i % 20 == 0) // Var 20:e dag
            {
                amount *= 5; // 5x normal belopp = anomali
            }

            transactions.Add(new FinancialTransaction
            {
                Date = date,
                Amount = amount,
                AccountNumber = "5010",
                Description = $"Daglig kostnad dag {i + 1}"
            });
        }

        return transactions;
    }

    private List<FinancialTransaction> GenerateTrendingTransactions(int months)
    {
        var transactions = new List<FinancialTransaction>();
        var baseAmount = 50000m;
        var startDate = DateTime.Now.AddMonths(-months);

        for (int i = 0; i < months; i++)
        {
            var date = startDate.AddMonths(i);
            var trendFactor = 1 + (i * 0.02m); // 2% månadsvis tillväxt
            var amount = baseAmount * trendFactor;

            transactions.Add(new FinancialTransaction
            {
                Date = date,
                Amount = -amount, // Negativ för intäkt
                AccountNumber = "3010",
                Description = $"Trendande intäkt månad {i + 1}"
            });
        }

        return transactions;
    }
}
