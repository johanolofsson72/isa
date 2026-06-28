using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace AnalysisPlugin.E2E.Tests;

/// <summary>
/// Global setup för E2E-tester
/// Säkerställer att AppHost körs och är tillgänglig innan testerna startar
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    private static ILogger<GlobalSetup> _logger = null!;
    private static IConfiguration _configuration = null!;
    private static HttpClient _httpClient = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Initiera logging
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<GlobalSetup>();

        // Ladda konfiguration
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        _logger.LogInformation("🚀 Startar E2E-testsetup...");

        // Kontrollera att AppHost är tillgänglig
        await EnsureAppHostIsRunning();

        // Installera Playwright browsers
        await InstallPlaywrightBrowsers();

        // Skapa testrapportmapp
        CreateTestReportDirectory();

        _logger.LogInformation("✅ E2E-testsetup slutförd framgångsrikt");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
        _logger.LogInformation("🏁 E2E-testsetup avslutad");
    }

    private async Task EnsureAppHostIsRunning()
    {
        var appHostUrl = _configuration["E2ETests:AppHostUrl"] ?? "https://localhost:7001";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        _logger.LogInformation("Kontrollerar att AppHost körs på {AppHostUrl}...", appHostUrl);

        var maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(5);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{appHostUrl}/health");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ AppHost är tillgänglig på {AppHostUrl}", appHostUrl);
                    return;
                }
                
                _logger.LogWarning("AppHost svarade med status {StatusCode}, försök {Attempt}/{MaxAttempts}", 
                    response.StatusCode, attempt, maxAttempts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AppHost inte tillgänglig (försök {Attempt}/{MaxAttempts}): {Error}", 
                    attempt, maxAttempts, ex.Message);
            }

            if (attempt < maxAttempts)
            {
                _logger.LogInformation("Väntar {Delay} sekunder innan nästa försök...", delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }

        var errorMessage = $"AppHost är inte tillgänglig på {appHostUrl} efter {maxAttempts} försök. " +
                          "Säkerställ att AppHost körs innan E2E-testerna startas.";
        
        _logger.LogError(errorMessage);
        throw new InvalidOperationException(errorMessage);
    }

    private Task InstallPlaywrightBrowsers()
    {
        _logger.LogInformation("Installerar Playwright-webbläsare...");
        
        try
        {
            // Kör Playwright install kommando
            Microsoft.Playwright.Program.Main(new[] { "install" });
            _logger.LogInformation("✅ Playwright-webbläsare installerade");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Varning vid installation av Playwright-webbläsare: {Error}", ex.Message);
            // Fortsätt ändå - webbläsarna kanske redan är installerade
        }

        return Task.CompletedTask;
    }

    private void CreateTestReportDirectory()
    {
        var reportDir = _configuration["AnalysisPlugin:ReportArchiveDirectory"] ?? "./test-reports";
        
        try
        {
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
                _logger.LogInformation("✅ Testrapportmapp skapad: {ReportDir}", reportDir);
            }
            else
            {
                _logger.LogInformation("Testrapportmapp finns redan: {ReportDir}", reportDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Kunde inte skapa testrapportmapp {ReportDir}: {Error}", reportDir, ex.Message);
        }
    }
}
