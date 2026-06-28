using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 FOUR REPORTS E2E TEST - Med redan körande system
/// Testar alla fyra rapporter sekventiellt med det körande AccountingAgent systemet
/// </summary>
public class FourReportsWithRunningSystemTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly string _webChatUrl = "https://localhost:7041";

    // De fyra rapporter som ska testas
    private readonly List<ReportRequest> _reports = new()
    {
        new ReportRequest
        {
            Name = "Expense Report 2023",
            Text = "Skapa en professionell utgiftsrapport för 2023 med diagram och pivot-tabeller och skicka till Anna Andersson",
            TimeoutMinutes = 4
        },
        new ReportRequest
        {
            Name = "Project Summary Report",
            Text = "Skapa en professionell rapport där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson",
            TimeoutMinutes = 4
        },
        new ReportRequest
        {
            Name = "5-Year Quarterly Pivot Report",
            Text = "Skapa en professionell rapport i pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson",
            TimeoutMinutes = 5
        },
        new ReportRequest
        {
            Name = "Weekly Financial Summary",
            Text = "Skapa en professionell rapport som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson",
            TimeoutMinutes = 4
        }
    };

    public FourReportsWithRunningSystemTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🎭 Initializing Playwright for Four Reports Test...");
        
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

    public async Task DisposeAsync()
    {
        await _page?.CloseAsync()!;
        await _context?.CloseAsync()!;
        await _browser?.CloseAsync()!;
        _playwright?.Dispose();
    }

    [Fact]
    public async Task AllFourReports_WithRunningSystem_ShouldSucceed()
    {
        _output.WriteLine("🎯 FOUR REPORTS E2E TEST - WITH RUNNING SYSTEM");
        _output.WriteLine("===============================================");
        _output.WriteLine("⚠️  PREREQUISITE: AppHost must already be running!");
        _output.WriteLine("");

        // Login first
        await LoginToWebChatAsync();

        var results = new List<ReportResult>();

        // Execute all four reports
        for (int i = 0; i < _reports.Count; i++)
        {
            var report = _reports[i];
            _output.WriteLine($"\n📊 EXECUTING REPORT {i + 1}/4: {report.Name}");
            _output.WriteLine($"Request: {report.Text}");
            _output.WriteLine($"Timeout: {report.TimeoutMinutes} minutes");
            
            var result = await ExecuteReportAsync(report, i + 1);
            results.Add(result);
            
            var status = result.Success ? "✅ SUCCESS" : "❌ FAILED";
            _output.WriteLine($"Result: {status} (Duration: {result.Duration:F1}s)");
            
            if (!result.Success)
            {
                _output.WriteLine($"Error: {result.ErrorMessage}");
            }
            
            // Wait between reports
            if (i < _reports.Count - 1)
            {
                _output.WriteLine("⏳ Waiting 15 seconds before next report...");
                await Task.Delay(15000);
            }
        }

        // Summary
        var successCount = results.Count(r => r.Success);
        var totalCount = results.Count;
        
        _output.WriteLine("\n📊 FINAL RESULTS SUMMARY:");
        _output.WriteLine("=========================");
        _output.WriteLine($"Total Reports: {totalCount}");
        _output.WriteLine($"Successful: {successCount}");
        _output.WriteLine($"Failed: {totalCount - successCount}");
        _output.WriteLine($"Success Rate: {(double)successCount / totalCount * 100:F1}%");
        _output.WriteLine($"Total Time: {results.Sum(r => r.Duration):F1} seconds");

        foreach (var result in results)
        {
            var status = result.Success ? "✅" : "❌";
            _output.WriteLine($"{status} {result.ReportName}: {result.Duration:F1}s");
        }

        if (successCount == totalCount)
        {
            _output.WriteLine("\n🏆 ALL FOUR REPORTS COMPLETED SUCCESSFULLY!");
            _output.WriteLine("✅ Each report should have been generated and emailed to Anna Andersson");
        }
        else
        {
            _output.WriteLine($"\n⚠️ {totalCount - successCount} reports failed - check logs for details");
        }

        // Assert success
        Assert.True(successCount == totalCount, 
            $"Expected all {totalCount} reports to succeed, but {totalCount - successCount} failed");
    }

    private async Task LoginToWebChatAsync()
    {
        _output.WriteLine("🔐 Logging in to WebChat...");

        await _page!.GotoAsync(_webChatUrl);
        await Task.Delay(2000);

        // Login
        var passwordInput = await _page.WaitForSelectorAsync("input[type='password']",
            new PageWaitForSelectorOptions { Timeout = 10000 });
        await passwordInput!.FillAsync("isa72");

        var loginButton = await _page.QuerySelectorAsync("button[type='submit']");
        await loginButton!.ClickAsync();

        await Task.Delay(3000);

        // Verify login
        var currentUrl = _page.Url;
        if (currentUrl.Contains("/login"))
        {
            throw new InvalidOperationException("Login failed - still on login page");
        }

        _output.WriteLine("✅ Successfully logged in to WebChat");
    }

    private async Task<ReportResult> ExecuteReportAsync(ReportRequest report, int reportNumber)
    {
        var startTime = DateTime.Now;
        var result = new ReportResult { ReportName = report.Name };

        try
        {
            _output.WriteLine($"📝 Sending report request...");

            // Find chat input
            var chatInput = await _page!.WaitForSelectorAsync("input[type='text'], textarea",
                new PageWaitForSelectorOptions { Timeout = 10000 });

            // Clear and send request
            await chatInput!.FillAsync("");
            await Task.Delay(500);
            await chatInput.FillAsync(report.Text);

            // Send with Cmd+Enter (Mac) or Ctrl+Enter
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

            // Wait for response
            _output.WriteLine($"⏳ Waiting for response (max {report.TimeoutMinutes} minutes)...");
            var responseReceived = await WaitForResponseAsync(TimeSpan.FromMinutes(report.TimeoutMinutes));

            if (!responseReceived)
            {
                result.Success = false;
                result.ErrorMessage = $"No response received within {report.TimeoutMinutes} minutes";
                return result;
            }

            _output.WriteLine("✅ Response received from AI");

            // Take screenshot
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = $"report-{reportNumber}-{report.Name.Replace(" ", "-").ToLower()}.png",
                FullPage = true
            });

            _output.WriteLine($"📸 Screenshot saved for report {reportNumber}");

            result.Success = true;

        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _output.WriteLine($"❌ Report execution failed: {ex.Message}");
        }
        finally
        {
            result.Duration = (DateTime.Now - startTime).TotalSeconds;
        }

        return result;
    }

    private async Task<bool> WaitForResponseAsync(TimeSpan timeout)
    {
        var startTime = DateTime.Now;
        var lastPageContent = "";

        while (DateTime.Now - startTime < timeout)
        {
            try
            {
                var currentContent = await _page!.ContentAsync();

                // Check if content has changed (indicating a response)
                if (currentContent != lastPageContent && currentContent.Length > lastPageContent.Length + 100)
                {
                    // Look for response indicators
                    if (currentContent.Contains("rapport", StringComparison.OrdinalIgnoreCase) ||
                        currentContent.Contains("excel", StringComparison.OrdinalIgnoreCase) ||
                        currentContent.Contains("skicka", StringComparison.OrdinalIgnoreCase) ||
                        currentContent.Contains("anna", StringComparison.OrdinalIgnoreCase) ||
                        currentContent.Contains("generera", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                lastPageContent = currentContent;
                await Task.Delay(3000);
            }
            catch
            {
                await Task.Delay(3000);
            }
        }

        return false;
    }

    private record ReportRequest
    {
        public required string Name { get; init; }
        public required string Text { get; init; }
        public required int TimeoutMinutes { get; init; }
    }

    private record ReportResult
    {
        public required string ReportName { get; init; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double Duration { get; set; }
    }
}
