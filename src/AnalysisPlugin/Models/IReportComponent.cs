using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisPlugin.Services;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Models;

/// <summary>
/// Abstract interface for report components that can be rendered to Excel worksheets
/// </summary>
public interface IReportComponent
{
    /// <summary>
    /// Type of the component (FrontPage, Chart, Table, Pivot, MLAnalysis)
    /// </summary>
    string ComponentType { get; }

    /// <summary>
    /// Title of the component
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Configuration parameters for the component
    /// </summary>
    Dictionary<string, object> Configuration { get; set; }

    /// <summary>
    /// Renders the component to an Excel worksheet
    /// </summary>
    /// <param name="worksheet">Target Excel worksheet</param>
    /// <param name="startRow">Starting row for rendering</param>
    /// <returns>Result of the rendering operation</returns>
    Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow);
}

/// <summary>
/// Result of a component rendering operation
/// </summary>
public class ComponentResult
{
    /// <summary>
    /// Whether the rendering was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Number of rows used by the component
    /// </summary>
    public int RowsUsed { get; set; } = 0;

    /// <summary>
    /// Error message if rendering failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata about the rendering
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a successful result
    /// </summary>
    /// <param name="rowsUsed">Number of rows used</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>Successful ComponentResult</returns>
    public static ComponentResult CreateSuccess(int rowsUsed, Dictionary<string, object>? metadata = null)
    {
        return new ComponentResult
        {
            Success = true,
            RowsUsed = rowsUsed,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed result
    /// </summary>
    /// <param name="errorMessage">Error message</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>Failed ComponentResult</returns>
    public static ComponentResult CreateFailure(string errorMessage, Dictionary<string, object>? metadata = null)
    {
        return new ComponentResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Base abstract class for report components with common functionality
/// </summary>
public abstract class ReportComponentBase : IReportComponent
{
    /// <inheritdoc/>
    public abstract string ComponentType { get; }

    /// <inheritdoc/>
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc/>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// SIE4 data för komponenten att använda
    /// </summary>
    public string SIE4Data { get; set; } = string.Empty;

    /// <summary>
    /// Parsed financial transactions from SIE4 data
    /// </summary>
    public List<FinancialTransaction> ParsedTransactions { get; private set; } = new();

    /// <summary>
    /// SIE4 processor service for parsing financial data
    /// </summary>
    private SIE4ProcessorService? _sie4Processor;

    /// <summary>
    /// Parses SIE4 data into financial transactions
    /// </summary>
    /// <param name="timePeriod">Optional time period filter</param>
    public async Task ParseSIE4DataAsync(string timePeriod = "Year")
    {
        if (string.IsNullOrEmpty(SIE4Data))
        {
            ParsedTransactions = new List<FinancialTransaction>();
            return;
        }

        try
        {
            // Initialize SIE4 processor if not already done
            if (_sie4Processor == null)
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                _sie4Processor = new SIE4ProcessorService(loggerFactory.CreateLogger<SIE4ProcessorService>());
            }

            // Parse SIE4 data to get real financial transactions
            var allTransactions = await _sie4Processor.ParseAsync(SIE4Data);

            // 🔥 FILTRERA TRANSAKTIONER BASERAT PÅ TIDSPERIOD
            ParsedTransactions = _sie4Processor.FilterByTimePeriod(allTransactions, timePeriod);

            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ReportComponentBase>();
            logger.LogInformation("🔥 ParseSIE4DataAsync för tidsperiod {TimePeriod}: {Original} → {Filtered} transaktioner",
                timePeriod, allTransactions.Count, ParsedTransactions.Count);
        }
        catch (Exception ex)
        {
            // Log error but continue with empty transactions to avoid breaking the report
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<ReportComponentBase>();
            logger.LogError(ex, "Failed to parse SIE4 data, using empty transaction list");
            ParsedTransactions = new List<FinancialTransaction>();
        }
    }

    /// <summary>
    /// Gets account balance from parsed transactions
    /// </summary>
    /// <param name="accountNumber">Account number to get balance for</param>
    /// <param name="year">Optional year filter</param>
    /// <returns>Account balance</returns>
    public decimal GetAccountBalance(string accountNumber, int? year = null)
    {
        if (!ParsedTransactions.Any())
        {
            return 0; // Return 0 instead of fallback values to show real data issues
        }

        var transactions = ParsedTransactions.Where(t => t.AccountNumber == accountNumber);

        if (year.HasValue)
        {
            transactions = transactions.Where(t => t.Date.Year == year.Value);
        }

        return transactions.Sum(t => t.Amount);
    }

    /// <summary>
    /// Gets transactions for a specific account
    /// </summary>
    /// <param name="accountNumber">Account number</param>
    /// <param name="year">Optional year filter</param>
    /// <returns>List of transactions</returns>
    protected List<FinancialTransaction> GetAccountTransactions(string accountNumber, int? year = null)
    {
        if (!ParsedTransactions.Any())
        {
            return new List<FinancialTransaction>();
        }

        var transactions = ParsedTransactions.Where(t => t.AccountNumber == accountNumber);

        if (year.HasValue)
        {
            transactions = transactions.Where(t => t.Date.Year == year.Value);
        }

        return transactions.ToList();
    }

    /// <inheritdoc/>
    public abstract Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow);

    /// <summary>
    /// Applies professional Swedish styling to a cell range
    /// </summary>
    /// <param name="range">Cell range to style</param>
    /// <param name="isHeader">Whether this is a header row</param>
    protected void ApplyProfessionalStyling(ExcelRange range, bool isHeader = false)
    {
        if (isHeader)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(227, 242, 253));
            range.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(21, 101, 192));
        }

        range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    }

    /// <summary>
    /// Formats currency values according to Swedish standards
    /// </summary>
    /// <param name="range">Cell range to format</param>
    protected void ApplySwedishCurrencyFormat(ExcelRange range)
    {
        range.Style.Numberformat.Format = "#,##0.00 \"kr\"";
    }

    /// <summary>
    /// Formats date values according to Swedish standards
    /// </summary>
    /// <param name="range">Cell range to format</param>
    protected void ApplySwedishDateFormat(ExcelRange range)
    {
        range.Style.Numberformat.Format = "yyyy-MM-dd";
    }
}
