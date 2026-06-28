using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using AnalysisPlugin.Models;

namespace AnalysisPlugin.Services;

/// <summary>
/// Service for parsing and validating SIE4 (Standard Import Export) financial data files.
/// Handles Swedish accounting standard format with BAS (Basic Accounting Scheme) account plan validation.
/// Provides comprehensive parsing of financial transactions, account definitions, and data validation.
/// </summary>
public class SIE4ProcessorService
{
    private readonly ILogger<SIE4ProcessorService> _logger;
    private static readonly Regex VerRecordRegex = new(@"#VER\s+(\w+)\s+(\d+)\s+(\d{8})\s+""([^""]*)""\s*(\d{8})?", RegexOptions.Compiled);
    private static readonly Regex TransRecordRegex = new(@"#TRANS\s+(\d+)\s+\{\}\s+(-?\d+(?:\.\d+)?)\s+""([^""]*)""\s+""([^""]*)""\s+(\d+)", RegexOptions.Compiled);
    private static readonly Regex KontoRecordRegex = new(@"#KONTO\s+(\d+)\s+""([^""]*)""", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the SIE4ProcessorService class
    /// </summary>
    /// <param name="logger">Logger instance for tracking parsing and validation events</param>
    public SIE4ProcessorService(ILogger<SIE4ProcessorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses SIE4 content and extracts financial transactions with account information.
    /// Processes both account definitions (#KONTO) and transaction records (#VER/#TRANS)
    /// to create a comprehensive list of financial transactions.
    /// </summary>
    /// <param name="sie4Content">Complete SIE4 file content as string</param>
    /// <returns>List of parsed financial transactions with account details</returns>
    /// <exception cref="ArgumentException">Thrown when SIE4 content is invalid or empty</exception>
    /// <exception cref="FormatException">Thrown when SIE4 format cannot be parsed</exception>
    public async Task<List<FinancialTransaction>> ParseAsync(string sie4Content)
    {
        try
        {
            _logger.LogDebug("Starting SIE4 parsing, content length: {Length}", sie4Content.Length);

            var transactions = new List<FinancialTransaction>();
            var accounts = new Dictionary<string, string>();
            
            // First pass: Extract account definitions
            await ExtractAccountDefinitions(sie4Content, accounts);
            
            // Second pass: Extract transactions
            await ExtractTransactions(sie4Content, accounts, transactions);

            _logger.LogInformation("Successfully parsed {Count} transactions from SIE4 data", transactions.Count);
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse SIE4 content");
            throw new InvalidOperationException("SIE4 parsing failed", ex);
        }
    }

    /// <summary>
    /// Validates SIE4 content format and structure
    /// </summary>
    /// <param name="sie4Content">SIE4 content to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool ValidateSIE4Format(string sie4Content)
    {
        if (string.IsNullOrWhiteSpace(sie4Content))
            return false;

        // Check for required SIE4 headers
        if (!sie4Content.Contains("#FLAGGA") || !sie4Content.Contains("#PROGRAM"))
        {
            _logger.LogWarning("SIE4 content missing required headers (#FLAGGA or #PROGRAM)");
            return false;
        }

        // Check for at least one transaction record
        if (!sie4Content.Contains("#VER"))
        {
            _logger.LogWarning("SIE4 content contains no transaction records (#VER)");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a transaction against BAS account plan
    /// </summary>
    /// <param name="transaction">Transaction to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool ValidateTransaction(FinancialTransaction transaction)
    {
        if (!transaction.IsValid())
            return false;

        // Validate BAS account number format (should be 4 digits)
        if (!Regex.IsMatch(transaction.AccountNumber, @"^\d{4}$"))
        {
            _logger.LogWarning("Invalid BAS account number format: {AccountNumber}", transaction.AccountNumber);
            return false;
        }

        // Validate BAS category matches account number
        var expectedCategory = FinancialTransaction.GetBASCategory(transaction.AccountNumber);
        if (transaction.Category != expectedCategory)
        {
            _logger.LogWarning("BAS category mismatch for account {AccountNumber}: expected {Expected}, got {Actual}",
                transaction.AccountNumber, expectedCategory, transaction.Category);
            return false;
        }

        return true;
    }

    private async Task ExtractAccountDefinitions(string sie4Content, Dictionary<string, string> accounts)
    {
        var lines = sie4Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        await Task.Run(() =>
        {
            foreach (var line in lines)
            {
                var match = KontoRecordRegex.Match(line.Trim());
                if (match.Success)
                {
                    var accountNumber = match.Groups[1].Value;
                    var accountName = match.Groups[2].Value;
                    accounts[accountNumber] = accountName;
                }
            }
        });

        _logger.LogDebug("Extracted {Count} account definitions", accounts.Count);
    }

    private async Task ExtractTransactions(string sie4Content, Dictionary<string, string> accounts, List<FinancialTransaction> transactions)
    {
        var lines = sie4Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? currentVerSeries = null;
        string? currentVerNumber = null;
        DateTime currentVerDate = DateTime.MinValue;
        string? currentVerDescription = null;

        await Task.Run(() =>
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Parse VER record (transaction header)
                var verMatch = VerRecordRegex.Match(line);
                if (verMatch.Success)
                {
                    currentVerSeries = verMatch.Groups[1].Value;
                    currentVerNumber = verMatch.Groups[2].Value;
                    if (DateTime.TryParseExact(verMatch.Groups[3].Value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        currentVerDate = date;
                    }
                    currentVerDescription = verMatch.Groups[4].Value;
                    continue;
                }

                // Parse TRANS record (transaction line)
                var transMatch = TransRecordRegex.Match(line);
                if (transMatch.Success && currentVerDate != DateTime.MinValue)
                {
                    var accountNumber = transMatch.Groups[1].Value;
                    var amount = decimal.Parse(transMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    var transDate = currentVerDate; // Use VER date for transactions
                    var description = string.IsNullOrWhiteSpace(transMatch.Groups[3].Value) ?
                        (string.IsNullOrWhiteSpace(transMatch.Groups[4].Value) ? currentVerDescription : transMatch.Groups[4].Value) :
                        transMatch.Groups[3].Value;

                    var transaction = new FinancialTransaction
                    {
                        Id = $"{currentVerSeries}-{currentVerNumber}-{accountNumber}-{transactions.Count + 1}",
                        Date = transDate,
                        Description = string.IsNullOrWhiteSpace(description) ? currentVerDescription ?? "Unknown" : description,
                        Amount = amount,
                        AccountNumber = accountNumber,
                        AccountName = accounts.GetValueOrDefault(accountNumber, "Unknown Account"),
                        Currency = "SEK",
                        ExchangeRate = 1.0m
                    };

                    transaction.UpdateBASCategory();

                    if (ValidateTransaction(transaction))
                    {
                        transactions.Add(transaction);
                    }
                    else
                    {
                        _logger.LogWarning("Skipping invalid transaction: {TransactionId}", transaction.Id);
                    }
                }
            }
        });

        _logger.LogDebug("Extracted {Count} transactions", transactions.Count);
    }

    /// <summary>
    /// Filtrerar transaktioner baserat på tidsperiod
    /// </summary>
    /// <param name="transactions">Alla transaktioner</param>
    /// <param name="timePeriod">Tidsperiod att filtrera för</param>
    /// <returns>Filtrerade transaktioner</returns>
    public List<FinancialTransaction> FilterByTimePeriod(List<FinancialTransaction> transactions, string timePeriod)
    {
        var now = DateTime.Now;

        // 🔥 HANTERA SPECIFIKA VECKOR (t.ex. "Week35_2022")
        if (timePeriod.StartsWith("Week") && timePeriod.Contains("_"))
        {
            return FilterForSpecificWeek(transactions, timePeriod);
        }

        // 🔥 HANTERA SPECIFIKA ÅR (t.ex. "Year2024")
        if (timePeriod.StartsWith("Year") && timePeriod.Length > 4)
        {
            return FilterForSpecificYear(transactions, timePeriod);
        }

        // 🔥 HANTERA SPECIFIKA KVARTAL (t.ex. "Quarter3_2025")
        if (timePeriod.StartsWith("Quarter") && timePeriod.Contains("_"))
        {
            return FilterForSpecificQuarter(transactions, timePeriod);
        }

        return timePeriod switch
        {
            "PreviousWeek" => FilterForPreviousWeek(transactions, now),
            "CurrentWeek" => FilterForCurrentWeek(transactions, now),
            "PreviousDay" => FilterForPreviousDay(transactions, now),
            "CurrentDay" => FilterForCurrentDay(transactions, now),
            "PreviousMonth" => FilterForPreviousMonth(transactions, now),
            "CurrentMonth" => FilterForCurrentMonth(transactions, now),
            "Week" => FilterForWeek(transactions, now),
            "Day" => FilterForDay(transactions, now),
            "Quarter" => FilterForQuarter(transactions, now),
            "Month" => FilterForMonth(transactions, now),
            _ => transactions // Year - returnera alla transaktioner
        };
    }

    private List<FinancialTransaction> FilterForPreviousWeek(List<FinancialTransaction> transactions, DateTime now)
    {
        // Beräkna föregående vecka (måndag till söndag)
        var startOfThisWeek = now.AddDays(-(int)now.DayOfWeek + 1); // Måndag denna vecka
        var startOfPreviousWeek = startOfThisWeek.AddDays(-7);
        var endOfPreviousWeek = startOfThisWeek.AddDays(-1);

        var filtered = transactions.Where(t => t.Date >= startOfPreviousWeek && t.Date <= endOfPreviousWeek).ToList();

        _logger.LogInformation("🔥 Filtrerade för föregående vecka ({Start:yyyy-MM-dd} till {End:yyyy-MM-dd}): {Original} → {Filtered} transaktioner",
            startOfPreviousWeek, endOfPreviousWeek, transactions.Count, filtered.Count);

        return filtered;
    }

    private List<FinancialTransaction> FilterForCurrentWeek(List<FinancialTransaction> transactions, DateTime now)
    {
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek + 1);
        var endOfWeek = startOfWeek.AddDays(6);
        return transactions.Where(t => t.Date >= startOfWeek && t.Date <= endOfWeek).ToList();
    }

    private List<FinancialTransaction> FilterForPreviousDay(List<FinancialTransaction> transactions, DateTime now)
    {
        var yesterday = now.Date.AddDays(-1);
        return transactions.Where(t => t.Date.Date == yesterday).ToList();
    }

    private List<FinancialTransaction> FilterForCurrentDay(List<FinancialTransaction> transactions, DateTime now)
    {
        return transactions.Where(t => t.Date.Date == now.Date).ToList();
    }

    private List<FinancialTransaction> FilterForPreviousMonth(List<FinancialTransaction> transactions, DateTime now)
    {
        var firstDayOfThisMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfPreviousMonth = firstDayOfThisMonth.AddMonths(-1);
        var lastDayOfPreviousMonth = firstDayOfThisMonth.AddDays(-1);
        return transactions.Where(t => t.Date >= firstDayOfPreviousMonth && t.Date <= lastDayOfPreviousMonth).ToList();
    }

    private List<FinancialTransaction> FilterForCurrentMonth(List<FinancialTransaction> transactions, DateTime now)
    {
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
        return transactions.Where(t => t.Date >= firstDayOfMonth && t.Date <= now).ToList();
    }

    private List<FinancialTransaction> FilterForWeek(List<FinancialTransaction> transactions, DateTime now) => FilterForCurrentWeek(transactions, now);
    private List<FinancialTransaction> FilterForDay(List<FinancialTransaction> transactions, DateTime now) => FilterForCurrentDay(transactions, now);
    private List<FinancialTransaction> FilterForQuarter(List<FinancialTransaction> transactions, DateTime now) => transactions;
    private List<FinancialTransaction> FilterForMonth(List<FinancialTransaction> transactions, DateTime now) => FilterForCurrentMonth(transactions, now);

    /// <summary>
    /// Filtrerar för specifik vecka och år (t.ex. "Week35_2022")
    /// </summary>
    private List<FinancialTransaction> FilterForSpecificWeek(List<FinancialTransaction> transactions, string timePeriod)
    {
        // Parse "Week35_2022" format
        var parts = timePeriod.Split('_');
        if (parts.Length != 2 || !parts[0].StartsWith("Week"))
        {
            _logger.LogWarning("Ogiltigt veckoformat: {TimePeriod}", timePeriod);
            return transactions;
        }

        var weekStr = parts[0].Substring(4); // Ta bort "Week" prefix
        var yearStr = parts[1];

        if (!int.TryParse(weekStr, out var weekNumber) || !int.TryParse(yearStr, out var year))
        {
            _logger.LogWarning("Kunde inte parsa vecka/år: {TimePeriod}", timePeriod);
            return transactions;
        }

        // Beräkna start- och slutdatum för veckan
        var jan1 = new DateTime(year, 1, 1);
        var daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
        var firstMonday = jan1.AddDays(daysOffset);

        // Vecka 1 börjar första måndagen i året
        var weekStart = firstMonday.AddDays((weekNumber - 1) * 7);
        var weekEnd = weekStart.AddDays(6);

        var filtered = transactions.Where(t => t.Date >= weekStart && t.Date <= weekEnd).ToList();

        _logger.LogInformation("🔥 Filtrerade för vecka {Week} år {Year} ({Start:yyyy-MM-dd} till {End:yyyy-MM-dd}): {Original} → {Filtered} transaktioner",
            weekNumber, year, weekStart, weekEnd, transactions.Count, filtered.Count);

        return filtered;
    }

    /// <summary>
    /// Filtrerar för specifikt år (t.ex. "Year2024")
    /// </summary>
    private List<FinancialTransaction> FilterForSpecificYear(List<FinancialTransaction> transactions, string timePeriod)
    {
        // Parse "Year2024" format
        var yearStr = timePeriod.Substring(4); // Ta bort "Year" prefix

        if (!int.TryParse(yearStr, out var year))
        {
            _logger.LogWarning("Kunde inte parsa år: {TimePeriod}", timePeriod);
            return transactions;
        }

        // Filtrera transaktioner för det specifika året
        var filtered = transactions.Where(t => t.Date.Year == year).ToList();

        _logger.LogInformation("🔥 Filtrerade för år {Year}: {Original} → {Filtered} transaktioner",
            year, transactions.Count, filtered.Count);

        return filtered;
    }

    /// <summary>
    /// Filtrerar för specifikt kvartal och år (t.ex. "Quarter3_2025")
    /// </summary>
    private List<FinancialTransaction> FilterForSpecificQuarter(List<FinancialTransaction> transactions, string timePeriod)
    {
        // Parse "Quarter3_2025" format
        var parts = timePeriod.Split('_');
        if (parts.Length != 2 || !parts[0].StartsWith("Quarter"))
        {
            _logger.LogWarning("Ogiltigt kvartalformat: {TimePeriod}", timePeriod);
            return transactions;
        }

        var quarterStr = parts[0].Substring(7); // Ta bort "Quarter" prefix
        var yearStr = parts[1];

        if (!int.TryParse(quarterStr, out var quarter) || !int.TryParse(yearStr, out var year))
        {
            _logger.LogWarning("Kunde inte parsa kvartal/år: {TimePeriod}", timePeriod);
            return transactions;
        }

        // Beräkna start- och slutdatum för kvartalet
        var startMonth = (quarter - 1) * 3 + 1; // Q1=1, Q2=4, Q3=7, Q4=10
        var startDate = new DateTime(year, startMonth, 1);
        var endDate = startDate.AddMonths(3).AddDays(-1); // Sista dagen i kvartalet

        var filtered = transactions.Where(t => t.Date >= startDate && t.Date <= endDate).ToList();

        _logger.LogInformation("🔥 Filtrerade för kvartal {Quarter} år {Year} ({Start:yyyy-MM-dd} till {End:yyyy-MM-dd}): {Original} → {Filtered} transaktioner",
            quarter, year, startDate, endDate, transactions.Count, filtered.Count);

        return filtered;
    }
}
