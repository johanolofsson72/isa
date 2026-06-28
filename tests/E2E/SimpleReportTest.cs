using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 ENKELT RAPPORT TEST
/// Testar bara att skicka en förfrågan och vänta på svar
/// </summary>
public class SimpleReportTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly string _webChatUrl = "https://localhost:7041";

    public SimpleReportTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🎭 Initialiserar Playwright för enkelt test...");
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 1000
        });
        
        _output.WriteLine("✅ Playwright initialiserat");
    }

    public async Task DisposeAsync()
    {
        await _browser?.CloseAsync()!;
        _playwright?.Dispose();
    }

    [Fact]
    public async Task SendExpenseReportRequest_ShouldGetResponse()
    {
        _output.WriteLine("🎯 ENKELT RAPPORT TEST");
        _output.WriteLine("======================");
        
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        
        var page = await context.NewPageAsync();
        
        try
        {
            // Navigera till WebChat
            _output.WriteLine($"🌐 Navigerar till {_webChatUrl}...");
            await page.GotoAsync(_webChatUrl);
            await Task.Delay(3000);

            // Logga in
            _output.WriteLine("🔐 Loggar in...");
            var passwordInput = await page.WaitForSelectorAsync("input[type='password']", new PageWaitForSelectorOptions { Timeout = 10000 });
            await passwordInput!.FillAsync("isa72");
            
            var loginButton = await page.QuerySelectorAsync("button[type='submit']");
            await loginButton!.ClickAsync();
            
            await Task.Delay(5000);
            _output.WriteLine("✅ Inloggad");

            // Skicka förfrågan
            var request = "Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/";
            _output.WriteLine($"📝 Skickar förfrågan: {request}");
            
            var chatInput = await page.WaitForSelectorAsync("input[type='text'], textarea", new PageWaitForSelectorOptions { Timeout = 10000 });
            await chatInput!.FillAsync(request);
            
            // Skicka med Cmd+Enter
            var isMac = Environment.OSVersion.Platform == PlatformID.Unix && 
                       Directory.Exists("/Applications");
            
            if (isMac)
            {
                await chatInput.PressAsync("Meta+Enter");
            }
            else
            {
                await chatInput.PressAsync("Control+Enter");
            }
            
            _output.WriteLine("✅ Förfrågan skickad");
            
            // Vänta och övervaka svar
            _output.WriteLine("⏳ Väntar på AI-svar och övervakar aktivitet...");
            
            var responseDetected = false;
            var reportMentioned = false;
            var emailMentioned = false;
            var saveMentioned = false;
            var underlagMentioned = false;
            
            for (int i = 0; i < 30; i++) // 5 minuter totalt
            {
                await Task.Delay(10000); // 10 sekunder i taget
                
                var pageContent = await page.ContentAsync();
                
                // Kontrollera olika typer av svar
                if (!responseDetected && (pageContent.Contains("rapport", StringComparison.OrdinalIgnoreCase) || 
                    pageContent.Contains("excel", StringComparison.OrdinalIgnoreCase)))
                {
                    responseDetected = true;
                    _output.WriteLine($"✅ AI-svar detekterat efter {(i + 1) * 10} sekunder");
                }
                
                if (!reportMentioned && pageContent.Contains("utgiftsrapport", StringComparison.OrdinalIgnoreCase))
                {
                    reportMentioned = true;
                    _output.WriteLine($"✅ Utgiftsrapport nämnd efter {(i + 1) * 10} sekunder");
                }
                
                if (!emailMentioned && (pageContent.Contains("skickat", StringComparison.OrdinalIgnoreCase) ||
                    pageContent.Contains("anna", StringComparison.OrdinalIgnoreCase)))
                {
                    emailMentioned = true;
                    _output.WriteLine($"✅ Mail/Anna nämnt efter {(i + 1) * 10} sekunder");
                }

                if (!saveMentioned && (pageContent.Contains("spara", StringComparison.OrdinalIgnoreCase) ||
                    pageContent.Contains("sparad", StringComparison.OrdinalIgnoreCase)))
                {
                    saveMentioned = true;
                    _output.WriteLine($"✅ Spara/Sparad nämnt efter {(i + 1) * 10} sekunder");
                }

                if (!underlagMentioned && pageContent.Contains("underlag", StringComparison.OrdinalIgnoreCase))
                {
                    underlagMentioned = true;
                    _output.WriteLine($"✅ Underlag nämnt efter {(i + 1) * 10} sekunder");
                }
                
                // Kontrollera om det finns fel
                if (pageContent.Contains("fel", StringComparison.OrdinalIgnoreCase) ||
                    pageContent.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    _output.WriteLine($"⚠️ Möjligt fel detekterat efter {(i + 1) * 10} sekunder");
                }
                
                if (i % 6 == 0) // Logga varje minut
                {
                    _output.WriteLine($"⏳ Väntar fortfarande... {(i + 1) * 10}/300 sekunder");
                }
                
                // Om vi har fått alla svar, vänta lite till för filskapande
                if (responseDetected && reportMentioned && emailMentioned && saveMentioned && i >= 18) // Minst 3 minuter
                {
                    _output.WriteLine("✅ Alla viktiga svar detekterade, väntar på filskapande...");
                    break;
                }
            }
            
            // Ta skärmdump
            await page.ScreenshotAsync(new PageScreenshotOptions 
            { 
                Path = "simple-test-screenshot.png",
                FullPage = true 
            });
            
            _output.WriteLine("📸 Skärmdump sparad: simple-test-screenshot.png");
            
            // Kontrollera om fil skapades
            await Task.Delay(5000); // Extra väntetid
            
            var reportsDir = "/path/to/accountingagent/analysis_reports/";
            var files = Directory.GetFiles(reportsDir, "*.xlsx")
                .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-10))
                .ToList();
                
            _output.WriteLine($"📁 Hittade {files.Count} nya Excel-filer");
            foreach (var file in files)
            {
                _output.WriteLine($"   📄 {Path.GetFileName(file)}");
            }
            
            // Sammanfattning
            _output.WriteLine("\n📊 TESTSAMMANFATTNING:");
            _output.WriteLine($"   AI-svar: {(responseDetected ? "✅" : "❌")}");
            _output.WriteLine($"   Rapport nämnd: {(reportMentioned ? "✅" : "❌")}");
            _output.WriteLine($"   Mail nämnt: {(emailMentioned ? "✅" : "❌")}");
            _output.WriteLine($"   Spara nämnt: {(saveMentioned ? "✅" : "❌")}");
            _output.WriteLine($"   Underlag nämnt: {(underlagMentioned ? "✅" : "❌")}");
            _output.WriteLine($"   Filer skapade: {files.Count}");
            
        }
        finally
        {
            await page.CloseAsync();
            await context.CloseAsync();
        }
    }
}
