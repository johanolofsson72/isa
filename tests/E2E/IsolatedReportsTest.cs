using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 ISOLATED REPORTS TEST
/// Testar rapporter med isolerade sessioner för att säkerställa att varje rapport är unik
/// </summary>
public class IsolatedReportsTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly string _webChatUrl = "https://localhost:7041";

    // De fyra rapporter som ska testas med olika sessioner
    private readonly List<ReportRequest> _reports = new()
    {
        new ReportRequest
        {
            Name = "Expense Report 2023",
            Text = "Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "utgifter", "utgiftsrapport", "2023", "startsida", "diagram" },
            UnexpectedKeywords = new[] { "2024", "2022", "projekt", "bruttovinst" }
        },
        new ReportRequest
        {
            Name = "Project Summary Report 2024",
            Text = "Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "projekt", "2024", "intäkter", "filtrera", "startsida" },
            UnexpectedKeywords = new[] { "2023", "2022", "utgifter", "bruttovinst" }
        },
        new ReportRequest
        {
            Name = "5-Year Quarterly Pivot Report",
            Text = "Skapa en professionell rapport med prydlig startsida och sedan sidor för pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "omsättning", "varukostnad", "kvartal", "5 år", "startsida", "pivot" },
            UnexpectedKeywords = new[] { "2023", "2024", "2022", "projekt", "vecka" }
        },
        new ReportRequest
        {
            Name = "Weekly Financial Summary 2022",
            Text = "Skapa en professionell rapport för 2022 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson",
            ExpectedKeywords = new[] { "bruttovinst", "2022", "SEK", "vecka", "startsida" },
            UnexpectedKeywords = new[] { "2023", "2024", "projekt", "kvartal" }
        }
    };

    public IsolatedReportsTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🎭 Initializing Playwright for Isolated Reports Test...");
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 500
        });
        
        _output.WriteLine("✅ Playwright initialized");
    }

    public async Task DisposeAsync()
    {
        await _browser?.CloseAsync()!;
        _playwright?.Dispose();
    }

    [Fact]
    public async Task IsolatedReports_WithSeparateSessions_ShouldGenerateUniqueContent()
    {
        _output.WriteLine("🎯 ISOLATED REPORTS TEST - SEPARATE SESSIONS");
        _output.WriteLine("============================================");
        _output.WriteLine("⚠️  PREREQUISITE: AppHost must already be running!");
        _output.WriteLine("");

        var results = new List<ReportResult>();

        // Execute each report in a completely separate browser context/session
        for (int i = 0; i < _reports.Count; i++)
        {
            var report = _reports[i];
            _output.WriteLine($"\n📊 EXECUTING ISOLATED REPORT {i + 1}/4: {report.Name}");
            
            var result = await ExecuteIsolatedReportAsync(report, i + 1);
            results.Add(result);
            
            var status = result.Success ? "✅ SUCCESS" : "❌ FAILED";
            _output.WriteLine($"Result: {status} (Duration: {result.Duration:F1}s)");
            _output.WriteLine($"Unique Content: {(result.IsUnique ? "✅ YES" : "❌ NO")}");
            
            if (!result.Success)
            {
                _output.WriteLine($"Error: {result.ErrorMessage}");
            }
        }

        // Summary
        var uniqueCount = results.Count(r => r.IsUnique);
        var successCount = results.Count(r => r.Success);
        var totalCount = results.Count;
        
        _output.WriteLine("\n📊 ISOLATED REPORTS RESULTS:");
        _output.WriteLine("============================");
        _output.WriteLine($"Total Reports: {totalCount}");
        _output.WriteLine($"Successful: {successCount}");
        _output.WriteLine($"Unique Content: {uniqueCount}");
        _output.WriteLine($"Success Rate: {(double)successCount / totalCount * 100:F1}%");
        _output.WriteLine($"Uniqueness Rate: {(double)uniqueCount / totalCount * 100:F1}%");

        foreach (var result in results)
        {
            var status = result.Success ? "✅" : "❌";
            var unique = result.IsUnique ? "🎯" : "🔄";
            _output.WriteLine($"{status}{unique} {result.ReportName}: {result.Duration:F1}s");
        }

        if (uniqueCount == totalCount)
        {
            _output.WriteLine("\n🏆 SUCCESS: All reports have unique content with isolated sessions!");
        }
        else
        {
            _output.WriteLine($"\n⚠️ WARNING: {totalCount - uniqueCount} reports still contain generic content");
            _output.WriteLine("This indicates deeper issues with report generation logic.");
        }

        // Assert that isolated sessions produce unique content
        Assert.True(uniqueCount >= totalCount * 0.75, 
            $"Expected at least 75% of isolated reports to have unique content, but only {(double)uniqueCount / totalCount * 100:F1}% did. " +
            $"This indicates fundamental issues with the report generation system.");
    }

    private async Task<ReportResult> ExecuteIsolatedReportAsync(ReportRequest report, int reportNumber)
    {
        var startTime = DateTime.Now;
        var result = new ReportResult { ReportName = report.Name };

        // Create completely isolated browser context for this report
        IBrowserContext? context = null;
        IPage? page = null;

        try
        {
            _output.WriteLine($"🔒 Creating isolated browser context for {report.Name}...");

            // New isolated context (separate cookies, session, etc.)
            context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true
            });

            page = await context.NewPageAsync();

            // Navigate and login in isolated context
            await page.GotoAsync(_webChatUrl);
            await Task.Delay(2000);

            // Login
            var passwordInput = await page.WaitForSelectorAsync("input[type='password']");
            await passwordInput!.FillAsync("isa72");

            var loginButton = await page.QuerySelectorAsync("button[type='submit']");
            await loginButton!.ClickAsync();

            await Task.Delay(3000);

            _output.WriteLine($"✅ Logged in to isolated session for {report.Name}");

            // Send report request
            var chatInput = await page.WaitForSelectorAsync("input[type='text'], textarea");
            await chatInput!.FillAsync(report.Text);

            // Send with Cmd+Enter
            var isMac = Environment.OSVersion.Platform == PlatformID.Unix &&
                       Directory.Exists("/Applications");

            if (isMac)
            {
                await chatInput.PressAsync("Meta+Enter");
            }
            else
            {
                await chatInput.PressAsync("Control+Enter");
            }

            _output.WriteLine($"📝 Sent isolated request for {report.Name}");

            // Wait for response
            await Task.Delay(30000); // 30 seconds for response

            // Capture content and analyze uniqueness
            var pageContent = await page.ContentAsync();
            result.Content = pageContent;

            // Check for expected keywords
            var expectedFound = report.ExpectedKeywords.Count(keyword =>
                pageContent.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            // Check for unexpected keywords (from other reports)
            var unexpectedFound = report.UnexpectedKeywords.Count(keyword =>
                pageContent.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            var expectedScore = (double)expectedFound / report.ExpectedKeywords.Length;
            result.IsUnique = expectedScore >= 0.5 && unexpectedFound <= 1;

            _output.WriteLine($"📊 Content Analysis for {report.Name}:");
            _output.WriteLine($"   Expected keywords: {expectedFound}/{report.ExpectedKeywords.Length} ({expectedScore:P0})");
            _output.WriteLine($"   Unexpected keywords: {unexpectedFound}/{report.UnexpectedKeywords.Length}");

            // Take screenshot
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = $"isolated-report-{reportNumber}-{report.Name.Replace(" ", "-").ToLower()}.png",
                FullPage = true
            });

            result.Success = true;

        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _output.WriteLine($"❌ Isolated report execution failed: {ex.Message}");
        }
        finally
        {
            // Clean up isolated context
            await page?.CloseAsync()!;
            await context?.CloseAsync()!;

            result.Duration = (DateTime.Now - startTime).TotalSeconds;
        }

        return result;
    }

    private record ReportRequest
    {
        public required string Name { get; init; }
        public required string Text { get; init; }
        public required string[] ExpectedKeywords { get; init; }
        public required string[] UnexpectedKeywords { get; init; }
    }

    private record ReportResult
    {
        public required string ReportName { get; init; }
        public bool Success { get; set; }
        public bool IsUnique { get; set; }
        public string? Content { get; set; }
        public string? ErrorMessage { get; set; }
        public double Duration { get; set; }
    }
}
