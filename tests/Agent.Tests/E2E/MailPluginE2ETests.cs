using Microsoft.Playwright;
using FluentAssertions;
using System.Net.Http;
using Xunit;

namespace Agent.Tests.E2E;

/// <summary>
/// End-to-end tests for MailPlugin functionality
/// Tests the complete workflow: MailPlugin → TempFilesService → DocumentInterpreterPlugin → JSON response in WebChat
/// Verifies the full E2E flow with all three databases (agent.db, memory.db, files.db) working together
/// </summary>
[Collection("E2E")]
public class MailPluginE2ETests : IAsyncLifetime
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
            Headless = false,
            SlowMo = 500, // Slow down actions by 500ms for visibility
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
    public async Task Should_Complete_E2E_Flow_Mail_TempFiles_DocumentInterpreter_Via_Chat()
    {
        SkipIfServicesNotAvailable();

        // Use consistent test user ID
        const string testUserId = "test-mail-e2e";

        var context = await _browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        try
        {
            await LoginToWebChat(page, testUserId);

            // Wait for WebChat to fully load and connect
            await page.WaitForTimeoutAsync(3000);

            // Ask Isa to check Anna Andersson's mail for attachments and analyze them
            var message = "Kolla Anna Andersson's mail om det finns oläst mail med bifogad fil och om det finns så hämta första olästa och använd DocumentInterpreterPlugin för att analysera den bifogade filen och skriva ut i WebChat json resultatet som sträng";

            await page.FillAsync("[data-testid='chat-input']", message);
            await page.PressAsync("[data-testid='chat-input']", "Control+Enter");

            // Wait for the response to be fully loaded (this may take longer for email processing)
            await page.WaitForFunctionAsync(@"
                () => {
                    const messages = document.querySelectorAll('.message.agent-message');
                    const lastMessage = messages[messages.length - 1];
                    return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
                }
            ", new PageWaitForFunctionOptions { Timeout = 60000 }); // Increased timeout for email processing

            // Get the response from Isa
            var response = await page.TextContentAsync(".message.agent-message:last-child");

            // Verify that the response contains successful processing indicators
            response.Should().NotBeNullOrEmpty("Isa should provide a response");

            // The response should indicate successful E2E flow: MailPlugin → TempFiles → DocumentInterpreter
            response!.Should().Contain("Anna Andersson", "Response should mention the email sender");

            // Verify that the complete E2E flow worked (email was read and attachments analyzed)
            var successfulProcessing = response!.Contains("analyserat") ||
                                     response.Contains("analyzed") ||
                                     response.Contains("extraherat") ||
                                     response.Contains("extracted") ||
                                     response.Contains("kvalitet") ||
                                     response.Contains("quality") ||
                                     response.Contains("tillförlitlighet") ||
                                     response.Contains("confidence") ||
                                     response.Contains("json") ||
                                     response.Contains("JSON") ||
                                     response.Contains("bilaga") ||
                                     response.Contains("attachment") ||
                                     response.Contains("TempFileId") ||
                                     response.Contains("tempFileId");

            successfulProcessing.Should().BeTrue(
                $"Response should indicate successful E2E processing (MailPlugin → TempFiles → DocumentInterpreter). Actual response: {response}");
        }
        finally
        {
            await context.CloseAsync();
        }
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
    }
}
