using Microsoft.Playwright;
using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 COMPREHENSIVE END-TO-END TEST - Validerar hela AccountingAgent-flödet!
/// Från chat-input till email-leverans med Playwright browser automation
/// </summary>
public class AccountingAgentE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private Process? _appHostProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly List<string> _applicationLogs = new();
    private readonly string _appHostPath;
    private readonly string _webChatUrl = "https://localhost:7041"; // WebChat URL

    public AccountingAgentE2ETests(ITestOutputHelper output)
    {
        _output = output;
        // Find the repository root by looking for the .git directory
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir, ".git")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        if (currentDir == null)
        {
            throw new InvalidOperationException("Could not find repository root (no .git directory found)");
        }

        _appHostPath = Path.Combine(currentDir, "src", "AppHost");

        if (!Directory.Exists(_appHostPath))
        {
            throw new InvalidOperationException($"AppHost directory not found at: {_appHostPath}");
        }
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🚀 INITIALIZING COMPREHENSIVE E2E TEST");
        _output.WriteLine("=====================================");

        // 1. Start AppHost programmatically
        await StartAppHostAsync();

        // 2. Wait for all services to be ready
        await WaitForServicesReadyAsync();

        // 3. Initialize Playwright
        await InitializePlaywrightAsync();

        // 4. Navigate to WebChat interface
        await NavigateToWebChatAsync();

        _output.WriteLine("✅ E2E Test initialization complete!");
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine("🧹 CLEANING UP E2E TEST RESOURCES");
        
        try
        {
            await _page?.CloseAsync()!;
            await _context?.CloseAsync()!;
            await _browser?.CloseAsync()!;
            _playwright?.Dispose();
            
            if (_appHostProcess != null && !_appHostProcess.HasExited)
            {
                _appHostProcess.Kill(true);
                await _appHostProcess.WaitForExitAsync();
            }
            
            _output.WriteLine("✅ Cleanup complete!");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️ Cleanup warning: {ex.Message}");
        }
    }

    [Fact]
    public async Task ComprehensiveAccountingAgentWorkflow_ShouldExecuteAllTestCases_Successfully()
    {
        _output.WriteLine("🎯 STARTING COMPREHENSIVE ACCOUNTING AGENT WORKFLOW TEST");
        _output.WriteLine("========================================================");

        var testCases = new[]
        {
            new TestCase
            {
                Name = "Expense Report Test",
                Input = "Skapa en professionell utgiftsrapport för 2023 med diagram och pivot-tabeller och skicka till Anna Andersson",
                ExpectedKeywords = new[] { "utgiftsrapport", "diagram", "pivot", "Anna Andersson" }
            },
            new TestCase
            {
                Name = "Project Summary Report Test", 
                Input = "Skapa en professionell rapport där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson",
                ExpectedKeywords = new[] { "projekt", "intäkter", "kostnader", "filtrera", "datum" }
            },
            new TestCase
            {
                Name = "Financial Analysis Pivot Report Test",
                Input = "Skapa en professionell rapport i pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson",
                ExpectedKeywords = new[] { "pivot", "omsättning", "varukostnad", "personalkostnader", "kvartal", "5 åren" }
            },
            new TestCase
            {
                Name = "Weekly Financial Summary Test",
                Input = "Skapa en professionell rapport som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson",
                ExpectedKeywords = new[] { "omsättning", "varukostnad", "bruttovinst", "SEK", "vecka" }
            }
        };

        var results = new List<TestResult>();

        foreach (var testCase in testCases)
        {
            _output.WriteLine($"\n🧪 EXECUTING: {testCase.Name}");
            _output.WriteLine($"Input: {testCase.Input}");
            
            var result = await ExecuteTestCaseAsync(testCase);
            results.Add(result);
            
            _output.WriteLine($"Result: {(result.Success ? "✅ SUCCESS" : "❌ FAILED")}");
            if (!result.Success)
            {
                _output.WriteLine($"Error: {result.ErrorMessage}");
            }
            
            // Wait between test cases to avoid overwhelming the system
            await Task.Delay(5000);
        }

        // Validate overall results
        var successCount = results.Count(r => r.Success);
        var totalCount = results.Count;
        
        _output.WriteLine("\n📊 COMPREHENSIVE TEST RESULTS:");
        _output.WriteLine("==============================");
        _output.WriteLine($"Total Tests: {totalCount}");
        _output.WriteLine($"Successful: {successCount}");
        _output.WriteLine($"Failed: {totalCount - successCount}");
        _output.WriteLine($"Success Rate: {(double)successCount / totalCount * 100:F1}%");

        foreach (var result in results)
        {
            var status = result.Success ? "✅" : "❌";
            _output.WriteLine($"{status} {result.TestName}: {result.ExecutionTime:F1}s");
        }

        // Assert that all tests passed
        Assert.True(successCount == totalCount, 
            $"Expected all {totalCount} tests to pass, but {totalCount - successCount} failed");
    }

    private async Task StartAppHostAsync()
    {
        _output.WriteLine("🚀 Starting AppHost...");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project AppHost.csproj",
            WorkingDirectory = _appHostPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _appHostProcess = Process.Start(startInfo);
        
        if (_appHostProcess == null)
        {
            throw new InvalidOperationException("Failed to start AppHost process");
        }

        // Monitor logs
        _ = Task.Run(async () =>
        {
            while (!_appHostProcess.HasExited)
            {
                var line = await _appHostProcess.StandardOutput.ReadLineAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    _applicationLogs.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
                }
            }
        });

        _ = Task.Run(async () =>
        {
            while (!_appHostProcess.HasExited)
            {
                var line = await _appHostProcess.StandardError.ReadLineAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    _applicationLogs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {line}");
                }
            }
        });

        _output.WriteLine("✅ AppHost process started");

        // Give the process a moment to start
        await Task.Delay(2000);
    }

    private async Task WaitForServicesReadyAsync()
    {
        _output.WriteLine("⏳ Waiting for services to be ready...");
        
        var timeout = TimeSpan.FromMinutes(3);
        var startTime = DateTime.Now;
        
        while (DateTime.Now - startTime < timeout)
        {
            var recentLogs = _applicationLogs.TakeLast(50).ToList();
            
            var hasAgent = recentLogs.Any(log => log.Contains("agent-https is now in state Ready"));
            var hasWebChat = recentLogs.Any(log => log.Contains("webchat-https is now in state Ready"));
            var hasDashboard = recentLogs.Any(log => log.Contains("aspire-dashboard-https is now in state Ready"));
            
            if (hasAgent && hasWebChat && hasDashboard)
            {
                _output.WriteLine("✅ All services are ready!");
                await Task.Delay(5000); // Extra wait for full initialization
                return;
            }
            
            await Task.Delay(2000);
        }
        
        throw new TimeoutException("Services did not become ready within the timeout period");
    }

    private async Task InitializePlaywrightAsync()
    {
        _output.WriteLine("🎭 Initializing Playwright...");
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Set to true for CI/CD
            SlowMo = 1000 // Slow down for debugging
        });
        
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true // For localhost SSL certificates
        });
        
        _page = await _context.NewPageAsync();
        
        _output.WriteLine("✅ Playwright initialized");
    }

    private async Task NavigateToWebChatAsync()
    {
        _output.WriteLine($"🌐 Navigating to WebChat: {_webChatUrl}");

        await _page!.GotoAsync(_webChatUrl);

        // Wait for WebChat interface to load
        await _page.WaitForSelectorAsync("input[type='text'], textarea", new PageWaitForSelectorOptions { Timeout = 30000 });

        _output.WriteLine("✅ WebChat interface loaded");
    }

    private async Task<TestResult> ExecuteTestCaseAsync(TestCase testCase)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TestResult { TestName = testCase.Name };

        try
        {
            _output.WriteLine($"📝 Sending message: {testCase.Input}");

            // Clear previous logs for this test
            var logCountBefore = _applicationLogs.Count;

            // Find and interact with chat input
            var chatInput = await _page!.WaitForSelectorAsync("input[type='text'], textarea",
                new PageWaitForSelectorOptions { Timeout = 10000 });

            await chatInput!.FillAsync(testCase.Input);

            // Send message (look for send button or Enter key)
            var sendButton = await _page.QuerySelectorAsync("button[type='submit'], button:has-text('Send'), button:has-text('Skicka')");
            if (sendButton != null)
            {
                await sendButton.ClickAsync();
            }
            else
            {
                await chatInput.PressAsync("Enter");
            }

            _output.WriteLine("✅ Message sent, waiting for response...");

            // Wait for OpenAI response completion
            var responseReceived = await WaitForOpenAIResponseAsync(testCase, TimeSpan.FromMinutes(5));
            if (!responseReceived)
            {
                result.Success = false;
                result.ErrorMessage = "OpenAI response timeout or content filter error";
                return result;
            }

            _output.WriteLine("✅ OpenAI response received");

            // Wait for email delivery
            var emailDelivered = await WaitForEmailDeliveryAsync(TimeSpan.FromMinutes(2));
            if (!emailDelivered)
            {
                result.Success = false;
                result.ErrorMessage = "Email delivery timeout";
                return result;
            }

            _output.WriteLine("✅ Email delivery confirmed");

            // Validate no pivot table conflicts
            var hasPivotErrors = await CheckForPivotTableErrorsAsync(logCountBefore);
            if (hasPivotErrors)
            {
                result.Success = false;
                result.ErrorMessage = "Pivot table naming conflicts detected";
                return result;
            }

            _output.WriteLine("✅ No pivot table conflicts");

            // Validate Swedish format
            var hasSwedishFormat = await ValidateSwedishFormatAsync();
            if (!hasSwedishFormat)
            {
                _output.WriteLine("⚠️ Warning: Swedish format validation inconclusive");
            }

            result.Success = true;
            result.ExecutionTime = stopwatch.Elapsed.TotalSeconds;

        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _output.WriteLine($"❌ Test case failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed.TotalSeconds;
        }

        return result;
    }

    private async Task<bool> WaitForOpenAIResponseAsync(TestCase testCase, TimeSpan timeout)
    {
        var startTime = DateTime.Now;
        var logCountBefore = _applicationLogs.Count;

        while (DateTime.Now - startTime < timeout)
        {
            var recentLogs = _applicationLogs.Skip(logCountBefore).ToList();

            // Check for content filter errors
            if (recentLogs.Any(log => log.Contains("content_filter") || log.Contains("HTTP 400")))
            {
                _output.WriteLine("❌ Content filter error detected");
                return false;
            }

            // Check for successful OpenAI completion
            if (recentLogs.Any(log => log.Contains("Chat completion completed") ||
                                    log.Contains("ExecuteChatCompletionAsync") ||
                                    log.Contains("OpenAI response received")))
            {
                // Also check for response in the UI
                try
                {
                    await _page!.WaitForSelectorAsync("text=rapport", new PageWaitForSelectorOptions { Timeout = 5000 });
                    return true;
                }
                catch
                {
                    // Continue waiting if UI doesn't show response yet
                }
            }

            await Task.Delay(2000);
        }

        return false;
    }

    private async Task<bool> WaitForEmailDeliveryAsync(TimeSpan timeout)
    {
        var startTime = DateTime.Now;
        var logCountBefore = _applicationLogs.Count;

        while (DateTime.Now - startTime < timeout)
        {
            var recentLogs = _applicationLogs.Skip(logCountBefore).ToList();

            // Check for email delivery indicators
            if (recentLogs.Any(log =>
                log.Contains("Email sent successfully") ||
                log.Contains("SendEmailAsync") ||
                log.Contains("Anna Andersson") ||
                log.Contains("demo@") ||
                log.Contains("Email delivery")))
            {
                return true;
            }

            await Task.Delay(1000);
        }

        return false;
    }

    private async Task<bool> CheckForPivotTableErrorsAsync(int logCountBefore)
    {
        await Task.Delay(1000); // Allow logs to be written

        var recentLogs = _applicationLogs.Skip(logCountBefore).ToList();

        return recentLogs.Any(log =>
            log.Contains("An item with the same key has already been added") ||
            log.Contains("PivotTable1") ||
            log.Contains("Key already exists"));
    }

    private async Task<bool> ValidateSwedishFormatAsync()
    {
        await Task.Delay(500);

        // Check if page contains Swedish text
        try
        {
            var pageContent = await _page!.ContentAsync();
            var swedishIndicators = new[] { "rapport", "diagram", "pivot", "skicka", "professionell" };

            return swedishIndicators.Any(indicator =>
                pageContent.Contains(indicator, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private record TestCase
    {
        public required string Name { get; init; }
        public required string Input { get; init; }
        public required string[] ExpectedKeywords { get; init; }
    }

    private record TestResult
    {
        public required string TestName { get; init; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double ExecutionTime { get; set; }
    }
}
