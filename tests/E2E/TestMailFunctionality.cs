using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som specifikt testar mail-funktionaliteten
/// </summary>
public class TestMailFunctionality : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public TestMailFunctionality(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 1000
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task TestMailFunctionality_ShouldSendEmailToAnna()
    {
        _output.WriteLine("🔍 TESTAR MAIL-FUNKTIONALITET");
        _output.WriteLine("=============================");

        var page = await _browser!.NewPageAsync();
        
        try
        {
            // Navigera till chat-sidan
            await page.GotoAsync("http://localhost:5000");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            _output.WriteLine("✅ Navigerat till chat-sidan");

            // Vänta på att sidan laddas
            await page.WaitForSelectorAsync(".chat-container", new PageWaitForSelectorOptions { Timeout = 10000 });
            _output.WriteLine("✅ Chat-container hittad");

            // Skicka en enkel mail-förfrågan
            var mailRequest = "Skicka ett testmail till Anna Andersson med ämnet 'Test från AccountingAgent' och meddelandet 'Detta är ett test av mail-funktionaliteten.'";

            await page.FillAsync("textarea[placeholder*='meddelande']", mailRequest);
            await page.ClickAsync("button[type='submit']");
            
            _output.WriteLine("✅ Mail-förfrågan skickad");

            // Vänta på svar
            await page.WaitForSelectorAsync(".message:last-child .message-content", 
                new PageWaitForSelectorOptions { Timeout = 60000 });

            // Läs svaret
            var responseElement = await page.QuerySelectorAsync(".message:last-child .message-content");
            var response = await responseElement!.InnerTextAsync();
            
            _output.WriteLine("\n📝 SVAR FRÅN SYSTEMET:");
            _output.WriteLine("======================");
            _output.WriteLine(response);

            // Analysera svaret
            await AnalyzeMailResponse(response);

        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task AnalyzeMailResponse(string response)
    {
        _output.WriteLine("\n🔍 ANALYS AV MAIL-SVAR:");
        _output.WriteLine("=======================");

        var mailKeywords = new[]
        {
            ("skickat", "Mail skickades"),
            ("anna", "Anna identifierad"),
            ("test", "Testmeddelande"),
            ("mail", "Mail-funktionalitet"),
            ("fel", "Fel uppstod"),
            ("error", "Error uppstod"),
            ("failed", "Misslyckades"),
            ("success", "Framgång"),
            ("sent", "Skickad"),
            ("delivered", "Levererad")
        };

        foreach (var (keyword, description) in mailKeywords)
        {
            var found = response.ToLowerInvariant().Contains(keyword.ToLowerInvariant());
            var status = found ? "✅ HITTAD" : "❌ SAKNAS";
            _output.WriteLine($"   {status}: '{keyword}' ({description})");
        }

        // Kontrollera om mailet verkligen skickades
        if (response.ToLowerInvariant().Contains("skickat") || 
            response.ToLowerInvariant().Contains("sent"))
        {
            _output.WriteLine("\n✅ MAIL VERKAR HA SKICKATS!");
            _output.WriteLine("   📧 Kontrollera din inkorg (inklusive spam)");
            _output.WriteLine("   📧 E-post: user@example.com");
        }
        else if (response.ToLowerInvariant().Contains("fel") || 
                 response.ToLowerInvariant().Contains("error") ||
                 response.ToLowerInvariant().Contains("failed"))
        {
            _output.WriteLine("\n❌ MAIL-FEL UPPTÄCKT!");
            _output.WriteLine("   🔧 Kontrollera mail-konfiguration");
            _output.WriteLine("   🔧 Kontrollera EnableRealMessaging=true");
            _output.WriteLine("   🔧 Kontrollera Mailjet API-nycklar");
        }
        else
        {
            _output.WriteLine("\n⚠️ OKLART MAIL-STATUS");
            _output.WriteLine("   📝 Svaret innehåller inte tydliga mail-indikatorer");
        }

        await Task.CompletedTask;
    }
}
