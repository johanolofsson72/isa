using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using System.Text.RegularExpressions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 REPORT CONTENT VALIDATION TEST
/// Validerar att olika rapporter faktiskt innehåller olika data och inte bara samma statiska innehåll
/// </summary>
public class ReportContentValidationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private readonly string _webChatUrl = "https://localhost:7041";

    // Test cases med förväntade unika nyckelord för varje rapport
    private readonly List<ReportValidationCase> _validationCases = new()
    {
        new ReportValidationCase
        {
            Name = "Expense Report 2023",
            Request = "Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson",
            ExpectedUniqueContent = new[] { "utgifter", "utgiftsrapport", "2023", "kostnader", "startsida", "diagram" },
            UnexpectedContent = new[] { "2024", "2022", "projekt", "bruttovinst", "vecka" }
        },
        new ReportValidationCase
        {
            Name = "Project Summary Report 2024",
            Request = "Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson",
            ExpectedUniqueContent = new[] { "projekt", "2024", "intäkter", "filtrera", "datum", "startsida" },
            UnexpectedContent = new[] { "2023", "2022", "utgifter", "bruttovinst", "vecka" }
        },
        new ReportValidationCase
        {
            Name = "5-Year Quarterly Pivot Report",
            Request = "Skapa en professionell rapport med prydlig startsida och sedan sidor för pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson",
            ExpectedUniqueContent = new[] { "omsättning", "varukostnad", "personalkostnader", "kvartal", "5 år", "startsida", "pivot" },
            UnexpectedContent = new[] { "2023", "2024", "2022", "projekt", "vecka" }
        },
        new ReportValidationCase
        {
            Name = "Weekly Financial Summary 2022",
            Request = "Skapa en professionell rapport för 2022 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson",
            ExpectedUniqueContent = new[] { "bruttovinst", "2022", "SEK", "vecka", "%", "startsida" },
            UnexpectedContent = new[] { "2023", "2024", "projekt", "kvartal", "5 år" }
        }
    };

    public ReportContentValidationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🎭 Initializing Playwright for Report Content Validation...");
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
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
    public async Task ReportContent_ShouldBeDifferentForEachReportType()
    {
        _output.WriteLine("🎯 REPORT CONTENT VALIDATION TEST");
        _output.WriteLine("=================================");
        _output.WriteLine("⚠️  PREREQUISITE: AppHost must already be running!");
        _output.WriteLine("");

        // Login first
        await LoginToWebChatAsync();

        var reportContents = new List<ReportContentResult>();

        // Execute each report and capture content
        for (int i = 0; i < _validationCases.Count; i++)
        {
            var testCase = _validationCases[i];
            _output.WriteLine($"\n📊 TESTING REPORT {i + 1}/4: {testCase.Name}");
            
            var contentResult = await CaptureReportContentAsync(testCase, i + 1);
            reportContents.Add(contentResult);
            
            // Wait between reports
            if (i < _validationCases.Count - 1)
            {
                _output.WriteLine("⏳ Waiting 10 seconds before next report...");
                await Task.Delay(10000);
            }
        }

        // Validate content differences
        await ValidateContentDifferencesAsync(reportContents);
    }

    private async Task LoginToWebChatAsync()
    {
        _output.WriteLine("🔐 Logging in to WebChat...");
        
        await _page!.GotoAsync(_webChatUrl);
        await Task.Delay(2000);

        var passwordInput = await _page.WaitForSelectorAsync("input[type='password']");
        await passwordInput!.FillAsync("isa72");
        
        var loginButton = await _page.QuerySelectorAsync("button[type='submit']");
        await loginButton!.ClickAsync();
        
        await Task.Delay(3000);
        
        _output.WriteLine("✅ Successfully logged in to WebChat");
    }

    private async Task<ReportContentResult> CaptureReportContentAsync(ReportValidationCase testCase, int reportNumber)
    {
        var result = new ReportContentResult { ReportName = testCase.Name };
        
        try
        {
            _output.WriteLine($"📝 Sending request: {testCase.Request}");
            
            // Clear chat and send request
            var chatInput = await _page!.WaitForSelectorAsync("input[type='text'], textarea");
            await chatInput!.FillAsync("");
            await Task.Delay(500);
            await chatInput.FillAsync(testCase.Request);
            
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
            
            _output.WriteLine("✅ Request sent, waiting for response...");
            
            // Wait for response
            await Task.Delay(30000); // Wait 30 seconds for full response
            
            // Capture page content
            var pageContent = await _page.ContentAsync();
            result.Content = pageContent;
            result.Success = true;
            
            // Take screenshot
            await _page.ScreenshotAsync(new PageScreenshotOptions 
            { 
                Path = $"content-validation-{reportNumber}-{testCase.Name.Replace(" ", "-").ToLower()}.png",
                FullPage = true 
            });
            
            _output.WriteLine($"📸 Screenshot saved for report {reportNumber}");
            
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _output.WriteLine($"❌ Failed to capture content: {ex.Message}");
        }
        
        return result;
    }

    private Task ValidateContentDifferencesAsync(List<ReportContentResult> reportContents)
    {
        _output.WriteLine("\n🔍 VALIDATING CONTENT DIFFERENCES");
        _output.WriteLine("=================================");

        var validationResults = new List<ContentValidationResult>();

        for (int i = 0; i < reportContents.Count; i++)
        {
            var report = reportContents[i];
            var testCase = _validationCases[i];

            if (!report.Success)
            {
                _output.WriteLine($"❌ {report.ReportName}: Failed to capture content");
                continue;
            }

            var validation = new ContentValidationResult { ReportName = report.ReportName };

            // Check for expected unique content
            var expectedFound = 0;
            var expectedTotal = testCase.ExpectedUniqueContent.Length;

            foreach (var expectedKeyword in testCase.ExpectedUniqueContent)
            {
                if (report.Content!.Contains(expectedKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    expectedFound++;
                    _output.WriteLine($"  ✅ Found expected keyword: '{expectedKeyword}'");
                }
                else
                {
                    _output.WriteLine($"  ❌ Missing expected keyword: '{expectedKeyword}'");
                }
            }

            // Check for unexpected content (should not be present)
            var unexpectedFound = 0;
            var unexpectedTotal = testCase.UnexpectedContent.Length;

            foreach (var unexpectedKeyword in testCase.UnexpectedContent)
            {
                if (report.Content!.Contains(unexpectedKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    unexpectedFound++;
                    _output.WriteLine($"  ⚠️ Found unexpected keyword: '{unexpectedKeyword}' (indicates generic content)");
                }
            }

            validation.ExpectedContentScore = (double)expectedFound / expectedTotal;
            validation.UnexpectedContentFound = unexpectedFound;
            validation.IsUnique = validation.ExpectedContentScore >= 0.5 && unexpectedFound <= 1;

            _output.WriteLine($"📊 {report.ReportName}:");
            _output.WriteLine($"   Expected content: {expectedFound}/{expectedTotal} ({validation.ExpectedContentScore:P0})");
            _output.WriteLine($"   Unexpected content: {unexpectedFound}/{unexpectedTotal}");
            _output.WriteLine($"   Unique content: {(validation.IsUnique ? "✅ YES" : "❌ NO")}");

            validationResults.Add(validation);
        }

        // Overall assessment
        var uniqueReports = validationResults.Count(v => v.IsUnique);
        var totalReports = validationResults.Count;

        _output.WriteLine("\n📊 OVERALL CONTENT VALIDATION RESULTS:");
        _output.WriteLine("======================================");
        _output.WriteLine($"Reports with unique content: {uniqueReports}/{totalReports}");
        _output.WriteLine($"Content uniqueness rate: {(double)uniqueReports / totalReports:P0}");

        foreach (var result in validationResults)
        {
            var status = result.IsUnique ? "✅ UNIQUE" : "❌ GENERIC";
            _output.WriteLine($"{status} {result.ReportName}: {result.ExpectedContentScore:P0} expected, {result.UnexpectedContentFound} unexpected");
        }

        if (uniqueReports == totalReports)
        {
            _output.WriteLine("\n🏆 SUCCESS: All reports contain unique, differentiated content!");
        }
        else
        {
            _output.WriteLine($"\n⚠️ WARNING: {totalReports - uniqueReports} reports appear to contain generic/duplicate content");
            _output.WriteLine("This indicates that the AI is not properly differentiating between report types.");
        }

        // Assert that at least 75% of reports have unique content
        var uniquenessThreshold = 0.75;
        var actualUniquenessRate = (double)uniqueReports / totalReports;

        Assert.True(actualUniquenessRate >= uniquenessThreshold,
            $"Expected at least {uniquenessThreshold:P0} of reports to have unique content, but only {actualUniquenessRate:P0} did. " +
            $"This indicates the system is generating generic reports instead of differentiated content based on user requests.");

        return Task.CompletedTask;
    }

    private record ReportValidationCase
    {
        public required string Name { get; init; }
        public required string Request { get; init; }
        public required string[] ExpectedUniqueContent { get; init; }
        public required string[] UnexpectedContent { get; init; }
    }

    private record ReportContentResult
    {
        public required string ReportName { get; init; }
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private record ContentValidationResult
    {
        public required string ReportName { get; init; }
        public double ExpectedContentScore { get; set; }
        public int UnexpectedContentFound { get; set; }
        public bool IsUnique { get; set; }
    }
}
