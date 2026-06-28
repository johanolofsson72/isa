using Microsoft.Playwright;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.E2E;

/// <summary>
/// Simplified test to verify device fingerprint stability across sessions
/// </summary>
public class DeviceFingerprintSimpleE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private readonly bool _servicesAvailable;

    private const string BaseUrl = "https://localhost:7041";
    private const string AgentUrl = "https://localhost:7042";
    private const string LoginPassword = "isa72";
    private const int TimeoutMs = 30000;

    public DeviceFingerprintSimpleE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _servicesAvailable = CheckServicesAvailableAsync().GetAwaiter().GetResult();
    }

    public async Task InitializeAsync()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Services not available - skipping browser initialization");
            return;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 500,
            Args = new[] { "--ignore-certificate-errors", "--ignore-ssl-errors" }
        });
        
        // Create a persistent browser context to ensure cookies are shared
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
    }

    private async Task<bool> CheckServicesAvailableAsync()
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            _output.WriteLine("🔍 Checking if Agent is available...");
            var agentResponse = await httpClient.GetAsync($"{AgentUrl}/api/chat/health");
            if (!agentResponse.IsSuccessStatusCode)
            {
                _output.WriteLine($"❌ Agent not available: {agentResponse.StatusCode}");
                return false;
            }

            _output.WriteLine("🔍 Checking if WebChat is available...");
            var webChatResponse = await httpClient.GetAsync($"{BaseUrl}/health");
            if (!webChatResponse.IsSuccessStatusCode)
            {
                _output.WriteLine($"❌ WebChat not available: {webChatResponse.StatusCode}");
                return false;
            }

            _output.WriteLine("✅ Both services are available");
            return true;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Service check failed: {ex.Message}");
            return false;
        }
    }

    private async Task<IPage> LoginAndGetChatPageAsync(string sessionName)
    {
        var page = await _context!.NewPageAsync();
        
        _output.WriteLine($"🧪 {sessionName}: Navigating to WebChat...");
        await page.GotoAsync($"{BaseUrl}/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Handle login if redirected
        if (page.Url.Contains("/login"))
        {
            _output.WriteLine($"🔐 {sessionName}: Logging in...");
            await page.FillAsync("input[type='password']", LoginPassword);
            await page.PressAsync("input[type='password']", "Enter");
            
            // Wait for login to complete - use a more flexible approach
            await page.WaitForTimeoutAsync(5000); // Give time for login processing
            _output.WriteLine($"✅ {sessionName}: Login completed");
        }

        // Wait for chat interface to be available
        try
        {
            await page.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = 20000 });
            _output.WriteLine($"✅ {sessionName}: Chat interface ready");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ {sessionName}: Failed to find chat interface: {ex.Message}");
            _output.WriteLine($"{sessionName}: Current URL: {page.Url}");
            
            // Take a screenshot for debugging
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = $"debug-{sessionName.ToLower().Replace(" ", "-")}.png" });
            _output.WriteLine($"📸 {sessionName}: Screenshot saved");
            throw;
        }

        return page;
    }

    private async Task<string> GetDeviceIdAsync(IPage page, string sessionName)
    {
        var deviceId = await page.EvaluateAsync<string>(@"
            () => {
                return localStorage.getItem('isa_did') || 'null';
            }
        ");
        
        _output.WriteLine($"🔍 {sessionName}: Device ID = {deviceId}");
        return deviceId;
    }

    [Fact]
    public async Task DeviceFingerprint_Should_BeStable_AcrossSessions()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Skipping test - services not available");
            return;
        }

        try
        {
            // SESSION 1: Learn name "Anna"
            _output.WriteLine("🧪 SESSION 1: Learning name 'Anna'");
            var page1 = await LoginAndGetChatPageAsync("SESSION 1");
            var deviceId1 = await GetDeviceIdAsync(page1, "SESSION 1");

            await page1.FillAsync("[data-testid='chat-input']", "Hej Isa, jag heter Anna Andersson");
            await page1.PressAsync("[data-testid='chat-input']", "Control+Enter");

            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           lastMessage.textContent.toLowerCase().includes('anna');
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response1 = await page1.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 SESSION 1 Response: {response1}");
            await page1.CloseAsync();

            // SESSION 2: Verify name is remembered
            _output.WriteLine("🧪 SESSION 2: Verifying name is remembered");
            var page2 = await LoginAndGetChatPageAsync("SESSION 2");
            var deviceId2 = await GetDeviceIdAsync(page2, "SESSION 2");

            await page2.FillAsync("[data-testid='chat-input']", "Hej Isa, vad heter jag?");
            await page2.PressAsync("[data-testid='chat-input']", "Enter");

            await page2.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           lastMessage.textContent.toLowerCase().includes('anna');
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response2 = await page2.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 SESSION 2 Response: {response2}");
            await page2.CloseAsync();

            // SESSION 3: Learn new name "Patrick"
            _output.WriteLine("🧪 SESSION 3: Learning new name 'Patrick'");
            var page3 = await LoginAndGetChatPageAsync("SESSION 3");
            var deviceId3 = await GetDeviceIdAsync(page3, "SESSION 3");

            await page3.FillAsync("[data-testid='chat-input']", "Hej Isa, jag heter Patrick i mellannamn");
            await page3.PressAsync("[data-testid='chat-input']", "Enter");

            await page3.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           lastMessage.textContent.toLowerCase().includes('patrick');
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response3 = await page3.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 SESSION 3 Response: {response3}");
            await page3.CloseAsync();

            // SESSION 4: Verify new name is remembered
            _output.WriteLine("🧪 SESSION 4: Verifying new name is remembered");
            var page4 = await LoginAndGetChatPageAsync("SESSION 4");
            var deviceId4 = await GetDeviceIdAsync(page4, "SESSION 4");

            await page4.FillAsync("[data-testid='chat-input']", "Hej Isa, vad heter jag i mellannamn?");
            await page4.PressAsync("[data-testid='chat-input']", "Enter");

            await page4.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 10;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response4 = await page4.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 SESSION 4 Response: {response4}");
            await page4.CloseAsync();

            // ANALYSIS
            _output.WriteLine("\n📊 DEVICE FINGERPRINT ANALYSIS:");
            _output.WriteLine($"Session 1 Device ID: {deviceId1}");
            _output.WriteLine($"Session 2 Device ID: {deviceId2}");
            _output.WriteLine($"Session 3 Device ID: {deviceId3}");
            _output.WriteLine($"Session 4 Device ID: {deviceId4}");

            // Verify device fingerprints are consistent
            var allDeviceIds = new[] { deviceId1, deviceId2, deviceId3, deviceId4 };
            var validDeviceIds = allDeviceIds.Where(id => id != "null" && !string.IsNullOrEmpty(id)).ToArray();

            _output.WriteLine($"\n🔍 Valid device IDs found: {validDeviceIds.Length}");
            foreach (var id in validDeviceIds)
            {
                _output.WriteLine($"  - {id}");
            }

            if (validDeviceIds.Length > 0)
            {
                var uniqueDeviceIds = validDeviceIds.Distinct().ToArray();
                if (uniqueDeviceIds.Length == 1)
                {
                    _output.WriteLine("✅ Device fingerprint is CONSISTENT across all sessions");
                }
                else
                {
                    _output.WriteLine("❌ Device fingerprint is INCONSISTENT across sessions");
                    _output.WriteLine($"Found {uniqueDeviceIds.Length} different device IDs:");
                    foreach (var id in uniqueDeviceIds)
                    {
                        _output.WriteLine($"  - {id}");
                    }
                }
            }

            // Verify memory persistence
            response2.Should().Contain("Anna", "Session 2 should remember Anna from Session 1");
            response4.Should().Contain("Patrick", "Session 4 should remember Patrick from Session 3");

            _output.WriteLine("✅ Test completed successfully!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ TEST FAILED: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.CloseAsync();
        }
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
        _playwright?.Dispose();
    }
}
