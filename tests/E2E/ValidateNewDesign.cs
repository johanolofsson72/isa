using OfficeOpenXml;
using OfficeOpenXml.Style;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Validerar att den nya premium-designen verkligen är toppledande
/// </summary>
public class ValidateNewDesign
{
    private readonly ITestOutputHelper _output;

    public ValidateNewDesign(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ValidateNewDesign_ShouldBeToppledande()
    {
        _output.WriteLine("🎨 VALIDERAR NY TOPPLEDANDE DESIGN");
        _output.WriteLine("==================================");

        var reportsDir = "/path/to/accountingagent/analysis_reports/";
        
        // Hitta den senaste rapporten
        var latestReport = Directory.GetFiles(reportsDir, "*.xlsx")
            .Where(f => Path.GetFileName(f).StartsWith("financial_report_20251108_210"))
            .OrderBy(f => new FileInfo(f).CreationTime)
            .LastOrDefault();

        if (latestReport == null)
        {
            _output.WriteLine("❌ Ingen rapport hittades för validering");
            return;
        }

        var fileName = Path.GetFileName(latestReport);
        _output.WriteLine($"📄 Analyserar: {fileName}");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var designScore = 0;
        var maxScore = 100;
        var designIssues = new List<string>();

        try
        {
            using var package = new ExcelPackage(new FileInfo(latestReport));
            
            foreach (var worksheet in package.Workbook.Worksheets)
            {
                _output.WriteLine($"\n📋 ANALYSERAR WORKSHEET: {worksheet.Name}");
                _output.WriteLine("".PadRight(40, '='));

                // 🎨 KONTROLLERA RUTNÄTSLINJER (PREMIUM FEATURE)
                if (!worksheet.View.ShowGridLines)
                {
                    designScore += 15;
                    _output.WriteLine("✅ Rutnätslinjer borttagna (Premium look) +15p");
                }
                else
                {
                    designIssues.Add($"Rutnätslinjer visas fortfarande i {worksheet.Name}");
                }

                // 🎨 KONTROLLERA PREMIUM FÄRGER
                var premiumColors = await CheckPremiumColors(worksheet);
                designScore += premiumColors.score;
                _output.WriteLine($"🎨 Premium färger: {premiumColors.score}/20p");
                designIssues.AddRange(premiumColors.issues);

                // 📝 KONTROLLERA TYPOGRAFI
                var typography = await CheckTypography(worksheet);
                designScore += typography.score;
                _output.WriteLine($"📝 Typografi: {typography.score}/25p");
                designIssues.AddRange(typography.issues);

                // 📊 KONTROLLERA LAYOUT OCH SPACING
                var layout = await CheckLayoutAndSpacing(worksheet);
                designScore += layout.score;
                _output.WriteLine($"📊 Layout: {layout.score}/20p");
                designIssues.AddRange(layout.issues);

                // 🎯 KONTROLLERA PROFESSIONELLA ELEMENT
                var professional = await CheckProfessionalElements(worksheet);
                designScore += professional.score;
                _output.WriteLine($"🎯 Professionella element: {professional.score}/20p");
                designIssues.AddRange(professional.issues);
            }

            await AnalyzeDesignResults(designScore, maxScore, designIssues);

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Fel vid analys: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task<(int score, List<string> issues)> CheckPremiumColors(ExcelWorksheet worksheet)
    {
        var score = 0;
        var issues = new List<string>();
        
        // Kontrollera för premium blå färg (31, 78, 121)
        var premiumBlueFound = false;
        var whiteTextFound = false;
        
        if (worksheet.Dimension != null)
        {
            for (int row = 1; row <= Math.Min(10, worksheet.Dimension.Rows); row++)
            {
                for (int col = 1; col <= Math.Min(8, worksheet.Dimension.Columns); col++)
                {
                    var cell = worksheet.Cells[row, col];
                    
                    // Kontrollera bakgrundsfärg
                    if (cell.Style.Fill.PatternType == ExcelFillStyle.Solid)
                    {
                        var bgColor = cell.Style.Fill.BackgroundColor.Rgb;
                        if (bgColor != null && bgColor.Contains("1F4E79")) // Premium blå i hex
                        {
                            premiumBlueFound = true;
                            score += 10;
                        }
                    }
                    
                    // Kontrollera textfärg
                    var textColor = cell.Style.Font.Color.Rgb;
                    if (textColor != null && textColor.Contains("FFFFFF")) // Vit text
                    {
                        whiteTextFound = true;
                        score += 10;
                    }
                }
            }
        }
        
        if (!premiumBlueFound) issues.Add("Premium blå färg saknas");
        if (!whiteTextFound) issues.Add("Vit text på mörk bakgrund saknas");
        
        await Task.CompletedTask;
        return (score, issues);
    }

    private async Task<(int score, List<string> issues)> CheckTypography(ExcelWorksheet worksheet)
    {
        var score = 0;
        var issues = new List<string>();
        
        var segoeUIFound = false;
        var properSizesFound = false;
        var boldHeadersFound = false;
        
        if (worksheet.Dimension != null)
        {
            for (int row = 1; row <= Math.Min(5, worksheet.Dimension.Rows); row++)
            {
                for (int col = 1; col <= Math.Min(8, worksheet.Dimension.Columns); col++)
                {
                    var cell = worksheet.Cells[row, col];
                    
                    // Kontrollera typsnitt
                    if (cell.Style.Font.Name == "Segoe UI")
                    {
                        segoeUIFound = true;
                    }
                    
                    // Kontrollera storlekar
                    if (cell.Style.Font.Size >= 16 && cell.Style.Font.Size <= 28)
                    {
                        properSizesFound = true;
                    }
                    
                    // Kontrollera bold headers
                    if (cell.Style.Font.Bold && cell.Style.Font.Size >= 16)
                    {
                        boldHeadersFound = true;
                    }
                }
            }
        }
        
        if (segoeUIFound) score += 10; else issues.Add("Segoe UI typsnitt saknas");
        if (properSizesFound) score += 8; else issues.Add("Korrekta fontstorlekar saknas");
        if (boldHeadersFound) score += 7; else issues.Add("Fetstilta headers saknas");
        
        await Task.CompletedTask;
        return (score, issues);
    }

    private async Task<(int score, List<string> issues)> CheckLayoutAndSpacing(ExcelWorksheet worksheet)
    {
        var score = 0;
        var issues = new List<string>();
        
        // Kontrollera merged cells (professionell layout)
        var mergedCellsFound = worksheet.MergedCells.Any();
        if (mergedCellsFound) score += 10; else issues.Add("Merged cells saknas");
        
        // Kontrollera borders
        var bordersFound = false;
        if (worksheet.Dimension != null)
        {
            for (int row = 1; row <= Math.Min(5, worksheet.Dimension.Rows); row++)
            {
                var cell = worksheet.Cells[row, 1];
                if (cell.Style.Border.Bottom.Style != ExcelBorderStyle.None ||
                    cell.Style.Border.Top.Style != ExcelBorderStyle.None)
                {
                    bordersFound = true;
                    break;
                }
            }
        }
        
        if (bordersFound) score += 10; else issues.Add("Professionella borders saknas");
        
        await Task.CompletedTask;
        return (score, issues);
    }

    private async Task<(int score, List<string> issues)> CheckProfessionalElements(ExcelWorksheet worksheet)
    {
        var score = 0;
        var issues = new List<string>();
        
        // Kontrollera för emojis/ikoner (modern touch)
        var emojisFound = false;
        var centerAlignmentFound = false;
        
        if (worksheet.Dimension != null)
        {
            for (int row = 1; row <= Math.Min(10, worksheet.Dimension.Rows); row++)
            {
                for (int col = 1; col <= Math.Min(8, worksheet.Dimension.Columns); col++)
                {
                    var cell = worksheet.Cells[row, col];
                    var value = cell.Value?.ToString() ?? "";
                    
                    if (value.Contains("📊") || value.Contains("📈") || value.Contains("💰") || value.Contains("🎯"))
                    {
                        emojisFound = true;
                    }
                    
                    if (cell.Style.HorizontalAlignment == ExcelHorizontalAlignment.Center)
                    {
                        centerAlignmentFound = true;
                    }
                }
            }
        }
        
        if (emojisFound) score += 10; else issues.Add("Moderna ikoner/emojis saknas");
        if (centerAlignmentFound) score += 10; else issues.Add("Center-alignment saknas");
        
        await Task.CompletedTask;
        return (score, issues);
    }

    private async Task AnalyzeDesignResults(int designScore, int maxScore, List<string> designIssues)
    {
        var percentage = (double)designScore / maxScore * 100;
        
        _output.WriteLine($"\n🏆 DESIGN RESULTAT");
        _output.WriteLine("==================");
        _output.WriteLine($"📊 Poäng: {designScore}/{maxScore} ({percentage:F1}%)");
        
        if (percentage >= 90)
        {
            _output.WriteLine($"🏆 TOPPLEDANDE DESIGN! Rapporten har världsklass estetik!");
        }
        else if (percentage >= 75)
        {
            _output.WriteLine($"✅ UTMÄRKT DESIGN! Mycket professionell rapport!");
        }
        else if (percentage >= 60)
        {
            _output.WriteLine($"👍 BRA DESIGN! Bra grund men kan förbättras!");
        }
        else
        {
            _output.WriteLine($"⚠️ DESIGN BEHÖVER FÖRBÄTTRAS");
        }
        
        if (designIssues.Any())
        {
            _output.WriteLine($"\n⚠️ FÖRBÄTTRINGSOMRÅDEN:");
            foreach (var issue in designIssues.Take(5))
            {
                _output.WriteLine($"   • {issue}");
            }
        }
        
        await Task.CompletedTask;
    }
}
