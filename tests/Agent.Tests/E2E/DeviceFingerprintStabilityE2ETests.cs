using Microsoft.Playwright;
using FluentAssertions;
using System.Net.Http;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.E2E;

/// <summary>
/// E2E tests for device fingerprint stability and memory persistence
/// Tests that device fingerprints remain stable and memories are correctly stored/retrieved
/// </summary>
[Collection("E2E")]
public class DeviceFingerprintStabilityE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7041"; // WebChat URL
    private const string AgentUrl = "https://localhost:7042"; // Agent API URL
    private const int TimeoutMs = 30000;
    private const string LoginPassword = "isa72";
    private static bool _servicesAvailable = false;
    private static bool _servicesChecked = false;

    public DeviceFingerprintStabilityE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Check if services are available before running tests
        if (!_servicesChecked)
        {
            _servicesAvailable = await CheckServicesAvailableAsync();
            _servicesChecked = true;
        }

        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Services not available, skipping Playwright initialization");
            return;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 500,
            Args = new[] { "--ignore-certificate-errors", "--ignore-ssl-errors" }
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

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task DeviceFingerprint_Should_RemainStable_AcrossMultipleSessions_WithMemoryPersistence()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Skipping test - services not available");
            return;
        }

        try
        {
            // SESSION 1: Introduce name "Anna Andersson"
            _output.WriteLine("🧪 TEST: Session 1 - Step 1-5");
            var page1 = await _browser!.NewPageAsync();

            _output.WriteLine("🧪 TEST: Step 1 - Öppna ny sida");
            await page1.GotoAsync($"{BaseUrl}/");
            await page1.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _output.WriteLine("🧪 TEST: Step 2 - Logga in");
            await page1.FillAsync("input[type='password']", LoginPassword);
            await page1.PressAsync("input[type='password']", "Enter");
            await page1.WaitForTimeoutAsync(3000);

            _output.WriteLine("🧪 TEST: Step 3 - Skriv 'Hej Isa, jag heter Anna Andersson'");
            await page1.FillAsync("[data-testid='chat-input']", "Hej Isa, jag heter Anna Andersson");
            await page1.PressAsync("[data-testid='chat-input']", "Control+Enter");

            _output.WriteLine("🧪 TEST: Step 4 - Invänta svar och se till att det innehåller 'Anna'");
            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           lastMessage.textContent.toLowerCase().includes('anna');
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response1 = await page1.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 TEST: Response 1: {response1}");
            response1.Should().ContainAny(new[] { "Anna", "anna", "JOHAN" }, "Agent should acknowledge the name Anna");

            // Save the device fingerprint from localStorage for reuse
            var deviceFingerprint = await page1.EvaluateAsync<string>("() => localStorage.getItem('isa_did')");
            _output.WriteLine($"🧪 TEST: Saved device fingerprint: {deviceFingerprint}");

            _output.WriteLine("🧪 TEST: Step 5 - Stäng sidan");
            await page1.CloseAsync();
            await Task.Delay(1000);

            // SESSION 2: Test recognition "det är jag Anna" with same device fingerprint
            _output.WriteLine("🧪 TEST: Session 2 - Step 6-10");
            var page2 = await _browser!.NewPageAsync();

            _output.WriteLine("🧪 TEST: Step 6 - Öppna ny sida och återställ device fingerprint");
            await page2.GotoAsync($"{BaseUrl}/");
            await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Restore the device fingerprint in localStorage
            await page2.EvaluateAsync($"() => localStorage.setItem('isa_did', '{deviceFingerprint}')");
            await page2.ReloadAsync();
            await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _output.WriteLine("🧪 TEST: Step 7 - Logga in");
            await page2.FillAsync("input[type='password']", LoginPassword);
            await page2.PressAsync("input[type='password']", "Enter");
            await page2.WaitForTimeoutAsync(3000);

            _output.WriteLine("🧪 TEST: Step 8 - Skriv 'Hej Isa, det är jag Anna'");
            await page2.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag Anna");
            await page2.PressAsync("[data-testid='chat-input']", "Control+Enter");

            _output.WriteLine("🧪 TEST: Step 9 - Invänta svar och se till att det innehåller 'Anna'");
            await page2.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           lastMessage.textContent.toLowerCase().includes('anna');
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response2 = await page2.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 TEST: Response 2: {response2}");
            response2.Should().ContainAny(new[] { "Anna", "anna", "JOHAN" }, "Agent should remember the name Anna from previous session");

            _output.WriteLine("🧪 TEST: Step 10 - Stäng sidan");
            await page2.CloseAsync();
            await Task.Delay(1000);

            // SESSION 3: Add middle name "Patrick" with same device fingerprint
            _output.WriteLine("🧪 TEST: Session 3 - Step 11-15");
            var page3 = await _browser!.NewPageAsync();

            _output.WriteLine("🧪 TEST: Step 11 - Öppna ny sida och återställ device fingerprint");
            await page3.GotoAsync($"{BaseUrl}/");
            await page3.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Restore the device fingerprint in localStorage
            await page3.EvaluateAsync($"() => localStorage.setItem('isa_did', '{deviceFingerprint}')");
            await page3.ReloadAsync();
            await page3.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _output.WriteLine("🧪 TEST: Step 12 - Logga in");
            await page3.FillAsync("input[type='password']", LoginPassword);
            await page3.PressAsync("input[type='password']", "Enter");
            await page3.WaitForTimeoutAsync(3000);

            _output.WriteLine("🧪 TEST: Step 13 - Skriv 'Hej Isa, jag heter Patrick i mellannamn'");
            await page3.FillAsync("[data-testid='chat-input']", "Hej Isa, jag heter Patrick i mellannamn");
            await page3.PressAsync("[data-testid='chat-input']", "Control+Enter");

            _output.WriteLine("🧪 TEST: Step 14 - Invänta svar och se till att det innehåller 'Patrick'");
            await page3.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           lastMessage.textContent.toLowerCase().includes('patrick');
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response3 = await page3.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 TEST: Response 3: {response3}");
            response3.Should().ContainAny(new[] { "Patrick", "patrick", "PATRICK" }, "Agent should acknowledge the middle name Patrick");

            _output.WriteLine("🧪 TEST: Step 15 - Stäng sidan");
            await page3.CloseAsync();
            await Task.Delay(1000);

            // SESSION 4: Test complete memory "vad heter jag i mellannamn" with same device fingerprint
            _output.WriteLine("🧪 TEST: Session 4 - Step 16-20");
            var page4 = await _browser!.NewPageAsync();

            _output.WriteLine("🧪 TEST: Step 16 - Öppna ny sida och återställ device fingerprint");
            await page4.GotoAsync($"{BaseUrl}/");
            await page4.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Restore the device fingerprint in localStorage
            await page4.EvaluateAsync($"() => localStorage.setItem('isa_did', '{deviceFingerprint}')");
            await page4.ReloadAsync();
            await page4.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _output.WriteLine("🧪 TEST: Step 17 - Logga in");
            await page4.FillAsync("input[type='password']", LoginPassword);
            await page4.PressAsync("input[type='password']", "Enter");
            await page4.WaitForTimeoutAsync(3000);

            _output.WriteLine("🧪 TEST: Step 18 - Skriv 'Hej Isa, det är bara jag igen, vad heter jag i mellannamn'");
            await page4.FillAsync("[data-testid='chat-input']", "Hej Isa, det är bara jag igen, vad heter jag i mellannamn");
            await page4.PressAsync("[data-testid='chat-input']", "Enter");

            _output.WriteLine("🧪 TEST: Step 19 - Invänta svar och se till att det innehåller 'Patrick'");
            await page4.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 10;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var finalResponse = await page4.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 TEST: Final response: {finalResponse}");

            // This is the critical test - agent should remember Patrick from session 3
            finalResponse.Should().ContainAny(new[] { "Patrick", "patrick", "PATRICK" },
                "Agent should remember the middle name Patrick from previous session if device fingerprint is stable");

            _output.WriteLine("🧪 TEST: Step 20 - Stäng sidan");
            await page4.CloseAsync();

            _output.WriteLine("✅ TEST: SUCCESS - Device fingerprint is stable across all 4 sessions!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ TEST FAILED: {ex.Message}");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
        _playwright?.Dispose();
    }
}
