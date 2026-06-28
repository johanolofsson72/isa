using Microsoft.Playwright;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 COMPREHENSIVE FOUR REPORTS E2E TEST
/// Testar alla fyra rapporter sekventiellt med full validering
/// </summary>
public class ComprehensiveFourReportsE2ETest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private Process? _appHostProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly List<string> _applicationLogs = new();
    private readonly string _appHostPath;
    private readonly string _webChatUrl = "https://localhost:7041";

    // Test cases för alla fyra rapporter
    private readonly List<ReportTestCase> _reportTestCases = new()
    {
        new ReportTestCase
        {
            Name = "Expense Report 2023",
            Request = "Skapa en professionell utgiftsrapport för 2023 med diagram och pivot-tabeller och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "utgiftsrapport", "2023", "diagram", "pivot", "Anna Andersson" },
            TimeoutMinutes = 3
        },
        new ReportTestCase
        {
            Name = "Project Summary Report",
            Request = "Skapa en professionell rapport där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "projekt", "intäkter", "kostnader", "filtrera", "datum", "Anna Andersson" },
            TimeoutMinutes = 3
        },
        new ReportTestCase
        {
            Name = "5-Year Quarterly Pivot Report",
            Request = "Skapa en professionell rapport i pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "pivot", "omsättning", "varukostnad", "personalkostnader", "kvartal", "5 åren", "Anna Andersson" },
            TimeoutMinutes = 4
        },
        new ReportTestCase
        {
            Name = "Weekly Financial Summary",
            Request = "Skapa en professionell rapport som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "omsättning", "varukostnad", "bruttovinst", "SEK", "vecka", "Anna Andersson" },
            TimeoutMinutes = 3
        }
    };

    public ComprehensiveFourReportsE2ETest(ITestOutputHelper output)
    {
        _output = output;
        
        // Find repository root
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir, ".git")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        
        if (currentDir == null)
        {
            throw new InvalidOperationException("Could not find repository root");
        }
        
        _appHostPath = Path.Combine(currentDir, "src", "AppHost");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🚀 INITIALIZING COMPREHENSIVE FOUR REPORTS E2E TEST");
        _output.WriteLine("===================================================");

        // 1. Start AppHost
        await StartAppHostAsync();

        // 2. Wait for services
        await WaitForServicesReadyAsync();

        // 3. Initialize Playwright
        await InitializePlaywrightAsync();

        // 4. Navigate and login to WebChat
        await NavigateAndLoginAsync();

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
    public async Task AllFourReports_ShouldGenerateAndEmailSuccessfully()
    {
        _output.WriteLine("🎯 STARTING COMPREHENSIVE FOUR REPORTS TEST");
        _output.WriteLine("===========================================");

        var results = new List<ReportTestResult>();

        for (int i = 0; i < _reportTestCases.Count; i++)
        {
            var testCase = _reportTestCases[i];
            _output.WriteLine($"\n📊 EXECUTING REPORT {i + 1}/4: {testCase.Name}");
            _output.WriteLine($"Request: {testCase.Request}");
            
            var result = await ExecuteReportTestAsync(testCase, i + 1);
            results.Add(result);
            
            var status = result.Success ? "✅ SUCCESS" : "❌ FAILED";
            _output.WriteLine($"Result: {status} (Duration: {result.ExecutionTime:F1}s)");
            
            if (!result.Success)
            {
                _output.WriteLine($"Error: {result.ErrorMessage}");
            }
            
            // Wait between reports to avoid overwhelming the system
            if (i < _reportTestCases.Count - 1)
            {
                _output.WriteLine("⏳ Waiting 10 seconds before next report...");
                await Task.Delay(10000);
            }
        }

        // Final results summary
        var successCount = results.Count(r => r.Success);
        var totalCount = results.Count;
        
        _output.WriteLine("\n📊 COMPREHENSIVE FOUR REPORTS TEST RESULTS:");
        _output.WriteLine("===========================================");
        _output.WriteLine($"Total Reports: {totalCount}");
        _output.WriteLine($"Successful: {successCount}");
        _output.WriteLine($"Failed: {totalCount - successCount}");
        _output.WriteLine($"Success Rate: {(double)successCount / totalCount * 100:F1}%");

        foreach (var result in results)
        {
            var status = result.Success ? "✅" : "❌";
            _output.WriteLine($"{status} {result.ReportName}: {result.ExecutionTime:F1}s");
            if (!result.Success)
            {
                _output.WriteLine($"   Error: {result.ErrorMessage}");
            }
        }

        // Assert that all reports succeeded
        Assert.True(successCount == totalCount, 
            $"Expected all {totalCount} reports to succeed, but {totalCount - successCount} failed");
        
        _output.WriteLine("\n🏆 ALL FOUR REPORTS GENERATED AND EMAILED SUCCESSFULLY!");
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

        // Monitor logs in background
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
        await Task.Delay(3000); // Give process time to start
    }

    private async Task WaitForServicesReadyAsync()
    {
        _output.WriteLine("⏳ Waiting for services to be ready...");

        var timeout = TimeSpan.FromMinutes(3);
        var startTime = DateTime.Now;

        while (DateTime.Now - startTime < timeout)
        {
            var recentLogs = _applicationLogs.TakeLast(100).ToList();

            var hasAgent = recentLogs.Any(log => log.Contains("agent-https is now in state Ready"));
            var hasWebChat = recentLogs.Any(log => log.Contains("webchat-https is now in state Ready"));
            var hasDashboard = recentLogs.Any(log => log.Contains("aspire-dashboard-https is now in state Ready"));

            if (hasAgent && hasWebChat && hasDashboard)
            {
                _output.WriteLine("✅ All services are ready!");
                await Task.Delay(5000); // Extra wait for full initialization
                return;
            }

            await Task.Delay(3000);
        }

        throw new TimeoutException("Services did not become ready within the timeout period");
    }

    private async Task InitializePlaywrightAsync()
    {
        _output.WriteLine("🎭 Initializing Playwright...");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Visible for debugging
            SlowMo = 500
        });

        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });

        _page = await _context.NewPageAsync();

        _output.WriteLine("✅ Playwright initialized");
    }

    private async Task NavigateAndLoginAsync()
    {
        _output.WriteLine($"🌐 Navigating to WebChat and logging in...");

        await _page!.GotoAsync(_webChatUrl);
        await Task.Delay(2000);

        // Login
        var passwordInput = await _page.WaitForSelectorAsync("input[type='password']",
            new PageWaitForSelectorOptions { Timeout = 30000 });
        await passwordInput!.FillAsync("isa72");

        var loginButton = await _page.QuerySelectorAsync("button[type='submit']");
        await loginButton!.ClickAsync();

        await Task.Delay(3000);

        // Verify we're logged in
        var currentUrl = _page.Url;
        if (!currentUrl.Contains("/login"))
        {
            _output.WriteLine("✅ Successfully logged in to WebChat");
        }
        else
        {
            throw new InvalidOperationException("Login failed - still on login page");
        }
    }

    private async Task<ReportTestResult> ExecuteReportTestAsync(ReportTestCase testCase, int reportNumber)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ReportTestResult { ReportName = testCase.Name };
        var logCountBefore = _applicationLogs.Count;

        try
        {
            _output.WriteLine($"📝 Sending report request...");

            // Find chat input
            var chatInput = await _page!.WaitForSelectorAsync("input[type='text'], textarea",
                new PageWaitForSelectorOptions { Timeout = 10000 });

            // Clear any existing text and send the request
            await chatInput!.FillAsync("");
            await chatInput.FillAsync(testCase.Request);

            // Send with platform-appropriate key combination
            var isMac = Environment.OSVersion.Platform == PlatformID.Unix &&
                       Directory.Exists("/Applications");

            if (isMac)
            {
                await chatInput.PressAsync("Meta+Enter");
                _output.WriteLine("✅ Sent with Cmd+Enter");
            }
            else
            {
                await chatInput.PressAsync("Control+Enter");
                _output.WriteLine("✅ Sent with Ctrl+Enter");
            }

            // Wait for OpenAI response
            _output.WriteLine($"⏳ Waiting for OpenAI response (max {testCase.TimeoutMinutes} minutes)...");
            var openAISuccess = await WaitForOpenAIResponseAsync(testCase, TimeSpan.FromMinutes(testCase.TimeoutMinutes));

            if (!openAISuccess)
            {
                result.Success = false;
                result.ErrorMessage = "OpenAI response timeout or content filter error";
                return result;
            }

            _output.WriteLine("✅ OpenAI response received");

            // Wait for email delivery
            _output.WriteLine("⏳ Waiting for email delivery to Anna Andersson...");
            var emailSuccess = await WaitForEmailDeliveryAsync(TimeSpan.FromMinutes(2));

            if (!emailSuccess)
            {
                result.Success = false;
                result.ErrorMessage = "Email delivery timeout - no confirmation found in logs";
                return result;
            }

            _output.WriteLine("✅ Email delivery confirmed");

            // Check for any errors in logs
            var hasErrors = await CheckForErrorsInLogsAsync(logCountBefore);
            if (hasErrors)
            {
                result.Success = false;
                result.ErrorMessage = "Errors detected in application logs";
                return result;
            }

            // Take screenshot for documentation
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = $"report-{reportNumber}-{testCase.Name.Replace(" ", "-").ToLower()}.png",
                FullPage = true
            });

            _output.WriteLine($"📸 Screenshot saved for report {reportNumber}");

            result.Success = true;

        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _output.WriteLine($"❌ Report test failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed.TotalSeconds;
        }

        return result;
    }

    private async Task<bool> WaitForOpenAIResponseAsync(ReportTestCase testCase, TimeSpan timeout)
    {
        var startTime = DateTime.Now;
        var logCountBefore = _applicationLogs.Count;

        while (DateTime.Now - startTime < timeout)
        {
            var recentLogs = _applicationLogs.Skip(logCountBefore).ToList();

            // Check for content filter errors
            if (recentLogs.Any(log => log.Contains("content_filter") ||
                                    log.Contains("HTTP 400") ||
                                    log.Contains("content management policy")))
            {
                _output.WriteLine("❌ Content filter error detected in logs");
                return false;
            }

            // Check for successful completion indicators
            if (recentLogs.Any(log => log.Contains("Chat completion completed") ||
                                    log.Contains("ExecuteChatCompletionAsync") ||
                                    log.Contains("rapport") ||
                                    log.Contains("Report generated") ||
                                    log.Contains("Excel file created")))
            {
                // Additional check - look for response in UI
                try
                {
                    var pageContent = await _page!.ContentAsync();
                    if (testCase.ExpectedKeywords.Any(keyword =>
                        pageContent.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Continue waiting if UI check fails
                }
            }

            await Task.Delay(3000);
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
                log.Contains("Email sent successfully", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("SendEmailAsync", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("Anna Andersson", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("demo@", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("Email delivery", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("Mail sent", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("SMTP", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            await Task.Delay(2000);
        }

        return false;
    }

    private async Task<bool> CheckForErrorsInLogsAsync(int logCountBefore)
    {
        await Task.Delay(1000); // Allow logs to be written

        var recentLogs = _applicationLogs.Skip(logCountBefore).ToList();

        return recentLogs.Any(log =>
            log.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            log.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
            log.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
            log.Contains("An item with the same key has already been added") ||
            (log.Contains("HTTP") && (log.Contains("500") || log.Contains("400") || log.Contains("404"))));
    }

    private record ReportTestCase
    {
        public required string Name { get; init; }
        public required string Request { get; init; }
        public required string[] ExpectedKeywords { get; init; }
        public required int TimeoutMinutes { get; init; }
    }

    private record ReportTestResult
    {
        public required string ReportName { get; init; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double ExecutionTime { get; set; }
    }
}
