using Microsoft.Playwright;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace Agent.Tests.E2E;

/// <summary>
/// Diagnostic test to trace exactly what happens in the memory system
/// during a conversation where a name is stored and then retrieved.
/// </summary>
public class MemoryDiagnosticE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private readonly bool _servicesAvailable;
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "https://localhost:7041";
    private const string AgentUrl = "https://localhost:7042";
    private const string LoginPassword = "isa72";
    private const int TimeoutMs = 30000;

    public MemoryDiagnosticE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
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
        
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
    }

    private async Task<bool> CheckServicesAvailableAsync()
    {
        try
        {
            _output.WriteLine("🔍 Checking if Agent is available...");
            var agentResponse = await _httpClient.GetAsync($"{AgentUrl}/api/chat/health");
            if (!agentResponse.IsSuccessStatusCode)
            {
                _output.WriteLine($"❌ Agent not available: {agentResponse.StatusCode}");
                return false;
            }

            _output.WriteLine("🔍 Checking if WebChat is available...");
            var webChatResponse = await _httpClient.GetAsync($"{BaseUrl}/health");
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

        if (page.Url.Contains("/login"))
        {
            _output.WriteLine($"🔐 {sessionName}: Logging in...");
            await page.FillAsync("input[type='password']", LoginPassword);
            await page.PressAsync("input[type='password']", "Enter");
            await page.WaitForTimeoutAsync(3000);
            _output.WriteLine($"✅ {sessionName}: Login completed");
        }

        try
        {
            await page.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });
            _output.WriteLine($"✅ {sessionName}: Chat interface ready");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ {sessionName}: Failed to find chat interface: {ex.Message}");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = $"debug-{sessionName.ToLower().Replace(" ", "-")}.png" });
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

    private async Task<(string response, string? userId)> SendMessageViaApiAsync(string message, string deviceId, string sessionName)
    {
        _output.WriteLine($"💬 {sessionName}: Sending API message: '{message}'");

        var requestBody = new
        {
            message = message,
            sessionId = $"diagnostic-{sessionName.ToLower().Replace(" ", "-")}",
            deviceFingerprint = deviceId
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{AgentUrl}/api/chat", content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"❌ {sessionName}: API call failed: {response.StatusCode} - {responseText}");
            throw new Exception($"API call failed: {response.StatusCode}");
        }

        _output.WriteLine($"🔍 {sessionName}: Raw API Response: {responseText}");

        try
        {
            var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);

            // Try different possible response structures
            string assistantResponse = "";
            if (responseObj.TryGetProperty("response", out var responseProp))
            {
                assistantResponse = responseProp.GetString() ?? "";
            }
            else if (responseObj.TryGetProperty("message", out var messageProp))
            {
                assistantResponse = messageProp.GetString() ?? "";
            }
            else if (responseObj.TryGetProperty("content", out var contentProp))
            {
                assistantResponse = contentProp.GetString() ?? "";
            }
            else
            {
                // If it's just a string response
                assistantResponse = responseObj.GetString() ?? responseText;
            }

            _output.WriteLine($"🤖 {sessionName}: API Response: '{assistantResponse}'");

            // Also get user info to check user ID consistency
            var userInfoResponse = await GetUserInfoAsync(deviceId, sessionName);

            return (assistantResponse, userInfoResponse);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ {sessionName}: Failed to parse JSON response: {ex.Message}");
            _output.WriteLine($"🔍 {sessionName}: Treating as plain text: '{responseText}'");
            return (responseText, null);
        }
    }

    private async Task<string?> GetUserInfoAsync(string deviceId, string sessionName)
    {
        try
        {
            var userResponse = await _httpClient.GetAsync($"{AgentUrl}/api/memory/user?deviceFingerprint={Uri.EscapeDataString(deviceId)}");
            if (userResponse.IsSuccessStatusCode)
            {
                var userJson = await userResponse.Content.ReadAsStringAsync();
                var userObj = JsonSerializer.Deserialize<JsonElement>(userJson);

                if (userObj.TryGetProperty("userId", out var userIdProp))
                {
                    var userId = userIdProp.GetString();
                    _output.WriteLine($"👤 {sessionName}: User ID = {userId}");
                    return userId;
                }
            }
            else
            {
                _output.WriteLine($"⚠️ {sessionName}: Failed to get user info: {userResponse.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ {sessionName}: Error getting user info: {ex.Message}");
        }

        return null;
    }

    private async Task<string> SendMessageViaBrowserAsync(IPage page, string message, string sessionName)
    {
        _output.WriteLine($"💬 {sessionName}: Sending browser message: '{message}'");
        
        await page.FillAsync("[data-testid='chat-input']", message);
        await page.PressAsync("[data-testid='chat-input']", "Enter");

        // Wait for response
        await page.WaitForFunctionAsync(@"
            () => {
                const messages = document.querySelectorAll('.message.agent-message');
                const lastMessage = messages[messages.length - 1];
                return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 10;
            }
        ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

        var response = await page.TextContentAsync(".message.agent-message:last-child");
        _output.WriteLine($"🤖 {sessionName}: Browser Response: '{response}'");
        
        return response ?? "";
    }

    [Fact]
    public async Task Memory_Diagnostic_Should_TraceCompleteFlow()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Skipping test - services not available");
            return;
        }

        try
        {
            _output.WriteLine("\n🔬 MEMORY DIAGNOSTIC TEST - COMPLETE FLOW TRACE");
            _output.WriteLine("=================================================");

            // STEP 1: Get device fingerprint
            _output.WriteLine("\n📱 STEP 1: Getting device fingerprint");
            var page1 = await CreateAndLoginPageAsync("STEP 1");
            var deviceId = await GetDeviceIdAsync(page1, "STEP 1");
            await page1.CloseAsync();

            deviceId.Should().NotBe("null", "Device ID should be generated");
            _output.WriteLine($"✅ Device ID obtained: {deviceId}");

            // STEP 2: Store name via API (direct to Agent)
            _output.WriteLine("\n💾 STEP 2: Storing name via API");
            var (apiResponse1, userId1) = await SendMessageViaApiAsync("Hej Isa, jag heter Anna Andersson", deviceId, "STEP 2");
            apiResponse1.Should().Contain("Anna", "API should acknowledge the name");
            _output.WriteLine($"✅ Name stored via API successfully");

            // STEP 3: Test retrieval via API
            _output.WriteLine("\n🔍 STEP 3: Testing retrieval via API");
            var (apiResponse2, userId2) = await SendMessageViaApiAsync("Hej Isa, vad heter jag?", deviceId, "STEP 3");
            _output.WriteLine($"📊 API retrieval result: {apiResponse2}");

            // STEP 4: Test retrieval via Browser (WebChat)
            _output.WriteLine("\n🌐 STEP 4: Testing retrieval via Browser");
            var page2 = await CreateAndLoginPageAsync("STEP 4");
            var deviceId2 = await GetDeviceIdAsync(page2, "STEP 4");
            
            deviceId2.Should().Be(deviceId, "Device ID should be consistent");
            
            var browserResponse = await SendMessageViaBrowserAsync(page2, "Hej Isa, vad heter jag?", "STEP 4");
            _output.WriteLine($"📊 Browser retrieval result: {browserResponse}");
            await page2.CloseAsync();

            // ANALYSIS
            _output.WriteLine("\n📊 DIAGNOSTIC ANALYSIS:");
            _output.WriteLine("=======================");
            _output.WriteLine($"Device ID: {deviceId}");
            _output.WriteLine($"User ID 1 (Store): {userId1 ?? "null"}");
            _output.WriteLine($"User ID 2 (Retrieve): {userId2 ?? "null"}");
            _output.WriteLine($"API Store Response: {apiResponse1}");
            _output.WriteLine($"API Retrieve Response: {apiResponse2}");
            _output.WriteLine($"Browser Retrieve Response: {browserResponse}");

            // Check user ID consistency
            if (userId1 != null && userId2 != null)
            {
                if (userId1 == userId2)
                {
                    _output.WriteLine("✅ USER ID CONSISTENCY - Same user across sessions");
                }
                else
                {
                    _output.WriteLine("❌ USER ID INCONSISTENCY - Different users across sessions!");
                    _output.WriteLine("🔍 Issue: Device fingerprint not mapping to same user");
                }
            }

            // Check if API retrieval works
            if (apiResponse2.Contains("Anna"))
            {
                _output.WriteLine("✅ API MEMORY RETRIEVAL WORKS - Memory system is functional");

                if (browserResponse.Contains("Anna"))
                {
                    _output.WriteLine("✅ BROWSER MEMORY RETRIEVAL WORKS - Complete success!");
                }
                else
                {
                    _output.WriteLine("❌ BROWSER MEMORY RETRIEVAL FAILS - Problem in WebChat integration");
                    _output.WriteLine("🔍 Issue: API works but WebChat doesn't - device fingerprint flow problem");
                }
            }
            else
            {
                _output.WriteLine("❌ API MEMORY RETRIEVAL FAILS - Core memory system problem");
                _output.WriteLine("🔍 Issue: Memory storage or retrieval is broken at the core level");
            }

            // For diagnostic purposes, don't fail the test - just gather information
            _output.WriteLine("\n🎯 Diagnostic test completed - check logs for detailed analysis");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ DIAGNOSTIC TEST FAILED: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
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
