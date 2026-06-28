using Microsoft.Playwright;
using FluentAssertions;
using System.Net.Http;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Delivery.Tests;

/// <summary>
/// Delivery test for file upload and document interpretation functionality
/// Tests file upload through WebChat interface and DocumentInterpreter plugin response
/// Only executed through CI script - excluded from normal test discovery
/// </summary>
[Trait("Category", "Delivery")]
[Trait("TestType", "E2E")]
public class UploadAndValidateFileTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7041"; // WebChat URL
    private const string AgentUrl = "https://localhost:7042"; // Agent API URL
    private const int TimeoutMs = 60000; // Extended timeout for document processing
    private const string LoginPassword = "isa72";
    private static bool _servicesAvailable = false;
    private static bool _servicesChecked = false;

    public UploadAndValidateFileTests(ITestOutputHelper output)
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

    [Fact]
    [Trait("Category", "Delivery")]
    [Trait("DeliveryTest", "FileUploadAndValidation")]
    public async Task Should_Upload_File_And_Return_JSON_Analysis()
    {
        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Skipping test - services not available");
            return;
        }

        try
        {
            _output.WriteLine("🚀 Starting File Upload and Validation Test");
            
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

            // 3. Create a dummy PDF file for testing
            _output.WriteLine("📄 Step 3: Creating dummy PDF file");
            var testFilePath = Path.Combine(Path.GetTempPath(), "test-document.pdf");
            await File.WriteAllTextAsync(testFilePath, "Test PDF content for upload validation test");
            _output.WriteLine($"✅ Created test file: {testFilePath}");

            // 4. Upload the file
            _output.WriteLine("📎 Step 4: Uploading file");
            var fileInput = await page.QuerySelectorAsync("input[type='file']");
            fileInput.Should().NotBeNull("File input should be present on the page");
            
            await fileInput!.SetInputFilesAsync(testFilePath);
            _output.WriteLine("✅ File uploaded successfully");

            // Wait for file upload confirmation message
            await page.WaitForSelectorAsync("text=Fil uppladdad:", new PageWaitForSelectorOptions { Timeout = 10000 });
            _output.WriteLine("✅ File upload confirmation received");

            // 5. Verify file input is cleared after upload
            _output.WriteLine("🔍 Step 5: Verifying file input is cleared");
            await page.WaitForTimeoutAsync(3000); // Give more time for clearing
            var fileInputValue = await fileInput.InputValueAsync();
            fileInputValue.Should().BeEmpty("File input should be cleared after upload");
            _output.WriteLine("✅ File input cleared successfully");

            // 5.5. Verify uploaded file appears in file area
            _output.WriteLine("🔍 Step 5.5: Verifying file appears in file area");
            await page.WaitForSelectorAsync(".file-item", new PageWaitForSelectorOptions { Timeout = 5000 });
            var fileItems = await page.QuerySelectorAllAsync(".file-item");
            fileItems.Should().HaveCount(1, "One file should be visible in file area");
            _output.WriteLine("✅ File appears in file area successfully");

            // 6. Send message asking for file analysis
            _output.WriteLine("💬 Step 6: Sending analysis request message");
            var message = "Hej Isa, kan du kolla upp denna filen och tolka den och sedan skriva ut json resultatet";
            
            await page.FillAsync("[data-testid='chat-input']", message);
            await page.PressAsync("[data-testid='chat-input']", "Enter");
            _output.WriteLine("✅ Message sent successfully");

            // 6.5. Verify thinking bubble appears when agent is processing
            _output.WriteLine("🔍 Step 6.5: Verifying thinking bubble appears");
            try
            {
                await page.WaitForSelectorAsync(".typing-indicator", new PageWaitForSelectorOptions { Timeout = 3000 });
                _output.WriteLine("✅ Thinking bubble appeared successfully");
            }
            catch (TimeoutException)
            {
                _output.WriteLine("⚠️ Thinking bubble not found - agent might respond too quickly");
            }

            // 7. Verify chat input is cleared after sending
            _output.WriteLine("🔍 Step 7: Verifying chat input is cleared");
            await page.WaitForTimeoutAsync(10000); // Give time for SendMessage to complete and clear input
            var chatInputValue = await page.InputValueAsync("[data-testid='chat-input']");
            chatInputValue.Should().BeEmpty("Chat input should be cleared after sending message");
            _output.WriteLine("✅ Chat input cleared successfully");

            // 8. Verify focus returns to chat input
            _output.WriteLine("🔍 Step 8: Verifying focus returns to chat input");
            var focusedElement = await page.EvaluateAsync<string>("document.activeElement.getAttribute('data-testid')");
            focusedElement.Should().Be("chat-input", "Focus should return to chat input after sending message");
            _output.WriteLine("✅ Focus returned to chat input successfully");

            // 9. Wait for Agent response with JSON content
            _output.WriteLine("⏳ Step 9: Waiting for Agent response with JSON analysis (up to 60 seconds)");
            
            try
            {
                await page.WaitForFunctionAsync(@"
                    () => {
                        const messages = document.querySelectorAll('.message.agent-message');
                        const lastMessage = messages[messages.length - 1];
                        return lastMessage && lastMessage.textContent && 
                               lastMessage.textContent.includes('json') && 
                               lastMessage.textContent.includes('{');
                    }
                ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });
                
                _output.WriteLine("✅ Agent responded with JSON content");
            }
            catch (TimeoutException)
            {
                _output.WriteLine("❌ Timeout waiting for JSON response - checking what we got...");
                
                // Get all messages for debugging
                var allMessages = await page.QuerySelectorAllAsync(".message");
                _output.WriteLine($"Found {allMessages.Count} total messages");
                
                for (int i = 0; i < allMessages.Count; i++)
                {
                    var messageText = await allMessages[i].TextContentAsync();
                    _output.WriteLine($"Message {i + 1}: {messageText?.Substring(0, Math.Min(100, messageText?.Length ?? 0))}...");
                }
                
                throw new Exception("Agent did not respond with JSON content within timeout");
            }

            // 10. Verify the JSON response content
            _output.WriteLine("🔍 Step 10: Verifying JSON response content");
            var response = await page.TextContentAsync(".message.agent-message:last-child");
            response.Should().NotBeNullOrWhiteSpace("Agent response should not be empty");
            response.Should().Contain("json", "Response should mention JSON");
            response.Should().Contain("{", "Response should contain JSON structure");
            
            // Check for common JSON fields that DocumentInterpreter should return
            var expectedJsonFields = new[] { "status", "documentType", "confidence", "extractedData" };
            var foundFields = expectedJsonFields.Where(field => response!.Contains(field)).ToArray();
            foundFields.Should().NotBeEmpty($"Response should contain at least one expected JSON field: {string.Join(", ", expectedJsonFields)}");
            
            _output.WriteLine($"✅ JSON response verified - found fields: {string.Join(", ", foundFields)}");
            _output.WriteLine($"📄 Response preview: {response?.Substring(0, Math.Min(200, response?.Length ?? 0))}...");

            // 11. Verify that file area is cleared immediately after sending message
            _output.WriteLine("🔍 Step 11: Verifying file area is cleared after sending message");

            // Wait longer for file clearing to complete and UI to update
            await page.WaitForTimeoutAsync(5000); // Give more time for clearing to complete

            // Check multiple times to handle potential race conditions
            var remainingFileItems = await page.QuerySelectorAllAsync(".file-item");
            _output.WriteLine($"🔍 Step 11: Found {remainingFileItems.Count} remaining file items");

            if (remainingFileItems.Count > 0)
            {
                _output.WriteLine("⚠️ Files still visible, waiting additional time...");
                await page.WaitForTimeoutAsync(3000); // Additional wait
                remainingFileItems = await page.QuerySelectorAllAsync(".file-item");
                _output.WriteLine($"🔍 Step 11: After additional wait, found {remainingFileItems.Count} remaining file items");
            }

            remainingFileItems.Should().BeEmpty("File area should be cleared immediately after sending message");
            _output.WriteLine("✅ File area cleared successfully after sending message");

            // Verify file area no longer has the has-files class
            var fileAreaHasFiles = await page.QuerySelectorAsync(".file-upload-area.has-files");
            fileAreaHasFiles.Should().BeNull("File area should not have has-files class when empty");
            _output.WriteLine("✅ File area correctly shows empty state");

            _output.WriteLine("🎉 Test completed successfully!");

            // Cleanup
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
                _output.WriteLine("🧹 Cleaned up test file");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Test failed: {ex.Message}");
            throw;
        }
    }
}
