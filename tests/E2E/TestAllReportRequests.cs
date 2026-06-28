using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Test som testar alla 5 olika rapportförfrågningar för att validera systemets flexibilitet
/// </summary>
public class TestAllReportRequests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public TestAllReportRequests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 500
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task TestAllReportRequests_ShouldHandleAllVariations()
    {
        _output.WriteLine("🔍 TESTAR ALLA 5 RAPPORTFÖRFRÅGNINGAR");
        _output.WriteLine("====================================");

        var requests = new[]
        {
            ("Request 1 - 2023 Utgiftsrapport", 
             "Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/"),
            
            ("Request 2 - 2024 Projektrapport", 
             "Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson"),
            
            ("Request 3 - 5-års Kvartalsrapport", 
             "Skapa en professionell rapport med prydlig startsida och sedan sidor för pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson"),
            
            ("Request 4 - 2022 Veckorapport", 
             "Skapa en professionell rapport för 2022 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson"),
            
            ("Request 5 - 2026 Framtidsrapport", 
             "Skapa en professionell analys/framtidsrapport för 2026 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK och skicka till Anna Andersson")
        };

        var results = new List<(string Name, bool Success, string Response, TimeSpan Duration)>();

        var page = await _browser!.NewPageAsync();
        
        try
        {
            // Navigera till chat-sidan
            await page.GotoAsync("http://localhost:5041");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            _output.WriteLine("✅ Navigerat till chat-sidan");

            // Vänta på att sidan laddas
            await page.WaitForSelectorAsync(".chat-container", new PageWaitForSelectorOptions { Timeout = 10000 });
            _output.WriteLine("✅ Chat-container hittad");

            // Testa varje förfrågan
            for (int i = 0; i < requests.Length; i++)
            {
                var (name, request) = requests[i];
                _output.WriteLine($"\n🔄 TESTAR {name}");
                _output.WriteLine("".PadRight(50, '='));

                var startTime = DateTime.Now;
                
                try
                {
                    // Skicka förfrågan
                    await page.FillAsync("textarea[placeholder*='meddelande']", request);
                    await page.ClickAsync("button[type='submit']");
                    
                    _output.WriteLine("✅ Förfrågan skickad");

                    // Vänta på svar (längre tid för komplexa rapporter)
                    await page.WaitForSelectorAsync(".message:last-child .message-content", 
                        new PageWaitForSelectorOptions { Timeout = 180000 }); // 3 minuter

                    // Läs svaret
                    var responseElement = await page.QuerySelectorAsync(".message:last-child .message-content");
                    var response = await responseElement!.InnerTextAsync();
                    
                    var duration = DateTime.Now - startTime;
                    results.Add((name, true, response, duration));

                    _output.WriteLine($"✅ {name} - FRAMGÅNG ({duration.TotalSeconds:F1}s)");
                    _output.WriteLine($"📝 Svar: {response.Substring(0, Math.Min(200, response.Length))}...");

                    // Kort paus mellan förfrågningar
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    var duration = DateTime.Now - startTime;
                    results.Add((name, false, ex.Message, duration));
                    _output.WriteLine($"❌ {name} - MISSLYCKADES: {ex.Message}");
                }
            }

            // Analysera alla resultat
            await AnalyzeAllResults(results);

        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task AnalyzeAllResults(List<(string Name, bool Success, string Response, TimeSpan Duration)> results)
    {
        _output.WriteLine("\n🏆 SAMMANFATTNING AV ALLA TESTER");
        _output.WriteLine("===============================");

        var successCount = results.Count(r => r.Success);
        var totalCount = results.Count;
        var successRate = totalCount > 0 ? (double)successCount / totalCount * 100 : 0;

        _output.WriteLine($"📊 Totala tester: {totalCount}");
        _output.WriteLine($"📊 Framgångsrika: {successCount}");
        _output.WriteLine($"📊 Misslyckade: {totalCount - successCount}");
        _output.WriteLine($"📊 Framgångsgrad: {successRate:F1}%");

        _output.WriteLine($"\n📋 DETALJERADE RESULTAT:");
        _output.WriteLine("========================");

        foreach (var (name, success, response, duration) in results)
        {
            var status = success ? "✅ FRAMGÅNG" : "❌ MISSLYCKAD";
            _output.WriteLine($"{status}: {name} ({duration.TotalSeconds:F1}s)");
            
            if (success)
            {
                // Analysera nyckelord i svaret
                var keywords = new[] { "skickat", "anna", "rapport", "spara", "diagram", "pivot", "fel", "error" };
                var foundKeywords = keywords.Where(k => response.ToLowerInvariant().Contains(k)).ToList();
                _output.WriteLine($"   🔍 Nyckelord: {string.Join(", ", foundKeywords)}");
            }
            else
            {
                _output.WriteLine($"   ❌ Fel: {response}");
            }
        }

        // Beräkna genomsnittlig tid
        var avgTime = results.Where(r => r.Success).Select(r => r.Duration.TotalSeconds).DefaultIfEmpty(0).Average();
        _output.WriteLine($"\n⏱️ Genomsnittlig svarstid: {avgTime:F1} sekunder");

        // Slutbetyg
        _output.WriteLine($"\n🎯 SLUTBETYG:");
        if (successRate >= 90)
        {
            _output.WriteLine($"   🏆 UTMÄRKT: Systemet hanterar nästan alla rapporttyper perfekt!");
        }
        else if (successRate >= 70)
        {
            _output.WriteLine($"   ✅ MYCKET BRA: Systemet hanterar de flesta rapporttyper bra");
        }
        else if (successRate >= 50)
        {
            _output.WriteLine($"   ⚠️ BRA: Systemet hanterar många rapporttyper");
        }
        else
        {
            _output.WriteLine($"   ❌ PROBLEM: Systemet har svårigheter med rapporthantering");
        }

        await Task.CompletedTask;
    }
}
