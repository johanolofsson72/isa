using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Agent.Tests.E2E;

/// <summary>
/// Basic E2E tests to debug page loading issues
/// </summary>
public class BasicPageLoadE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7041";
    private const string LoginPassword = "isa72";
    private const int TimeoutMs = 30000;

    public BasicPageLoadE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Install Playwright browsers if needed
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright install failed with exit code {exitCode}");
        }

        // Launch browser
        var playwright = await Playwright.CreateAsync();
        _browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Set to false for debugging
            Args = new[] { "--disable-web-security", "--disable-features=VizDisplayCompositor" }
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task BasicPageLoad_Should_Work()
    {
        // Check if services are available
        _output.WriteLine("🔍 Checking if services are available...");
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        try
        {
            var agentResponse = await httpClient.GetAsync("https://localhost:7042/health");
            agentResponse.EnsureSuccessStatusCode();
            _output.WriteLine("✅ Agent service is available");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Agent service is not available. Make sure AppHost is running.", ex);
        }

        try
        {
            var webChatResponse = await httpClient.GetAsync($"{BaseUrl}/health");
            webChatResponse.EnsureSuccessStatusCode();
            _output.WriteLine("✅ WebChat service is available");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("WebChat service is not available. Make sure AppHost is running.", ex);
        }

        try
        {
            _output.WriteLine("🧪 TEST: Step 1 - Open page");
            var page = await _browser!.NewPageAsync();
            
            // Navigate to the page
            await page.GotoAsync($"{BaseUrl}/");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            _output.WriteLine("✅ Page loaded successfully");

            // Take a screenshot for debugging
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-page-load.png" });
            _output.WriteLine("📸 Screenshot saved as debug-page-load.png");

            // Check if we can see the login form
            var passwordInput = await page.QuerySelectorAsync("input[type='password']");
            if (passwordInput != null)
            {
                _output.WriteLine("✅ Password input found");

                _output.WriteLine("🧪 TEST: Step 2 - Login");
                await page.FillAsync("input[type='password']", LoginPassword);
                await page.PressAsync("input[type='password']", "Enter");

                // Wait for navigation or authentication state change
                try
                {
                    // Wait for either navigation to complete or for the chat input to appear
                    await page.WaitForFunctionAsync(@"
                        () => {
                            // Check if we're still on login page
                            if (window.location.pathname === '/login') return false;

                            // Check if chat input exists
                            const chatInput = document.querySelector('[data-testid=""chat-input""]');
                            if (chatInput) return true;

                            // Check if we can see the main content (authorized view)
                            const mainContent = document.querySelector('.main-content');
                            if (mainContent) return true;

                            return false;
                        }
                    ", new PageWaitForFunctionOptions { Timeout = 10000 });

                    _output.WriteLine("✅ Authentication state updated successfully");
                }
                catch (TimeoutException)
                {
                    _output.WriteLine("⚠️ Timeout waiting for authentication state update");
                }

                // Take another screenshot after login
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-after-login.png" });
                _output.WriteLine("📸 Screenshot after login saved as debug-after-login.png");

                // Check current URL
                _output.WriteLine($"📍 Current URL: {page.Url}");

                // Check if chat input is now visible
                var chatInput = await page.QuerySelectorAsync("[data-testid='chat-input']");
                if (chatInput != null)
                {
                    _output.WriteLine("✅ Chat input found after login");
                }
                else
                {
                    _output.WriteLine("❌ Chat input NOT found after login");

                    // Check if we're still on login page
                    if (page.Url.Contains("/login"))
                    {
                        _output.WriteLine("🔍 Still on login page - authentication may have failed");
                    }
                    else
                    {
                        _output.WriteLine("🔍 Not on login page but chat input missing - checking page structure");

                        // Check for main content area
                        var mainContent = await page.QuerySelectorAsync(".main-content");
                        if (mainContent != null)
                        {
                            _output.WriteLine("✅ Main content area found (authorized view is working)");
                        }
                        else
                        {
                            _output.WriteLine("❌ Main content area NOT found (authorization issue)");
                        }

                        // Check for chat container
                        var chatContainer = await page.QuerySelectorAsync(".chat-container");
                        if (chatContainer != null)
                        {
                            _output.WriteLine("✅ Chat container found");
                        }
                        else
                        {
                            _output.WriteLine("❌ Chat container NOT found");
                        }
                    }

                    // Let's see what elements are actually on the page
                    var pageContent = await page.ContentAsync();
                    _output.WriteLine($"📄 Page content length: {pageContent.Length}");

                    // Look for any input elements
                    var allInputs = await page.QuerySelectorAllAsync("input");
                    _output.WriteLine($"🔍 Found {allInputs.Count} input elements");

                    foreach (var input in allInputs)
                    {
                        var type = await input.GetAttributeAsync("type");
                        var testId = await input.GetAttributeAsync("data-testid");
                        var className = await input.GetAttributeAsync("class");
                        _output.WriteLine($"  - Input: type='{type}', data-testid='{testId}', class='{className}'");
                    }
                }
            }
            else
            {
                _output.WriteLine("❌ Password input NOT found");
                
                // Let's see what's actually on the page
                var pageContent = await page.ContentAsync();
                _output.WriteLine($"📄 Page content length: {pageContent.Length}");
                _output.WriteLine($"📄 Page title: {await page.TitleAsync()}");
                _output.WriteLine($"📄 Page URL: {page.Url}");
            }

            await page.CloseAsync();
            _output.WriteLine("✅ TEST: Basic page load test completed");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ TEST: FAILED - {ex.Message}");
            throw;
        }
    }
}
