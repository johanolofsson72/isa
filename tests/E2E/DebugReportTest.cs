using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🔍 DEBUG RAPPORT TEST
/// Testar bara att skicka förfrågan och fånga AI:ns exakta svar
/// </summary>
public class DebugReportTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly string _webChatUrl = "https://localhost:7041";

    public DebugReportTest(ITestOutputHelper output)
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
        await _browser?.CloseAsync()!;
        _playwright?.Dispose();
    }

    [Fact]
    public async Task DebugExpenseReportRequest_ShouldCaptureExactResponse()
    {
        _output.WriteLine("🔍 DEBUG RAPPORT TEST");
        _output.WriteLine("=====================");
        
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        
        var page = await context.NewPageAsync();
        
        try
        {
            // Navigera och logga in
            await page.GotoAsync(_webChatUrl);
            await Task.Delay(3000);

            var passwordInput = await page.WaitForSelectorAsync("input[type='password']");
            await passwordInput!.FillAsync("isa72");
            
            var loginButton = await page.QuerySelectorAsync("button[type='submit']");
            await loginButton!.ClickAsync();
            
            await Task.Delay(5000);
            _output.WriteLine("✅ Inloggad");

            // Skicka förfrågan
            var request = "Skapa en professionell rapport för 2024 med toppledande design och optimerade kolumnbredder och skicka till Anna Andersson";
            
            var chatInput = await page.WaitForSelectorAsync("input[type='text'], textarea");
            await chatInput!.FillAsync(request);
            
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
            
            // Vänta på svar och fånga allt innehåll
            await Task.Delay(30000); // 30 sekunder
            
            // Fånga hela sidans innehåll
            var fullContent = await page.ContentAsync();
            
            // Extrahera bara textinnehållet från chat-området
            var chatMessages = await page.QuerySelectorAllAsync("[data-testid='chat-message'], .message, .chat-message");
            
            _output.WriteLine("\n📝 CHAT MEDDELANDEN:");
            _output.WriteLine("===================");
            
            for (int i = 0; i < chatMessages.Count; i++)
            {
                var messageText = await chatMessages[i].TextContentAsync();
                if (!string.IsNullOrWhiteSpace(messageText))
                {
                    _output.WriteLine($"\n💬 Meddelande {i + 1}:");
                    _output.WriteLine($"   {messageText.Trim()}");
                }
            }
            
            // Sök efter specifika nyckelord i hela innehållet
            _output.WriteLine("\n🔍 NYCKELORD ANALYS:");
            _output.WriteLine("====================");
            
            var keywords = new[] { "utgiftsrapport", "2023", "diagram", "pivot", "anna", "skickat", "spara", "underlag", "fel", "error", "exception" };
            
            foreach (var keyword in keywords)
            {
                var found = fullContent.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                _output.WriteLine($"   {(found ? "✅" : "❌")} '{keyword}': {(found ? "HITTAD" : "SAKNAS")}");
            }
            
            // Ta skärmdump
            await page.ScreenshotAsync(new PageScreenshotOptions 
            { 
                Path = "debug-test-screenshot.png",
                FullPage = true 
            });
            
            _output.WriteLine("\n📸 Skärmdump sparad: debug-test-screenshot.png");
            
            // Kontrollera filer
            var reportsDir = "/path/to/accountingagent/analysis_reports/";
            var files = Directory.GetFiles(reportsDir, "*.*")
                .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-5))
                .ToList();
                
            _output.WriteLine($"\n📁 Nya filer (senaste 5 min): {files.Count}");
            foreach (var file in files)
            {
                _output.WriteLine($"   📄 {Path.GetFileName(file)} ({File.GetCreationTime(file):HH:mm:ss})");
            }
            
        }
        finally
        {
            await page.CloseAsync();
            await context.CloseAsync();
        }
    }
}
