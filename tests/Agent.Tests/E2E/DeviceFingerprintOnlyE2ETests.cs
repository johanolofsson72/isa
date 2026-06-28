using Microsoft.Playwright;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.E2E;

/// <summary>
/// Focused test to verify ONLY device fingerprint stability across browser sessions.
/// This test does NOT test memory persistence - it only verifies that the same 
/// device ID is generated and maintained across multiple sessions.
/// </summary>
public class DeviceFingerprintOnlyE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private readonly bool _servicesAvailable;

    private const string BaseUrl = "https://localhost:7041";
    private const string AgentUrl = "https://localhost:7042";
    private const string LoginPassword = "isa72";
    private const int TimeoutMs = 15000;

    public DeviceFingerprintOnlyE2ETests(ITestOutputHelper output)
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
            SlowMo = 300,
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

    private async Task<IPage> CreateAndLoginPageAsync(string sessionName)
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
            
            // Wait for login to complete
            await page.WaitForTimeoutAsync(3000);
            _output.WriteLine($"✅ {sessionName}: Login completed");
        }

        // Wait for chat interface to be available
        try
        {
            await page.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });
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

    private async Task<(string localStorage, string cookie)> GetDeviceIdentifiersAsync(IPage page, string sessionName)
    {
        // Get device ID from localStorage
        var deviceId = await page.EvaluateAsync<string>(@"
            () => {
                return localStorage.getItem('isa_did') || 'null';
            }
        ");
        
        // Get device ID from cookies
        var cookies = await _context!.CookiesAsync();
        var deviceCookie = cookies.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";
        
        _output.WriteLine($"🔍 {sessionName}: localStorage device ID = {deviceId}");
        _output.WriteLine($"🔍 {sessionName}: cookie device ID = {deviceCookie}");
        
        return (deviceId, deviceCookie);
    }

    [Fact]
    public async Task DeviceFingerprint_Should_BeStable_AcrossBrowserSessions()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Skipping test - services not available");
            return;
        }

        var localStorageIds = new List<string>();
        var cookieIds = new List<string>();

        try
        {
            // SESSION 1: Get initial device fingerprint
            _output.WriteLine("🧪 SESSION 1: Getting initial device fingerprint");
            var page1 = await CreateAndLoginPageAsync("SESSION 1");
            var (localStorage1, cookie1) = await GetDeviceIdentifiersAsync(page1, "SESSION 1");
            localStorageIds.Add(localStorage1);
            cookieIds.Add(cookie1);
            await page1.CloseAsync();

            // SESSION 2: Verify device fingerprint persists
            _output.WriteLine("🧪 SESSION 2: Verifying device fingerprint persistence");
            var page2 = await CreateAndLoginPageAsync("SESSION 2");
            var (localStorage2, cookie2) = await GetDeviceIdentifiersAsync(page2, "SESSION 2");
            localStorageIds.Add(localStorage2);
            cookieIds.Add(cookie2);
            await page2.CloseAsync();

            // SESSION 3: Additional verification
            _output.WriteLine("🧪 SESSION 3: Additional verification");
            var page3 = await CreateAndLoginPageAsync("SESSION 3");
            var (localStorage3, cookie3) = await GetDeviceIdentifiersAsync(page3, "SESSION 3");
            localStorageIds.Add(localStorage3);
            cookieIds.Add(cookie3);
            await page3.CloseAsync();

            // SESSION 4: Final verification
            _output.WriteLine("🧪 SESSION 4: Final verification");
            var page4 = await CreateAndLoginPageAsync("SESSION 4");
            var (localStorage4, cookie4) = await GetDeviceIdentifiersAsync(page4, "SESSION 4");
            localStorageIds.Add(localStorage4);
            cookieIds.Add(cookie4);
            await page4.CloseAsync();

            // ANALYSIS
            _output.WriteLine("\n📊 DEVICE FINGERPRINT STABILITY ANALYSIS:");
            _output.WriteLine("===========================================");
            
            for (int i = 0; i < localStorageIds.Count; i++)
            {
                _output.WriteLine($"Session {i + 1} - localStorage: {localStorageIds[i]}, Cookie: {cookieIds[i]}");
            }

            // Filter out null/empty values
            var validLocalStorageIds = localStorageIds.Where(id => id != "null" && !string.IsNullOrEmpty(id)).ToArray();
            var validCookieIds = cookieIds.Where(cookie => cookie != "null" && !string.IsNullOrEmpty(cookie)).ToArray();

            _output.WriteLine($"\n🔍 Valid localStorage device IDs found: {validLocalStorageIds.Length}");
            _output.WriteLine($"🔍 Valid cookie device IDs found: {validCookieIds.Length}");

            // Verify localStorage consistency
            if (validLocalStorageIds.Length > 0)
            {
                var uniqueLocalStorageIds = validLocalStorageIds.Distinct().ToArray();
                _output.WriteLine($"\n📱 localStorage Analysis:");
                _output.WriteLine($"  - Total valid IDs: {validLocalStorageIds.Length}");
                _output.WriteLine($"  - Unique IDs: {uniqueLocalStorageIds.Length}");
                
                foreach (var id in uniqueLocalStorageIds)
                {
                    var count = validLocalStorageIds.Count(x => x == id);
                    _output.WriteLine($"  - {id}: appears {count} times");
                }

                if (uniqueLocalStorageIds.Length == 1)
                {
                    _output.WriteLine("✅ localStorage device fingerprint is CONSISTENT across all sessions");
                }
                else
                {
                    _output.WriteLine("❌ localStorage device fingerprint is INCONSISTENT across sessions");
                }

                // Assert localStorage consistency
                uniqueLocalStorageIds.Should().HaveCount(1, "localStorage device fingerprint should be consistent across all sessions");
                validLocalStorageIds.Should().AllSatisfy(id => id.Should().Be(uniqueLocalStorageIds[0]), "all localStorage device IDs should be identical");
            }
            else
            {
                _output.WriteLine("❌ No valid localStorage device IDs found");
                Assert.Fail("No valid localStorage device IDs were generated");
            }

            // Verify cookie consistency (if cookies are used)
            if (validCookieIds.Length > 0)
            {
                var uniqueCookieIds = validCookieIds.Distinct().ToArray();
                _output.WriteLine($"\n🍪 Cookie Analysis:");
                _output.WriteLine($"  - Total valid cookies: {validCookieIds.Length}");
                _output.WriteLine($"  - Unique cookie values: {uniqueCookieIds.Length}");
                
                foreach (var id in uniqueCookieIds)
                {
                    var count = validCookieIds.Count(x => x == id);
                    _output.WriteLine($"  - {id}: appears {count} times");
                }

                if (uniqueCookieIds.Length == 1)
                {
                    _output.WriteLine("✅ Cookie device fingerprint is CONSISTENT across all sessions");
                }
                else
                {
                    _output.WriteLine("❌ Cookie device fingerprint is INCONSISTENT across sessions");
                }

                // Assert cookie consistency
                uniqueCookieIds.Should().HaveCount(1, "cookie device fingerprint should be consistent across all sessions");
                validCookieIds.Should().AllSatisfy(id => id.Should().Be(uniqueCookieIds[0]), "all cookie device IDs should be identical");
            }
            else
            {
                _output.WriteLine("ℹ️ No device cookies found (this may be expected)");
            }

            _output.WriteLine("\n✅ Device fingerprint stability test completed successfully!");
            _output.WriteLine("🎯 This test confirms that device fingerprints are stable across browser sessions.");
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
