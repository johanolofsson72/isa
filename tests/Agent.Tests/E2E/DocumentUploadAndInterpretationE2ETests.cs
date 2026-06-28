using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Net.Http;

namespace Agent.Tests.E2E;

/// <summary>
/// Comprehensive end-to-end test for file upload and document interpretation workflow.
/// Tests the complete chain: WebChat file upload → Agent file ID processing → DocumentInterpreter → AI response
/// Validates that the file ID processing and DocumentInterpreter integration works correctly.
/// </summary>
[Collection("E2E")]
public class DocumentUploadAndInterpretationE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7041"; // WebChat URL
    private const string AgentUrl = "https://localhost:7042"; // Agent API URL
    private const string LoginPassword = "isa72";
    private const int TimeoutMs = 30000;
    private const int FileProcessingTimeoutMs = 120000; // 2 minutes for document processing
    private static bool _servicesAvailable = false;
    private static bool _servicesChecked = false;

    public DocumentUploadAndInterpretationE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🚀 Initializing DocumentUploadAndInterpretationE2ETests...");

        // Check if services are available before running tests
        if (!_servicesChecked)
        {
            _servicesAvailable = await CheckServicesAvailableAsync();
            _servicesChecked = true;
        }

        if (!_servicesAvailable)
        {
            _output.WriteLine("⚠️ Services not available - skipping Playwright initialization");
            return;
        }

        // Install Playwright browsers if needed
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright install failed with exit code {exitCode}");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Set to false for debugging, true for CI
            SlowMo = 500, // Slow down actions for visibility
            Args = new[] { "--ignore-certificate-errors", "--ignore-ssl-errors", "--disable-web-security" }
        });

        _output.WriteLine("✅ Playwright initialized successfully");
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _output.WriteLine("🧹 Browser closed");
        }
        _playwright?.Dispose();
    }

    private async Task<bool> CheckServicesAvailableAsync()
    {
        _output.WriteLine("🔍 Checking if services are available...");
        
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            // Check Agent service
            var agentResponse = await httpClient.GetAsync($"{AgentUrl}/health");
            agentResponse.EnsureSuccessStatusCode();
            _output.WriteLine("✅ Agent service is available");

            // Check WebChat service
            var webChatResponse = await httpClient.GetAsync($"{BaseUrl}/health");
            webChatResponse.EnsureSuccessStatusCode();
            _output.WriteLine("✅ WebChat service is available");

            return true;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Services not available: {ex.Message}");
            return false;
        }
    }

    private void SkipIfServicesNotAvailable()
    {
        if (!_servicesAvailable)
        {
            throw new InvalidOperationException(
                "WebChat and Agent services are not running. " +
                "Start AppHost (dotnet run --project src/AppHost) to run E2E tests.");
        }
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task Should_Upload_All_Files_And_Successfully_Interpret_Each_Document()
    {
        SkipIfServicesNotAvailable();

        _output.WriteLine("🧪 Starting comprehensive test with ALL files from ekonomi directory...");

        // Use consistent test user ID for deterministic testing
        const string testUserId = "test-document-upload-all-files-e2e";

        // Get ALL files from ekonomi directory
        // Find the repository root by looking for the ekonomi directory
        var currentDir = Directory.GetCurrentDirectory();
        var ekonomiDir = "";

        // Try different paths to find ekonomi directory
        var possiblePaths = new[]
        {
            Path.Combine(currentDir, "ekonomi"),
            Path.Combine(currentDir, "../../ekonomi"),
            Path.Combine(currentDir, "../../../ekonomi"),
            Path.Combine(currentDir, "../../../../ekonomi"),
            "/path/to/accountingagent/ekonomi"
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                ekonomiDir = fullPath;
                break;
            }
        }

        if (string.IsNullOrEmpty(ekonomiDir))
        {
            throw new DirectoryNotFoundException($"Ekonomi directory not found. Tried paths: {string.Join(", ", possiblePaths)}");
        }

        // Get ALL PDF files in the ekonomi directory
        var allFiles = Directory.GetFiles(ekonomiDir, "*.pdf").OrderBy(f => Path.GetFileName(f)).ToArray();

        if (allFiles.Length == 0)
        {
            throw new InvalidOperationException($"No PDF files found in {ekonomiDir}");
        }

        _output.WriteLine($"📁 Found {allFiles.Length} PDF files to process:");
        for (int i = 0; i < allFiles.Length; i++)
        {
            _output.WriteLine($"  {i + 1}. {Path.GetFileName(allFiles[i])}");
        }

        var context = await _browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        var processedFiles = new List<string>();
        var failedFiles = new List<(string fileName, string error)>();

        try
        {
            // Step 1: Setup and Authentication
            _output.WriteLine("📋 Step 1: Authenticating with WebChat...");
            await LoginToWebChat(page, testUserId);
            _output.WriteLine("✅ Authentication completed");

            // Process each file sequentially
            for (int i = 0; i < allFiles.Length; i++)
            {
                var currentFile = allFiles[i];
                var fileName = Path.GetFileName(currentFile);

                _output.WriteLine($"\n🔄 === PROCESSING FILE {i + 1}/{allFiles.Length}: {fileName} ===");

                try
                {
                    // Step 2: File Upload
                    _output.WriteLine($"📋 Step 2.{i + 1}: Uploading {fileName}...");
                    await UploadFile(page, currentFile);
                    _output.WriteLine($"✅ File upload completed for {fileName}");

                    // Step 3: Document Analysis Request
                    _output.WriteLine($"📋 Step 3.{i + 1}: Requesting document analysis for {fileName}...");
                    const string analysisMessage = "Hej Isa, kan du tolka denna filen och sedan skriva ut resultatet här i chatten?";

                    await page.FillAsync("[data-testid='chat-input']", analysisMessage);

                    // Click the send button instead of pressing Enter to ensure message is sent
                    await page.ClickAsync("[data-testid='send-button']");
                    _output.WriteLine($"✅ Analysis request sent for {fileName} (clicked send button)");

                    // Step 4: Wait for and validate response
                    _output.WriteLine($"📋 Step 4.{i + 1}: Waiting for document analysis response for {fileName}...");
                    await WaitForDocumentAnalysisResponse(page);

                    var response = await GetLatestAgentResponse(page);
                    _output.WriteLine($"📄 Received response for {fileName} (length: {response?.Length ?? 0})");

                    // Step 5: Validation
                    _output.WriteLine($"📋 Step 5.{i + 1}: Validating response content for {fileName}...");
                    ValidateDocumentAnalysisResponse(response, fileName);
                    _output.WriteLine($"✅ Validation passed for {fileName}!");

                    processedFiles.Add(fileName);

                    // Wait a bit between files to avoid overwhelming the system
                    if (i < allFiles.Length - 1)
                    {
                        _output.WriteLine("⏳ Waiting 3 seconds before next file...");
                        await page.WaitForTimeoutAsync(3000);
                    }
                }
                catch (Exception fileEx)
                {
                    var errorMessage = $"Failed to process {fileName}: {fileEx.Message}";
                    _output.WriteLine($"❌ {errorMessage}");
                    failedFiles.Add((fileName, fileEx.Message));

                    // Take screenshot for this specific file failure
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = $"debug-file-failure-{fileName.Replace(" ", "_")}-{DateTime.Now:yyyyMMdd-HHmmss}.png"
                    });
                    _output.WriteLine($"📸 Debug screenshot saved for {fileName}");

                    // Continue with next file instead of failing the entire test
                    continue;
                }
            }

            // Final validation - check if we processed all files successfully
            _output.WriteLine($"\n📊 PROCESSING SUMMARY:");
            _output.WriteLine($"   Total files: {allFiles.Length}");
            _output.WriteLine($"   Successfully processed: {processedFiles.Count}");
            _output.WriteLine($"   Failed: {failedFiles.Count}");

            if (failedFiles.Any())
            {
                _output.WriteLine($"\n❌ FAILED FILES:");
                foreach (var (fileName, error) in failedFiles)
                {
                    _output.WriteLine($"   - {fileName}: {error}");
                }

                throw new InvalidOperationException($"Failed to process {failedFiles.Count} out of {allFiles.Length} files. See details above.");
            }

            _output.WriteLine($"\n🎉 ALL {allFiles.Length} FILES PROCESSED SUCCESSFULLY!");
            _output.WriteLine("✅ Complete file upload and document interpretation workflow validated for entire ekonomi directory!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Test failed: {ex.Message}");

            if (processedFiles.Any())
            {
                _output.WriteLine($"📋 Successfully processed files before failure:");
                foreach (var file in processedFiles)
                {
                    _output.WriteLine($"   ✅ {file}");
                }
            }

            // Take screenshot for debugging
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = $"debug-all-documents-test-{DateTime.Now:yyyyMMdd-HHmmss}.png"
            });
            _output.WriteLine("📸 Debug screenshot saved");

            throw;
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private async Task LoginToWebChat(IPage page, string testUserId)
    {
        // Set test user ID header for deterministic testing
        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["X-Test-User-Id"] = testUserId
        });

        // Clear session storage and navigate to login
        await page.GotoAsync(BaseUrl);
        await page.EvaluateAsync("() => sessionStorage.clear()");
        await page.GotoAsync($"{BaseUrl}/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for password input and login
        await page.WaitForSelectorAsync("input[type='password']", new() { Timeout = TimeoutMs });
        await page.FillAsync("input[type='password']", LoginPassword);
        await page.ClickAsync("button:has-text('Logga in')");

        // Wait for chat interface to be ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync("[data-testid='chat-input']", new() { Timeout = TimeoutMs });
        
        // Wait for SignalR connection
        await page.WaitForTimeoutAsync(3000);
    }

    private async Task UploadFile(IPage page, string filePath)
    {
        // Verify file exists
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test file not found: {filePath}");
        }

        var absolutePath = Path.GetFullPath(filePath);
        _output.WriteLine($"📁 Uploading file: {absolutePath}");

        // Look for file input element
        var fileInput = await page.QuerySelectorAsync("input[type='file']");
        if (fileInput == null)
        {
            throw new InvalidOperationException("File input element not found on page");
        }

        // Upload the file
        await fileInput.SetInputFilesAsync(absolutePath);
        
        // Wait for upload to complete - look for file in the file list
        await page.WaitForFunctionAsync(@"
            () => {
                const fileItems = document.querySelectorAll('.file-item');
                return fileItems.length > 0;
            }
        ", new PageWaitForFunctionOptions { Timeout = TimeoutMs });

        _output.WriteLine("📎 File appears in file list");
    }

    private async Task WaitForDocumentAnalysisResponse(IPage page)
    {
        // Wait for Isa to respond with document analysis
        await page.WaitForFunctionAsync(@"
            () => {
                const messages = document.querySelectorAll('.message.agent-message');
                const lastMessage = messages[messages.length - 1];
                if (!lastMessage || !lastMessage.textContent) return false;
                
                const content = lastMessage.textContent.toLowerCase();
                
                // Check for document analysis indicators
                return content.includes('dokument') || 
                       content.includes('faktura') || 
                       content.includes('analys') || 
                       content.includes('innehåll') ||
                       content.includes('leverantör') ||
                       content.includes('belopp') ||
                       content.includes('datum') ||
                       content.length > 100; // Substantial response
            }
        ", new PageWaitForFunctionOptions { Timeout = FileProcessingTimeoutMs });
    }

    private async Task<string?> GetLatestAgentResponse(IPage page)
    {
        return await page.TextContentAsync(".message.agent-message:last-child");
    }

    private void ValidateDocumentAnalysisResponse(string? response, string fileName)
    {
        response.Should().NotBeNullOrEmpty("Isa should provide a response to the document analysis request");

        var lowerResponse = response!.ToLowerInvariant();

        // Verify it's not an error response
        lowerResponse.Should().NotContain("kan inte hitta", "Response should not indicate file not found");
        lowerResponse.Should().NotContain("inte tillgång", "Response should not indicate no access to file");
        lowerResponse.Should().NotContain("fel", "Response should not indicate an error");

        // Verify it contains document analysis content
        var hasDocumentContent = lowerResponse.Contains("dokument") ||
                               lowerResponse.Contains("faktura") ||
                               lowerResponse.Contains("leverantör") ||
                               lowerResponse.Contains("belopp") ||
                               lowerResponse.Contains("datum") ||
                               lowerResponse.Contains("analys") ||
                               lowerResponse.Contains("innehåll");

        hasDocumentContent.Should().BeTrue(
            $"Response should contain document analysis content. Actual response: {response}");

        // Verify response is substantial (not just a generic message)
        response.Length.Should().BeGreaterThan(50, 
            "Response should be substantial, indicating actual document processing occurred");

        _output.WriteLine("✅ Response validation passed - document was successfully processed");
    }
}
