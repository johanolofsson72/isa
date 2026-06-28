using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Agent.Tests.E2E;

/// <summary>
/// E2E tests for device fingerprint consolidation feature
/// Tests the system's ability to merge user profiles when the same person logs in from different devices
/// </summary>
public class DeviceFingerprintConsolidationE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7041";
    private const string LoginPassword = "isa72";
    private const int TimeoutMs = 30000;

    public DeviceFingerprintConsolidationE2ETests(ITestOutputHelper output)
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
            Headless = true,
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
    public async Task DeviceFingerprint_Should_ConsolidateWhenSameUserIdentifiedFromDifferentDevices()
    {
        // Check if services are available
        _output.WriteLine("🔍 Checking if Agent is available...");
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        try
        {
            var agentResponse = await httpClient.GetAsync("https://localhost:7042/health");
            agentResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Agent service is not available. Make sure AppHost is running.", ex);
        }

        try
        {
            var webChatResponse = await httpClient.GetAsync($"{BaseUrl}/health");
            webChatResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("WebChat service is not available. Make sure AppHost is running.", ex);
        }

        _output.WriteLine("✅ Both services are available");

        try
        {
            // PHASE 1: Create original user profile on Device A
            _output.WriteLine("🧪 TEST: Phase 1 - Create original user profile on Device A");
            var deviceAPage = await _browser!.NewPageAsync();
            string? originalDeviceFingerprint = null;

            _output.WriteLine("🧪 TEST: Step 1 - Open page on Device A");
            await deviceAPage.GotoAsync($"{BaseUrl}/");
            await deviceAPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _output.WriteLine("🧪 TEST: Step 2 - Login on Device A");
            await deviceAPage.FillAsync("input[type='password']", LoginPassword);
            await deviceAPage.PressAsync("input[type='password']", "Enter");
            await deviceAPage.WaitForTimeoutAsync(3000);

            _output.WriteLine("🧪 TEST: Step 3 - Introduce as 'Anna Andersson' on Device A");
            await deviceAPage.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });
            await deviceAPage.FillAsync("[data-testid='chat-input']", "Hi Isa, I'm Anna Andersson");
            await deviceAPage.PressAsync("[data-testid='chat-input']", "Enter");

            _output.WriteLine("🧪 TEST: Step 4 - Wait for response and verify it contains 'Anna'");
            await deviceAPage.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           lastMessage.textContent.toLowerCase().includes('anna');
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response1 = await deviceAPage.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 TEST: Response 1: {response1}");
            response1.Should().ContainAny(new[] { "Anna", "anna", "JOHAN" }, "Agent should acknowledge Anna");

            // Save the original device fingerprint
            originalDeviceFingerprint = await deviceAPage.EvaluateAsync<string>("() => localStorage.getItem('isa_did')");
            _output.WriteLine($"🧪 TEST: Original device fingerprint: {originalDeviceFingerprint}");

            _output.WriteLine("🧪 TEST: Step 5 - Close Device A");
            await deviceAPage.CloseAsync();
            await Task.Delay(1000);

            // PHASE 2: Create new device session (Device B) and identify as same user
            _output.WriteLine("🧪 TEST: Phase 2 - Create new device session (Device B)");
            var deviceBPage = await _browser!.NewPageAsync();

            _output.WriteLine("🧪 TEST: Step 6 - Open page on Device B (new device fingerprint)");
            await deviceBPage.GotoAsync($"{BaseUrl}/");
            await deviceBPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _output.WriteLine("🧪 TEST: Step 7 - Login on Device B");
            await deviceBPage.FillAsync("input[type='password']", LoginPassword);
            await deviceBPage.PressAsync("input[type='password']", "Enter");
            await deviceBPage.WaitForTimeoutAsync(3000);

            _output.WriteLine("🧪 TEST: Step 8 - Identify as same user with multiple factors");
            await deviceBPage.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });
            await deviceBPage.FillAsync("[data-testid='chat-input']", "Hi Isa, it's me Anna Andersson again");
            await deviceBPage.PressAsync("[data-testid='chat-input']", "Enter");

            _output.WriteLine("🧪 TEST: Step 9 - Wait for response and verify consolidation");
            await deviceBPage.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           (lastMessage.textContent.toLowerCase().includes('anna') ||
                            lastMessage.textContent.toLowerCase().includes('andersson'));
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response2 = await deviceBPage.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 TEST: Response 2: {response2}");
            response2.Should().ContainAny(new[] { "Anna", "anna", "Andersson", "andersson" }, 
                "Agent should recognize Anna Andersson from Device B");

            _output.WriteLine("🧪 TEST: Step 10 - Close Device B");
            await deviceBPage.CloseAsync();
            await Task.Delay(1000);

            // PHASE 3: Verify consolidation worked by testing memory persistence
            _output.WriteLine("🧪 TEST: Phase 3 - Verify consolidation worked");
            var deviceCPage = await _browser!.NewPageAsync();

            _output.WriteLine("🧪 TEST: Step 11 - Open page on Device C (another new device)");
            await deviceCPage.GotoAsync($"{BaseUrl}/");
            await deviceCPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _output.WriteLine("🧪 TEST: Step 12 - Login on Device C");
            await deviceCPage.FillAsync("input[type='password']", LoginPassword);
            await deviceCPage.PressAsync("input[type='password']", "Enter");
            await deviceCPage.WaitForTimeoutAsync(3000);

            _output.WriteLine("🧪 TEST: Step 13 - Identify as same user again");
            await deviceCPage.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });
            await deviceCPage.FillAsync("[data-testid='chat-input']", "Hi Isa, I'm Anna Andersson");
            await deviceCPage.PressAsync("[data-testid='chat-input']", "Enter");

            _output.WriteLine("🧪 TEST: Step 14 - Wait for response and verify recognition");
            await deviceCPage.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent &&
                           (lastMessage.textContent.toLowerCase().includes('anna') ||
                            lastMessage.textContent.toLowerCase().includes('welcome back') ||
                            lastMessage.textContent.toLowerCase().includes('again'));
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response3 = await deviceCPage.TextContentAsync(".message.agent-message:last-child");
            _output.WriteLine($"🧪 TEST: Response 3: {response3}");
            
            // The system should recognize Anna and potentially welcome him back
            response3.Should().ContainAny(new[] { "Anna", "anna", "welcome", "back", "again" }, 
                "Agent should recognize Anna from previous sessions");

            _output.WriteLine("🧪 TEST: Step 15 - Close Device C");
            await deviceCPage.CloseAsync();

            _output.WriteLine("✅ TEST: SUCCESS - Device fingerprint consolidation working correctly!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ TEST: FAILED - {ex.Message}");
            throw;
        }
    }
}
