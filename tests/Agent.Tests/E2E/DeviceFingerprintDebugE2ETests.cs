using Microsoft.Playwright;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.E2E;

/// <summary>
/// Debug test to track device fingerprint values across sessions
/// </summary>
public class DeviceFingerprintDebugE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private readonly bool _servicesAvailable;

    private const string BaseUrl = "https://localhost:7041";
    private const string AgentUrl = "https://localhost:7042";
    private const string LoginPassword = "test123";
    private const int TimeoutMs = 30000;

    public DeviceFingerprintDebugE2ETests(ITestOutputHelper output)
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

    [Fact]
    public async Task DebugDeviceFingerprint_TrackValues_AcrossSessions()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Skipping test - services not available");
            return;
        }

        try
        {
            // SESSION 1: Track device fingerprint
            _output.WriteLine("🧪 DEBUG: Session 1 - Track device fingerprint");
            var page1 = await _context!.NewPageAsync();

            await page1.GotoAsync($"{BaseUrl}/");
            await page1.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Check if we're redirected to login page
            if (page1.Url.Contains("/login"))
            {
                _output.WriteLine("🔐 Redirected to login page, logging in...");
                await page1.FillAsync("input[type='password']", LoginPassword);
                await page1.PressAsync("input[type='password']", "Enter");
                await page1.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ Login completed, waiting for chat page...");

                // Wait for navigation to complete and page to reload
                await page1.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
                await page1.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ Navigation completed, waiting for chat components...");
            }

            // Wait for chat page to load with more robust waiting
            try
            {
                // First wait for the main chat container
                await page1.WaitForSelectorAsync(".chat-container", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ Chat container found, waiting for input field...");

                // Then wait for the chat input specifically
                await page1.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ Chat input found!");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Failed to find chat elements: {ex.Message}");
                _output.WriteLine($"Current URL: {page1.Url}");

                // Take a screenshot for debugging
                await page1.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-session1.png" });
                _output.WriteLine("📸 Screenshot saved as debug-session1.png");
                throw;
            }

            // Get device fingerprint from localStorage
            var deviceId1 = await page1.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            // Get cookies
            var cookies1 = await _context!.CookiesAsync();
            var deviceCookie1 = cookies1.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 1 - localStorage device ID: {deviceId1}");
            _output.WriteLine($"🔍 SESSION 1 - Cookie device ID: {deviceCookie1}");

            // Get device fingerprint after login
            var deviceId1AfterLogin = await page1.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            var cookies1AfterLogin = await _context!.CookiesAsync();
            var deviceCookie1AfterLogin = cookies1AfterLogin.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 1 AFTER LOGIN - localStorage device ID: {deviceId1AfterLogin}");
            _output.WriteLine($"🔍 SESSION 1 AFTER LOGIN - Cookie device ID: {deviceCookie1AfterLogin}");

            await page1.FillAsync("[data-testid='chat-input']", "Hej Isa, jag heter Anna Andersson");
            await page1.PressAsync("[data-testid='chat-input']", "Enter");

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
            await Task.Delay(1000);

            // SESSION 2: Track device fingerprint
            _output.WriteLine("🧪 DEBUG: Session 2 - Track device fingerprint");
            var page2 = await _context!.NewPageAsync();

            await page2.GotoAsync($"{BaseUrl}/");
            await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Check if we're redirected to login page
            if (page2.Url.Contains("/login"))
            {
                _output.WriteLine("🔐 SESSION 2: Redirected to login page, logging in...");
                await page2.FillAsync("input[type='password']", LoginPassword);
                await page2.PressAsync("input[type='password']", "Enter");
                await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ SESSION 2: Login completed, waiting for chat page...");

                // Wait for navigation to complete and page to reload
                await page2.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
                await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ SESSION 2: Navigation completed, waiting for chat components...");
            }

            // Wait for chat page to load with more robust waiting
            try
            {
                // First wait for the main chat container
                await page2.WaitForSelectorAsync(".chat-container", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ SESSION 2: Chat container found, waiting for input field...");

                // Then wait for the chat input specifically
                await page2.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ SESSION 2: Chat input found!");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ SESSION 2: Failed to find chat elements: {ex.Message}");
                _output.WriteLine($"SESSION 2: Current URL: {page2.Url}");

                // Take a screenshot for debugging
                await page2.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-session2.png" });
                _output.WriteLine("📸 SESSION 2: Screenshot saved as debug-session2.png");
                throw;
            }

            var deviceId2 = await page2.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            var cookies2 = await _context!.CookiesAsync();
            var deviceCookie2 = cookies2.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 2 - localStorage device ID: {deviceId2}");
            _output.WriteLine($"🔍 SESSION 2 - Cookie device ID: {deviceCookie2}");

            var deviceId2AfterLogin = await page2.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            var cookies2AfterLogin = await _context!.CookiesAsync();
            var deviceCookie2AfterLogin = cookies2AfterLogin.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 2 AFTER LOGIN - localStorage device ID: {deviceId2AfterLogin}");
            _output.WriteLine($"🔍 SESSION 2 AFTER LOGIN - Cookie device ID: {deviceCookie2AfterLogin}");

            await page2.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag Anna");
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
            await Task.Delay(1000);

            // SESSION 3: Track device fingerprint
            _output.WriteLine("🧪 DEBUG: Session 3 - Track device fingerprint");
            var page3 = await _context!.NewPageAsync();

            await page3.GotoAsync($"{BaseUrl}/");
            await page3.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Check if we're redirected to login page
            if (page3.Url.Contains("/login"))
            {
                _output.WriteLine("🔐 SESSION 3: Redirected to login page, logging in...");
                await page3.FillAsync("input[type='password']", LoginPassword);
                await page3.PressAsync("input[type='password']", "Enter");
                await page3.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ SESSION 3: Login completed, waiting for chat page...");

                // Wait for navigation to complete and page to reload
                await page3.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
                await page3.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ SESSION 3: Navigation completed, waiting for chat components...");
            }

            // Wait for chat page to load with more robust waiting
            try
            {
                // First wait for the main chat container
                await page3.WaitForSelectorAsync(".chat-container", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ SESSION 3: Chat container found, waiting for input field...");

                // Then wait for the chat input specifically
                await page3.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ SESSION 3: Chat input found!");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ SESSION 3: Failed to find chat elements: {ex.Message}");
                _output.WriteLine($"SESSION 3: Current URL: {page3.Url}");

                // Take a screenshot for debugging
                await page3.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-session3.png" });
                _output.WriteLine("📸 SESSION 3: Screenshot saved as debug-session3.png");
                throw;
            }

            var deviceId3 = await page3.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            var cookies3 = await _context!.CookiesAsync();
            var deviceCookie3 = cookies3.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 3 - localStorage device ID: {deviceId3}");
            _output.WriteLine($"🔍 SESSION 3 - Cookie device ID: {deviceCookie3}");

            var deviceId3AfterLogin = await page3.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            var cookies3AfterLogin = await _context!.CookiesAsync();
            var deviceCookie3AfterLogin = cookies3AfterLogin.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 3 AFTER LOGIN - localStorage device ID: {deviceId3AfterLogin}");
            _output.WriteLine($"🔍 SESSION 3 AFTER LOGIN - Cookie device ID: {deviceCookie3AfterLogin}");

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
            await Task.Delay(1000);

            // SESSION 4: Track device fingerprint
            _output.WriteLine("🧪 DEBUG: Session 4 - Track device fingerprint");
            var page4 = await _context!.NewPageAsync();

            await page4.GotoAsync($"{BaseUrl}/");
            await page4.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Check if we're redirected to login page
            if (page4.Url.Contains("/login"))
            {
                _output.WriteLine("🔐 SESSION 4: Redirected to login page, logging in...");
                await page4.FillAsync("input[type='password']", LoginPassword);
                await page4.PressAsync("input[type='password']", "Enter");
                await page4.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ SESSION 4: Login completed, waiting for chat page...");

                // Wait for navigation to complete and page to reload
                await page4.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
                await page4.WaitForLoadStateAsync(LoadState.NetworkIdle);
                _output.WriteLine("✅ SESSION 4: Navigation completed, waiting for chat components...");
            }

            // Wait for chat page to load with more robust waiting
            try
            {
                // First wait for the main chat container
                await page4.WaitForSelectorAsync(".chat-container", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ SESSION 4: Chat container found, waiting for input field...");

                // Then wait for the chat input specifically
                await page4.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = 15000 });
                _output.WriteLine("✅ SESSION 4: Chat input found!");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ SESSION 4: Failed to find chat elements: {ex.Message}");
                _output.WriteLine($"SESSION 4: Current URL: {page4.Url}");

                // Take a screenshot for debugging
                await page4.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-session4.png" });
                _output.WriteLine("📸 SESSION 4: Screenshot saved as debug-session4.png");
                throw;
            }

            var deviceId4 = await page4.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            var cookies4 = await _context!.CookiesAsync();
            var deviceCookie4 = cookies4.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 4 - localStorage device ID: {deviceId4}");
            _output.WriteLine($"🔍 SESSION 4 - Cookie device ID: {deviceCookie4}");

            var deviceId4AfterLogin = await page4.EvaluateAsync<string>(@"
                () => {
                    return localStorage.getItem('isa_did') || 'null';
                }
            ");

            var cookies4AfterLogin = await _context!.CookiesAsync();
            var deviceCookie4AfterLogin = cookies4AfterLogin.FirstOrDefault(c => c.Name == "isa_did")?.Value ?? "null";

            _output.WriteLine($"🔍 SESSION 4 AFTER LOGIN - localStorage device ID: {deviceId4AfterLogin}");
            _output.WriteLine($"🔍 SESSION 4 AFTER LOGIN - Cookie device ID: {deviceCookie4AfterLogin}");

            await page4.FillAsync("[data-testid='chat-input']", "Hej Isa, det är bara jag igen, vad heter jag i mellannamn");
            await page4.PressAsync("[data-testid='chat-input']", "Enter");

            await page4.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 10;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var finalResponse = await page4.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 SESSION 4 Final Response: {finalResponse}");

            await page4.CloseAsync();

            // ANALYSIS
            _output.WriteLine("\n📊 DEVICE FINGERPRINT ANALYSIS:");
            _output.WriteLine($"Session 1 localStorage: {deviceId1AfterLogin}");
            _output.WriteLine($"Session 2 localStorage: {deviceId2AfterLogin}");
            _output.WriteLine($"Session 3 localStorage: {deviceId3AfterLogin}");
            _output.WriteLine($"Session 4 localStorage: {deviceId4AfterLogin}");
            _output.WriteLine($"Session 1 cookie: {deviceCookie1AfterLogin}");
            _output.WriteLine($"Session 2 cookie: {deviceCookie2AfterLogin}");
            _output.WriteLine($"Session 3 cookie: {deviceCookie3AfterLogin}");
            _output.WriteLine($"Session 4 cookie: {deviceCookie4AfterLogin}");

            // Check if device fingerprints are consistent
            var allDeviceIds = new[] { deviceId1AfterLogin, deviceId2AfterLogin, deviceId3AfterLogin, deviceId4AfterLogin };
            var uniqueDeviceIds = allDeviceIds.Where(id => id != "null").Distinct().ToArray();

            _output.WriteLine($"\n🔍 Unique device IDs found: {uniqueDeviceIds.Length}");
            foreach (var id in uniqueDeviceIds)
            {
                _output.WriteLine($"  - {id}");
            }

            if (uniqueDeviceIds.Length == 1)
            {
                _output.WriteLine("✅ Device fingerprint is CONSISTENT across all sessions");
            }
            else
            {
                _output.WriteLine("❌ Device fingerprint is INCONSISTENT across sessions");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ DEBUG TEST FAILED: {ex.Message}");
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
