using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace AnalysisPlugin.Templates;

/// <summary>
/// Professional styling system baserat på e1-e5 designerna 🎨
/// Implementerar färgscheman och stilar för professionella Excel-rapporter
/// </summary>
public static class ProfessionalStyles
{
    // 🎨 FÄRGPALETTER från designerna
    public static class Colors
    {
        // e1 - Financial Planning Template
        public static readonly Color PrimaryBlueGray = Color.FromArgb(79, 129, 189);    // Blågrå titel
        public static readonly Color SecondaryGreen = Color.FromArgb(155, 187, 89);     // Grön diagram
        public static readonly Color AccentOrange = Color.FromArgb(247, 150, 70);       // Orange kostnader
        public static readonly Color LightBlue = Color.FromArgb(184, 204, 228);         // Ljusblå headers
        public static readonly Color LightGray = Color.FromArgb(242, 242, 242);         // Ljusgrå fält
        
        // e2 - Business Budget Template  
        public static readonly Color DarkBlue = Color.FromArgb(31, 73, 125);            // Mörkblå rubriker
        public static readonly Color PositiveGreen = Color.FromArgb(0, 176, 80);        // Positiva avvikelser
        public static readonly Color NegativeRed = Color.FromArgb(192, 0, 0);           // Negativa avvikelser
        
        // e3/e4 - Project Schedule
        public static readonly Color PhaseBlueGray = Color.FromArgb(149, 179, 215);     // Phase-fält
        public static readonly Color GanttGray = Color.FromArgb(166, 166, 166);         // Gantt-staplar
        public static readonly Color CompletedBlue = Color.FromArgb(68, 114, 196);      // Genomförd
        public static readonly Color TodayRed = Color.FromArgb(255, 0, 0);              // Dagens datum
        
        // e5 - KPI Dashboard
        public static readonly Color KpiLightGreen = Color.FromArgb(146, 208, 80);      // Goal
        public static readonly Color KpiDarkGreen = Color.FromArgb(79, 129, 189);       // Actual
        public static readonly Color ProfitOrange = Color.FromArgb(255, 192, 0);        // Gross profit
        public static readonly Color NetBlue = Color.FromArgb(68, 114, 196);            // Net profit
        
        // Gemensamma färger
        public static readonly Color White = Color.White;
        public static readonly Color Black = Color.Black;
        public static readonly Color DarkGray = Color.FromArgb(89, 89, 89);
        public static readonly Color MediumGray = Color.FromArgb(128, 128, 128);
    }

    // 📝 TYPOGRAFI
    public static class Fonts
    {
        public const string Primary = "Calibri";
        public const string Secondary = "Segoe UI";
        public const int TitleSize = 18;
        public const int HeaderSize = 14;
        public const int BodySize = 11;
        public const int SmallSize = 9;
    }

    // 🎯 STYLING METODER
    
    /// <summary>
    /// Applicerar titel-stil (e1 stil)
    /// </summary>
    public static void ApplyTitleStyle(ExcelRange range, string title)
    {
        range.Value = title;
        range.Style.Font.Name = Fonts.Primary;
        range.Style.Font.Size = Fonts.TitleSize;
        range.Style.Font.Bold = true;
        range.Style.Font.Color.SetColor(Colors.PrimaryBlueGray);
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    }

    /// <summary>
    /// Applicerar header-stil för tabeller
    /// </summary>
    public static void ApplyTableHeaderStyle(ExcelRange range)
    {
        range.Style.Font.Name = Fonts.Primary;
        range.Style.Font.Size = Fonts.HeaderSize;
        range.Style.Font.Bold = true;
        range.Style.Font.Color.SetColor(Colors.White);
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(Colors.LightBlue);
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        range.Style.Border.BorderAround(ExcelBorderStyle.Thin, Colors.DarkGray);
    }

    /// <summary>
    /// Applicerar data-stil för tabellceller
    /// </summary>
    public static void ApplyTableDataStyle(ExcelRange range, bool isAlternateRow = false)
    {
        range.Style.Font.Name = Fonts.Primary;
        range.Style.Font.Size = Fonts.BodySize;
        range.Style.Font.Color.SetColor(Colors.Black);
        
        if (isAlternateRow)
        {
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Colors.LightGray);
        }
        
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        range.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(217, 217, 217));
    }

    /// <summary>
    /// Applicerar nummer-formatering för valuta
    /// </summary>
    public static void ApplyCurrencyFormat(ExcelRange range, string currency = "kr")
    {
        range.Style.Numberformat.Format = $"#,##0 \"{currency}\"";
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
    }

    /// <summary>
    /// Applicerar procent-formatering
    /// </summary>
    public static void ApplyPercentFormat(ExcelRange range)
    {
        range.Style.Numberformat.Format = "0.0%";
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
    }

    /// <summary>
    /// Applicerar positiv/negativ färgkodning (e2 stil)
    /// </summary>
    public static void ApplyVarianceStyle(ExcelRange range, double value)
    {
        range.Style.Font.Name = Fonts.Primary;
        range.Style.Font.Size = Fonts.BodySize;
        range.Style.Font.Bold = true;
        
        if (value >= 0)
        {
            range.Style.Font.Color.SetColor(Colors.PositiveGreen);
        }
        else
        {
            range.Style.Font.Color.SetColor(Colors.NegativeRed);
        }
        
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
    }

    /// <summary>
    /// Applicerar total-summa stil (fet, blå bakgrund)
    /// </summary>
    public static void ApplyTotalStyle(ExcelRange range)
    {
        range.Style.Font.Name = Fonts.Primary;
        range.Style.Font.Size = Fonts.HeaderSize;
        range.Style.Font.Bold = true;
        range.Style.Font.Color.SetColor(Colors.White);
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(Colors.PrimaryBlueGray);
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        range.Style.Border.BorderAround(ExcelBorderStyle.Medium, Colors.DarkBlue);
    }

    /// <summary>
    /// Tar bort alla rutnätslinjer för ren vit bakgrund (som i designerna)
    /// </summary>
    public static void RemoveGridlines(ExcelWorksheet worksheet)
    {
        worksheet.View.ShowGridLines = false;
    }

    /// <summary>
    /// Sätter kolumnbredder för optimal layout
    /// </summary>
    public static void SetOptimalColumnWidths(ExcelWorksheet worksheet, params double[] widths)
    {
        for (int i = 0; i < widths.Length; i++)
        {
            worksheet.Column(i + 1).Width = widths[i];
        }
    }
}
