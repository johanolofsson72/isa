using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace AnalysisPlugin.E2E.Tests;

/// <summary>
/// End-to-end tester för AnalysisPlugin API-endpoints
/// Testar direkta API-anrop via AppHost med MailPlugin-integration
/// </summary>
[TestFixture]
public class AnalysisPluginApiTests
{
    private HttpClient _httpClient = null!;
    private IConfiguration _configuration = null!;
    private ILogger<AnalysisPluginApiTests> _logger = null!;
    private string _appHostUrl = null!;
    private string _sie4TestData = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Ladda konfiguration
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        // Skapa logger
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<AnalysisPluginApiTests>();

        // Initiera HTTP-klient
        _appHostUrl = _configuration["E2ETests:AppHostUrl"] ?? "https://localhost:7001";
        _httpClient = new HttpClient { BaseAddress = new Uri(_appHostUrl) };
        
        // Lägg till autentiseringsheaders om det behövs
        // _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "your-token");

        // E-postleverans hanteras av AppHost och MailPlugin

        // Ladda test-SIE4-data
        _sie4TestData = await File.ReadAllTextAsync("test-data/sample-financial-data.sie");

        _logger.LogInformation("API-tester initialiserade med AppHost: {AppHostUrl}", _appHostUrl);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    [Category("E2E")]
    [Category("API")]
    public async Task CreateReportJobAsync_ValidSIE4Data_ReturnsJobId()
    {
        // Arrange
        var request = new
        {
            sie4Data = _sie4TestData,
            language = "sv"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync("/api/analysis/create-report-job", content);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True, 
            $"API-anrop ska lyckas. Status: {response.StatusCode}, Content: {await response.Content.ReadAsStringAsync()}");

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

        Assert.That(result.TryGetProperty("reportJobId", out var jobIdElement), Is.True);
        Assert.That(string.IsNullOrEmpty(jobIdElement.GetString()), Is.False);

        _logger.LogInformation("✅ Rapport-jobb skapat med ID: {JobId}", jobIdElement.GetString());
    }

    [Test]
    [Category("E2E")]
    [Category("API")]
    public async Task CompleteReportWorkflow_WithEmailDelivery_CompletesSuccessfully()
    {
        try
        {
            // Steg 1: Skapa rapport-jobb
            var reportJobId = await CreateReportJob();
            _logger.LogInformation("Rapport-jobb skapat: {JobId}", reportJobId);

            // Steg 2: Lägg till framsida
            await AddFrontPage(reportJobId, "E2E Test Rapport", "Automatiserad testrapport", "Demo Ekonomi AB");
            _logger.LogInformation("Framsida tillagd");

            // Steg 3: Lägg till diagram
            await AddChart(reportJobId, "revenue_trend", "Intäktsutveckling 2024", "historical");
            _logger.LogInformation("Diagram tillagt");

            // Steg 4: Lägg till tabell
            await AddTable(reportJobId, "monthly_summary", "Månadssammandrag", true);
            _logger.LogInformation("Tabell tillagd");

            // Steg 5: Lägg till pivot-tabell
            await AddPivotTable(reportJobId, "monthly_revenue", "Månadsvis intäktsanalys", "Month", "Revenue");
            _logger.LogInformation("Pivot-tabell tillagd");

            // Steg 6: Lägg till ML-analys
            await AddMLAnalysis(reportJobId, "trend_analysis", "AI-driven trendanalys", 0.95);
            _logger.LogInformation("ML-analys tillagd");

            // Steg 7: Slutför rapport och begär e-postleverans
            var filePath = await FinishReportAndSendEmail(reportJobId, "demo@example.com");
            _logger.LogInformation("Rapport slutförd och e-postbegäran skickad: {FilePath}", filePath);

            // Steg 8: Verifiera att API-anropen lyckades
            // (MailPlugin hanterar faktisk e-postleverans via AppHost)
            Assert.That(string.IsNullOrEmpty(filePath), Is.False,
                "API ska returnera en filsökväg för den genererade rapporten");

            Assert.That(filePath, Does.EndWith(".xlsx"),
                "Genererad rapport ska vara en Excel-fil");

            _logger.LogInformation("✅ Komplett rapportarbetsflöde slutfört framgångsrikt via API");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Komplett rapportarbetsflöde misslyckades");
            throw;
        }
    }

    private async Task<string> CreateReportJob()
    {
        var request = new { sie4Data = _sie4TestData, language = "sv" };
        var response = await PostJsonAsync("/api/analysis/create-report-job", request);
        var result = JsonSerializer.Deserialize<JsonElement>(response);
        return result.GetProperty("reportJobId").GetString()!;
    }

    private async Task AddFrontPage(string reportJobId, string title, string subtitle, string companyName)
    {
        var request = new { reportJobId, title, subtitle, companyName };
        await PostJsonAsync("/api/analysis/add-front-page", request);
    }

    private async Task AddChart(string reportJobId, string chartType, string title, string dataSource)
    {
        var request = new { reportJobId, chartType, title, dataSource };
        await PostJsonAsync("/api/analysis/add-chart", request);
    }

    private async Task AddTable(string reportJobId, string tableType, string title, bool sortable)
    {
        var request = new { reportJobId, tableType, title, sortable };
        await PostJsonAsync("/api/analysis/add-table", request);
    }

    private async Task AddPivotTable(string reportJobId, string pivotType, string title, string groupBy, string aggregateBy)
    {
        var request = new { reportJobId, pivotType, title, groupBy, aggregateBy };
        await PostJsonAsync("/api/analysis/add-pivot-table", request);
    }

    private async Task AddMLAnalysis(string reportJobId, string analysisType, string title, double confidenceLevel)
    {
        var request = new { reportJobId, analysisType, title, confidenceLevel };
        await PostJsonAsync("/api/analysis/add-ml-analysis", request);
    }

    private async Task<string> FinishReportAndSendEmail(string reportJobId, string emailAddress)
    {
        var request = new { reportJobId, emailAddress };
        var response = await PostJsonAsync("/api/analysis/finish-report-and-email", request);
        var result = JsonSerializer.Deserialize<JsonElement>(response);
        return result.GetProperty("filePath").GetString()!;
    }

    private async Task<string> PostJsonAsync(string endpoint, object request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(endpoint, content);
        
        Assert.That(response.IsSuccessStatusCode, Is.True, 
            $"API-anrop till {endpoint} ska lyckas. Status: {response.StatusCode}");
        
        return await response.Content.ReadAsStringAsync();
    }
}
