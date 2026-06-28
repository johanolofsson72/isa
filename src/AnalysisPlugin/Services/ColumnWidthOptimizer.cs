using OfficeOpenXml;
using OfficeOpenXml.Style;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Services;

/// <summary>
/// 📏 Intelligent kolumnbreddsjustering för optimal läsbarhet
/// Analyserar både rubriker och innehåll för att sätta perfekta kolumnbredder
/// </summary>
public class ColumnWidthOptimizer
{
    private readonly ILogger<ColumnWidthOptimizer> _logger;

    public ColumnWidthOptimizer()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ColumnWidthOptimizer>();
    }

    /// <summary>
    /// Optimerar kolumnbredder för hela worksheetet
    /// </summary>
    public void OptimizeColumnWidths(ExcelWorksheet worksheet)
    {
        if (worksheet.Dimension == null)
        {
            _logger.LogWarning("Worksheet har ingen data - hoppar över kolumnoptimering");
            return;
        }

        _logger.LogInformation($"🔧 Optimerar kolumnbredder för worksheet: {worksheet.Name}");

        var columnWidths = CalculateOptimalWidths(worksheet);
        ApplyOptimalWidths(worksheet, columnWidths);

        _logger.LogInformation($"✅ Kolumnbredder optimerade för {columnWidths.Count} kolumner");
    }

    /// <summary>
    /// Beräknar optimala bredder baserat på innehåll och rubriker
    /// </summary>
    private Dictionary<int, double> CalculateOptimalWidths(ExcelWorksheet worksheet)
    {
        var columnWidths = new Dictionary<int, double>();
        var dimension = worksheet.Dimension;

        for (int col = dimension.Start.Column; col <= dimension.End.Column; col++)
        {
            double maxWidth = GetMinimumColumnWidth();
            
            // Analysera alla celler i kolumnen
            for (int row = dimension.Start.Row; row <= dimension.End.Row; row++)
            {
                var cell = worksheet.Cells[row, col];
                var cellWidth = CalculateCellWidth(cell);
                
                if (cellWidth > maxWidth)
                {
                    maxWidth = cellWidth;
                }
            }

            // Sätt maxgräns för att undvika extremt breda kolumner
            maxWidth = Math.Min(maxWidth, GetMaximumColumnWidth());
            
            columnWidths[col] = maxWidth;
            
            _logger.LogDebug($"Kolumn {col}: Optimal bredd = {maxWidth:F1}");
        }

        return columnWidths;
    }

    /// <summary>
    /// Beräknar bredd för en specifik cell baserat på innehåll och formatering
    /// </summary>
    private double CalculateCellWidth(ExcelRange cell)
    {
        var value = cell.Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(value))
        {
            return GetMinimumColumnWidth();
        }

        // Grundbredd baserat på textlängd
        double baseWidth = value.Length * GetCharacterWidth();

        // Justera för typsnittsstorlek
        var fontSize = cell.Style.Font.Size > 0 ? cell.Style.Font.Size : 11;
        double fontMultiplier = fontSize / 11.0; // 11 är standardstorlek
        baseWidth *= fontMultiplier;

        // Justera för fetstil
        if (cell.Style.Font.Bold)
        {
            baseWidth *= 1.15; // Fetstil tar ~15% mer plats
        }

        // Extra utrymme för numeriska värden med formatering
        if (IsNumericCell(cell))
        {
            baseWidth += GetNumericPadding(cell);
        }

        // Extra utrymme för merged cells (rubriker)
        if (IsMergedCell(cell))
        {
            baseWidth *= 0.8; // Merged cells kan vara lite smalare per kolumn
        }

        return Math.Max(baseWidth, GetMinimumColumnWidth());
    }

    /// <summary>
    /// Kontrollerar om cellen innehåller numerisk data
    /// </summary>
    private bool IsNumericCell(ExcelRange cell)
    {
        if (cell.Value == null) return false;
        
        return cell.Value is decimal || cell.Value is double || cell.Value is int || cell.Value is float ||
               (cell.Style.Numberformat.Format.Contains("kr") || 
                cell.Style.Numberformat.Format.Contains("#,##0") ||
                cell.Style.Numberformat.Format.Contains("%"));
    }

    /// <summary>
    /// Beräknar extra padding för numeriska celler
    /// </summary>
    private double GetNumericPadding(ExcelRange cell)
    {
        var format = cell.Style.Numberformat.Format;
        
        // Extra utrymme för valutaformat
        if (format.Contains("kr"))
        {
            return 3.0; // Utrymme för " kr"
        }
        
        // Extra utrymme för procentformat
        if (format.Contains("%"))
        {
            return 2.0; // Utrymme för " %"
        }
        
        // Extra utrymme för tusentalsavskiljare
        if (format.Contains("#,##0"))
        {
            return 2.0; // Utrymme för kommatecken
        }
        
        return 1.0; // Standard numerisk padding
    }

    /// <summary>
    /// Kontrollerar om cellen är del av en merged range
    /// </summary>
    private bool IsMergedCell(ExcelRange cell)
    {
        var worksheet = cell.Worksheet;
        var address = cell.Address;
        
        return worksheet.MergedCells.Any(range => range.Contains(address));
    }

    /// <summary>
    /// Applicerar de beräknade bredderna på worksheetet
    /// </summary>
    private void ApplyOptimalWidths(ExcelWorksheet worksheet, Dictionary<int, double> columnWidths)
    {
        foreach (var (columnIndex, width) in columnWidths)
        {
            worksheet.Column(columnIndex).Width = width;
            
            _logger.LogDebug($"Kolumn {columnIndex} bredd satt till: {width:F1}");
        }
    }

    /// <summary>
    /// Hämtar grundläggande teckenbredd (baserat på Segoe UI 11pt)
    /// </summary>
    private double GetCharacterWidth()
    {
        return 0.85; // Genomsnittlig teckenbredd för Segoe UI
    }

    /// <summary>
    /// Minimum kolumnbredd för läsbarhet
    /// </summary>
    private double GetMinimumColumnWidth()
    {
        return 8.0; // Minimum för läsbarhet
    }

    /// <summary>
    /// Maximum kolumnbredd för att undvika extremt breda kolumner
    /// </summary>
    private double GetMaximumColumnWidth()
    {
        return 50.0; // Maximum för att behålla översikt
    }

    /// <summary>
    /// Optimerar specifika kolumner med anpassade regler
    /// </summary>
    public void OptimizeSpecificColumns(ExcelWorksheet worksheet, Dictionary<int, ColumnType> columnTypes)
    {
        foreach (var (columnIndex, columnType) in columnTypes)
        {
            var optimalWidth = columnType switch
            {
                ColumnType.AccountNumber => 12.0,      // Kontonummer (4 siffror)
                ColumnType.AccountName => 35.0,        // Kontonamn (längre text)
                ColumnType.Currency => 15.0,           // Valutabelopp
                ColumnType.Percentage => 10.0,         // Procentsatser
                ColumnType.Date => 12.0,               // Datum
                ColumnType.ShortText => 20.0,          // Kort text
                ColumnType.LongText => 40.0,           // Lång text/beskrivningar
                _ => worksheet.Column(columnIndex).Width // Behåll befintlig bredd
            };

            worksheet.Column(columnIndex).Width = optimalWidth;
            _logger.LogDebug($"Kolumn {columnIndex} ({columnType}) bredd satt till: {optimalWidth}");
        }
    }
}

/// <summary>
/// Kolumntyper för specifik optimering
/// </summary>
public enum ColumnType
{
    AccountNumber,
    AccountName,
    Currency,
    Percentage,
    Date,
    ShortText,
    LongText
}
