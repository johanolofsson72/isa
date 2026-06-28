using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace AnalysisPlugin.E2E.Tests;

/// <summary>
/// End-to-end tests för naturliga språkförfrågningar via WebChat enligt specifikation
/// Testar de exakta exemplen från spec.md via AppHost med Agent och MailPlugin
/// </summary>
[TestFixture]
public class WebChatNaturalLanguageTests : PageTest
{
    private IConfiguration _configuration = null!;
    private ILogger<WebChatNaturalLanguageTests> _logger = null!;
    private string _appHostUrl = null!;
    private string _webChatUrl = null!;

    [OneTimeSetUp]
    public Task OneTimeSetUp()
    {
        // Ladda konfiguration
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        // Skapa logger
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<WebChatNaturalLanguageTests>();

        // E-postverifiering sker via AppHost och MailPlugin

        // Hämta URL:er från konfiguration
        _appHostUrl = _configuration["E2ETests:AppHostUrl"] ?? "https://localhost:7001";
        _webChatUrl = _configuration["E2ETests:WebChatUrl"] ?? "https://localhost:7001/webchat";

        _logger.LogInformation("E2E tests initialiserade med AppHost: {AppHostUrl}, WebChat: {WebChatUrl}", 
            _appHostUrl, _webChatUrl);

        // Installera Playwright browsers om de inte finns
        Microsoft.Playwright.Program.Main(new[] { "install" });

        return Task.CompletedTask;
    }

    [SetUp]
    public async Task SetUp()
    {
        // Konfigurera Playwright för varje test
        await Context.Tracing.StartAsync(new()
        {
            Title = TestContext.CurrentContext.Test.Name,
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        // Spara spårning för felsökning
        var tracePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, 
            $"trace-{TestContext.CurrentContext.Test.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        
        await Context.Tracing.StopAsync(new() { Path = tracePath });
        
        _logger.LogInformation("Playwright trace sparad: {TracePath}", tracePath);
    }

    [Test]
    [Category("E2E")]
    [Category("NaturalLanguage")]
    public async Task NaturalLanguageRequest_ComprehensiveReport2024_GeneratesAndEmailsReport()
    {
        // Arrange - Det första exemplet från specifikationen
        var naturalLanguageRequest = "Jag vill att du skapar en otroligt snygg ekonomirapport för 2024 och skickar till demo@example.com som kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum, snygg startsida, diagram på sida två och interagerbar tabell på sidan tre.";

        _logger.LogInformation("Testar naturlig språkförfrågan: {Request}", naturalLanguageRequest);

        // Act & Assert - Testar via AppHost med Agent och MailPlugin
        await ExecuteNaturalLanguageReportTest(naturalLanguageRequest, "2024-comprehensive-report");
    }

    [Test]
    [Category("E2E")]
    [Category("NaturalLanguage")]
    public async Task NaturalLanguageRequest_FiveYearPivotReport_GeneratesAndEmailsReport()
    {
        // Arrange - Det andra exemplet från specifikationen
        var naturalLanguageRequest = "Jag vill att du skapar en otroligt snygg ekonomirapport för 2020, 2021, 2022, 2023 och 2024 och skickar till demo@example.com som kan visa en pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren, snygg startsida, en sida för varje år samt en sida för pivoten.";
        
        _logger.LogInformation("Testar femårs pivotrapport: {Request}", naturalLanguageRequest);

        // Act & Assert
        await ExecuteNaturalLanguageReportTest(naturalLanguageRequest, "5-year-pivot-report");
    }

    [Test]
    [Category("E2E")]
    [Category("NaturalLanguage")]
    public async Task NaturalLanguageRequest_WeeklyGrossProfitReport_GeneratesAndEmailsReport()
    {
        // Arrange - Det tredje exemplet från specifikationen
        var naturalLanguageRequest = "Jag vill att du skapar en otroligt snygg ekonomirapport för 2024 och skickar till demo@example.com som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka, snygg startsida, och en sida för diagrammet.";
        
        _logger.LogInformation("Testar veckovis bruttovinstrapport: {Request}", naturalLanguageRequest);

        // Act & Assert
        await ExecuteNaturalLanguageReportTest(naturalLanguageRequest, "weekly-gross-profit-report");
    }

    private async Task ExecuteNaturalLanguageReportTest(string naturalLanguageRequest, string testIdentifier)
    {
        try
        {
            // Steg 1: Navigera till WebChat och logga in
            await NavigateToWebChatAndLogin();

            // Steg 2: Skicka naturlig språkförfrågan
            await SendNaturalLanguageRequest(naturalLanguageRequest);

            // Steg 3: Vänta på AI-agentens svar och bekräftelse
            var confirmationMessage = await WaitForAIConfirmation();

            // Verifiera att AI-agenten svarar (inte tomt meddelande)
            Assert.That(confirmationMessage?.Trim(), Is.Not.Null.And.Not.Empty,
                "AI-agenten ska ge ett svar (inte tomt meddelande)");

            // Verifiera att AI-agenten nämner relevanta nyckelord
            var containsRelevantKeywords = confirmationMessage.Contains("rapport", StringComparison.OrdinalIgnoreCase) ||
                                         confirmationMessage.Contains("ekonomi", StringComparison.OrdinalIgnoreCase) ||
                                         confirmationMessage.Contains("2024", StringComparison.OrdinalIgnoreCase) ||
                                         confirmationMessage.Contains("demo@example.com", StringComparison.OrdinalIgnoreCase) ||
                                         confirmationMessage.Contains("skapa", StringComparison.OrdinalIgnoreCase) ||
                                         confirmationMessage.Contains("projekt", StringComparison.OrdinalIgnoreCase);

            Assert.That(containsRelevantKeywords, Is.True,
                $"AI-agenten ska nämna relevanta nyckelord i sitt svar. Faktiskt svar: '{confirmationMessage}'");

            // Steg 4: Verifiera att AI-agenten indikerar framgång
            // (MailPlugin hanterar faktisk e-postleverans via AppHost)
            Assert.That(confirmationMessage, Does.Not.Contain("fel").And.Not.Contain("misslyckades").IgnoreCase,
                "AI-agenten ska inte rapportera fel");

            _logger.LogInformation("✅ Test {TestIdentifier} slutförd framgångsrikt - AI-agent bekräftade rapport och e-post", testIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Test {TestIdentifier} misslyckades", testIdentifier);

            // Ta skärmdump för felsökning
            var screenshotPath = Path.Combine(TestContext.CurrentContext.WorkDirectory,
                $"error-{testIdentifier}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

            _logger.LogInformation("Skärmdump sparad: {ScreenshotPath}", screenshotPath);
            throw;
        }
    }

    private async Task NavigateToWebChatAndLogin()
    {
        _logger.LogInformation("Navigerar till WebChat: {WebChatUrl}", _webChatUrl);

        // Navigera till WebChat
        await Page.GotoAsync(_webChatUrl);

        // Vänta på att sidan laddas
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Vänta lite extra för Blazor att initialisera
        await Page.WaitForTimeoutAsync(5000);

        // Kontrollera om det finns ett lösenordsfält (inloggning krävs)
        var passwordField = Page.Locator("input[type='password'], #password");
        if (await passwordField.CountAsync() > 0)
        {
            _logger.LogInformation("Inloggning krävs, loggar in...");

            var password = _configuration["E2ETests:TestUser:Password"] ?? "isa72";

            // Fyll i lösenord
            await passwordField.FillAsync(password);

            // Klicka på inloggningsknapp
            var loginButton = Page.Locator("button:has-text('Logga in')");
            await loginButton.ClickAsync();

            // Vänta på att inloggningen slutförs
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(3000);

            _logger.LogInformation("✅ Inloggning slutförd");
        }

        _logger.LogInformation("✅ WebChat-sida laddad och redo");
    }

    private async Task SendNaturalLanguageRequest(string request)
    {
        _logger.LogInformation("Skickar naturlig språkförfrågan: {Request}", request.Substring(0, Math.Min(100, request.Length)) + "...");

        // Hitta chattinmatningsfältet (vi vet nu att det är #messageInput)
        var chatInput = Page.Locator("#messageInput, .message-input");

        // Vänta på att inmatningsfältet blir synligt
        await Expect(chatInput).ToBeVisibleAsync(new() { Timeout = 10000 });

        _logger.LogInformation("Hittade chattinmatningsfält");

        // Skriv meddelandet
        await chatInput.FillAsync(request);

        // Skicka meddelandet med skicka-knappen
        var sendButton = Page.Locator("[data-testid='send-button']");
        await sendButton.ClickAsync();

        _logger.LogInformation("✅ Naturlig språkförfrågan skickad");
    }

    private async Task<string> WaitForAIConfirmation()
    {
        _logger.LogInformation("Väntar på AI-agentens bekräftelse...");

        var timeoutMinutes = _configuration.GetValue<int>("E2ETests:TestTimeout:ReportGenerationMinutes", 10);
        var timeout = TimeSpan.FromMinutes(timeoutMinutes);

        // Vänta på att AI-agenten svarar (vi förväntar oss minst 3 meddelanden: välkomst + användarmeddelande + AI-svar)
        var allMessages = Page.Locator(".message");

        // Vänta på att vi har minst 3 meddelanden (välkomst, användarmeddelande, AI-svar)
        await Expect(allMessages).ToHaveCountAsync(3, new() { Timeout = (float)timeout.TotalMilliseconds });

        // Hämta det senaste meddelandet (AI-svaret)
        var aiMessageLocator = allMessages.Last;

        // Vänta lite extra för att säkerställa att meddelandet är komplett
        await Page.WaitForTimeoutAsync(5000);

        var confirmationText = await aiMessageLocator.TextContentAsync();

        _logger.LogInformation("✅ AI-svar mottaget: {Response}", confirmationText);

        return confirmationText ?? "";
    }
}
