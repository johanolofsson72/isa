using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Components;
using AnalysisPlugin.Services;
using OfficeOpenXml;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Testar kolumnbreddsjustering för optimal läsbarhet
/// </summary>
public class TestColumnWidthOptimization
{
    private readonly ITestOutputHelper _output;

    public TestColumnWidthOptimization(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestColumnWidthOptimizer_ShouldOptimizeWidths()
    {
        _output.WriteLine("📏 TESTAR KOLUMNBREDDSJUSTERING");
        _output.WriteLine("===============================");

        try
        {
            // Skapa test worksheet med data
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Test");
            
            // Lägg till testdata med olika längder
            worksheet.Cells[1, 1].Value = "Kort";
            worksheet.Cells[1, 2].Value = "Mycket längre rubrik som behöver mer utrymme";
            worksheet.Cells[1, 3].Value = "12,345,678 kr";
            worksheet.Cells[1, 4].Value = "25.5%";
            
            worksheet.Cells[2, 1].Value = "Test";
            worksheet.Cells[2, 2].Value = "Ännu längre beskrivning som verkligen behöver mycket plats för att synas ordentligt";
            worksheet.Cells[2, 3].Value = "987,654,321 kr";
            worksheet.Cells[2, 4].Value = "100.0%";

            // Sätt formatering
            worksheet.Cells[1, 1, 1, 4].Style.Font.Bold = true;
            worksheet.Cells[1, 3, 2, 3].Style.Numberformat.Format = "#,##0 \"kr\"";
            worksheet.Cells[1, 4, 2, 4].Style.Numberformat.Format = "0.0%";

            _output.WriteLine("📊 Testdata skapad");

            // Testa kolumnoptimering
            var optimizer = new ColumnWidthOptimizer();
            
            // Spara bredder före optimering
            var widthsBefore = new Dictionary<int, double>();
            for (int col = 1; col <= 4; col++)
            {
                widthsBefore[col] = worksheet.Column(col).Width;
                _output.WriteLine($"Kolumn {col} före: {widthsBefore[col]:F1}");
            }

            // Optimera
            optimizer.OptimizeColumnWidths(worksheet);

            // Kontrollera bredder efter optimering
            var widthsAfter = new Dictionary<int, double>();
            for (int col = 1; col <= 4; col++)
            {
                widthsAfter[col] = worksheet.Column(col).Width;
                _output.WriteLine($"Kolumn {col} efter: {widthsAfter[col]:F1}");
            }

            // Validera att bredderna är rimliga
            Assert.True(widthsAfter[1] >= 8.0, "Kolumn 1 för smal");
            Assert.True(widthsAfter[2] >= 30.0, "Kolumn 2 för smal för lång text");
            Assert.True(widthsAfter[3] >= 12.0, "Kolumn 3 för smal för valuta");
            Assert.True(widthsAfter[4] >= 8.0, "Kolumn 4 för smal för procent");

            _output.WriteLine("✅ Kolumnbredder optimerade korrekt");

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestIncomeStatementColumnOptimization_ShouldWork()
    {
        _output.WriteLine("📏 TESTAR RESULTATRAPPORT KOLUMNOPTIMERING");
        _output.WriteLine("==========================================");

        try
        {
            // Skapa resultatrapport komponent
            var component = new IncomeStatementComponent(2024);
            component.SIE4Data = GetTestSIE4Data();
            
            // Skapa Excel worksheet
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Resultaträkning");
            
            // Rendera komponenten (inkluderar kolumnoptimering)
            var result = await component.RenderAsync(worksheet, 1);
            
            Assert.True(result.Success, $"IncomeStatementComponent misslyckades: {result.ErrorMessage}");

            // Kontrollera att kolumnbredder är optimerade
            _output.WriteLine("📊 KOLUMNBREDDER EFTER OPTIMERING:");
            for (int col = 1; col <= 8; col++)
            {
                var width = worksheet.Column(col).Width;
                _output.WriteLine($"Kolumn {col}: {width:F1}");
                
                // Validera rimliga bredder
                Assert.True(width >= 3.0, $"Kolumn {col} för smal: {width}");
                Assert.True(width <= 50.0, $"Kolumn {col} för bred: {width}");
            }

            // Specifika valideringar för resultatrapport
            Assert.True(worksheet.Column(2).Width >= 30, "Huvudkategorier kolumn för smal");
            Assert.True(worksheet.Column(7).Width >= 15, "Aktuellt år kolumn för smal");
            Assert.True(worksheet.Column(8).Width >= 15, "Föregående år kolumn för smal");

            _output.WriteLine("✅ Resultatrapport kolumnbredder optimerade korrekt");

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestAnnualReportColumnOptimization_ShouldWork()
    {
        _output.WriteLine("📏 TESTAR ÅRSRAPPORT KOLUMNOPTIMERING");
        _output.WriteLine("====================================");

        try
        {
            // Skapa årsrapport komponent
            var component = new AnnualReportComponent(2024);
            component.SIE4Data = GetTestSIE4Data();
            
            // Skapa Excel worksheet
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Årsrapport");
            
            // Rendera komponenten (inkluderar kolumnoptimering)
            var result = await component.RenderAsync(worksheet, 1);
            
            Assert.True(result.Success, $"AnnualReportComponent misslyckades: {result.ErrorMessage}");

            // Kontrollera att kolumnbredder är optimerade
            _output.WriteLine("📊 KOLUMNBREDDER EFTER OPTIMERING:");
            for (int col = 1; col <= 8; col++)
            {
                var width = worksheet.Column(col).Width;
                _output.WriteLine($"Kolumn {col}: {width:F1}");
            }

            // Specifika valideringar för årsrapport
            Assert.True(worksheet.Column(2).Width >= 40, "VD-ord kolumn för smal");
            Assert.True(worksheet.Column(6).Width >= 15, "Aktuellt år kolumn för smal");
            Assert.True(worksheet.Column(7).Width >= 15, "Föregående år kolumn för smal");

            _output.WriteLine("✅ Årsrapport kolumnbredder optimerade korrekt");

        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    private string GetTestSIE4Data()
    {
        return @"#FLAGGA 0
#PROGRAM ""TestProgram"" ""1.0""
#FORMAT PC8
#GEN 20241108
#SIETYP 4
#PROSA ""Test SIE4 Data""
#FNAMN ""Test Företag AB""
#ORGNR 556123-4567
#KONTO 1930 ""Checkräkningskonto""
#KONTO 3010 ""Försäljning varor 25% moms""
#KONTO 4010 ""Inköp varor 25% moms""
#KONTO 5010 ""Övriga externa kostnader""
#KONTO 7010 ""Löner""
#TRANS 20240115 {} 1930 3437000.00
#TRANS 20240115 {} 3010 -12500000.00
#TRANS 20240115 {} 4010 7500000.00
#TRANS 20240115 {} 5010 2800000.00
#TRANS 20240115 {} 7010 1800000.00";
    }
}
