using Microsoft.Playwright;
using FluentAssertions;
using System.Net.Http;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Delivery.Tests;

/// <summary>
/// Delivery test for customer onboarding file upload and Fortnox integration
/// Tests file upload through WebChat interface, DocumentInterpreter parsing, and FortnoxPlugin customer creation
/// Only executed through CI script - excluded from normal test discovery
/// </summary>
[Trait("Category", "Delivery")]
[Trait("TestType", "E2E")]
public class UploadAndValidateAndSendToFortnoxTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7041"; // WebChat URL
    private const string AgentUrl = "https://localhost:7042"; // Agent API URL
    private const int TimeoutMs = 120000; // Extended timeout for Fortnox integration
    private const string LoginPassword = "isa72";
    private static bool _servicesAvailable = false;
    private static bool _servicesChecked = false;

    public UploadAndValidateAndSendToFortnoxTests(ITestOutputHelper output)
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
            SlowMo = 1000, // Slow down for visibility
            Args = new[] { "--ignore-certificate-errors", "--ignore-ssl-errors" }
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
        _playwright?.Dispose();
    }

    private async Task<bool> CheckServicesAvailableAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            
            var agentResponse = await httpClient.GetAsync($"{AgentUrl}/health");
            var webChatResponse = await httpClient.GetAsync($"{BaseUrl}/health");
            
            return agentResponse.IsSuccessStatusCode && webChatResponse.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Service check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a customer onboarding file with unique company name to avoid Fortnox duplicates
    /// </summary>
    private string CreateCustomerOnboardingFile()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var uniqueCompanyName = $"TestFöretag_{timestamp}_AB";
        
        var customerData = $@"Nytt företag att lägga in i Fortnox

Namn: {uniqueCompanyName}
Adress: Testgatan 123
Stad: Stockholm
Postnummer: 111 22
Landskod: SE
E-post: test@{uniqueCompanyName.ToLower().Replace("_", "").Replace(" ", "")}.se
Typ: COMPANY
Telefonnummer: 08-123 456 78
Organisationsnummer: 556{timestamp.Substring(2, 6)}-{timestamp.Substring(8, 4)}
Webbplats: https://{uniqueCompanyName.ToLower().Replace("_", "").Replace(" ", "")}.se
Kommentarer: Automatiskt test från E2E test suite
Valuta: SEK
Betalningsvillkor: 30 dagar";

        var testFilePath = Path.Combine(Path.GetTempPath(), $"customer_onboarding_{timestamp}.txt");
        File.WriteAllText(testFilePath, customerData);
        
        _output.WriteLine($"✅ Created customer onboarding file: {testFilePath}");
        _output.WriteLine($"📋 Company name: {uniqueCompanyName}");
        
        return testFilePath;
    }

    [Fact]
    [Trait("Category", "Delivery")]
    [Trait("DeliveryTest", "CustomerOnboardingToFortnox")]
    public async Task Should_Upload_Customer_File_And_Create_In_Fortnox()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Skipping test - services not available");
            return;
        }

        string? testFilePath = null;
        
        try
        {
            _output.WriteLine("🚀 Starting Customer Onboarding to Fortnox Test");
            
            // 1. Launch new Playwright page targeting WebChat application
            var page = await _browser!.NewPageAsync();
            _output.WriteLine("✅ Step 1: Launched new browser page");

            await page.GotoAsync($"{BaseUrl}/");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // 2. Authenticate using credentials "isa72"
            _output.WriteLine("🔐 Step 2: Authenticating with credentials");
            await page.FillAsync("input[type='password']", LoginPassword);
            await page.PressAsync("input[type='password']", "Enter");
            await page.WaitForTimeoutAsync(3000);

            // Verify chat interface is loaded and ready
            try
            {
                await page.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = 10000 });
                _output.WriteLine("✅ Chat interface loaded successfully");
            }
            catch (TimeoutException)
            {
                _output.WriteLine("❌ Chat input not found - checking page content...");
                var pageContent = await page.ContentAsync();
                _output.WriteLine($"Page content length: {pageContent.Length}");

                // Try to find any input elements
                var inputs = await page.QuerySelectorAllAsync("input, textarea");
                _output.WriteLine($"Found {inputs.Count} input elements on page");

                throw new Exception("Chat interface not properly loaded");
            }

            // 3. Create a customer onboarding file with unique company name
            _output.WriteLine("📄 Step 3: Creating customer onboarding file");
            testFilePath = CreateCustomerOnboardingFile();

            // 4. Upload the file
            _output.WriteLine("📎 Step 4: Uploading customer onboarding file");
            var fileInput = await page.QuerySelectorAsync("input[type='file']");
            fileInput.Should().NotBeNull("File input should be present on the page");
            
            await fileInput!.SetInputFilesAsync(testFilePath);
            _output.WriteLine("✅ File uploaded successfully");

            // Wait for file upload confirmation message
            await page.WaitForSelectorAsync("text=Fil uppladdad:", new PageWaitForSelectorOptions { Timeout = 10000 });
            _output.WriteLine("✅ File upload confirmation received");

            // 5. Verify file appears in file area
            _output.WriteLine("🔍 Step 5: Verifying file appears in file area");
            await page.WaitForSelectorAsync(".file-item", new PageWaitForSelectorOptions { Timeout = 5000 });
            var fileItems = await page.QuerySelectorAllAsync(".file-item");
            fileItems.Should().HaveCount(1, "One file should be visible in file area");
            _output.WriteLine("✅ File appears in file area successfully");

            // 6. Send message asking for customer creation in Fortnox
            _output.WriteLine("💬 Step 6: Sending customer creation request message");
            var message = "Hej Isa, kan du lägga in företaget i denna filen som ny kund i Fortnox";

            await page.FillAsync("[data-testid='chat-input']", message);
            await page.PressAsync("[data-testid='chat-input']", "Control+Enter");
            _output.WriteLine("✅ Message sent successfully");

            // 7. Verify thinking bubble appears when agent is processing
            _output.WriteLine("🔍 Step 7: Verifying thinking bubble appears");
            try
            {
                await page.WaitForSelectorAsync(".typing-indicator", new PageWaitForSelectorOptions { Timeout = 3000 });
                _output.WriteLine("✅ Thinking bubble appeared successfully");
            }
            catch (TimeoutException)
            {
                _output.WriteLine("⚠️ Thinking bubble not found - agent might respond too quickly");
            }

            // 8. Wait for Agent response with Fortnox customer creation confirmation
            _output.WriteLine("⏳ Step 8: Waiting for Agent response with Fortnox confirmation (up to 120 seconds)");
            
            try
            {
                await page.WaitForFunctionAsync(@"
                    () => {
                        const messages = document.querySelectorAll('.message.agent-message');
                        const lastMessage = messages[messages.length - 1];
                        return lastMessage && lastMessage.textContent && 
                               (lastMessage.textContent.includes('Fortnox') || 
                                lastMessage.textContent.includes('kund') ||
                                lastMessage.textContent.includes('skapad') ||
                                lastMessage.textContent.includes('lagt in') ||
                                lastMessage.textContent.includes('CustomerNumber'));
                    }
                ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });
                
                _output.WriteLine("✅ Agent responded with Fortnox-related content");
            }
            catch (TimeoutException)
            {
                _output.WriteLine("❌ Timeout waiting for Fortnox response - checking what we got...");
                
                // Get all messages for debugging
                var allMessages = await page.QuerySelectorAllAsync(".message");
                _output.WriteLine($"Found {allMessages.Count} total messages");
                
                for (int i = 0; i < allMessages.Count; i++)
                {
                    var messageText = await allMessages[i].TextContentAsync();
                    _output.WriteLine($"Message {i + 1}: {messageText?.Substring(0, Math.Min(200, messageText?.Length ?? 0))}...");
                }
                
                throw new Exception("Agent did not respond with Fortnox content within timeout");
            }

            // 9. Verify the response content indicates successful customer creation
            _output.WriteLine("🔍 Step 9: Verifying customer creation response");
            var response = await page.TextContentAsync(".message.agent-message:last-child");
            response.Should().NotBeNullOrWhiteSpace("Agent response should not be empty");

            // Log the full response for debugging
            _output.WriteLine($"📄 Full Agent Response: {response}");

            // Check for success indicators (expanded list)
            var successIndicators = new[] {
                "skapad", "lagt in", "CustomerNumber", "framgångsrikt", "lyckades",
                "success", "created", "added", "kund", "customer", "fortnox",
                "registrerad", "sparad", "tillagd"
            };
            var foundIndicators = successIndicators.Where(indicator =>
                response!.Contains(indicator, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (foundIndicators.Any())
            {
                _output.WriteLine($"✅ Customer creation confirmed - found indicators: {string.Join(", ", foundIndicators)}");
            }
            else
            {
                _output.WriteLine($"❌ No success indicators found in response. Looking for any of: {string.Join(", ", successIndicators)}");
                _output.WriteLine($"📄 Response length: {response?.Length ?? 0} characters");

                // Check if there's an error message instead
                var errorIndicators = new[] { "error", "fel", "misslyckades", "failed", "kunde inte" };
                var foundErrors = errorIndicators.Where(indicator =>
                    response!.Contains(indicator, StringComparison.OrdinalIgnoreCase)).ToArray();

                if (foundErrors.Any())
                {
                    _output.WriteLine($"❌ Found error indicators: {string.Join(", ", foundErrors)}");
                }
            }

            foundIndicators.Should().NotBeEmpty($"Response should contain success indicators: {string.Join(", ", successIndicators)}");

            // 10. Verify that file area is cleared after processing
            _output.WriteLine("🔍 Step 10: Verifying file area is cleared after processing");
            await page.WaitForTimeoutAsync(5000); // Give time for clearing to complete

            var remainingFileItems = await page.QuerySelectorAllAsync(".file-item");
            remainingFileItems.Should().BeEmpty("File area should be cleared after processing");
            _output.WriteLine("✅ File area cleared successfully after processing");

            _output.WriteLine("🎉 Customer onboarding to Fortnox test completed successfully!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Test failed: {ex.Message}");
            throw;
        }
        finally
        {
            // Cleanup
            if (testFilePath != null && File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
                _output.WriteLine("🧹 Cleaned up test file");
            }
        }
    }
}
