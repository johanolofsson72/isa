using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AnalysisPlugin.Services;

/// <summary>
/// Parser för SIE4-data som extraherar finansiell information
/// </summary>
public class SIE4Parser
{
    private readonly ILogger<SIE4Parser> _logger;

    public SIE4Parser(ILogger<SIE4Parser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parsar SIE4-data och returnerar strukturerad finansiell information
    /// </summary>
    public SIE4ParsedData ParseSIE4Data(string sie4Content)
    {
        var data = new SIE4ParsedData();
        
        try
        {
            var lines = sie4Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Extrahera grundläggande information
            data.Year = ExtractYear(lines);
            data.CompanyName = ExtractCompanyName(lines);
            data.Accounts = ExtractAccounts(lines);
            data.Transactions = ExtractTransactions(lines, data.Accounts);
            
            // Beräkna kategoriserad data
            data.ExpensesByCategory = CalculateExpensesByCategory(data.Transactions);
            data.RevenueByCategory = CalculateRevenueByCategory(data.Transactions);
            data.MonthlyData = CalculateMonthlyData(data.Transactions);
            
            _logger.LogInformation("Parsed SIE4 data: {Year}, {TransactionCount} transactions, {AccountCount} accounts", 
                data.Year, data.Transactions.Count, data.Accounts.Count);
                
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse SIE4 data");
            return data; // Return empty data structure
        }
    }

    private int ExtractYear(string[] lines)
    {
        var rarLine = lines.FirstOrDefault(l => l.StartsWith("#RAR 0"));
        if (rarLine != null)
        {
            var match = Regex.Match(rarLine, @"#RAR 0 (\d{4})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
            {
                return year;
            }
        }
        return DateTime.Now.Year;
    }

    private string ExtractCompanyName(string[] lines)
    {
        var fnamn = lines.FirstOrDefault(l => l.StartsWith("#FNAMN"));
        if (fnamn != null)
        {
            var match = Regex.Match(fnamn, @"#FNAMN ""([^""]*)""");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return "Okänt företag";
    }

    private Dictionary<string, SIE4Account> ExtractAccounts(string[] lines)
    {
        var accounts = new Dictionary<string, SIE4Account>();
        
        foreach (var line in lines)
        {
            if (line.StartsWith("#KONTO"))
            {
                var match = Regex.Match(line, @"#KONTO (\d+) ""([^""]*)""");
                if (match.Success)
                {
                    var accountNumber = match.Groups[1].Value;
                    var accountName = match.Groups[2].Value;
                    
                    accounts[accountNumber] = new SIE4Account
                    {
                        Number = accountNumber,
                        Name = accountName,
                        Category = CategorizeAccount(accountNumber, accountName)
                    };
                }
            }
        }
        
        return accounts;
    }

    private string CategorizeAccount(string accountNumber, string accountName)
    {
        var number = int.TryParse(accountNumber, out var num) ? num : 0;
        var name = accountName.ToLowerInvariant();

        // Kategorisera baserat på kontonummer och namn enligt svensk kontoplan
        return number switch
        {
            // === PERSONAL OCH LÖNER (7000-7999) ===
            7010 or 7210 or 7220 => "Personal", // Löner till kollektiv/tjänstemän/företagsledare
            7291 or 7292 => "Personal", // Semesterlöneskuld
            7321 or 7322 => "Personal", // Traktamenten
            7331 or 7332 => "Personal", // Bilersättningar
            7385 or 7388 or 7390 => "Personal", // Förmåner
            7410 => "Personal", // Pensionsförsäkring
            7510 or 7518 => "Personal", // Sociala avgifter
            >= 7000 and <= 7999 => "Personal", // Alla övriga personalkostnader

            // === LOKALER (5000-5199) ===
            5010 => "Lokaler", // Lokalhyra
            5020 or 5030 or 5040 => "Lokaler", // El, värme, vatten
            5060 or 5070 or 5090 => "Lokaler", // Städning, reparation, övriga lokalkostnader
            5170 => "Lokaler", // Fastighetsunderhåll
            >= 5000 and <= 5199 => "Lokaler",

            // === IT OCH TEKNIK ===
            5410 => "IT", // Förbrukningsinventarier (ofta IT)
            5420 => "IT", // Programvaror
            5520 => "IT", // Reparation av datorer m.m.
            6540 => "IT", // IT-tjänster
            6230 or 6231 => "IT", // Datakommunikation, Dropbox

            // === KOMMUNIKATION ===
            6210 or 6211 or 6212 => "Telefon", // Telekommunikation, fast/mobil telefon
            6250 => "Telefon", // Postbefordran

            // === FORDON OCH RESOR ===
            5611 or 5612 or 5613 => "Fordon", // Drivmedel, försäkring, reparation personbilar
            5800 => "Resor", // Resekostnader
            // 7331 och 7332 hanteras redan under Personal

            // === REPRESENTATION OCH MARKNADSFÖRING ===
            6071 or 6072 => "Representation", // Representation avdragsgill/ej avdragsgill
            6090 => "Marknadsföring", // Övriga försäljningskostnader

            // === ADMINISTRATION ===
            6110 => "Kontorsmaterial", // Kontorsmateriel
            6410 => "Administration", // Styrelsearvoden
            6420 => "Administration", // Revisorsersättningar
            6490 => "Administration", // Övriga förvaltningskostnader
            6530 => "Administration", // Redovisningstjänster

            // === FÖRSÄKRINGAR ===
            6310 or 6320 => "Försäkringar", // Företagsförsäkringar, självrisker
            6370 => "Försäkringar", // Bevakning och larm

            // === ÖVRIGA KOSTNADER ===
            5460 or 5480 or 5490 => "Övrigt", // Förbrukningsmaterial, arbetskläder
            5580 => "Övrigt", // Underhåll arbetskläder
            6040 => "Övrigt", // Kontokortsavgifter
            6350 => "Övrigt", // Förluster på kundfordringar

            // === INTÄKTER (3000-3999) ===
            >= 3000 and <= 3999 => "Intäkter",

            // === BANKKONTON ===
            1930 => "Checkräkningskonto",
            >= 1900 and <= 1999 => "Bank",

            // === MASKINER OCH INVENTARIER ===
            5210 or 5220 => "Hyror", // Hyra av maskiner/inventarier
            >= 1200 and <= 1299 => "Inventarier", // Maskiner och inventarier

            // === FALLBACK ===
            >= 5000 and <= 6999 => "Övrigt", // Övriga rörelsekostnader
            _ => "Okategoriserad"
        };
    }

    private List<SIE4Transaction> ExtractTransactions(string[] lines, Dictionary<string, SIE4Account> accounts)
    {
        var transactions = new List<SIE4Transaction>();
        var inTransaction = false;
        string currentVerDate = "";
        string currentVerDescription = "";

        foreach (var line in lines)
        {
            if (line.StartsWith("#VER"))
            {
                inTransaction = true;
                var match = Regex.Match(line, @"#VER \w+ \d+ (\d{8}) ""([^""]*)""");
                if (match.Success)
                {
                    currentVerDate = match.Groups[1].Value;
                    currentVerDescription = match.Groups[2].Value;
                }
                continue;
            }

            if (line.Trim() == "}")
            {
                inTransaction = false;
                continue;
            }

            if (inTransaction && line.StartsWith("#TRANS"))
            {
                // Hantera både gamla och nya format av #TRANS
                var match = Regex.Match(line, @"#TRANS (\d+) \{\} (-?\d+(?:\.\d+)?)\s*(?:(\d{8})|\s*""([^""]*)""\s*\{\})?");
                if (match.Success)
                {
                    var accountNumber = match.Groups[1].Value;
                    var amount = decimal.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    var date = match.Groups[3].Success ? match.Groups[3].Value : currentVerDate;

                    var account = accounts.GetValueOrDefault(accountNumber);

                    transactions.Add(new SIE4Transaction
                    {
                        AccountNumber = accountNumber,
                        AccountName = account?.Name ?? $"Konto {accountNumber}",
                        Category = account?.Category ?? CategorizeAccount(accountNumber, account?.Name ?? ""),
                        Amount = amount,
                        Date = date,
                        Description = currentVerDescription
                    });
                }
                else
                {
                    // Försök med enklare regex för andra format
                    var simpleMatch = Regex.Match(line, @"#TRANS (\d+) \{\} (-?\d+(?:\.\d+)?)");
                    if (simpleMatch.Success)
                    {
                        var accountNumber = simpleMatch.Groups[1].Value;
                        var amount = decimal.Parse(simpleMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

                        var account = accounts.GetValueOrDefault(accountNumber);

                        transactions.Add(new SIE4Transaction
                        {
                            AccountNumber = accountNumber,
                            AccountName = account?.Name ?? $"Konto {accountNumber}",
                            Category = account?.Category ?? CategorizeAccount(accountNumber, account?.Name ?? ""),
                            Amount = amount,
                            Date = currentVerDate,
                            Description = currentVerDescription
                        });
                    }
                }
            }
        }

        return transactions;
    }

    private Dictionary<string, decimal> CalculateExpensesByCategory(List<SIE4Transaction> transactions)
    {
        return transactions
            .Where(t => t.Amount < 0) // Utgifter är negativa
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(t => t.Amount)));
    }

    private Dictionary<string, decimal> CalculateRevenueByCategory(List<SIE4Transaction> transactions)
    {
        return transactions
            .Where(t => t.Amount > 0 && t.Category == "Intäkter") // Intäkter är positiva
            .GroupBy(t => t.AccountName)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }

    private Dictionary<string, Dictionary<string, decimal>> CalculateMonthlyData(List<SIE4Transaction> transactions)
    {
        var monthlyData = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var transaction in transactions.Where(t => t.Amount < 0)) // Endast utgifter
        {
            if (transaction.Date.Length >= 6)
            {
                var month = transaction.Date[4..6]; // YYYYMMDD -> MM
                var monthName = month switch
                {
                    "01" => "Jan", "02" => "Feb", "03" => "Mar", "04" => "Apr",
                    "05" => "May", "06" => "Jun", "07" => "Jul", "08" => "Aug",
                    "09" => "Sep", "10" => "Oct", "11" => "Nov", "12" => "Dec",
                    _ => "Unknown"
                };

                if (!monthlyData.ContainsKey(transaction.Category))
                    monthlyData[transaction.Category] = new Dictionary<string, decimal>();

                if (!monthlyData[transaction.Category].ContainsKey(monthName))
                    monthlyData[transaction.Category][monthName] = 0;

                monthlyData[transaction.Category][monthName] += Math.Abs(transaction.Amount);
            }
        }

        return monthlyData;
    }
}

/// <summary>
/// Strukturerad SIE4-data
/// </summary>
public class SIE4ParsedData
{
    public int Year { get; set; }
    public string CompanyName { get; set; } = "";
    public Dictionary<string, SIE4Account> Accounts { get; set; } = new();
    public List<SIE4Transaction> Transactions { get; set; } = new();
    public Dictionary<string, decimal> ExpensesByCategory { get; set; } = new();
    public Dictionary<string, decimal> RevenueByCategory { get; set; } = new();
    public Dictionary<string, Dictionary<string, decimal>> MonthlyData { get; set; } = new();
}

/// <summary>
/// SIE4-konto
/// </summary>
public class SIE4Account
{
    public string Number { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
}

/// <summary>
/// SIE4-transaktion
/// </summary>
public class SIE4Transaction
{
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
}
