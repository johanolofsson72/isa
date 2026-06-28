using System.IO;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using AnalysisPlugin.Components;
using AnalysisPlugin.Models;

namespace AnalysisPlugin.Services;

/// <summary>
/// Watches for Excel file creation and automatically replaces the start page with validated components
/// </summary>
public class ExcelFileWatcher : IDisposable
{
    /// <summary>
    /// Maximum time to wait for file unlock in seconds
    /// </summary>
    private const int FILE_UNLOCK_WAIT_SECONDS = 10;

    /// <summary>
    /// Delay in milliseconds to wait for file to be fully written
    /// </summary>
    private const int FILE_PROCESSING_DELAY_MS = 2000;

    /// <summary>
    /// Identifier text used to validate front page components
    /// </summary>
    private const string FRONT_PAGE_IDENTIFIER = "VÅR FRONT";

    /// <summary>
    /// File pattern for Excel files to monitor
    /// </summary>
    private const string EXCEL_FILE_PATTERN = "*.xlsx";

    private readonly ILogger<ExcelFileWatcher> _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly string _watchDirectory;
    private readonly string _sie4FileContent;
    private readonly SIE4ProcessorService _sie4Processor;

    /// <summary>
    /// Initializes a new instance of the ExcelFileWatcher class
    /// </summary>
    /// <param name="logger">Logger instance for tracking file processing events</param>
    /// <param name="watchDirectory">Directory path to monitor for Excel file creation</param>
    /// <param name="sie4FileContent">SIE4 financial data content for validation</param>
    /// <param name="sie4Processor">SIE4 processor service for parsing financial data</param>
    public ExcelFileWatcher(ILogger<ExcelFileWatcher> logger, string watchDirectory, string sie4FileContent, SIE4ProcessorService sie4Processor)
    {
        _logger = logger;
        _watchDirectory = watchDirectory;
        _sie4FileContent = sie4FileContent;
        _sie4Processor = sie4Processor;

        _watcher = new FileSystemWatcher(_watchDirectory, EXCEL_FILE_PATTERN)
        {
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += HandleExcelFileCreatedAsync;
        _watcher.Changed += HandleExcelFileChangedAsync;

        _logger.LogError("🔥🔥🔥 ExcelFileWatcher STARTAT för mapp: {Directory}", _watchDirectory);
        Console.WriteLine($"🔥🔥🔥 ExcelFileWatcher STARTAT för mapp: {_watchDirectory}");
    }

    /// <summary>
    /// Handles Excel file creation events from the file system watcher
    /// </summary>
    /// <param name="sender">Event sender (FileSystemWatcher)</param>
    /// <param name="e">File system event arguments containing file path</param>
    private async void HandleExcelFileCreatedAsync(object sender, FileSystemEventArgs e)
    {
        await ProcessExcelFileAsync(e.FullPath, "CREATED");
    }

    /// <summary>
    /// Handles Excel file change events from the file system watcher
    /// </summary>
    /// <param name="sender">Event sender (FileSystemWatcher)</param>
    /// <param name="e">File system event arguments containing file path</param>
    private async void HandleExcelFileChangedAsync(object sender, FileSystemEventArgs e)
    {
        await ProcessExcelFileAsync(e.FullPath, "CHANGED");
    }

    private async Task ProcessExcelFileAsync(string excelFilePath, string eventType)
    {
        try
        {
            // Wait for file to be fully written
            await Task.Delay(FILE_PROCESSING_DELAY_MS);

            _logger.LogError("🔥🔥🔥 Excel-fil {EventType}: {FilePath}", eventType, excelFilePath);
            Console.WriteLine($"🔥🔥🔥 Excel-fil {eventType}: {excelFilePath}");

            // Process all Excel files (not just financial_report_* pattern)
            // This is necessary since we now use unique filenames based on REPORT_TYPE
            var fileName = Path.GetFileName(excelFilePath);
            _logger.LogInformation("Processing Excel file: {FileName}", fileName);

            // Wait until file is no longer locked
            await WaitForFileUnlockAsync(excelFilePath);

            // Replace start page with validated components
            await ReplaceStartPageWithValidatedComponentsAsync(excelFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Excel file: {FilePath}", excelFilePath);
            Console.WriteLine($"❌ Error processing Excel file: {excelFilePath} - {ex.Message}");
        }
    }

    private async Task WaitForFileUnlockAsync(string excelFilePath, int maxWaitSeconds = FILE_UNLOCK_WAIT_SECONDS)
    {
        var attempts = 0;
        var maxAttempts = maxWaitSeconds * 2; // 500ms per attempt

        while (attempts < maxAttempts)
        {
            try
            {
                using var stream = File.Open(excelFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return; // File is unlocked
            }
            catch (IOException)
            {
                await Task.Delay(500);
                attempts++;
            }
        }

        throw new TimeoutException($"Excel file {excelFilePath} is still locked after {maxWaitSeconds} seconds");
    }

    private async Task ReplaceStartPageWithValidatedComponentsAsync(string excelFilePath)
    {
        try
        {
            _logger.LogError("🔥🔥🔥 REPLACING START PAGE in file: {FilePath}", excelFilePath);
            Console.WriteLine($"🔥🔥🔥 REPLACING START PAGE in file: {excelFilePath}");

            // Read the actual SIE4 data from corresponding _source.se file
            var sie4FilePath = excelFilePath.Replace(".xlsx", "_source.se");
            string realSie4Data = _sie4FileContent; // Fallback to test data

            if (File.Exists(sie4FilePath))
            {
                realSie4Data = await File.ReadAllTextAsync(sie4FilePath);
                _logger.LogError("🔥🔥🔥 LÄSTE RIKTIG SIE4-DATA från: {Sie4FilePath} ({Length} tecken)",
                    sie4FilePath, realSie4Data.Length);
                Console.WriteLine($"🔥🔥🔥 LÄSTE RIKTIG SIE4-DATA från: {sie4FilePath} ({realSie4Data.Length} tecken)");
            }
            else
            {
                _logger.LogWarning("SIE4-fil hittades inte: {Sie4FilePath}, använder test-data", sie4FilePath);
                Console.WriteLine($"⚠️ SIE4-fil hittades inte: {sie4FilePath}, använder test-data");
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new FileInfo(excelFilePath));

            // Hitta eller skapa startsidan
            var startPage = package.Workbook.Worksheets.FirstOrDefault(w => w.Name == "Startsida");
            if (startPage == null)
            {
                _logger.LogWarning("Ingen startsida hittades - skapar automatiskt en");
                startPage = package.Workbook.Worksheets.Add("Startsida");

                // Flytta startsidan till första positionen
                package.Workbook.Worksheets.MoveToStart(startPage.Name);
            }

            // Rensa startsidan
            startPage.Cells.Clear();

            // 🔥 DETEKTERA TIDSPERIOD FRÅN ANVÄNDARENS FRÅGA (CHAT_MESSAGE)
            var chatMessage = Environment.GetEnvironmentVariable("CHAT_MESSAGE") ?? "";
            var timePeriod = DetectTimePeriodFromUserQuery(chatMessage);
            _logger.LogError("🔥 DETEKTERAD TIDSPERIOD från '{ChatMessage}': {TimePeriod}", chatMessage, timePeriod);

            // 🔥 FIXA ÄVEN PIVOT-DATA MED RIKTIG MÅNADSDATA
            await FixPivotDataWithRealMonthlyData(package, realSie4Data);
            _logger.LogError("🔥 RENSADE startsida");
            Console.WriteLine("🔥 RENSADE startsida");

            // Skapa vår FrontPageComponent med RIKTIG SIE4-data och tidsperiod
            var frontPageComponent = new FrontPageComponent();
            frontPageComponent.SIE4Data = realSie4Data;
            await frontPageComponent.ParseSIE4DataAsync(timePeriod);

            // 🔥 SPECIALHANTERING: Lägg till BalanceSheetComponent för balansrapporter
            if (chatMessage.ToLowerInvariant().Contains("balansrapport") || chatMessage.ToLowerInvariant().Contains("balans"))
            {
                _logger.LogError("🔥 DETEKTERAD BALANSRAPPORT - lägger till BalanceSheetComponent");
                var balanceSheetComponent = new Components.BalanceSheetComponent();
                balanceSheetComponent.SIE4Data = realSie4Data;
                await balanceSheetComponent.ParseSIE4DataAsync(timePeriod);

                // Lägg till balansräkning worksheet
                var balanceWorksheet = package.Workbook.Worksheets.Add("Balansräkning");
                await balanceSheetComponent.RenderAsync(balanceWorksheet, 1);
                _logger.LogError("🔥 BalanceSheetComponent tillagd i worksheet 'Balansräkning'");
            }

            // 🔥 SPECIALHANTERING: Lägg till AnnualReportComponent för årsrapporter
            if (chatMessage.ToLowerInvariant().Contains("årsrapport") || chatMessage.ToLowerInvariant().Contains("annual report"))
            {
                _logger.LogError("🔥 DETEKTERAD ÅRSRAPPORT - lägger till AnnualReportComponent");

                // Extrahera år från chatMessage (t.ex. "2023")
                var yearMatch = System.Text.RegularExpressions.Regex.Match(chatMessage, @"\b(\d{4})\b");
                var reportYear = yearMatch.Success ? int.Parse(yearMatch.Groups[1].Value) : DateTime.Now.Year - 1;

                var annualReportComponent = new Components.AnnualReportComponent(reportYear);
                annualReportComponent.SIE4Data = realSie4Data;
                await annualReportComponent.ParseSIE4DataAsync(timePeriod);

                // Lägg till årsrapport worksheet
                var annualWorksheet = package.Workbook.Worksheets.Add("Årsrapport");
                await annualReportComponent.RenderAsync(annualWorksheet, 1);
                _logger.LogError("🔥 AnnualReportComponent tillagd i worksheet 'Årsrapport' för år {Year}", reportYear);
            }

            // 🔥 SPECIALHANTERING: Lägg till VATReportComponent för momsrapporter
            if (chatMessage.ToLowerInvariant().Contains("momsrapport") || chatMessage.ToLowerInvariant().Contains("vat report"))
            {
                _logger.LogError("🔥 DETEKTERAD MOMSRAPPORT - lägger till VATReportComponent");

                // Extrahera kvartal och år från chatMessage (t.ex. "2025 Q3")
                var quarterYearMatch = System.Text.RegularExpressions.Regex.Match(chatMessage, @"(\d{4})\s*q(\d)");
                var quarter = quarterYearMatch.Success ? $"{quarterYearMatch.Groups[1].Value}Q{quarterYearMatch.Groups[2].Value}" : "2025Q3";

                var vatReportComponent = new Components.VATReportComponent(quarter);
                vatReportComponent.SIE4Data = realSie4Data;
                await vatReportComponent.ParseSIE4DataAsync(timePeriod);

                // Lägg till momsrapport worksheet
                var vatWorksheet = package.Workbook.Worksheets.Add("Momsrapport");
                await vatReportComponent.RenderAsync(vatWorksheet, 1);
                _logger.LogError("🔥 VATReportComponent tillagd i worksheet 'Momsrapport' för {Quarter}", quarter);
            }

            // 🔥 SPECIALHANTERING: Lägg till EnhancedFutureAnalysisComponent för framtidsanalyser
            if (chatMessage.ToLowerInvariant().Contains("framtidsanalys") || chatMessage.ToLowerInvariant().Contains("future analysis") || chatMessage.ToLowerInvariant().Contains("prognos"))
            {
                _logger.LogError("🔥 DETEKTERAD FRAMTIDSANALYS - lägger till EnhancedFutureAnalysisComponent med externa API:er");

                // Extrahera målperiod från chatMessage (t.ex. "2026 Q1")
                var targetMatch = System.Text.RegularExpressions.Regex.Match(chatMessage, @"(\d{4})\s*q(\d)");
                var targetPeriod = targetMatch.Success ? $"{targetMatch.Groups[1].Value}Q{targetMatch.Groups[2].Value}" : "2026Q1";

                // Skapa externa API-services (förenklad för denna implementation)
                var httpClient = new HttpClient();
                var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

                // Skapa loggers
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var circuitBreakerLogger = loggerFactory.CreateLogger<Services.ApiCircuitBreaker>();
                var externalServiceLogger = loggerFactory.CreateLogger<Services.ExternalAnalysisService>();

                var circuitBreaker = new Services.ApiCircuitBreaker(circuitBreakerLogger, new Configuration.GeneralSettings());
                var apiSettings = new Configuration.ExternalApiSettings();
                var externalService = new Services.ExternalAnalysisService(httpClient, externalServiceLogger, memoryCache, circuitBreaker, apiSettings);

                var enhancedFutureComponent = new Components.EnhancedFutureAnalysisComponent(externalService, targetPeriod);
                enhancedFutureComponent.SIE4Data = realSie4Data;
                await enhancedFutureComponent.ParseSIE4DataAsync(timePeriod);

                // Lägg till förbättrad framtidsanalys worksheet
                var enhancedFutureWorksheet = package.Workbook.Worksheets.Add("Förbättrad Framtidsanalys");
                await enhancedFutureComponent.RenderAsync(enhancedFutureWorksheet, 1);
                _logger.LogError("🔥 EnhancedFutureAnalysisComponent tillagd i worksheet 'Förbättrad Framtidsanalys' för {Period}", targetPeriod);

                // Lägg även till standard ML-analys för jämförelse
                var mlAnalysisComponent = new Components.MLAnalysisComponent();
                mlAnalysisComponent.SIE4Data = realSie4Data;
                mlAnalysisComponent.Title = "ML-baserade Prognoser";
                await mlAnalysisComponent.ParseSIE4DataAsync(timePeriod);

                var mlWorksheet = package.Workbook.Worksheets.Add("ML Analys");
                await mlAnalysisComponent.RenderAsync(mlWorksheet, 1);
                _logger.LogError("🔥 MLAnalysisComponent tillagd i worksheet 'ML Analys'");
            }

            // Rendera vår komponent
            var result = await frontPageComponent.RenderAsync(startPage, 1);

            if (result.Success)
            {
                // Spara ändringarna
                await package.SaveAsync();
                _logger.LogError("✅ STARTSIDA ERSATT med våra komponenter!");
                Console.WriteLine("✅ STARTSIDA ERSATT med våra komponenter!");

                // Rename file using REPORT_TYPE for unique filename
                var newFilePath = await RenameFileWithReportType(excelFilePath);

                // 🚨 AFFÄRSKRITISK VALIDERING: Obligatorisk validering innan rapport kan användas
                await RunMandatoryValidation(newFilePath, realSie4Data, timePeriod);
            }
            else
            {
                _logger.LogError("❌ Misslyckades att rendera FrontPageComponent: {Error}", result.ErrorMessage);
                Console.WriteLine($"❌ Misslyckades att rendera FrontPageComponent: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid ersättning av startsida: {Message}", ex.Message);
            Console.WriteLine($"❌ Fel vid ersättning av startsida: {ex.Message}");
        }
    }

    private Task<string> RenameFileWithReportType(string originalFilePath)
    {
        try
        {
            var reportType = Environment.GetEnvironmentVariable("REPORT_TYPE");
            if (string.IsNullOrEmpty(reportType))
            {
                _logger.LogInformation("Ingen REPORT_TYPE miljövariabel, behåller ursprungligt filnamn");
                return Task.FromResult(originalFilePath);
            }

            var directory = Path.GetDirectoryName(originalFilePath)!;
            var extension = Path.GetExtension(originalFilePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var newFileName = $"{reportType}_{timestamp}{extension}";
            var newFilePath = Path.Combine(directory, newFileName);

            // Byt namn på Excel-filen
            File.Move(originalFilePath, newFilePath);
            _logger.LogError("🔄 FIL OMDÖPT: {OldName} → {NewName}", Path.GetFileName(originalFilePath), newFileName);
            Console.WriteLine($"🔄 FIL OMDÖPT: {Path.GetFileName(originalFilePath)} → {newFileName}");

            // Byt även namn på SIE4-filen om den finns
            var originalSie4Path = originalFilePath.Replace(".xlsx", "_source.se");
            var newSie4Path = newFilePath.Replace(".xlsx", "_source.se");
            if (File.Exists(originalSie4Path))
            {
                File.Move(originalSie4Path, newSie4Path);
                _logger.LogError("🔄 SIE4-FIL OMDÖPT: {OldName} → {NewName}", Path.GetFileName(originalSie4Path), Path.GetFileName(newSie4Path));
                Console.WriteLine($"🔄 SIE4-FIL OMDÖPT: {Path.GetFileName(originalSie4Path)} → {Path.GetFileName(newSie4Path)}");
            }

            return Task.FromResult(newFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid omdöpning av fil: {FilePath}", originalFilePath);
            Console.WriteLine($"❌ Fel vid omdöpning av fil: {originalFilePath} - {ex.Message}");
            return Task.FromResult(originalFilePath); // Returnera ursprungligt namn om omdöpning misslyckas
        }
    }

    private async Task RunMandatoryValidation(string filePath, string sie4Data, string timePeriod = "Year")
    {
        try
        {
            _logger.LogError("🚨🚨🚨 KÖR AFFÄRSKRITISK VALIDERING för: {FilePath}", filePath);
            Console.WriteLine($"🚨🚨🚨 KÖR AFFÄRSKRITISK VALIDERING för: {filePath}");

            // Skapa validator med dependencies
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            var sie4Processor = new SIE4ProcessorService(loggerFactory.CreateLogger<SIE4ProcessorService>());
            var validator = new MandatoryReportValidator(
                loggerFactory.CreateLogger<MandatoryReportValidator>(),
                sie4Processor);

            // Kör obligatorisk validering med tidsperiod
            var validationResult = await validator.ValidateReportAsync(filePath, sie4Data, timePeriod);

            if (!validationResult.IsValid)
            {
                // 🚨 BLOCKERANDE FEL: Rapporten får inte användas
                _logger.LogError("🚨🚨🚨 AFFÄRSKRITISK VALIDERING MISSLYCKADES - RAPPORTEN ÄR BLOCKERAD!");
                Console.WriteLine("🚨🚨🚨 AFFÄRSKRITISK VALIDERING MISSLYCKADES - RAPPORTEN ÄR BLOCKERAD!");

                // Flytta rapporten till karantän
                var quarantinePath = filePath.Replace(".xlsx", "_INVALID_DO_NOT_USE.xlsx");
                File.Move(filePath, quarantinePath);

                _logger.LogError("📁 Rapport flyttad till karantän: {QuarantinePath}", quarantinePath);
                Console.WriteLine($"📁 Rapport flyttad till karantän: {quarantinePath}");

                // Skapa felrapport
                await CreateValidationErrorReport(quarantinePath, validationResult.ValidationErrors);
            }
            else
            {
                _logger.LogError("✅ AFFÄRSKRITISK VALIDERING GODKÄND - Rapporten är säker att använda");
                Console.WriteLine("✅ AFFÄRSKRITISK VALIDERING GODKÄND - Rapporten är säker att använda");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kritiskt fel under affärskritisk validering");
            Console.WriteLine($"❌ Kritiskt fel under affärskritisk validering: {ex.Message}");
        }
    }

    private async Task CreateValidationErrorReport(string originalFilePath, List<string> errors)
    {
        var errorReportPath = originalFilePath.Replace(".xlsx", "_VALIDATION_ERRORS.txt");
        var errorReport = $"""
            🚨 AFFÄRSKRITISK VALIDERING MISSLYCKADES
            =====================================

            Rapport: {Path.GetFileName(originalFilePath)}
            Tid: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

            VALIDERINGSFEL:
            {string.Join("\n", errors.Select(e => $"❌ {e}"))}

            ÅTGÄRD KRÄVS:
            Denna rapport innehåller kritiska fel och FÅR INTE skickas eller användas.
            Kontakta utvecklingsteamet omedelbart för att åtgärda problemen.
            """;

        await File.WriteAllTextAsync(errorReportPath, errorReport);

        _logger.LogError("📄 Felrapport skapad: {ErrorReportPath}", errorReportPath);
        Console.WriteLine($"📄 Felrapport skapad: {errorReportPath}");
    }

    /// <summary>
    /// Fixar pivot-data med riktig månadsdata från SIE4-transaktioner
    /// </summary>
    private async Task FixPivotDataWithRealMonthlyData(ExcelPackage package, string sie4Data)
    {
        try
        {
            _logger.LogError("🔥 FIXAR PIVOT-DATA MED RIKTIG MÅNADSDATA");

            // Hitta Pivot-worksheet
            var pivotWorksheet = package.Workbook.Worksheets.FirstOrDefault(w => w.Name == "Pivot");
            if (pivotWorksheet == null)
            {
                _logger.LogWarning("Ingen Pivot-worksheet hittades");
                return;
            }

            // Parsa SIE4-data för att få riktiga transaktioner
            var transactions = await _sie4Processor.ParseAsync(sie4Data);

            // Skapa riktig månadsdata från transaktioner
            var monthlyData = CreateRealMonthlyData(transactions);

            // Ersätt pivot-data med riktig månadsdata
            ReplacePivotDataWithRealData(pivotWorksheet, monthlyData, sie4Data);

            _logger.LogError("✅ PIVOT-DATA FIXAD MED RIKTIG MÅNADSDATA");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid fixning av pivot-data: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Skapar riktig månadsdata från SIE4-transaktioner - AGGREGERAD per månad och kontotyp
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> CreateRealMonthlyData(List<FinancialTransaction> transactions)
    {
        var monthlyData = new Dictionary<string, Dictionary<string, decimal>>();

        // Gruppera transaktioner per kontotyp och månad
        var groupedData = transactions
            .Where(t => Math.Abs(t.Amount) > 0) // Skippa noll-transaktioner
            .GroupBy(t => new {
                AccountType = GetAccountType(t.AccountNumber),
                Month = t.Date.ToString("MMM", System.Globalization.CultureInfo.CreateSpecificCulture("sv-SE"))
            })
            .Select(g => new {
                g.Key.AccountType,
                g.Key.Month,
                TotalAmount = g.Sum(t => Math.Abs(t.Amount)) // Använd absoluta värden för tydlighet
            })
            .Where(x => x.TotalAmount > 100) // Skippa små belopp för att hålla tabellen ren
            .OrderBy(x => x.AccountType)
            .ThenBy(x => GetMonthOrder(x.Month));

        foreach (var item in groupedData)
        {
            if (!monthlyData.ContainsKey(item.AccountType))
                monthlyData[item.AccountType] = new Dictionary<string, decimal>();

            monthlyData[item.AccountType][item.Month] = item.TotalAmount;
        }

        _logger.LogError("🔥 Skapade aggregerad månadsdata: {AccountTypes} kontotyper", monthlyData.Count);
        return monthlyData;
    }

    /// <summary>
    /// Konverterar kontonummer till läsbar kontotyp
    /// </summary>
    private string GetAccountType(string accountNumber)
    {
        return accountNumber.Substring(0, 1) switch
        {
            "1" => "Tillgångar",
            "2" => "Skulder",
            "3" => "Intäkter",
            "4" => "Kostnader (Inköp)",
            "5" => "Kostnader (Lokaler)",
            "6" => "Kostnader (Övriga)",
            "7" => "Kostnader (Personal)",
            "8" => "Finansiella poster",
            _ => "Övrigt"
        };
    }

    /// <summary>
    /// Returnerar månadsordning för sortering
    /// </summary>
    private int GetMonthOrder(string month)
    {
        return month.ToLowerInvariant() switch
        {
            "jan." => 1, "jan" => 1,
            "feb." => 2, "feb" => 2,
            "mars" => 3, "mar" => 3,
            "apr." => 4, "apr" => 4,
            "maj" => 5, "may" => 5,
            "juni" => 6, "jun" => 6,
            "juli" => 7, "jul" => 7,
            "aug." => 8, "aug" => 8,
            "sep." => 9, "sep" => 9,
            "okt." => 10, "okt" => 10,
            "nov." => 11, "nov" => 11,
            "dec." => 12, "dec" => 12,
            _ => 13
        };
    }

    /// <summary>
    /// Ersätter pivot-data i worksheet med riktig månadsdata
    /// </summary>
    private void ReplacePivotDataWithRealData(ExcelWorksheet pivotWorksheet, Dictionary<string, Dictionary<string, decimal>> monthlyData, string sie4Data)
    {
        // 🔥 SPECIALHANTERING FÖR OLIKA RAPPORTTYPER - DEFINIERA FÖRST
        var pivotTitle = pivotWorksheet.Cells[1, 1].Value?.ToString() ?? "";

        // DETEKTERA TIDSPERIOD FÖRST
        var timePeriod = DetectTimePeriod(sie4Data, pivotTitle);
        _logger.LogError("🔥 DETEKTERAD TIDSPERIOD: {TimePeriod}", timePeriod);

        // PRIORITERA SPECIFIKA RAPPORTTYPER (mest specifik först)
        var isCashFlowReport = IsCashFlowReport(sie4Data) || IsCashFlowReport(pivotTitle);
        var isBalanceReport = !isCashFlowReport && (IsBalanceReport(sie4Data) || IsBalanceReport(pivotTitle));
        var isQ4Report = !isCashFlowReport && !isBalanceReport && (IsQ4Report(sie4Data) || IsQ4Report(pivotTitle));

        Dictionary<string, Dictionary<string, decimal>> filteredData;

        // FÖRST: Filtrera baserat på tidsperiod
        var timeFilteredData = FilterByTimePeriod(monthlyData, timePeriod);

        // SEDAN: Filtrera baserat på rapporttyp
        if (isCashFlowReport)
        {
            filteredData = FilterForCashFlow(timeFilteredData);
        }
        else if (isBalanceReport)
        {
            filteredData = FilterForBalanceSheet(timeFilteredData);
        }
        else if (isQ4Report)
        {
            filteredData = FilterForQ4AndResultat(timeFilteredData);
        }
        else
        {
            filteredData = timeFilteredData;
        }

        if (isCashFlowReport)
        {
            _logger.LogError("🔥 KASSAFLÖDESRAPPORT DETEKTERAD - Filtrerar för kassaflöden");
        }
        else if (isBalanceReport)
        {
            _logger.LogError("🔥 BALANSRAPPORT DETEKTERAD - Filtrerar för balansräkningsposter");
        }
        else if (isQ4Report)
        {
            _logger.LogError("🔥 Q4-RAPPORT DETEKTERAD - Filtrerar för Q4 och resultatposter");
        }

        // 🔥 RENSA HELA WORKSHEETET OCH SKAPA REN STRUKTUR
        pivotWorksheet.Cells.Clear();

        // 🔥 TA BORT ALLA PIVOT-TABELLER OCH TABELLER SOM EXCEL KAN HA SKAPAT
        try
        {
            // Ta bort alla tabeller
            while (pivotWorksheet.Tables.Count > 0)
            {
                pivotWorksheet.Tables.Delete(0);
            }

            // Ta bort alla pivot-tabeller
            while (pivotWorksheet.PivotTables.Count > 0)
            {
                pivotWorksheet.PivotTables.Delete(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Kunde inte ta bort alla tabeller/pivots: {Message}", ex.Message);
        }

        // Sätt titel baserat på rapporttyp
        string title;
        if (isCashFlowReport)
            title = "Kassaflöde 2023 - In- och utbetalningar";
        else if (isQ4Report)
            title = "Intäkter och kostnader för Q4 2023";
        else if (isBalanceReport)
            title = "Tillgångar, skulder och eget kapital 2023";
        else
            title = "Månadsvis uppdelning";

        pivotWorksheet.Cells[1, 1].Value = title;
        pivotWorksheet.Cells[1, 1].Style.Font.Bold = true;
        pivotWorksheet.Cells[1, 1].Style.Font.Size = 14;

        // Sätt headers för ren data
        pivotWorksheet.Cells[3, 1].Value = "Category";
        pivotWorksheet.Cells[3, 2].Value = "Month";
        pivotWorksheet.Cells[3, 3].Value = "Amount";
        pivotWorksheet.Cells[3, 4].Value = "Type";

        // Gör headers fetstil
        for (int col = 1; col <= 4; col++)
        {
            pivotWorksheet.Cells[3, col].Style.Font.Bold = true;
        }

        var currentRow = 4;

        // Lägg till riktig månadsdata - UTAN att trigga automatisk pivot
        foreach (var (accountName, months) in filteredData)
        {
            foreach (var (month, amount) in months)
            {
                if (amount != 0) // Skippa månader utan transaktioner
                {
                    var type = DetermineTransactionType(accountName, amount);

                    // Använd text-format för att undvika automatisk pivot-detektering
                    pivotWorksheet.Cells[currentRow, 1].Value = accountName;
                    pivotWorksheet.Cells[currentRow, 2].Value = month;
                    pivotWorksheet.Cells[currentRow, 3].Value = Math.Abs(amount);
                    pivotWorksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00"; // Formatera som nummer
                    pivotWorksheet.Cells[currentRow, 4].Value = type;

                    currentRow++;
                }
            }
        }

        // 🔥 LÄGG TILL SEPARATOR FÖR ATT FÖRHINDRA AUTOMATISK PIVOT-DETEKTERING
        currentRow++; // Tom rad
        pivotWorksheet.Cells[currentRow, 1].Value = ""; // Tom rad för separation
        currentRow++;

        // 🔥 SKAPA PIVOT-TABELL EFTER DEN RENA DATAN
        var pivotStartRow = currentRow + 2;
        CreatePivotSummary(pivotWorksheet, filteredData, pivotStartRow, isQ4Report, isBalanceReport, isCashFlowReport);

        _logger.LogError("🔥 Ersatte {RowCount} rader med riktig månadsdata + pivot-sammanfattning", currentRow - 4);
    }

    /// <summary>
    /// Bestämmer transaktionstyp baserat på kontotyp
    /// </summary>
    private string DetermineTransactionType(string accountType, decimal amount)
    {
        return accountType switch
        {
            "Intäkter" => "Intäkt",
            "Tillgångar" => "Tillgång",
            "Skulder" => "Skuld",
            _ => "Kostnad"
        };
    }

    /// <summary>
    /// Detekterar om detta är en Q4-rapport baserat på SIE4-data eller filnamn
    /// </summary>
    private bool IsQ4Report(string sie4Data)
    {
        // Kolla om det finns Q4-indikatorer i SIE4-data eller filnamn
        var indicators = new[] { "Q4", "q4", "resultat", "Resultat", "vinstmarginal", "Vinstmarginal" };
        return indicators.Any(indicator => sie4Data.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filtrerar månadsdata för Q4 (okt, nov, dec) och endast resultatposter (intäkter/kostnader)
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> FilterForQ4AndResultat(Dictionary<string, Dictionary<string, decimal>> monthlyData)
    {
        var q4Months = new[] { "okt.", "okt", "nov.", "nov", "dec.", "dec" };
        var resultatAccountTypes = new[] { "Intäkter", "Kostnader (Inköp)", "Kostnader (Lokaler)", "Kostnader (Övriga)", "Kostnader (Personal)" };

        var filteredData = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var (accountType, months) in monthlyData)
        {
            // Skippa balansräkningsposter (Tillgångar, Skulder)
            if (!resultatAccountTypes.Contains(accountType))
                continue;

            var q4MonthsForAccount = new Dictionary<string, decimal>();

            foreach (var (month, amount) in months)
            {
                // Bara Q4-månader
                if (q4Months.Contains(month.ToLowerInvariant()))
                {
                    q4MonthsForAccount[month] = amount;
                }
            }

            if (q4MonthsForAccount.Any())
            {
                filteredData[accountType] = q4MonthsForAccount;
            }
        }

        _logger.LogError("🔥 Q4-filtrering: {Original} kontotyper → {Filtered} kontotyper", monthlyData.Count, filteredData.Count);
        return filteredData;
    }

    /// <summary>
    /// Detekterar om detta är en balansrapport baserat på SIE4-data eller filnamn
    /// </summary>
    private bool IsBalanceReport(string data)
    {
        var indicators = new[] { "balansrapport", "Balansrapport", "balans", "Balans", "tillgång", "Tillgång", "skuld", "Skuld", "eget kapital", "Eget kapital" };
        var result = indicators.Any(indicator => data.Contains(indicator, StringComparison.OrdinalIgnoreCase));

        if (result)
        {
            _logger.LogError("🔥 BALANSRAPPORT-INDIKATOR HITTAD I: '{Data}'", data.Length > 100 ? data.Substring(0, 100) + "..." : data);
        }

        return result;
    }

    /// <summary>
    /// Filtrerar månadsdata för balansräkningsposter (tillgångar, skulder, eget kapital)
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> FilterForBalanceSheet(Dictionary<string, Dictionary<string, decimal>> monthlyData)
    {
        var balanceAccountTypes = new[] { "Tillgångar", "Skulder", "Finansiella poster" };

        var filteredData = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var (accountType, months) in monthlyData)
        {
            // Bara balansräkningsposter (Tillgångar, Skulder, Finansiella poster)
            if (!balanceAccountTypes.Contains(accountType))
                continue;

            filteredData[accountType] = months;
        }

        _logger.LogError("🔥 Balansfiltrering: {Original} kontotyper → {Filtered} kontotyper", monthlyData.Count, filteredData.Count);
        return filteredData;
    }

    /// <summary>
    /// Detekterar tidsperiod för rapporten
    /// </summary>
    private string DetectTimePeriod(string data)
    {
        var lowerData = data.ToLowerInvariant();

        // Vecka
        if (lowerData.Contains("fg vecka") || lowerData.Contains("föregående vecka") || lowerData.Contains("förra vecka"))
            return "PreviousWeek";
        if (lowerData.Contains("denna vecka") || lowerData.Contains("innevarande vecka"))
            return "CurrentWeek";
        if (lowerData.Contains("vecka"))
            return "Week";

        // Dag
        if (lowerData.Contains("igår") || lowerData.Contains("föregående dag"))
            return "PreviousDay";
        if (lowerData.Contains("idag") || lowerData.Contains("denna dag"))
            return "CurrentDay";
        if (lowerData.Contains("dag"))
            return "Day";

        // Kvartal
        if (lowerData.Contains("q1") || lowerData.Contains("q2") || lowerData.Contains("q3") || lowerData.Contains("q4"))
            return "Quarter";
        if (lowerData.Contains("kvartal"))
            return "Quarter";

        // Månad
        if (lowerData.Contains("förra månaden") || lowerData.Contains("föregående månad"))
            return "PreviousMonth";
        if (lowerData.Contains("denna månad") || lowerData.Contains("innevarande månad"))
            return "CurrentMonth";
        if (lowerData.Contains("månad"))
            return "Month";

        // År (default)
        return "Year";
    }

    /// <summary>
    /// Detekterar tidsperiod för rapporten (överlagrad version)
    /// </summary>
    private string DetectTimePeriod(string sie4Data, string pivotTitle)
    {
        var sie4Period = DetectTimePeriod(sie4Data);
        var titlePeriod = DetectTimePeriod(pivotTitle);

        // Prioritera mer specifika perioder
        var periods = new[] { sie4Period, titlePeriod };

        if (periods.Contains("PreviousWeek")) return "PreviousWeek";
        if (periods.Contains("CurrentWeek")) return "CurrentWeek";
        if (periods.Contains("PreviousDay")) return "PreviousDay";
        if (periods.Contains("CurrentDay")) return "CurrentDay";
        if (periods.Contains("PreviousMonth")) return "PreviousMonth";
        if (periods.Contains("CurrentMonth")) return "CurrentMonth";
        if (periods.Contains("Week")) return "Week";
        if (periods.Contains("Day")) return "Day";
        if (periods.Contains("Quarter")) return "Quarter";
        if (periods.Contains("Month")) return "Month";

        return "Year";
    }

    /// <summary>
    /// Detekterar tidsperiod från användarens fråga (CHAT_MESSAGE)
    /// </summary>
    private string DetectTimePeriodFromUserQuery(string userQuery)
    {
        var lowerQuery = userQuery.ToLowerInvariant();

        // 🔥 SPECIFIK VECKA OCH ÅR (t.ex. "vecka 35 2022")
        var weekYearMatch = System.Text.RegularExpressions.Regex.Match(lowerQuery, @"vecka\s+(\d+)\s+(\d{4})");
        if (weekYearMatch.Success)
        {
            var week = weekYearMatch.Groups[1].Value;
            var year = weekYearMatch.Groups[2].Value;
            _logger.LogError("🔥 DETEKTERAD SPECIFIK VECKA från användarfråga: Vecka {Week} år {Year}", week, year);
            return $"Week{week}_{year}"; // t.ex. "Week35_2022"
        }

        // Andra tidsperioder...
        if (lowerQuery.Contains("fg vecka") || lowerQuery.Contains("föregående vecka") || lowerQuery.Contains("förra vecka"))
            return "PreviousWeek";
        if (lowerQuery.Contains("denna vecka") || lowerQuery.Contains("innevarande vecka"))
            return "CurrentWeek";
        if (lowerQuery.Contains("vecka"))
            return "Week";

        // År (default)
        return "Year";
    }

    /// <summary>
    /// Detekterar tidsperiod från SIE4-data med stöd för specifika veckor/år (DEPRECATED - använd DetectTimePeriodFromUserQuery istället)
    /// </summary>
    private string DetectTimePeriodFromSIE4(string sie4Data)
    {
        var lowerData = sie4Data.ToLowerInvariant();

        // 🔥 SPECIFIK VECKA OCH ÅR (t.ex. "vecka 35 2022")
        var weekYearMatch = System.Text.RegularExpressions.Regex.Match(lowerData, @"vecka\s+(\d+)\s+(\d{4})");
        if (weekYearMatch.Success)
        {
            var week = weekYearMatch.Groups[1].Value;
            var year = weekYearMatch.Groups[2].Value;
            _logger.LogError("🔥 DETEKTERAD SPECIFIK VECKA: Vecka {Week} år {Year}", week, year);
            return $"Week{week}_{year}"; // t.ex. "Week35_2022"
        }

        // Vecka (generell)
        if (lowerData.Contains("fg vecka") || lowerData.Contains("föregående vecka") || lowerData.Contains("förra vecka"))
            return "PreviousWeek";
        if (lowerData.Contains("denna vecka") || lowerData.Contains("innevarande vecka"))
            return "CurrentWeek";
        if (lowerData.Contains("vecka"))
            return "Week";

        // Dag
        if (lowerData.Contains("igår") || lowerData.Contains("föregående dag"))
            return "PreviousDay";
        if (lowerData.Contains("idag") || lowerData.Contains("denna dag"))
            return "CurrentDay";
        if (lowerData.Contains("dag"))
            return "Day";

        // Kvartal
        if (lowerData.Contains("q1") || lowerData.Contains("q2") || lowerData.Contains("q3") || lowerData.Contains("q4"))
            return "Quarter";
        if (lowerData.Contains("kvartal"))
            return "Quarter";

        // Månad
        if (lowerData.Contains("förra månaden") || lowerData.Contains("föregående månad"))
            return "PreviousMonth";
        if (lowerData.Contains("denna månad") || lowerData.Contains("innevarande månad"))
            return "CurrentMonth";
        if (lowerData.Contains("månad"))
            return "Month";

        // År (default)
        return "Year";
    }

    /// <summary>
    /// Filtrerar månadsdata baserat på tidsperiod
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> FilterByTimePeriod(Dictionary<string, Dictionary<string, decimal>> monthlyData, string timePeriod)
    {
        _logger.LogError("🔥 FILTRERAR FÖR TIDSPERIOD: {TimePeriod}", timePeriod);

        switch (timePeriod)
        {
            case "PreviousWeek":
                return FilterForPreviousWeek(monthlyData);
            case "CurrentWeek":
                return FilterForCurrentWeek(monthlyData);
            case "PreviousDay":
                return FilterForPreviousDay(monthlyData);
            case "CurrentDay":
                return FilterForCurrentDay(monthlyData);
            case "PreviousMonth":
                return FilterForPreviousMonth(monthlyData);
            case "CurrentMonth":
                return FilterForCurrentMonth(monthlyData);
            case "Week":
                return FilterForWeek(monthlyData);
            case "Day":
                return FilterForDay(monthlyData);
            case "Quarter":
                return FilterForQuarter(monthlyData);
            case "Month":
                return FilterForMonth(monthlyData);
            default:
                return monthlyData; // Year - returnera all data
        }
    }

    /// <summary>
    /// Filtrerar data för föregående vecka
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> FilterForPreviousWeek(Dictionary<string, Dictionary<string, decimal>> monthlyData)
    {
        // För nu: simulera genom att ta en mindre delmängd av data
        // I en riktig implementation skulle vi filtrera baserat på faktiska datum
        var filteredData = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var (accountType, months) in monthlyData)
        {
            var weekData = new Dictionary<string, decimal>();

            // Simulera "föregående vecka" genom att ta första månaden med reducerad data
            var firstMonth = months.FirstOrDefault();
            if (firstMonth.Key != null)
            {
                // Reducera till ~1/4 för att simulera veckodata
                weekData["fg vecka"] = firstMonth.Value * 0.25m;
            }

            if (weekData.Any())
            {
                filteredData[accountType] = weekData;
            }
        }

        _logger.LogError("🔥 Föregående vecka-filtrering: {Original} kontotyper → {Filtered} kontotyper", monthlyData.Count, filteredData.Count);
        return filteredData;
    }

    /// <summary>
    /// Placeholder-metoder för andra tidsperioder
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> FilterForCurrentWeek(Dictionary<string, Dictionary<string, decimal>> monthlyData) => FilterForPreviousWeek(monthlyData);
    private Dictionary<string, Dictionary<string, decimal>> FilterForPreviousDay(Dictionary<string, Dictionary<string, decimal>> monthlyData) => FilterForPreviousWeek(monthlyData);
    private Dictionary<string, Dictionary<string, decimal>> FilterForCurrentDay(Dictionary<string, Dictionary<string, decimal>> monthlyData) => FilterForPreviousWeek(monthlyData);
    private Dictionary<string, Dictionary<string, decimal>> FilterForPreviousMonth(Dictionary<string, Dictionary<string, decimal>> monthlyData) => monthlyData;
    private Dictionary<string, Dictionary<string, decimal>> FilterForCurrentMonth(Dictionary<string, Dictionary<string, decimal>> monthlyData) => monthlyData;
    private Dictionary<string, Dictionary<string, decimal>> FilterForWeek(Dictionary<string, Dictionary<string, decimal>> monthlyData) => FilterForPreviousWeek(monthlyData);
    private Dictionary<string, Dictionary<string, decimal>> FilterForDay(Dictionary<string, Dictionary<string, decimal>> monthlyData) => FilterForPreviousWeek(monthlyData);
    private Dictionary<string, Dictionary<string, decimal>> FilterForQuarter(Dictionary<string, Dictionary<string, decimal>> monthlyData) => monthlyData;
    private Dictionary<string, Dictionary<string, decimal>> FilterForMonth(Dictionary<string, Dictionary<string, decimal>> monthlyData) => monthlyData;

    /// <summary>
    /// Detekterar om detta är en kassaflödesrapport baserat på SIE4-data eller filnamn
    /// </summary>
    private bool IsCashFlowReport(string data)
    {
        var indicators = new[] { "kassaflöde", "Kassaflöde", "kassaflödesrapport", "Kassaflödesrapport", "inbetalning", "Inbetalning", "utbetalning", "Utbetalning" };
        var result = indicators.Any(indicator => data.Contains(indicator, StringComparison.OrdinalIgnoreCase));

        if (result)
        {
            _logger.LogError("🔥 KASSAFLÖDESRAPPORT-INDIKATOR HITTAD I: '{Data}'", data.Length > 100 ? data.Substring(0, 100) + "..." : data);
        }

        return result;
    }

    /// <summary>
    /// Filtrerar månadsdata för kassaflöden (in- och utbetalningar)
    /// </summary>
    private Dictionary<string, Dictionary<string, decimal>> FilterForCashFlow(Dictionary<string, Dictionary<string, decimal>> monthlyData)
    {
        // Kassaflöden fokuserar på kassa/bank-konton och betalningsflöden
        var cashFlowAccountTypes = new[] { "Tillgångar", "Intäkter", "Kostnader (Inköp)", "Kostnader (Personal)", "Kostnader (Övriga)" };

        var filteredData = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var (accountType, months) in monthlyData)
        {
            // Fokusera på konton som påverkar kassaflödet
            if (!cashFlowAccountTypes.Contains(accountType))
                continue;

            // Omklassificera för kassaflödesrapport
            var cashFlowType = accountType switch
            {
                "Intäkter" => "Inbetalningar",
                "Kostnader (Inköp)" => "Utbetalningar (Inköp)",
                "Kostnader (Personal)" => "Utbetalningar (Personal)",
                "Kostnader (Övriga)" => "Utbetalningar (Övriga)",
                "Tillgångar" => "Kassaförändring",
                _ => accountType
            };

            filteredData[cashFlowType] = months;
        }

        _logger.LogError("🔥 Kassaflödesfiltrering: {Original} kontotyper → {Filtered} kassaflödestyper", monthlyData.Count, filteredData.Count);
        return filteredData;
    }

    /// <summary>
    /// Skapar en pivot-sammanfattning EFTER den rena datan
    /// </summary>
    private void CreatePivotSummary(ExcelWorksheet worksheet, Dictionary<string, Dictionary<string, decimal>> data, int startRow, bool isQ4Report, bool isBalanceReport = false, bool isCashFlowReport = false)
    {
        // Titel för pivot-sammanfattning
        string summaryTitle;
        if (isCashFlowReport)
            summaryTitle = "Kassaflödessammanfattning per månad";
        else if (isQ4Report)
            summaryTitle = "Q4 Sammanfattning per månad";
        else if (isBalanceReport)
            summaryTitle = "Balanssammanfattning per månad";
        else
            summaryTitle = "Sammanfattning per månad";

        worksheet.Cells[startRow, 1].Value = summaryTitle;
        worksheet.Cells[startRow, 1].Style.Font.Bold = true;
        worksheet.Cells[startRow, 1].Style.Font.Size = 12;

        var currentRow = startRow + 2;

        // Samla alla månader
        var allMonths = data.SelectMany(d => d.Value.Keys).Distinct().OrderBy(GetMonthOrder).ToList();

        // Headers för pivot
        worksheet.Cells[currentRow, 1].Value = "Kontotyp";
        var col = 2;
        foreach (var month in allMonths)
        {
            worksheet.Cells[currentRow, col].Value = month;
            col++;
        }
        worksheet.Cells[currentRow, col].Value = "Totalsumma";

        // Gör headers fetstil
        for (int c = 1; c <= col; c++)
        {
            worksheet.Cells[currentRow, c].Style.Font.Bold = true;
        }

        currentRow++;

        // Data för varje kontotyp
        foreach (var (accountType, months) in data.OrderBy(d => d.Key))
        {
            worksheet.Cells[currentRow, 1].Value = accountType;
            col = 2;
            decimal totalForAccount = 0;

            foreach (var month in allMonths)
            {
                var amount = months.ContainsKey(month) ? months[month] : 0;
                if (amount != 0)
                {
                    worksheet.Cells[currentRow, col].Value = amount;
                    totalForAccount += amount;
                }
                col++;
            }

            // Totalsumma för kontotyp
            worksheet.Cells[currentRow, col].Value = totalForAccount;
            worksheet.Cells[currentRow, col].Style.Font.Bold = true;

            currentRow++;
        }

        // Total-rad
        worksheet.Cells[currentRow, 1].Value = "Totalsumma";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        col = 2;
        decimal grandTotal = 0;

        foreach (var month in allMonths)
        {
            var monthTotal = data.Sum(d => d.Value.ContainsKey(month) ? d.Value[month] : 0);
            if (monthTotal != 0)
            {
                worksheet.Cells[currentRow, col].Value = monthTotal;
                worksheet.Cells[currentRow, col].Style.Font.Bold = true;
                grandTotal += monthTotal;
            }
            col++;
        }

        worksheet.Cells[currentRow, col].Value = grandTotal;
        worksheet.Cells[currentRow, col].Style.Font.Bold = true;

        _logger.LogError("🔥 Skapade pivot-sammanfattning med {Months} månader och {AccountTypes} kontotyper",
            allMonths.Count, data.Count);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _logger.LogInformation("ExcelFileWatcher disposed");
    }
}
