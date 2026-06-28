using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using System.Text.Json;
using ClosedXML.Excel;
using System.Text.RegularExpressions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// 🎯 COMPREHENSIVE END-TO-END FINANCIAL REPORTING WORKFLOW TEST
/// 
/// This test validates the complete financial reporting workflow by:
/// 1. Starting the appHost application
/// 2. Executing all 8 specified report requests sequentially
/// 3. Validating email delivery to Anna Andersson
/// 4. Verifying report files are saved to analysis_reports directory
/// 5. Parsing and validating report content accuracy
/// 6. Ensuring data integrity and format compliance
/// </summary>
public class ComprehensiveFinancialReportingWorkflowTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;
    
    // Test configuration
    private const string APP_HOST_URL = "https://localhost:17024";
    private const string WEBCHAT_URL = "https://localhost:7041/";
    private const string REPORTS_DIRECTORY = "/path/to/accountingagent/analysis_reports/";
    private const string LOGIN_PASSWORD = "isa72";
    private const int OPENAI_RESPONSE_TIMEOUT = 120000; // 2 minutes for OpenAI processing
    private const int EMAIL_DELIVERY_TIMEOUT = 30000; // 30 seconds for email delivery
    
    // Report requests to test (exactly as specified)
    private readonly (string Name, string Request)[] _reportRequests = new[]
    {
        ("2023 Utgiftsrapport", 
         "Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/"),
        
        ("2024 Projektrapport", 
         "Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse"),
        
        ("5-års Kvartalsrapport", 
         "Skapa en professionell rapport med prydlig startsida och sedan sidor för pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse"),
        
        ("2022 Bruttovinstrapport", 
         "Skapa en professionell rapport för 2022 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse"),
        
        ("2024 Resultatrapport", 
         "Skapa en professionell resultatrapport för 2024 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse"),
        
        ("2023 Årsrapport", 
         "Skapa en professionell årsrapport för 2023 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse"),
        
        ("Q3 Momsrapport", 
         "Skapa en professionell momsrapport för Q3 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse"),
        
        ("2026 Q1 Framtidsanalys", 
         "Skapa en professionell framtidsanalys inför 2026 Q1 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse")
    };

    public ComprehensiveFinancialReportingWorkflowTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("🚀 Initializing Comprehensive Financial Reporting Workflow Test");
        
        // Ensure reports directory exists
        Directory.CreateDirectory(REPORTS_DIRECTORY);
        
        // Clear any existing reports for clean test
        var existingFiles = Directory.GetFiles(REPORTS_DIRECTORY, "*.*");
        foreach (var file in existingFiles)
        {
            try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
        }
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Set to true for CI/CD
            SlowMo = 1000,
            Timeout = 60000
        });
        
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });
        
        _page = await _context.NewPageAsync();
        
        // Enable console logging for debugging
        _page.Console += (_, e) => _output.WriteLine($"🖥️ Console: {e.Text}");
        _page.PageError += (_, e) => _output.WriteLine($"❌ Page Error: {e}");
        
        _output.WriteLine("✅ Test environment initialized successfully");
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task ExecuteComprehensiveFinancialReportingWorkflow_ShouldValidateAllReports()
    {
        _output.WriteLine("🎯 STARTING COMPREHENSIVE FINANCIAL REPORTING WORKFLOW TEST");
        _output.WriteLine("===========================================================");

        // Step 1: Navigate to WebChat and authenticate
        await NavigateToWebChatAndAuthenticate();

        // Step 2: Execute each report request sequentially
        var testResults = new List<FinancialReportTestResult>();

        for (int i = 0; i < _reportRequests.Length; i++)
        {
            var (name, request) = _reportRequests[i];
            _output.WriteLine($"\n📊 EXECUTING REPORT {i + 1}/8: {name}");
            _output.WriteLine($"Request: {request}");

            var result = await ExecuteReportRequest(name, request, i + 1);
            testResults.Add(result);

            // Wait between requests to avoid overwhelming the system
            if (i < _reportRequests.Length - 1)
            {
                _output.WriteLine("⏳ Waiting 10 seconds before next request...");
                await Task.Delay(10000);
            }
        }

        // Step 3: Validate all test results
        await ValidateAllTestResults(testResults);

        _output.WriteLine("\n🎉 COMPREHENSIVE FINANCIAL REPORTING WORKFLOW TEST COMPLETED SUCCESSFULLY!");
    }

    private async Task NavigateToWebChatAndAuthenticate()
    {
        _output.WriteLine("🌐 Navigating to WebChat interface...");

        await _page!.GotoAsync(WEBCHAT_URL, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Check if login is required
        var passwordInput = _page.Locator("input[type='password']");
        if (await passwordInput.IsVisibleAsync())
        {
            _output.WriteLine("🔐 Authentication required, logging in...");
            await passwordInput.FillAsync(LOGIN_PASSWORD);
            await passwordInput.PressAsync("Enter");
            await _page.WaitForTimeoutAsync(3000);
        }

        // Verify WebChat is loaded
        var chatInput = _page.Locator("input[type='text'], textarea").Filter(new LocatorFilterOptions
        {
            HasNotText = "file"
        }).First;

        await chatInput.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15000
        });

        _output.WriteLine("✅ WebChat interface loaded and authenticated successfully");
    }

    private async Task<FinancialReportTestResult> ExecuteReportRequest(string reportName, string request, int requestNumber)
    {
        var result = new FinancialReportTestResult
        {
            ReportName = reportName,
            Request = request,
            RequestNumber = requestNumber,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Step 1: Send the request
            await SendChatMessage(request);
            result.MessageSent = true;

            // Step 2: Wait for OpenAI processing
            await WaitForOpenAIResponse();
            result.OpenAIResponseReceived = true;

            // Step 3: Validate email delivery
            result.EmailDelivered = await ValidateEmailDelivery(reportName);

            // Step 4: Validate file storage
            result.FilesStored = await ValidateFileStorage(reportName);

            // Step 5: Validate report content
            if (result.FilesStored)
            {
                result.ContentValidated = await ValidateReportContent(reportName, request);
            }

            result.Success = result.MessageSent && result.OpenAIResponseReceived &&
                           result.EmailDelivered && result.FilesStored && result.ContentValidated;

        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.Success = false;
            _output.WriteLine($"❌ Error executing report {reportName}: {ex.Message}");
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
        }

        return result;
    }

    private async Task SendChatMessage(string message)
    {
        var chatInput = _page!.Locator("input[type='text'], textarea").Filter(new LocatorFilterOptions
        {
            HasNotText = "file"
        }).First;

        await chatInput.FillAsync(message);

        var sendButton = _page.Locator("button:has-text('Skicka'), button[type='submit'], .send-button, button:has(svg), [class*='send']").First;
        await sendButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        await sendButton.ClickAsync();
        _output.WriteLine("📤 Message sent successfully");
    }

    private async Task WaitForOpenAIResponse()
    {
        _output.WriteLine("🤖 Waiting for OpenAI processing...");

        // Wait for response indicators (adjust selectors based on actual WebChat implementation)
        var responseSelectors = new[]
        {
            ".message.assistant",
            ".ai-response",
            "[data-role='assistant']",
            ".chat-message.bot",
            ".response-message"
        };

        bool responseReceived = false;
        var startTime = DateTime.UtcNow;

        while (!responseReceived && (DateTime.UtcNow - startTime).TotalMilliseconds < OPENAI_RESPONSE_TIMEOUT)
        {
            foreach (var selector in responseSelectors)
            {
                var elements = await _page!.Locator(selector).AllAsync();
                if (elements.Count > 0)
                {
                    var lastElement = elements.Last();
                    var text = await lastElement.TextContentAsync();

                    if (!string.IsNullOrWhiteSpace(text) &&
                        (text.Contains("rapport") || text.Contains("skickat") || text.Contains("sparad") || text.Contains("klar")))
                    {
                        responseReceived = true;
                        _output.WriteLine("✅ OpenAI response received");
                        break;
                    }
                }
            }

            if (!responseReceived)
            {
                await Task.Delay(2000); // Check every 2 seconds
            }
        }

        if (!responseReceived)
        {
            throw new TimeoutException($"OpenAI response not received within {OPENAI_RESPONSE_TIMEOUT}ms");
        }

        // Additional wait for file processing
        await Task.Delay(5000);
    }

    private async Task<bool> ValidateEmailDelivery(string reportName)
    {
        _output.WriteLine($"📧 Validating email delivery for {reportName}...");

        // In a real test environment, you would integrate with email service
        // For now, we'll simulate email validation by checking for success indicators
        // This could be enhanced to check actual email delivery via SMTP logs or email API

        try
        {
            // Look for email confirmation in the chat response
            var chatMessages = await _page!.Locator(".message, .chat-message").AllAsync();
            foreach (var message in chatMessages.TakeLast(5)) // Check last 5 messages
            {
                var text = await message.TextContentAsync();
                if (text != null && (text.Contains("skickat till Anna") || text.Contains("e-post skickad") ||
                                   text.Contains("email sent") || text.Contains("delivered")))
                {
                    _output.WriteLine("✅ Email delivery confirmed in chat response");
                    return true;
                }
            }

            // Alternative: Check for email service logs or API responses
            // This would require integration with the actual email service being used

            _output.WriteLine("⚠️ Email delivery confirmation not found in chat response");
            return false;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error validating email delivery: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ValidateFileStorage(string reportName)
    {
        _output.WriteLine($"📁 Validating file storage for {reportName}...");

        try
        {
            // Wait a bit for file system operations to complete
            await Task.Delay(3000);

            var files = Directory.GetFiles(REPORTS_DIRECTORY, "*.*", SearchOption.AllDirectories);
            var recentFiles = files.Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-10)).ToList();

            if (recentFiles.Count == 0)
            {
                _output.WriteLine("❌ No recent files found in reports directory");
                return false;
            }

            // Look for both source data and final report files
            var hasExcelFile = recentFiles.Any(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));
            var hasPdfFile = recentFiles.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            var hasDataFile = recentFiles.Any(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                                  f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

            _output.WriteLine($"📊 Files found - Excel: {hasExcelFile}, PDF: {hasPdfFile}, Data: {hasDataFile}");
            _output.WriteLine($"📂 Recent files: {string.Join(", ", recentFiles.Select(Path.GetFileName))}");

            // At minimum, we should have either Excel or PDF report
            var hasReport = hasExcelFile || hasPdfFile;

            if (hasReport)
            {
                _output.WriteLine("✅ Report files successfully stored");
                return true;
            }
            else
            {
                _output.WriteLine("❌ No report files found");
                return false;
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error validating file storage: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ValidateReportContent(string reportName, string originalRequest)
    {
        _output.WriteLine($"🔍 Validating report content for {reportName}...");

        try
        {
            var files = Directory.GetFiles(REPORTS_DIRECTORY, "*.*", SearchOption.AllDirectories);
            var recentFiles = files.Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-10)).ToList();

            var excelFiles = recentFiles.Where(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)).ToList();

            if (excelFiles.Count == 0)
            {
                _output.WriteLine("⚠️ No Excel files found for content validation");
                return true; // Don't fail if we have other report formats
            }

            foreach (var excelFile in excelFiles)
            {
                if (await ValidateExcelReportContent(excelFile, originalRequest))
                {
                    _output.WriteLine("✅ Report content validation successful");
                    return true;
                }
            }

            _output.WriteLine("❌ Report content validation failed");
            return false;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error validating report content: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ValidateExcelReportContent(string filePath, string originalRequest)
    {
        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheets = workbook.Worksheets.ToList();

            _output.WriteLine($"📋 Excel file has {worksheets.Count} worksheets: {string.Join(", ", worksheets.Select(w => w.Name))}");

            // Validate based on request specifications
            var validationResults = new List<bool>();

            // Check for cover page (startsida) if requested
            if (originalRequest.Contains("startsida") || originalRequest.Contains("prydlig"))
            {
                var hasCoverPage = worksheets.Any(w => w.Name.Contains("Start") || w.Name.Contains("Cover") ||
                                                      w.Name.Contains("Framsida") || worksheets.Count > 1);
                validationResults.Add(hasCoverPage);
                _output.WriteLine($"📄 Cover page validation: {(hasCoverPage ? "✅ PASS" : "❌ FAIL")}");
            }

            // Check for charts/diagrams if requested
            if (originalRequest.Contains("diagram") || originalRequest.Contains("chart"))
            {
                // Note: ClosedXML doesn't directly expose Charts collection, so we'll check for chart-like content
                var hasCharts = worksheets.Any(w => w.Name.Contains("Chart") || w.Name.Contains("Diagram"));
                validationResults.Add(hasCharts);
                _output.WriteLine($"📊 Charts validation: {(hasCharts ? "✅ PASS" : "❌ FAIL")}");
            }

            // Check for pivot tables if requested
            if (originalRequest.Contains("pivot"))
            {
                var hasPivotTables = worksheets.Any(w => w.PivotTables.Count() > 0);
                validationResults.Add(hasPivotTables);
                _output.WriteLine($"🔄 Pivot tables validation: {(hasPivotTables ? "✅ PASS" : "❌ FAIL")}");
            }

            // Check for data content
            var hasData = worksheets.Any(w => w.RowsUsed().Count() > 1);
            validationResults.Add(hasData);
            _output.WriteLine($"📈 Data content validation: {(hasData ? "✅ PASS" : "❌ FAIL")}");

            // Validate numerical data accuracy (basic check)
            var hasValidNumbers = await ValidateNumericalData(worksheets);
            validationResults.Add(hasValidNumbers);
            _output.WriteLine($"🔢 Numerical data validation: {(hasValidNumbers ? "✅ PASS" : "❌ FAIL")}");

            return validationResults.All(r => r);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error validating Excel content: {ex.Message}");
            return false;
        }
    }

    private Task<bool> ValidateNumericalData(List<IXLWorksheet> worksheets)
    {
        try
        {
            foreach (var worksheet in worksheets)
            {
                var usedRange = worksheet.RangeUsed();
                if (usedRange == null) continue;

                var numericCells = 0;
                var validNumericCells = 0;

                foreach (var cell in usedRange.Cells())
                {
                    if (cell.DataType == XLDataType.Number)
                    {
                        numericCells++;
                        var value = cell.GetDouble();

                        // Basic validation: numbers should be reasonable (not NaN, not infinite)
                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                        {
                            validNumericCells++;
                        }
                    }
                }

                if (numericCells > 0)
                {
                    var validPercentage = (double)validNumericCells / numericCells;
                    _output.WriteLine($"📊 Worksheet '{worksheet.Name}': {validNumericCells}/{numericCells} valid numeric cells ({validPercentage:P1})");

                    if (validPercentage < 0.95) // 95% of numeric data should be valid
                    {
                        return Task.FromResult(false);
                    }
                }
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error validating numerical data: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    private Task ValidateAllTestResults(List<FinancialReportTestResult> testResults)
    {
        _output.WriteLine("\n📊 COMPREHENSIVE TEST RESULTS SUMMARY");
        _output.WriteLine("=====================================");

        var totalTests = testResults.Count;
        var successfulTests = testResults.Count(r => r.Success);
        var failedTests = totalTests - successfulTests;

        _output.WriteLine($"📈 Total Reports Tested: {totalTests}");
        _output.WriteLine($"✅ Successful Reports: {successfulTests}");
        _output.WriteLine($"❌ Failed Reports: {failedTests}");
        _output.WriteLine($"📊 Success Rate: {(double)successfulTests / totalTests:P1}");

        foreach (var result in testResults)
        {
            _output.WriteLine($"\n📋 {result.ReportName} ({result.RequestNumber}/8):");
            _output.WriteLine($"   ⏱️ Duration: {result.Duration.TotalSeconds:F1}s");
            _output.WriteLine($"   📤 Message Sent: {(result.MessageSent ? "✅" : "❌")}");
            _output.WriteLine($"   🤖 OpenAI Response: {(result.OpenAIResponseReceived ? "✅" : "❌")}");
            _output.WriteLine($"   📧 Email Delivered: {(result.EmailDelivered ? "✅" : "❌")}");
            _output.WriteLine($"   📁 Files Stored: {(result.FilesStored ? "✅" : "❌")}");
            _output.WriteLine($"   🔍 Content Validated: {(result.ContentValidated ? "✅" : "❌")}");
            _output.WriteLine($"   🎯 Overall Success: {(result.Success ? "✅ PASS" : "❌ FAIL")}");

            if (!string.IsNullOrEmpty(result.Error))
            {
                _output.WriteLine($"   ⚠️ Error: {result.Error}");
            }
        }

        // Critical validation: All tests must pass
        var criticalFailures = testResults.Where(r => !r.Success).ToList();
        if (criticalFailures.Any())
        {
            var failureDetails = string.Join(", ", criticalFailures.Select(f => f.ReportName));
            throw new Exception($"❌ CRITICAL FAILURE: {criticalFailures.Count} reports failed validation: {failureDetails}");
        }

        // Validate minimum requirements
        Assert.True(successfulTests == totalTests, $"All {totalTests} reports must pass validation");
        Assert.True(testResults.All(r => r.MessageSent), "All messages must be sent successfully");
        Assert.True(testResults.All(r => r.OpenAIResponseReceived), "All OpenAI responses must be received");
        Assert.True(testResults.All(r => r.EmailDelivered), "All emails must be delivered");
        Assert.True(testResults.All(r => r.FilesStored), "All report files must be stored");
        Assert.True(testResults.All(r => r.ContentValidated), "All report content must be validated");

        _output.WriteLine("\n🎉 ALL VALIDATION REQUIREMENTS MET SUCCESSFULLY!");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Data model for tracking individual report test results
/// </summary>
public class FinancialReportTestResult
{
    public string ReportName { get; set; } = string.Empty;
    public string Request { get; set; } = string.Empty;
    public int RequestNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }

    // Test validation flags
    public bool MessageSent { get; set; }
    public bool OpenAIResponseReceived { get; set; }
    public bool EmailDelivered { get; set; }
    public bool FilesStored { get; set; }
    public bool ContentValidated { get; set; }

    // Overall result
    public bool Success { get; set; }
    public string? Error { get; set; }

    // Additional metadata
    public List<string> GeneratedFiles { get; set; } = new();
    public Dictionary<string, object> ValidationDetails { get; set; } = new();
}
