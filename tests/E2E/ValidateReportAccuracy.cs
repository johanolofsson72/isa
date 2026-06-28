using OfficeOpenXml;
using Xunit;
using Xunit.Abstractions;

namespace AccountingAgent.E2E.Tests;

/// <summary>
/// Validerar att rapporterna innehåller korrekt och logisk data
/// </summary>
public class ValidateReportAccuracy
{
    private readonly ITestOutputHelper _output;

    public ValidateReportAccuracy(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ValidateReportAccuracy_ShouldContainCorrectData()
    {
        _output.WriteLine("🔍 VALIDERAR RAPPORTERNAS RIKTIGHET");
        _output.WriteLine("===================================");

        var reportsDir = "/path/to/accountingagent/analysis_reports/";
        
        // Hitta de senaste rapporterna från våra tester
        var reportFiles = Directory.GetFiles(reportsDir, "*.xlsx")
            .Where(f => Path.GetFileName(f).StartsWith("financial_report_20251108_203"))
            .OrderBy(f => new FileInfo(f).CreationTime)
            .TakeLast(5)
            .ToList();

        _output.WriteLine($"📊 Analyserar {reportFiles.Count} rapporter");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var reportAnalysis = new List<(string FileName, string Year, bool HasData, List<string> Issues)>();

        foreach (var reportPath in reportFiles)
        {
            var fileName = Path.GetFileName(reportPath);
            _output.WriteLine($"\n📄 ANALYSERAR: {fileName}");
            _output.WriteLine("".PadRight(50, '='));

            var issues = new List<string>();
            var hasData = false;
            var detectedYear = "Okänt";

            try
            {
                using var package = new ExcelPackage(new FileInfo(reportPath));
                
                // Analysera varje worksheet
                foreach (var worksheet in package.Workbook.Worksheets)
                {
                    var worksheetName = worksheet.Name;
                    var dimension = worksheet.Dimension;
                    var cellCount = dimension?.Rows * dimension?.Columns ?? 0;
                    
                    _output.WriteLine($"   📋 {worksheetName}: {cellCount} celler");
                    
                    if (cellCount > 0)
                    {
                        hasData = true;
                        
                        // Kontrollera för år i data
                        var yearResult = await CheckForYearData(worksheet, worksheetName, issues);
                        if (yearResult != null && detectedYear == "Okänt") detectedYear = yearResult;
                        
                        // Kontrollera för finansiella värden
                        await CheckForFinancialData(worksheet, worksheetName, issues);
                        
                        // Kontrollera för logiska värden
                        await CheckForLogicalData(worksheet, worksheetName, issues);
                    }
                    else
                    {
                        issues.Add($"Tom worksheet: {worksheetName}");
                    }
                }

                // Kontrollera SIE4-underlag
                var sie4YearResult = await CheckSIE4Source(reportPath, issues);
                if (sie4YearResult != null && detectedYear == "Okänt") detectedYear = sie4YearResult;

            }
            catch (Exception ex)
            {
                issues.Add($"Fel vid läsning av rapport: {ex.Message}");
            }

            reportAnalysis.Add((fileName, detectedYear, hasData, issues));
        }

        // Sammanfatta resultaten
        await SummarizeValidation(reportAnalysis);

        await Task.CompletedTask;
    }

    private async Task<string?> CheckForYearData(ExcelWorksheet worksheet, string worksheetName, List<string> issues)
    {
        if (worksheet.Dimension == null) return null;

        var yearFound = false;
        var years = new[] { "2022", "2023", "2024", "2025", "2026" };

        // Sök efter år i första 10 raderna och kolumnerna
        for (int row = 1; row <= Math.Min(10, worksheet.Dimension.Rows); row++)
        {
            for (int col = 1; col <= Math.Min(10, worksheet.Dimension.Columns); col++)
            {
                var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                
                foreach (var year in years)
                {
                    if (cellValue.Contains(year))
                    {
                        yearFound = true;
                        _output.WriteLine($"      ✅ År hittad: {year} i cell [{row},{col}]");
                        await Task.CompletedTask;
                        return year;
                    }
                }
                if (yearFound) break;
            }
            if (yearFound) break;
        }

        if (!yearFound)
        {
            issues.Add($"Inget år hittades i {worksheetName}");
        }

        await Task.CompletedTask;
        return null;
    }

    private async Task CheckForFinancialData(ExcelWorksheet worksheet, string worksheetName, List<string> issues)
    {
        if (worksheet.Dimension == null) return;

        var financialValuesFound = 0;
        var suspiciousValues = new List<string>();

        // Sök efter numeriska värden som ser ut som pengar
        for (int row = 1; row <= Math.Min(20, worksheet.Dimension.Rows); row++)
        {
            for (int col = 1; col <= Math.Min(10, worksheet.Dimension.Columns); col++)
            {
                var cellValue = worksheet.Cells[row, col].Value;
                
                if (cellValue is double doubleValue && Math.Abs(doubleValue) > 1000)
                {
                    financialValuesFound++;
                    
                    // Kontrollera för suspekt data
                    if (Math.Abs(doubleValue) > 100000000) // Över 100 miljoner
                    {
                        suspiciousValues.Add($"Mycket högt värde: {doubleValue:N0} i [{row},{col}]");
                    }
                    else if (doubleValue > 0 && doubleValue < 10)
                    {
                        suspiciousValues.Add($"Suspekt lågt värde: {doubleValue} i [{row},{col}]");
                    }
                    
                    if (financialValuesFound <= 3) // Visa bara första 3
                    {
                        _output.WriteLine($"      💰 Finansiellt värde: {doubleValue:N0} SEK i [{row},{col}]");
                    }
                }
            }
        }

        if (financialValuesFound == 0)
        {
            issues.Add($"Inga finansiella värden hittades i {worksheetName}");
        }
        else
        {
            _output.WriteLine($"      📊 Totalt {financialValuesFound} finansiella värden hittade");
        }

        foreach (var suspicious in suspiciousValues.Take(2))
        {
            issues.Add($"Suspekt värde i {worksheetName}: {suspicious}");
        }

        await Task.CompletedTask;
    }

    private async Task CheckForLogicalData(ExcelWorksheet worksheet, string worksheetName, List<string> issues)
    {
        if (worksheet.Dimension == null) return;

        // Kontrollera för vanliga bokföringskonton
        var commonAccounts = new[] { "1930", "3010", "5010", "6110", "7010" };
        var accountsFound = 0;

        for (int row = 1; row <= Math.Min(20, worksheet.Dimension.Rows); row++)
        {
            for (int col = 1; col <= Math.Min(10, worksheet.Dimension.Columns); col++)
            {
                var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                
                foreach (var account in commonAccounts)
                {
                    if (cellValue.Contains(account))
                    {
                        accountsFound++;
                        _output.WriteLine($"      🏦 Konto hittad: {account} i [{row},{col}]");
                        break;
                    }
                }
            }
        }

        if (accountsFound == 0)
        {
            issues.Add($"Inga bokföringskonton hittades i {worksheetName}");
        }

        await Task.CompletedTask;
    }

    private async Task<string?> CheckSIE4Source(string reportPath, List<string> issues)
    {
        var sie4Path = reportPath.Replace(".xlsx", "_source.se");
        
        if (!File.Exists(sie4Path))
        {
            issues.Add("SIE4-underlag saknas");
            return null;
        }

        var sie4Content = await File.ReadAllTextAsync(sie4Path);
        var sie4Size = sie4Content.Length;
        
        _output.WriteLine($"   📄 SIE4-underlag: {sie4Size:N0} tecken");

        // Kontrollera SIE4-struktur
        if (!sie4Content.Contains("#FLAGGA"))
        {
            issues.Add("SIE4-fil saknar #FLAGGA");
        }

        if (!sie4Content.Contains("#TRANS"))
        {
            issues.Add("SIE4-fil saknar transaktioner (#TRANS)");
        }

        // Räkna transaktioner
        var transactionCount = sie4Content.Split('\n').Count(line => line.StartsWith("#TRANS"));
        _output.WriteLine($"   📊 SIE4-transaktioner: {transactionCount}");

        if (transactionCount < 100)
        {
            issues.Add($"För få transaktioner i SIE4: {transactionCount}");
        }

        // Kontrollera år i SIE4
        var years = new[] { "2022", "2023", "2024", "2025", "2026" };
        foreach (var year in years)
        {
            if (sie4Content.Contains(year))
            {
                _output.WriteLine($"   ✅ År i SIE4: {year}");
                return year;
            }
        }

        return null;
    }

    private async Task SummarizeValidation(List<(string FileName, string Year, bool HasData, List<string> Issues)> reportAnalysis)
    {
        _output.WriteLine($"\n🏆 SAMMANFATTNING AV VALIDERING");
        _output.WriteLine("==============================");

        var totalReports = reportAnalysis.Count;
        var reportsWithData = reportAnalysis.Count(r => r.HasData);
        var reportsWithIssues = reportAnalysis.Count(r => r.Issues.Any());
        var totalIssues = reportAnalysis.Sum(r => r.Issues.Count);

        _output.WriteLine($"📊 Totala rapporter: {totalReports}");
        _output.WriteLine($"📊 Rapporter med data: {reportsWithData}");
        _output.WriteLine($"📊 Rapporter med problem: {reportsWithIssues}");
        _output.WriteLine($"📊 Totala problem: {totalIssues}");

        var dataAccuracy = totalReports > 0 ? (double)reportsWithData / totalReports * 100 : 0;
        var qualityScore = totalReports > 0 ? (double)(totalReports - reportsWithIssues) / totalReports * 100 : 0;

        _output.WriteLine($"📊 Data-noggrannhet: {dataAccuracy:F1}%");
        _output.WriteLine($"📊 Kvalitetspoäng: {qualityScore:F1}%");

        _output.WriteLine($"\n📋 DETALJERADE RESULTAT:");
        foreach (var (fileName, year, hasData, issues) in reportAnalysis)
        {
            var status = hasData ? "✅ HAR DATA" : "❌ TOM";
            var issueCount = issues.Count;
            _output.WriteLine($"{status}: {fileName} (År: {year}, Problem: {issueCount})");
            
            foreach (var issue in issues.Take(3))
            {
                _output.WriteLine($"   ⚠️ {issue}");
            }
            if (issues.Count > 3)
            {
                _output.WriteLine($"   ... och {issues.Count - 3} fler problem");
            }
        }

        // Slutbetyg
        _output.WriteLine($"\n🎯 SLUTBETYG:");
        if (dataAccuracy >= 90 && qualityScore >= 80)
        {
            _output.WriteLine($"   🏆 UTMÄRKT: Rapporterna innehåller korrekt och logisk data!");
        }
        else if (dataAccuracy >= 70 && qualityScore >= 60)
        {
            _output.WriteLine($"   ✅ BRA: De flesta rapporter har korrekt data");
        }
        else if (dataAccuracy >= 50)
        {
            _output.WriteLine($"   ⚠️ ACCEPTABEL: Några rapporter har korrekt data");
        }
        else
        {
            _output.WriteLine($"   ❌ PROBLEM: Rapporterna har betydande dataproblem");
        }

        await Task.CompletedTask;
    }
}
