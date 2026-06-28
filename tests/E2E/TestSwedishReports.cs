using Xunit;
using Xunit.Abstractions;
using AnalysisPlugin.Components;
using OfficeOpenXml;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Testar de nya svenska rapportkomponenterna
/// </summary>
public class TestSwedishReports
{
    private readonly ITestOutputHelper _output;

    public TestSwedishReports(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestIncomeStatementComponent_ShouldWork()
    {
        _output.WriteLine("🇸🇪 TESTAR RESULTATRAPPORT KOMPONENT");
        _output.WriteLine("====================================");

        try
        {
            // Skapa komponent
            var component = new IncomeStatementComponent(2024);
            component.SIE4Data = GetTestSIE4Data();
            
            // Skapa Excel worksheet
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Test");
            
            // Rendera komponenten
            var result = await component.RenderAsync(worksheet, 1);
            
            _output.WriteLine($"✅ Resultat: {result.Success}");
            if (!result.Success)
            {
                _output.WriteLine($"❌ Fel: {result.ErrorMessage}");
            }
            else
            {
                _output.WriteLine($"📊 Komponent renderad framgångsrikt");
            }
            
            Assert.True(result.Success, $"IncomeStatementComponent misslyckades: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestVATReportComponent_ShouldWork()
    {
        _output.WriteLine("🇸🇪 TESTAR MOMSRAPPORT KOMPONENT");
        _output.WriteLine("=================================");

        try
        {
            // Skapa komponent
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 31);
            var component = new VATReportComponent(startDate, endDate, "månad");
            component.SIE4Data = GetTestSIE4Data();
            
            // Skapa Excel worksheet
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Test");
            
            // Rendera komponenten
            var result = await component.RenderAsync(worksheet, 1);
            
            _output.WriteLine($"✅ Resultat: {result.Success}");
            if (!result.Success)
            {
                _output.WriteLine($"❌ Fel: {result.ErrorMessage}");
            }
            else
            {
                _output.WriteLine($"📊 Komponent renderad framgångsrikt");
            }
            
            Assert.True(result.Success, $"VATReportComponent misslyckades: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task TestAnnualReportComponent_ShouldWork()
    {
        _output.WriteLine("🇸🇪 TESTAR ÅRSRAPPORT KOMPONENT");
        _output.WriteLine("================================");

        try
        {
            // Skapa komponent
            var component = new AnnualReportComponent(2024);
            component.SIE4Data = GetTestSIE4Data();
            
            // Skapa Excel worksheet
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Test");
            
            // Rendera komponenten
            var result = await component.RenderAsync(worksheet, 1);
            
            _output.WriteLine($"✅ Resultat: {result.Success}");
            if (!result.Success)
            {
                _output.WriteLine($"❌ Fel: {result.ErrorMessage}");
            }
            else
            {
                _output.WriteLine($"📊 Komponent renderad framgångsrikt");
            }
            
            Assert.True(result.Success, $"AnnualReportComponent misslyckades: {result.ErrorMessage}");
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
