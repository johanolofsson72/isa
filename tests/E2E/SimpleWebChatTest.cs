using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using System.Diagnostics;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Simple test to debug WebChat connectivity issues
/// </summary>
public class SimpleWebChatTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private Process? _appHostProcess;
    private const string WEBCHAT_URL = "http://localhost:5041";

    public SimpleWebChatTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🚀 Starting simple WebChat test...");
        
        // Start AppHost
        await StartAppHostAsync();
        
        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Show browser for debugging
            SlowMo = 2000, // Slow down for better visibility
            Args = new[] { "--ignore-certificate-errors", "--ignore-ssl-errors", "--allow-running-insecure-content" }
        });
        
        _output.WriteLine("✅ Test environment initialized");
    }

    public async Task DisposeAsync()
    {
        await _browser?.CloseAsync()!;
        _playwright?.Dispose();
        
        // Stop AppHost
        if (_appHostProcess != null && !_appHostProcess.HasExited)
        {
            _appHostProcess.Kill();
            _appHostProcess.Dispose();
        }
        
        _output.WriteLine("🧹 Test environment cleaned up");
    }

    private async Task StartAppHostAsync()
    {
        _output.WriteLine("🏗️ Starting AppHost...");
        
        var appHostPath = "/path/to/accountingagent/src/AppHost";
        _appHostProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --no-build",
                WorkingDirectory = appHostPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        
        _appHostProcess.Start();
        
        // Wait for AppHost to start
        var startupTimeout = TimeSpan.FromMinutes(2);
        var startTime = DateTime.Now;
        
        while (DateTime.Now - startTime < startupTimeout)
        {
            if (!_appHostProcess.HasExited)
            {
                await Task.Delay(2000);
                // Try to connect to WebChat
                try
                {
                    using var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync($"{WEBCHAT_URL}/");
                    _output.WriteLine($"🔍 HTTP Response: {response.StatusCode}");
                    if (response.StatusCode == System.Net.HttpStatusCode.OK || 
                        response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect)
                    {
                        _output.WriteLine("✅ AppHost started successfully");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"🔍 Connection attempt failed: {ex.Message}");
                }
            }
            else
            {
                throw new Exception("AppHost process exited unexpectedly");
            }
        }
        
        throw new TimeoutException("AppHost failed to start within timeout period");
    }

    [Fact]
    public async Task TestWebChatNavigation()
    {
        _output.WriteLine("🧪 Testing WebChat navigation...");
        
        var page = await _browser.NewPageAsync();
        
        try
        {
            // Navigate to WebChat
            _output.WriteLine($"🌐 Navigating to WebChat: {WEBCHAT_URL}");
            var response = await page.GotoAsync(WEBCHAT_URL);
            _output.WriteLine($"🔍 Navigation response status: {response?.Status}");
            
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Debug: Get page info
            var pageTitle = await page.TitleAsync();
            var pageUrl = page.Url;
            _output.WriteLine($"🔍 Page title: {pageTitle}");
            _output.WriteLine($"🔍 Current URL: {pageUrl}");
            
            // Take a screenshot
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-webchat-navigation.png" });
            _output.WriteLine("📸 Screenshot saved: debug-webchat-navigation.png");
            
            // Check if we're on the login page
            if (pageUrl.Contains("/login"))
            {
                _output.WriteLine("🔐 Login page detected, attempting to log in...");

                // Fill in the password
                await page.FillAsync("#password", "isa72");
                await page.ClickAsync("button[type='submit']");

                // Wait for navigation to complete
                await page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });

                // Update page info after login
                pageTitle = await page.TitleAsync();
                pageUrl = page.Url;
                _output.WriteLine($"🔍 After login - Page title: {pageTitle}");
                _output.WriteLine($"🔍 After login - Current URL: {pageUrl}");

                // Take another screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-webchat-after-login.png" });
                _output.WriteLine("📸 Screenshot saved: debug-webchat-after-login.png");
            }

            // Try to find the message input
            var messageInputExists = await page.Locator("#messageInput").CountAsync() > 0;
            _output.WriteLine($"🔍 Message input exists: {messageInputExists}");

            if (!messageInputExists)
            {
                // List all input elements
                var inputs = await page.Locator("input, textarea").AllAsync();
                _output.WriteLine($"🔍 Found {inputs.Count} input/textarea elements:");
                for (int i = 0; i < inputs.Count; i++)
                {
                    var id = await inputs[i].GetAttributeAsync("id");
                    var className = await inputs[i].GetAttributeAsync("class");
                    var type = await inputs[i].GetAttributeAsync("type");
                    _output.WriteLine($"  {i + 1}. ID: {id}, Class: {className}, Type: {type}");
                }
            }
            else
            {
                _output.WriteLine("✅ Message input found! WebChat is ready for testing.");
            }
            
            _output.WriteLine("✅ WebChat navigation test completed");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
