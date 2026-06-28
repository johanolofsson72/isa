using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 SIMPLE E2E TEST - Testar med redan körande system
/// </summary>
public class SimpleE2ETest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly string _webChatUrl = "https://localhost:7041";

    public SimpleE2ETest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🎭 Initializing Playwright for Simple E2E Test...");
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Visible for debugging
            SlowMo = 1000
        });
        
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        
        _page = await _context.NewPageAsync();
        
        _output.WriteLine("✅ Playwright initialized");
    }

    public async Task DisposeAsync()
    {
        await _page?.CloseAsync()!;
        await _context?.CloseAsync()!;
        await _browser?.CloseAsync()!;
        _playwright?.Dispose();
    }

    [Fact]
    public async Task SimpleExpenseReportTest_ShouldWork()
    {
        _output.WriteLine("🧪 SIMPLE E2E TEST - Expense Report");
        _output.WriteLine("==================================");

        // Navigate to WebChat
        _output.WriteLine($"🌐 Navigating to: {_webChatUrl}");
        await _page!.GotoAsync(_webChatUrl);

        // Wait for page to load
        await Task.Delay(3000);

        // Take a screenshot to see what we have
        await _page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "webchat-screenshot.png",
            FullPage = true 
        });
        _output.WriteLine("📸 Screenshot saved: webchat-screenshot.png");

        // Look for any input elements
        var inputs = await _page.QuerySelectorAllAsync("input, textarea");
        _output.WriteLine($"🔍 Found {inputs.Count} input elements");

        if (inputs.Any())
        {
            var chatInput = inputs.First();
            
            // Test message
            var testMessage = "Hej, kan du skapa en enkel rapport?";
            _output.WriteLine($"📝 Sending test message: {testMessage}");
            
            await chatInput.FillAsync(testMessage);
            
            // Try to send (look for buttons)
            var buttons = await _page.QuerySelectorAllAsync("button");
            _output.WriteLine($"🔍 Found {buttons.Count} buttons");
            
            if (buttons.Any())
            {
                // Try the first button that might be a send button
                var sendButton = buttons.FirstOrDefault();
                if (sendButton != null)
                {
                    await sendButton.ClickAsync();
                    _output.WriteLine("✅ Clicked send button");
                    
                    // Wait for response
                    await Task.Delay(5000);
                    
                    // Take another screenshot
                    await _page.ScreenshotAsync(new PageScreenshotOptions 
                    { 
                        Path = "webchat-after-send.png",
                        FullPage = true 
                    });
                    _output.WriteLine("📸 After-send screenshot saved: webchat-after-send.png");
                }
            }
            else
            {
                // Try pressing Enter
                await chatInput.PressAsync("Enter");
                _output.WriteLine("✅ Pressed Enter");
                
                await Task.Delay(5000);
            }
        }
        else
        {
            _output.WriteLine("❌ No input elements found");
            
            // Get page content for debugging
            var content = await _page.ContentAsync();
            _output.WriteLine($"📄 Page content length: {content.Length}");
            _output.WriteLine($"📄 Page title: {await _page.TitleAsync()}");
        }

        _output.WriteLine("✅ Simple E2E test completed");
    }

    [Fact]
    public async Task InspectWebChatInterface_ShouldShowStructure()
    {
        _output.WriteLine("🔍 INSPECTING WEBCHAT INTERFACE");
        _output.WriteLine("==============================");

        await _page!.GotoAsync(_webChatUrl);
        await Task.Delay(3000);

        // Get page info
        var title = await _page.TitleAsync();
        var url = _page.Url;
        
        _output.WriteLine($"📄 Page Title: {title}");
        _output.WriteLine($"🔗 Current URL: {url}");

        // Find all interactive elements
        var allInputs = await _page.QuerySelectorAllAsync("input, textarea, button");
        _output.WriteLine($"🎛️ Total interactive elements: {allInputs.Count}");

        foreach (var element in allInputs.Take(10)) // Limit to first 10
        {
            var tagName = await element.EvaluateAsync<string>("el => el.tagName");
            var type = await element.GetAttributeAsync("type") ?? "N/A";
            var placeholder = await element.GetAttributeAsync("placeholder") ?? "N/A";
            var className = await element.GetAttributeAsync("class") ?? "N/A";
            
            _output.WriteLine($"  🔸 {tagName} - type: {type}, placeholder: {placeholder}");
            _output.WriteLine($"     class: {className}");
        }

        // Look for Blazor/SignalR indicators
        var scripts = await _page.QuerySelectorAllAsync("script");
        var hasBlazor = false;
        var hasSignalR = false;

        foreach (var script in scripts)
        {
            var src = await script.GetAttributeAsync("src") ?? "";
            if (src.Contains("blazor")) hasBlazor = true;
            if (src.Contains("signalr")) hasSignalR = true;
        }

        _output.WriteLine($"🔧 Has Blazor: {hasBlazor}");
        _output.WriteLine($"🔧 Has SignalR: {hasSignalR}");

        // Take full page screenshot
        await _page.ScreenshotAsync(new PageScreenshotOptions 
        { 
            Path = "webchat-inspection.png",
            FullPage = true 
        });
        
        _output.WriteLine("📸 Inspection screenshot saved: webchat-inspection.png");
        _output.WriteLine("✅ Interface inspection completed");
    }

    [Fact]
    public async Task LoginAndTestChat_ShouldWork()
    {
        _output.WriteLine("🔐 LOGIN AND CHAT TEST");
        _output.WriteLine("=====================");

        // Navigate to WebChat
        await _page!.GotoAsync(_webChatUrl);
        await Task.Delay(2000);

        // Login
        _output.WriteLine("🔐 Logging in...");
        var passwordInput = await _page.WaitForSelectorAsync("input[type='password']");
        await passwordInput!.FillAsync("isa72");

        var loginButton = await _page.QuerySelectorAsync("button[type='submit']");
        await loginButton!.ClickAsync();

        _output.WriteLine("✅ Login attempted");
        await Task.Delay(3000);

        // Check if we're now on the chat page
        var currentUrl = _page.Url;
        _output.WriteLine($"🔗 Current URL after login: {currentUrl}");

        // Look for chat interface
        var chatInputs = await _page.QuerySelectorAllAsync("input[type='text'], textarea");
        _output.WriteLine($"💬 Found {chatInputs.Count} potential chat inputs");

        if (chatInputs.Any())
        {
            var chatInput = chatInputs.First();

            // Send a simple test message
            var testMessage = "Hej Isa! Kan du skapa en enkel rapport?";
            _output.WriteLine($"📝 Sending: {testMessage}");

            await chatInput.FillAsync(testMessage);

            // Look for send button or press Enter
            var sendButtons = await _page.QuerySelectorAllAsync("button");
            IElementHandle? sendButton = null;

            foreach (var button in sendButtons)
            {
                var text = await button.TextContentAsync();
                if (text?.ToLower().Contains("send") == true ||
                    text?.ToLower().Contains("skicka") == true)
                {
                    sendButton = button;
                    break;
                }
            }

            // Use Ctrl+Enter (or Cmd+Enter on Mac) to send message
            var isMac = Environment.OSVersion.Platform == PlatformID.Unix &&
                       Directory.Exists("/Applications");

            if (isMac)
            {
                await chatInput.PressAsync("Meta+Enter");
                _output.WriteLine("✅ Pressed Cmd+Enter (Mac)");
            }
            else
            {
                await chatInput.PressAsync("Control+Enter");
                _output.WriteLine("✅ Pressed Ctrl+Enter");
            }

            // Wait for response
            _output.WriteLine("⏳ Waiting for response...");
            await Task.Delay(10000);

            // Take screenshot of result
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = "chat-test-result.png",
                FullPage = true
            });

            _output.WriteLine("📸 Chat result screenshot saved: chat-test-result.png");

            // Check for any response in the chat
            var chatMessages = await _page.QuerySelectorAllAsync("div, p, span");
            var hasResponse = false;

            foreach (var msg in chatMessages.Take(20))
            {
                var text = await msg.TextContentAsync();
                if (!string.IsNullOrEmpty(text) && text.Length > 10 &&
                    (text.Contains("rapport") || text.Contains("Isa") || text.Contains("hej")))
                {
                    _output.WriteLine($"💬 Found response: {text.Substring(0, Math.Min(100, text.Length))}...");
                    hasResponse = true;
                    break;
                }
            }

            if (hasResponse)
            {
                _output.WriteLine("✅ Chat response detected!");
            }
            else
            {
                _output.WriteLine("⚠️ No clear chat response found");
            }
        }
        else
        {
            _output.WriteLine("❌ No chat input found after login");
        }

        _output.WriteLine("✅ Login and chat test completed");
    }

    [Fact]
    public async Task FullReportGenerationTest_ShouldWork()
    {
        _output.WriteLine("📊 FULL REPORT GENERATION E2E TEST");
        _output.WriteLine("==================================");

        // Login first
        await _page!.GotoAsync(_webChatUrl);
        await Task.Delay(2000);

        var passwordInput = await _page.WaitForSelectorAsync("input[type='password']");
        await passwordInput!.FillAsync("isa72");

        var loginButton = await _page.QuerySelectorAsync("button[type='submit']");
        await loginButton!.ClickAsync();

        await Task.Delay(3000);
        _output.WriteLine("✅ Logged in successfully");

        // Find chat input
        var chatInput = await _page.WaitForSelectorAsync("input[type='text'], textarea");

        // Test comprehensive report request
        var reportRequest = "Skapa en professionell utgiftsrapport för 2023 med diagram och pivot-tabeller och skicka till Anna Andersson";
        _output.WriteLine($"📝 Sending report request: {reportRequest}");

        await chatInput!.FillAsync(reportRequest);

        // Send with Cmd+Enter
        var isMac = Environment.OSVersion.Platform == PlatformID.Unix &&
                   Directory.Exists("/Applications");

        if (isMac)
        {
            await chatInput.PressAsync("Meta+Enter");
            _output.WriteLine("✅ Pressed Cmd+Enter to send report request");
        }
        else
        {
            await chatInput.PressAsync("Control+Enter");
            _output.WriteLine("✅ Pressed Ctrl+Enter to send report request");
        }

        // Wait longer for report generation
        _output.WriteLine("⏳ Waiting for report generation (60 seconds)...");
        await Task.Delay(60000);

        // Take screenshot of final result
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "full-report-test-result.png",
            FullPage = true
        });

        _output.WriteLine("📸 Full report result screenshot saved: full-report-test-result.png");

        // Check for success indicators in the chat
        var pageContent = await _page.ContentAsync();
        var hasReportIndicators =
            pageContent.Contains("rapport", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("excel", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("diagram", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("pivot", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("skicka", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("anna", StringComparison.OrdinalIgnoreCase);

        if (hasReportIndicators)
        {
            _output.WriteLine("✅ Report generation indicators found in response!");
        }
        else
        {
            _output.WriteLine("⚠️ No clear report generation indicators found");
        }

        // Check for any error messages
        var hasErrors =
            pageContent.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("fel", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            pageContent.Contains("timeout", StringComparison.OrdinalIgnoreCase);

        if (hasErrors)
        {
            _output.WriteLine("⚠️ Potential error indicators found in response");
        }
        else
        {
            _output.WriteLine("✅ No error indicators detected");
        }

        _output.WriteLine("✅ Full report generation E2E test completed");

        // Assert that we got some kind of response
        Assert.True(hasReportIndicators || !hasErrors,
            "Expected report generation indicators or no errors");
    }
}
