using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using OfficeOpenXml;

namespace AnalysisPlugin.Tests;

/// <summary>
/// Test to inspect what's actually in the Excel report
/// </summary>
public class InspectExcelReport
{
    private readonly ITestOutputHelper _output;

    public InspectExcelReport(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectLatestExcelReport()
    {
        _output.WriteLine("🔍 INSPEKTERAR: Vad finns faktiskt i Excel-rapporten?");
        _output.WriteLine("===================================================");
        
        // Find the latest generated Excel file
        var reportsDir = "/path/to/accountingagent/analysis_reports";
        var excelFiles = Directory.GetFiles(reportsDir, "financial_report_*.xlsx")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToArray();
        
        Assert.True(excelFiles.Length > 0, "Should have at least one Excel report");
        
        var latestExcel = excelFiles[0];
        _output.WriteLine($"📊 Inspekterar: {Path.GetFileName(latestExcel)}");
        
        // Analyze Excel report
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(latestExcel));
        
        _output.WriteLine($"📋 Excel-rapport har {package.Workbook.Worksheets.Count} worksheets:");
        
        foreach (var worksheet in package.Workbook.Worksheets)
        {
            _output.WriteLine($"\n📄 WORKSHEET: {worksheet.Name}");
            _output.WriteLine("=" + new string('=', worksheet.Name.Length + 12));
            
            if (worksheet.Dimension == null)
            {
                _output.WriteLine("⚠️ Worksheet är tom");
                continue;
            }
            
            var maxRow = Math.Min(50, worksheet.Dimension.End.Row);
            var maxCol = Math.Min(10, worksheet.Dimension.End.Column);
            
            _output.WriteLine($"📐 Dimensioner: {worksheet.Dimension.End.Row} rader x {worksheet.Dimension.End.Column} kolumner");
            _output.WriteLine($"🔍 Visar första {maxRow} rader och {maxCol} kolumner:");
            
            // Show header row
            var headerLine = "     ";
            for (int col = 1; col <= maxCol; col++)
            {
                headerLine += $"{GetExcelColumnName(col),-15}";
            }
            _output.WriteLine(headerLine);
            
            // Show data rows
            for (int row = 1; row <= maxRow; row++)
            {
                var rowLine = $"{row,3}: ";

                for (int col = 1; col <= maxCol; col++)
                {
                    var cell = worksheet.Cells[row, col];
                    var value = cell.Value?.ToString() ?? "";

                    // Truncate long values
                    if (value.Length > 12)
                        value = value.Substring(0, 12) + "...";

                    rowLine += $"{value,-15}";
                }
                _output.WriteLine(rowLine);
            }
            
            // Look for numerical values specifically
            _output.WriteLine("\n💰 NUMERISKA VÄRDEN I WORKSHEET:");
            var numericalValues = new List<(int row, int col, decimal value)>();
            
            for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
            {
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Value;
                    if (cellValue is decimal decValue && decValue != 0)
                    {
                        numericalValues.Add((row, col, decValue));
                    }
                    else if (cellValue is double doubleValue && doubleValue != 0)
                    {
                        numericalValues.Add((row, col, (decimal)doubleValue));
                    }
                    else if (decimal.TryParse(cellValue?.ToString(), out var parsedValue) && parsedValue != 0)
                    {
                        numericalValues.Add((row, col, parsedValue));
                    }
                }
            }
            
            if (numericalValues.Any())
            {
                _output.WriteLine($"Hittade {numericalValues.Count} numeriska värden:");
                foreach (var (row, col, value) in numericalValues.Take(20)) // Show first 20
                {
                    _output.WriteLine($"  {GetExcelColumnName(col)}{row}: {value:N2} SEK");
                }
                if (numericalValues.Count > 20)
                {
                    _output.WriteLine($"  ... och {numericalValues.Count - 20} till");
                }
            }
            else
            {
                _output.WriteLine("❌ Inga numeriska värden hittade!");
            }
        }
        
        _output.WriteLine("\n🎯 SAMMANFATTNING:");
        _output.WriteLine("==================");
        _output.WriteLine("Nu kan vi se exakt vad som finns i rapporten och varför valideringen misslyckades.");
    }
    
    private string GetExcelColumnName(int columnNumber)
    {
        string columnName = "";
        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            columnNumber = (columnNumber - modulo) / 26;
        }
        return columnName;
    }
}
