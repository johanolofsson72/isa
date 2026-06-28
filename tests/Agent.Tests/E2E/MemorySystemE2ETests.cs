using Microsoft.Playwright;
using FluentAssertions;
using System.Net.Http;
using Xunit;

namespace Agent.Tests.E2E;

[Collection("E2E")]
public class MemorySystemE2ETests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7041"; // WebChat URL
    private const string AgentUrl = "https://localhost:7042"; // Agent API URL
    private const int TimeoutMs = 30000;
    private const string LoginPassword = "isa72";
    private static bool _servicesAvailable = false;
    private static bool _servicesChecked = false;

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
            // Skip Playwright initialization if services aren't available
            return;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 500, // Slow down actions by 1 second for visibility 1000
            Args = new[] { "--ignore-certificate-errors", "--ignore-ssl-errors" }
        });
    }

    private static async Task<bool> CheckServicesAvailableAsync()
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            // Check WebChat
            var webChatResponse = await httpClient.GetAsync(BaseUrl);

            // Check Agent API
            var agentResponse = await httpClient.GetAsync($"{AgentUrl}/health");

            return webChatResponse.IsSuccessStatusCode && agentResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private void SkipIfServicesNotAvailable()
    {
        if (!_servicesAvailable)
        {
            throw new InvalidOperationException("WebChat and Agent services are not running. Start both applications to run E2E tests.");
        }
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task Test1_Anna_TeachesIronManSecret_IsaRemembersAcrossSessions()
    {
        SkipIfServicesNotAvailable();

        // Use consistent test user ID for Anna across both sessions
        const string annaUserId = "test-anna-ironman-e2e";

        // Session 1: Anna introduces himself and shares the Iron Man secret
        var context1 = await _browser!.NewContextAsync();
        var page1 = await context1.NewPageAsync();
        
        try
        {
            await LoginToWebChat(page1, annaUserId);

            // Wait for WebChat to fully load and connect
            await page1.WaitForTimeoutAsync(3000);

            // Anna introduces himself
            await page1.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag Anna Andersson");
            await page1.PressAsync("[data-testid='chat-input']", "Control+Enter");

            // Wait for Isa's response
            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response1 = await page1.TextContentAsync(".message.agent-message:last-child");
            response1.Should().ContainAny("Hej Anna", "Hej", "Anna");

            // Wait before sending next message
            await page1.WaitForTimeoutAsync(2000);

            // Anna shares his Iron Man secret
            await page1.FillAsync("[data-testid='chat-input']", "Jag ska berätta en sak för dej som du ska komma ihåg och det är att jag egentligen är Iron Man.");
            await page1.PressAsync("[data-testid='chat-input']", "Enter");

            // Wait for Isa's response
            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response2 = await page1.TextContentAsync(".message.agent-message:last-child");
            response2.Should().ContainAny("komma ihåg", "kommer ihåg", "förstår", "noter");
        }
        finally
        {
            await context1.CloseAsync();
        }

        // Session 2: New Playwright instance - Anna asks if Isa remembers
        var context2 = await _browser.NewContextAsync();
        var page2 = await context2.NewPageAsync();
        
        try
        {
            await LoginToWebChat(page2, annaUserId); // Same user ID as session 1

            // Wait for WebChat to fully load and connect
            await page2.WaitForTimeoutAsync(3000);

            // Anna asks if Isa remembers who he really is
            await page2.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag Anna igen, kom jag ihåg att berätta för dej vem jag egentligen är?");
            await page2.PressAsync("[data-testid='chat-input']", "Enter");

            // Wait for Isa's response
            await page2.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response = await page2.TextContentAsync(".message.agent-message:last-child");
            response.Should().ContainAny("Iron Man", "Iron-Man", "IronMan");
        }
        finally
        {
            await context2.CloseAsync();
        }
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task Test2_David_TeachesPatorp_IsaRemembersAcrossSessions()
    {
        SkipIfServicesNotAvailable();

        // Use consistent test user ID for David across both sessions
        const string davidUserId = "test-david-patorp-e2e";

        // Session 1: David introduces himself and shares where he lived as a child
        var context1 = await _browser!.NewContextAsync();
        var page1 = await context1.NewPageAsync();
        
        try
        {
            await LoginToWebChat(page1, davidUserId);

            // Wait for WebChat to fully load and connect
            await page1.WaitForTimeoutAsync(3000);

            // David introduces himself
            await page1.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag David Olandersson");
            await page1.PressAsync("[data-testid='chat-input']", "Control+Enter");

            // Wait for Isa's response
            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response1 = await page1.TextContentAsync(".message.agent-message:last-child");
            response1.Should().ContainAny("Hej David", "Hej", "David");

            // Wait before sending next message
            await page1.WaitForTimeoutAsync(2000);

            // David shares where he lived as a child
            await page1.FillAsync("[data-testid='chat-input']", "Jag ska berätta en sak för dej som du ska komma ihåg och det är att jag bodde i Påtorp när jag var liten.");
            await page1.PressAsync("[data-testid='chat-input']", "Control+Enter");

            // Wait for Isa's response
            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response2 = await page1.TextContentAsync(".message.agent-message:last-child");
            response2.Should().ContainAny("komma ihåg", "kommer ihåg", "förstår", "noter");
        }
        finally
        {
            await context1.CloseAsync();
        }

        // Session 2: New Playwright instance - David asks if Isa remembers
        var context2 = await _browser.NewContextAsync();
        var page2 = await context2.NewPageAsync();
        
        try
        {
            await LoginToWebChat(page2, davidUserId); // Same user ID as session 1

            // Wait for WebChat to fully load and connect
            await page2.WaitForTimeoutAsync(3000);

            // David asks if Isa remembers where he lived as a child
            await page2.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag David igen, kom jag ihåg att berätta för dej var jag bodde när jag var liten?");
            await page2.PressAsync("[data-testid='chat-input']", "Enter");

            // Wait for Isa's response
            await page2.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response = await page2.TextContentAsync(".message.agent-message:last-child");
            response.Should().Contain("Påtorp");
        }
        finally
        {
            await context2.CloseAsync();
        }
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task Test3_Anna_SharesSecretGarestad_David_CannotAccess()
    {
        SkipIfServicesNotAvailable();

        // Use consistent test user IDs
        const string annaUserId = "test-anna-garestad-secret-e2e";
        const string davidUserId = "test-david-garestad-ask-e2e";

        // Session 1: Anna introduces himself and shares his secret about living in Gärestad
        var context1 = await _browser!.NewContextAsync();
        var page1 = await context1.NewPageAsync();
        
        try
        {
            await LoginToWebChat(page1, annaUserId);

            // Wait for WebChat to fully load and connect
            await page1.WaitForTimeoutAsync(3000);

            // Anna introduces himself
            await page1.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag Anna Andersson");
            await page1.PressAsync("[data-testid='chat-input']", "Control+Enter");

            // Wait for Isa's response
            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response1 = await page1.TextContentAsync(".message.agent-message:last-child");
            response1.Should().ContainAny("Hej Anna", "Hej", "Anna");

            // Wait before sending next message
            await page1.WaitForTimeoutAsync(2000);

            // Anna shares his secret about where he lived as a child
            await page1.FillAsync("[data-testid='chat-input']", "Jag ska berätta en hemlighet för dej som du ska komma ihåg och det är att jag bodde i Gärestad när jag var liten.");
            await page1.PressAsync("[data-testid='chat-input']", "Enter");

            // Wait for Isa's response
            await page1.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response2 = await page1.TextContentAsync(".message.agent-message:last-child");
            response2.Should().ContainAny("hemlighet", "hemligt", "inte berätta", "privat", "hålla");
        }
        finally
        {
            await context1.CloseAsync();
        }

        // Session 2: New Playwright instance - Anna asks if Isa remembers his secret
        var context2 = await _browser.NewContextAsync();
        var page2 = await context2.NewPageAsync();
        
        try
        {
            await LoginToWebChat(page2, annaUserId); // Same user ID as session 1

            // Wait for WebChat to fully load and connect
            await page2.WaitForTimeoutAsync(3000);

            // Anna asks if Isa remembers where he lived as a child
            await page2.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag Anna igen, kom jag ihåg att berätta för dej var jag bodde när jag var liten?");
            await page2.PressAsync("[data-testid='chat-input']", "Enter");

            // Wait for Isa's response
            await page2.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response = await page2.TextContentAsync(".message.agent-message:last-child");
            response.Should().Contain("Gärestad");
        }
        finally
        {
            await context2.CloseAsync();
        }

        // Session 3: New Playwright instance - David asks about Anna's secret (should be denied)
        var context3 = await _browser.NewContextAsync();
        var page3 = await context3.NewPageAsync();
        
        try
        {
            await LoginToWebChat(page3, davidUserId); // Different user ID for David

            // Wait for WebChat to fully load and connect
            await page3.WaitForTimeoutAsync(3000);

            // David introduces himself
            await page3.FillAsync("[data-testid='chat-input']", "Hej Isa, det är jag David igen, vet du var Anna bodde när han var liten?");
            await page3.PressAsync("[data-testid='chat-input']", "Enter");

            // Wait for Isa's response
            await page3.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

            var response = await page3.TextContentAsync(".message.agent-message:last-child");
            // Isa should refuse to share the secret information
            response.Should().ContainAny("vet inte", "vet jag inte", "inte en råtta", "privat", "hemligt");
            response.Should().NotContain("Gärestad");
        }
        finally
        {
            await context3.CloseAsync();
        }
    }

    private async Task WaitForChatResponse(IPage page, int messageIndex = 1)
    {
        await page.WaitForSelectorAsync($".message.agent-message:nth-child({messageIndex * 2})", new() { Timeout = TimeoutMs });
        await Task.Delay(1000); // Allow for any animations or additional processing
    }

    private async Task LoginToWebChat(IPage page, string? testUserId = null)
    {
        // Set test user ID header if provided (for deterministic E2E testing)
        if (!string.IsNullOrEmpty(testUserId))
        {
            await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                ["X-Test-User-Id"] = testUserId
            });
        }

        // Clear session storage to force login
        await page.GotoAsync(BaseUrl);
        await page.EvaluateAsync("() => sessionStorage.clear()");

        // Navigate to login page explicitly
        await page.GotoAsync($"{BaseUrl}/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for password input to be visible
        await page.WaitForSelectorAsync("input[type='password']", new() { Timeout = TimeoutMs });

        // Fill in password and login
        await page.FillAsync("input[type='password']", LoginPassword);
        await page.ClickAsync("button:has-text('Logga in')");

        // Wait for login to complete - check for chat interface instead of URL change
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for chat to be ready (this indicates successful login)
        // Try multiple selectors in case of JavaScript issues
        try
        {
            await page.WaitForSelectorAsync("[data-testid='chat-input']", new() { Timeout = TimeoutMs });
        }
        catch (TimeoutException)
        {
            // Fallback: try to find the input by class name
            await page.WaitForSelectorAsync(".message-input", new() { Timeout = TimeoutMs });
        }

        // Wait for SignalR connection to establish
        await page.WaitForTimeoutAsync(3000);

        // Verify send button exists
        //await page.WaitForSelectorAsync("[data-testid='send-button']", new() { Timeout = TimeoutMs });
    }

    private async Task SendMessage(IPage page, string message)
    {
        // Type the message - try multiple selectors
        try
        {
            await page.FillAsync("[data-testid='chat-input']", message);
        }
        catch (TimeoutException)
        {
            // Fallback: try to find the input by class name
            await page.FillAsync(".message-input", message);
        }

        // Send message using Ctrl+Enter key (more reliable than clicking disabled button)
        await page.PressAsync("[data-testid='chat-input']", "Control+Enter");

        // Wait for the message to appear in chat
        await page.WaitForSelectorAsync($"text={message}", new() { Timeout = TimeoutMs });
    }
}
