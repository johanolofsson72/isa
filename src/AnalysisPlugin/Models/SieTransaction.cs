namespace AnalysisPlugin.Models;

/// <summary>
/// Representerar en transaktion från SIE-fil
/// Används för att mata Professional Template System med VERKLIG data
/// </summary>
public class SieTransaction
{
    public string Account { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public double Amount { get; set; }
    public string Reference { get; set; } = "";
    public string VoucherNumber { get; set; } = "";
}
