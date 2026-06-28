using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.E2E;

/// <summary>
/// End-to-end tests for multi-platform document processing
/// Tests the complete workflow across Dropbox, Google Workspace, and Microsoft Office platforms:
/// Platform → TempFilesService → DocumentInterpreterPlugin → Structured JSON for Fortnox integration
/// </summary>
[Collection("E2E")]
public class MultiPlatformDocumentE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private const string BaseUrl = "https://localhost:7041"; // WebChat URL
    private const string AgentUrl = "https://localhost:7042"; // Agent API URL
    private const int TimeoutMs = 30000;

    public MultiPlatformDocumentE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--ignore-certificate-errors", "--ignore-ssl-errors" }
        });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private IPage Page => _page ?? throw new InvalidOperationException("Page not initialized");

    [Theory(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    [InlineData("Dropbox", "bussbiljett_praktikant.pdf")]
    [InlineData("Dropbox", "Faktura Säkra Firman.pdf")]
    [InlineData("GoogleWorkspace", "bussbiljett_praktikant.pdf")]
    [InlineData("GoogleWorkspace", "Faktura Säkra Firman.pdf")]
    [InlineData("MicrosoftOffice", "bussbiljett_praktikant.pdf")]
    [InlineData("MicrosoftOffice", "Faktura Säkra Firman.pdf")]
    public async Task Should_Process_Document_From_All_Platforms_For_Fortnox_Integration(string platform, string fileName)
    {
        // Navigate to the chat interface
        await Page.GotoAsync($"{BaseUrl}/chat");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for chat interface to be ready
        await Page.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });

        // Create platform-specific download and analysis message
        var message = platform switch
        {
            "Dropbox" => $"Use DropboxPlugin to download the file \"{fileName}\" from the testfiler directory, then use DocumentInterpreterPlugin to analyze it and return structured JSON for Fortnox integration",
            "GoogleWorkspace" => $"Use GoogleWorkspacePlugin to download the file \"{fileName}\" from the Work directory, then use DocumentInterpreterPlugin to analyze it and return structured JSON for Fortnox integration",
            "MicrosoftOffice" => $"Use MicrosoftOfficePlugin to download the file \"{fileName}\" from the Work directory, then use DocumentInterpreterPlugin to analyze it and return structured JSON for Fortnox integration",
            _ => throw new ArgumentException($"Unknown platform: {platform}")
        };

        await Page.FillAsync("[data-testid='chat-input']", message);
        await Page.PressAsync("[data-testid='chat-input']", "Enter");

        // Wait for the response to be fully loaded (this may take longer for file processing)
        await Page.WaitForFunctionAsync(@"
            () => {
                const messages = document.querySelectorAll('.message.agent-message');
                const lastMessage = messages[messages.length - 1];
                return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
            }
        ", new PageWaitForFunctionOptions { Timeout = 90000 }); // Extended timeout for file processing

        // Get the response from Isa
        var response = await Page.TextContentAsync(".message.agent-message:last-child");

        // Verify that the response contains successful processing indicators
        response.Should().NotBeNullOrEmpty("Isa should provide a response");

        // Verify platform-specific success indicators
        response!.Should().Contain(fileName, "Response should mention the processed file");

        // Verify that the complete E2E flow worked (file was downloaded and analyzed)
        var successfulProcessing = response!.Contains("analyserat") ||
                                 response.Contains("analyzed") ||
                                 response.Contains("extraherat") ||
                                 response.Contains("extracted") ||
                                 response.Contains("strukturerad") ||
                                 response.Contains("structured") ||
                                 response.Contains("Fortnox") ||
                                 response.Contains("JSON") ||
                                 response.Contains("status") ||
                                 response.Contains("documentType") ||
                                 response.Contains("confidence");

        successfulProcessing.Should().BeTrue(
            $"Response should indicate successful E2E processing ({platform} → TempFiles → DocumentInterpreter → Fortnox JSON). Actual response: {response}");

        // Verify platform-specific indicators
        var platformIndicators = platform switch
        {
            "Dropbox" => response.Contains("Dropbox") || response.Contains("dropbox"),
            "GoogleWorkspace" => response.Contains("Google") || response.Contains("Drive") || response.Contains("GoogleDrive"),
            "MicrosoftOffice" => response.Contains("OneDrive") || response.Contains("Microsoft") || response.Contains("Office"),
            _ => false
        };

        platformIndicators.Should().BeTrue(
            $"Response should indicate successful {platform} integration. Actual response: {response}");

        // Log the successful test
        _output.WriteLine($"✅ E2E test passed for {platform} with {fileName}");
        _output.WriteLine($"Response length: {response.Length} characters");
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task Should_Process_Receipt_Document_With_Structured_Fortnox_Data()
    {
        // Navigate to the chat interface
        await Page.GotoAsync($"{BaseUrl}/chat");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for chat interface to be ready
        await Page.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });

        // Test with receipt document that should return structured data
        var message = "Use DropboxPlugin to download the file \"bussbiljett_praktikant.pdf\" from the testfiler directory, then use DocumentInterpreterPlugin to analyze it and return the structured JSON response for Fortnox integration";

        await Page.FillAsync("[data-testid='chat-input']", message);
        await Page.PressAsync("[data-testid='chat-input']", "Enter");

        // Wait for the response
        await Page.WaitForFunctionAsync(@"
            () => {
                const messages = document.querySelectorAll('.message.agent-message');
                const lastMessage = messages[messages.length - 1];
                return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
            }
        ", new PageWaitForFunctionOptions { Timeout = 90000 });

        // Get the response
        var response = await Page.TextContentAsync(".message.agent-message:last-child");

        // Verify structured JSON response for Fortnox integration
        response.Should().NotBeNullOrEmpty("Should receive structured response");
        
        // Check for Fortnox-specific structured data fields
        var fortnoxFields = new[]
        {
            "status",
            "documentType", 
            "confidence",
            "extractedData",
            "fortnoxData",
            "qualityMetrics"
        };

        var foundFields = fortnoxFields.Count(field => response!.Contains(field));
        foundFields.Should().BeGreaterThan(2, 
            $"Response should contain multiple Fortnox integration fields. Found {foundFields} of {fortnoxFields.Length} fields. Response: {response}");

        _output.WriteLine($"✅ Structured Fortnox data test passed");
        _output.WriteLine($"Found {foundFields} of {fortnoxFields.Length} expected Fortnox fields");
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task Should_Process_Invoice_Document_With_Structured_Fortnox_Data()
    {
        // Navigate to the chat interface
        await Page.GotoAsync($"{BaseUrl}/chat");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for chat interface to be ready
        await Page.WaitForSelectorAsync("[data-testid='chat-input']", new PageWaitForSelectorOptions { Timeout = TimeoutMs });

        // Test with invoice document that should return structured data
        var message = "Use DropboxPlugin to download the file \"Faktura Säkra Firman.pdf\" from the testfiler directory, then use DocumentInterpreterPlugin to analyze it and return the structured JSON response for Fortnox integration";

        await Page.FillAsync("[data-testid='chat-input']", message);
        await Page.PressAsync("[data-testid='chat-input']", "Enter");

        // Wait for the response
        await Page.WaitForFunctionAsync(@"
            () => {
                const messages = document.querySelectorAll('.message.agent-message');
                const lastMessage = messages[messages.length - 1];
                return lastMessage && lastMessage.textContent && lastMessage.textContent.trim().length > 0;
            }
        ", new PageWaitForFunctionOptions { Timeout = 90000 });

        // Get the response
        var response = await Page.TextContentAsync(".message.agent-message:last-child");

        // Verify structured JSON response for Fortnox integration
        response.Should().NotBeNullOrEmpty("Should receive structured response");
        
        // Check for invoice-specific fields
        var invoiceFields = new[]
        {
            "status",
            "documentType",
            "supplierName",
            "invoiceNumber", 
            "invoiceDate",
            "amount",
            "currency",
            "fortnoxData"
        };

        var foundFields = invoiceFields.Count(field => response!.Contains(field));
        foundFields.Should().BeGreaterThan(3, 
            $"Response should contain multiple invoice fields for Fortnox integration. Found {foundFields} of {invoiceFields.Length} fields. Response: {response}");

        _output.WriteLine($"✅ Structured invoice data test passed");
        _output.WriteLine($"Found {foundFields} of {invoiceFields.Length} expected invoice fields");
    }
}
