using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using AnalysisPlugin.Configuration;
using AnalysisPlugin.Models;
using AnalysisPlugin.Services;
using Shared.Services;

namespace AnalysisPlugin;

/// <summary>
/// Main AnalysisPlugin class with KernelFunction methods for AI agent interaction
/// </summary>
public class AnalysisPlugin
{
    private readonly ILogger<AnalysisPlugin> _logger;
    private readonly AnalysisPluginOptions _options;
    private readonly ReportBuilderService _reportBuilder;
    private readonly SIE4ProcessorService _sie4Processor;
    private readonly MLAnalysisService _mlAnalysis;
    private readonly NaturalLanguageService _naturalLanguage;

    /// <summary>
    /// Static service provider for accessing DI services (set by Agent during startup)
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    public AnalysisPlugin(IConfiguration configuration, ILogger<AnalysisPlugin> logger)
    {
        _logger = logger;
        _options = configuration.GetSection("AnalysisPlugin").Get<AnalysisPluginOptions>() ?? new AnalysisPluginOptions();
        
        // Initialize services (in production, these would be injected via DI)
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _sie4Processor = new SIE4ProcessorService(loggerFactory.CreateLogger<SIE4ProcessorService>());
        _reportBuilder = new ReportBuilderService(_options, loggerFactory.CreateLogger<ReportBuilderService>());

        // Get HttpClient from ServiceProvider if available (for SSL certificate bypass)
        HttpClient? httpClient = null;
        try
        {
            httpClient = ServiceProvider?.GetService<IHttpClientFactory>()?.CreateClient();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get HttpClient from ServiceProvider, using default HttpClient");
        }

        _mlAnalysis = new MLAnalysisService(loggerFactory.CreateLogger<MLAnalysisService>(), httpClient);
        _naturalLanguage = new NaturalLanguageService(loggerFactory.CreateLogger<NaturalLanguageService>());
        
        _logger.LogInformation("AnalysisPlugin initialized with options: {@Options}", _options);
    }

    /// <summary>
    /// Creates a new report job for building financial reports step by step
    /// </summary>
    /// <param name="sie4Data">SIE4 financial data content</param>
    /// <param name="language">Report language (sv/en)</param>
    /// <returns>JSON with reportJobId and status</returns>
    [KernelFunction]
    [Description("AVANCERAD FUNKTION: Skapar en Excel-rapport från SIE4-bokföringsdata. Använd ENDAST denna funktion om användaren specifikt ber om manuell rapportskapande eller om CreateCompleteReportAsync inte fungerar. För normala rapportförfrågningar, använd CreateCompleteReportAsync istället.")]
    public async Task<string> CreateReportJobAsync(
        [Description("SIE4-bokföringsdata som innehåller transaktioner, konton och ekonomisk information")] string sie4Data,
        [Description("Rapportspråk: 'sv' för svenska, 'en' för engelska")] string language = "sv")
    {
        try
        {
            _logger.LogInformation("Creating new report job with language: {Language}", language);

            // Validate SIE4 data
            if (!_sie4Processor.ValidateSIE4Format(sie4Data))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "INVALID_SIE4_DATA",
                    message = "Malformed SIE4 content provided"
                });
            }

            // Create report job
            var reportJobId = await _reportBuilder.CreateReportJobAsync(sie4Data, language);

            return JsonSerializer.Serialize(new
            {
                reportJobId,
                status = "created",
                message = "Report job created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create report job");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "REPORT_CREATION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Creates a new report job using SIE data from TempFilesService
    /// </summary>
    /// <param name="sieFileId">File ID from TempFilesService containing SIE4 data</param>
    /// <param name="language">Report language (sv/en)</param>
    /// <returns>JSON with reportJobId and status</returns>
    [KernelFunction]
    [Description("STEP 2: Creates Excel report job from SIE file. Use immediately after GetSIEAsync. Creates report structure only - add components with AddChartAsync, AddPivotTableAsync etc, then call FinishReportAsync.")]
    public async Task<string> CreateReportJobFromFileAsync(
        [Description("File ID från TempFilesService som innehåller SIE4-bokföringsdata för ett specifikt år")] string sieFileId,
        [Description("Rapportspråk: 'sv' för svenska, 'en' för engelska")] string language = "sv")
    {
        try
        {
            // 🚨 OMFATTANDE LOGGNING: Spåra AI-agentens anrop
            _logger.LogError("🔥🔥🔥 AI-AGENT ANROPADE CreateReportJobFromFileAsync!");
            _logger.LogError("🔥🔥🔥 PARAMETRAR: SieFileId='{SieFileId}', Language='{Language}'", sieFileId, language);
            Console.WriteLine($"🔥🔥🔥 AI-AGENT ANROPADE CreateReportJobFromFileAsync!");
            Console.WriteLine($"🔥🔥🔥 PARAMETRAR: SieFileId='{sieFileId}', Language='{language}'");

            _logger.LogInformation("Creating new report job from File ID: {FileId}, Language: {Language}", sieFileId, language);

            // Parse File ID
            if (!Guid.TryParse(sieFileId, out var fileGuid))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "INVALID_FILE_ID",
                    message = "File ID must be a valid GUID"
                });
            }

            // Get SIE data from TempFilesService
            using var scope = ServiceProvider?.CreateScope();
            if (scope == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "SERVICE_PROVIDER_NOT_AVAILABLE",
                    message = "ServiceProvider is not available. Ensure AnalysisPlugin is properly initialized by Agent."
                });
            }
            var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

            var sieBytes = await tempFilesService.ReadFileAsync(fileGuid);
            if (sieBytes == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "FILE_NOT_FOUND",
                    message = $"SIE file with ID {sieFileId} not found in TempFilesService"
                });
            }

            var sieContent = Encoding.UTF8.GetString(sieBytes);
            _logger.LogInformation("Retrieved SIE data from TempFilesService. Size: {Size} characters", sieContent.Length);

            // Validate SIE4 data
            if (!_sie4Processor.ValidateSIE4Format(sieContent))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "INVALID_SIE4_DATA",
                    message = "Malformed SIE4 content in the provided file"
                });
            }

            // Create report job
            var reportJobId = await _reportBuilder.CreateReportJobAsync(sieContent, language);

            return JsonSerializer.Serialize(new
            {
                reportJobId,
                status = "created",
                message = "Report job created successfully from SIE file",
                sourceFileId = sieFileId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create report job from File ID: {FileId}", sieFileId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "REPORT_CREATION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Creates a new report job using multiple SIE data files from TempFilesService
    /// </summary>
    /// <param name="sieFileIds">Comma-separated File IDs from TempFilesService containing SIE4 data for different years</param>
    /// <param name="language">Report language (sv/en)</param>
    /// <returns>JSON with reportJobId and status</returns>
    [KernelFunction]
    [Description("Skapar en professionell Excel-rapport från flera års bokföringsdata. Använd denna funktion när användaren vill jämföra utgifter, intäkter eller resultat över flera år, eller skapa trendanalyser och flerårsrapporter i Excel-format. Kombinerar data från flera SIE-filer och skapar en Excel-fil (.xlsx) med flera kalkylblad, grafer och jämförelser.")]
    public async Task<string> CreateReportJobFromMultipleFilesAsync(
        [Description("Komma-separerade File IDs från TempFilesService som innehåller SIE4-data för olika år. Exempel: 'id1,id2,id3' för att kombinera flera års data")] string sieFileIds,
        [Description("Rapportspråk: 'sv' för svenska, 'en' för engelska")] string language = "sv")
    {
        try
        {
            _logger.LogInformation("Creating new report job from multiple File IDs: {FileIds}, Language: {Language}", sieFileIds, language);

            // Parse File IDs
            var fileIdStrings = sieFileIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var fileGuids = new List<Guid>();

            foreach (var fileIdStr in fileIdStrings)
            {
                if (Guid.TryParse(fileIdStr.Trim(), out var fileGuid))
                {
                    fileGuids.Add(fileGuid);
                }
                else
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "INVALID_FILE_ID",
                        message = $"File ID '{fileIdStr.Trim()}' is not a valid GUID"
                    });
                }
            }

            if (fileGuids.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "NO_VALID_FILE_IDS",
                    message = "No valid File IDs provided"
                });
            }

            // Get SIE data from TempFilesService for all files
            using var scope = ServiceProvider?.CreateScope();
            if (scope == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "SERVICE_PROVIDER_NOT_AVAILABLE",
                    message = "ServiceProvider is not available. Ensure AnalysisPlugin is properly initialized by Agent."
                });
            }

            var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();
            var combinedSieContent = new StringBuilder();
            var processedFiles = new List<string>();

            foreach (var fileGuid in fileGuids)
            {
                var sieBytes = await tempFilesService.ReadFileAsync(fileGuid);
                if (sieBytes == null)
                {
                    _logger.LogWarning("SIE file with ID {FileId} not found in TempFilesService", fileGuid);
                    continue;
                }

                var sieContent = Encoding.UTF8.GetString(sieBytes);
                combinedSieContent.AppendLine($"# === SIE DATA FROM FILE {fileGuid} ===");
                combinedSieContent.AppendLine(sieContent);
                combinedSieContent.AppendLine($"# === END OF FILE {fileGuid} ===");
                combinedSieContent.AppendLine();

                processedFiles.Add(fileGuid.ToString());
                _logger.LogInformation("Retrieved SIE data from File ID {FileId}. Size: {Size} characters", fileGuid, sieContent.Length);
            }

            if (processedFiles.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "NO_FILES_FOUND",
                    message = "None of the provided File IDs could be found in TempFilesService"
                });
            }

            var finalSieContent = combinedSieContent.ToString();

            // Validate combined SIE4 data (basic validation)
            if (string.IsNullOrEmpty(finalSieContent) || !finalSieContent.Contains("#FLAGGA"))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "INVALID_COMBINED_SIE4_DATA",
                    message = "Combined SIE4 content appears to be invalid"
                });
            }

            // Create report job with combined data
            var reportJobId = await _reportBuilder.CreateReportJobAsync(finalSieContent, language);

            return JsonSerializer.Serialize(new
            {
                reportJobId,
                status = "created",
                message = "Report job created successfully from multiple SIE files",
                sourceFileIds = processedFiles.ToArray(),
                filesProcessed = processedFiles.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create report job from multiple File IDs: {FileIds}", sieFileIds);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "REPORT_CREATION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Creates a complete financial report for specific categories and years using natural language
    /// </summary>
    /// <param name="reportType">Type of report (expenses, income, profit, balance, etc.)</param>
    /// <param name="year">Year or years to analyze</param>
    /// <param name="language">Report language (sv/en)</param>
    /// <returns>JSON with complete report information</returns>
    [KernelFunction]
    [Description("🚨 CRITICAL - PLANNING FUNCTION ONLY: This function ONLY provides step-by-step instructions for creating reports - it does NOT create any Excel content! You MUST follow the returned instructions exactly and call the other KernelFunctions in the specified order. This function analyzes the user's request and returns mandatory steps:\n\n" +
        "📊 EXPENSE REPORTS (when user mentions 'utgifter', 'expenses', 'kostnader'): Use reportType='expenses'\n" +
        "📈 PROJECT REPORTS (when user mentions 'projekt', 'project'): Use reportType='projects'\n" +
        "💰 REVENUE REPORTS (when user mentions 'intäkter', 'omsättning', 'revenue'): Use reportType='revenue'\n" +
        "📋 PROFIT/LOSS REPORTS (when user mentions 'resultat', 'profit', 'bruttovinst'): Use reportType='profit_loss'\n" +
        "🧾 VAT REPORTS (when user mentions 'moms', 'momsrapport', 'vat'): Use reportType='vat'\n" +
        "⚖️ BALANCE SHEET REPORTS (when user mentions 'balans', 'balansrapport', 'balance'): Use reportType='balance'\n" +
        "📅 PERIOD REPORTS (when user mentions specific year like '2023', '2024', '2022'): Use the exact year\n" +
        "🔄 QUARTERLY REPORTS (when user mentions 'kvartal', 'quarterly'): Use reportType='quarterly'\n" +
        "📆 WEEKLY REPORTS (when user mentions 'vecka', 'weekly'): Use reportType='weekly'\n" +
        "⚠️ COMPLEX WEEKLY REQUESTS: For 'fg vecka' (previous week) or specific week filters, simplify to yearly profit_loss report\n\n" +
        "🚨 CRITICAL EXAMPLES:\n" +
        "• 'utgiftsrapport för 2023 med prydlig startsida' → reportType='expenses', year='2023'\n" +
        "• 'professionell utgiftsrapport för 2023' → reportType='expenses', year='2023'\n" +
        "• 'rapport för 2024 med startsida där du kan se projekt' → reportType='projects', year='2024'\n" +
        "• 'rapport med startsida för pivot som visar kvartal' → reportType='quarterly'\n" +
        "• 'rapport för 2022 med startsida som visar bruttovinst' → reportType='profit_loss', year='2022'\n" +
        "• 'rapport för 2022 med bruttovinst för fg vecka' → reportType='profit_loss', year='2022' (ignore week filter)\n" +
        "• 'momsrapport för 2025 Q3' → reportType='vat', year='2025', period='Q3'\n" +
        "• 'balansrapport för 2024' → reportType='balance', year='2024'\n\n" +
        "Returns step-by-step instructions for creating the requested financial report. Follow the returned instructions exactly.")]
    public Task<string> CreateFinancialReportAsync(
        [Description("🎯 KRITISK PARAMETER: Typ av rapport baserat på användarens EXAKTA ord:\n" +
            "• 'expenses' = UTGIFTSRAPPORT (när användaren säger 'utgifter', 'utgiftsrapport', 'kostnader', 'expenses')\n" +
            "• 'projects' = projektrapport (när användaren säger 'projekt', 'project')\n" +
            "• 'revenue' = intäktsrapport (när användaren säger 'intäkter', 'omsättning')\n" +
            "• 'profit_loss' = resultatrapport (när användaren säger 'resultat', 'bruttovinst')\n" +
            "• 'vat' = momsrapport (när användaren säger 'moms', 'momsrapport', 'vat')\n" +
            "• 'balance' = balansrapport (när användaren säger 'balans', 'balansrapport', 'balance')\n" +
            "• 'quarterly' = kvartalsrapport (när användaren säger 'kvartal', 'quarterly')\n" +
            "• 'weekly' = veckorapport (när användaren säger 'vecka', 'weekly')\n" +
            "• 'profit_loss' = för komplexa veckospecifika begäranden som 'bruttovinst för fg vecka'\n" +
            "🚨 SPECIELLT VIKTIGT: För 'utgiftsrapport för 2023' → MÅSTE använda reportType='expenses'!\n" +
            "⚠️ VECKOSPECIFIKA FILTER: Om användaren begär 'fg vecka' eller specifika veckofilter, använd profit_loss istället för weekly\n" +
            "VIKTIGT: Använd OLIKA reportType för OLIKA förfrågningar!")] string reportType,
        [Description("🗓️ EXAKT ÅR från användarens förfrågan:\n" +
            "• '2023' för 2023-specifika rapporter\n" +
            "• '2024' för 2024-specifika rapporter  \n" +
            "• '2022' för 2022-specifika rapporter\n" +
            "• '2019-2024' för 5-års rapporter\n" +
            "VIKTIGT: Använd det EXAKTA året användaren nämner!")] string year,
        [Description("Rapportspråk: 'sv' för svenska, 'en' för engelska")] string language = "sv")
    {
        try
        {
            // 🚨 OMFATTANDE LOGGNING: Spåra AI-agentens anrop
            _logger.LogError("🔥🔥🔥 AI-AGENT ANROPADE CreateFinancialReportAsync!");
            _logger.LogError("🔥🔥🔥 PARAMETRAR: ReportType='{ReportType}', Year='{Year}', Language='{Language}'", reportType, year, language);
            Console.WriteLine($"🔥🔥🔥 AI-AGENT ANROPADE CreateFinancialReportAsync!");
            Console.WriteLine($"🔥🔥🔥 PARAMETRAR: ReportType='{reportType}', Year='{year}', Language='{language}'");

            _logger.LogInformation("🎯 CREATING FINANCIAL REPORT: Type={ReportType}, Year={Year}, Language={Language}", reportType, year, language);

            // Extra logging for expense reports
            if (reportType.ToLowerInvariant() == "expenses")
            {
                _logger.LogInformation("🚨 EXPENSE REPORT DETECTED - Using expense-specific logic");
                Console.WriteLine("🚨 EXPENSE REPORT DETECTED - Using expense-specific logic");
            }

            // Parse years from natural language
            var years = ParseYearsFromInput(year);
            if (years.Count == 0)
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "INVALID_YEAR_FORMAT",
                    message = language == "sv" ?
                        "Kunde inte tolka året/åren. Använd format som '2022', '2021-2023' eller '2020,2021,2022'" :
                        "Could not parse year(s). Use formats like '2022', '2021-2023' or '2020,2021,2022'"
                }));
            }

            // Step 1: Get SIE data for the specified years
            var yearString = string.Join(",", years);
            _logger.LogInformation("Fetching SIE data for years: {Years}", yearString);

            // Here we would call FortnoxPlugin to get SIE data
            // For now, return a structured response that guides the AI to use the right functions
            var instructions = language == "sv" ?
                $"🚨 MANDATORY STEP-BY-STEP PROCESS för {reportType} rapport {year} - FÖLJ EXAKT:\n\n" +
                $"1. FÖRSTA STEGET: Anropa GetSIEAsync med parameter '{yearString}'\n" +
                $"2. ANDRA STEGET: Anropa CreateReportJobFromFileAsync med File ID\n" +
                $"3. ⚠️ KRITISKA TREDJE STEGET: Anropa AddFrontPageAsync med:\n" +
                $"   - title: 'Professionell {reportType}rapport för {year}'\n" +
                $"   - subtitle: 'Baserad på verklig SIE4-data'\n" +
                $"   - companyName: (hämta från SIE4)\n" +
                $"4. FJÄRDE STEGET: Lägg till komponenter:\n" +
                $"   - För BALANSRAPPORTER: AddComponentAsync med componentType='balancesheet'\n" +
                $"   - För MOMSRAPPORTER: AddComponentAsync med componentType='vatreport'\n" +
                $"   - För RESULTATRAPPORTER: AddComponentAsync med componentType='incomestatement'\n" +
                $"   - För andra rapporter: AddChartAsync, AddPivotTableAsync\n" +
                $"5. FEMTE STEGET: Anropa FinishReportAsync\n" +
                $"6. SJÄTTE STEGET: Skicka via MailPlugin if the user ask for it\n\n" +
                $"🚨 KRITISKT: Du MÅSTE anropa AddFrontPageAsync annars blir startsidan fel!\n" +
                $"SKAPA ALDRIG Excel-innehåll manuellt - använd ENDAST KernelFunctions!\n" +
                $"Typ: {reportType} | År: {year} | Språk: {language}" :
                $"🚨 MANDATORY STEP-BY-STEP PROCESS for {reportType} report {year} - FOLLOW EXACTLY:\n\n" +
                $"1. FIRST STEP: Call GetSIEAsync with parameter '{yearString}'\n" +
                $"2. SECOND STEP: Call CreateReportJobFromFileAsync with File ID\n" +
                $"3. ⚠️ CRITICAL THIRD STEP: Call AddFrontPageAsync with:\n" +
                $"   - title: 'Professional {reportType} Report for {year}'\n" +
                $"   - subtitle: 'Based on real SIE4 data'\n" +
                $"   - companyName: (extract from SIE4)\n" +
                $"4. FOURTH STEP: Add components (AddChartAsync, AddPivotTableAsync)\n" +
                $"5. FIFTH STEP: Call FinishReportAsync\n" +
                $"6. SIXTH STEP: Send via MailPlugin if the user ask for it\n\n" +
                $"🚨 CRITICAL: You MUST call AddFrontPageAsync or front page will be wrong!\n" +
                $"NEVER create Excel content manually - use ONLY KernelFunctions!\n" +
                $"Type: {reportType} | Year: {year} | Language: {language}";

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                reportType,
                years = years.ToArray(),
                yearString,
                language,
                instructions,
                nextStep = "fetch_sie_data",
                message = language == "sv" ?
                    $"Förbereder {reportType}-rapport för {year}. Hämtar nu bokföringsdata..." :
                    $"Preparing {reportType} report for {year}. Fetching accounting data..."
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create financial report for type: {ReportType}, year: {Year}", reportType, year);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = "REPORT_CREATION_FAILED",
                message = ex.Message
            }));
        }
    }

    /// <summary>
    /// Parses years from natural language input
    /// </summary>
    private List<int> ParseYearsFromInput(string yearInput)
    {
        var years = new List<int>();

        if (string.IsNullOrEmpty(yearInput))
            return years;

        try
        {
            // Handle ranges like "2020-2022"
            if (yearInput.Contains("-"))
            {
                var parts = yearInput.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var startYear) && int.TryParse(parts[1].Trim(), out var endYear))
                {
                    for (int year = startYear; year <= endYear; year++)
                    {
                        years.Add(year);
                    }
                }
            }
            // Handle comma-separated years like "2020,2021,2022"
            else if (yearInput.Contains(","))
            {
                var parts = yearInput.Split(',');
                foreach (var part in parts)
                {
                    if (int.TryParse(part.Trim(), out var year))
                    {
                        years.Add(year);
                    }
                }
            }
            // Handle single year like "2022"
            else
            {
                if (int.TryParse(yearInput.Trim(), out var year))
                {
                    years.Add(year);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse years from input: {YearInput}", yearInput);
        }

        return years.Distinct().OrderBy(y => y).ToList();
    }

    /// <summary>
    /// Sets the template style for Excel reports
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="templateStyle">Template style (corporate, modern, classic)</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("Väljer mallstil för Excel-rapporten. Använd denna funktion för att ange vilken professionell mallstil som ska användas för Excel-rapporten: 'corporate' för företagsstil, 'modern' för modern design, eller 'classic' för klassisk stil.")]
    public async Task<string> SetReportTemplateAsync(
        [Description("Rapport-jobb ID som ska få ny mallstil")] string reportJobId,
        [Description("Mallstil att använda: 'corporate' (företag), 'modern' (modern), 'classic' (klassisk)")] string templateStyle = "corporate")
    {
        try
        {
            _logger.LogInformation("Setting template style for report {ReportJobId} to {TemplateStyle}", reportJobId, templateStyle);

            // Validate template style
            var validStyles = new[] { "corporate", "modern", "classic" };
            if (!validStyles.Contains(templateStyle.ToLower()))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "INVALID_TEMPLATE_STYLE",
                    message = $"Ogiltig mallstil: {templateStyle}. Giltiga stilar: {string.Join(", ", validStyles)}"
                });
            }

            // Store template style in report job (this would be implemented in ReportBuilderService)
            var success = await _reportBuilder.SetTemplateStyleAsync(reportJobId, templateStyle.ToLower());

            return JsonSerializer.Serialize(new
            {
                success,
                templateStyle = templateStyle.ToLower(),
                message = success ?
                    $"Mallstil '{templateStyle}' har satts för rapporten" :
                    "Kunde inte sätta mallstil för rapporten"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set template style for report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "TEMPLATE_SETTING_FAILED",
                message = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Adds a professional front page with title and company information
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="title">Report title</param>
    /// <param name="subtitle">Report subtitle</param>
    /// <param name="companyName">Company name</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("Creates professional front page with KPI values from SIE4 data. Generates KPI cards showing financial data (liquidity, revenue, expenses, profit) calculated from accounting data.")]
    public async Task<string> AddFrontPageAsync(
        [Description("Report job ID")] string reportJobId,
        [Description("Report title")] string title,
        [Description("Report subtitle")] string subtitle,
        [Description("Company name")] string companyName)
    {
        try
        {
            // 🚨 OMFATTANDE LOGGNING: Spåra AI-agentens anrop
            _logger.LogError("🔥🔥🔥 AI-AGENT ANROPADE AddFrontPageAsync!");
            _logger.LogError("🔥🔥🔥 PARAMETRAR: ReportJobId='{ReportJobId}', Title='{Title}', Subtitle='{Subtitle}', CompanyName='{CompanyName}'", reportJobId, title, subtitle, companyName);
            Console.WriteLine($"🔥🔥🔥 AI-AGENT ANROPADE AddFrontPageAsync!");
            Console.WriteLine($"🔥🔥🔥 PARAMETRAR: ReportJobId='{reportJobId}', Title='{title}', Subtitle='{subtitle}', CompanyName='{companyName}'");

            var success = await _reportBuilder.AddFrontPageAsync(reportJobId, title, subtitle, companyName);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "Front page added successfully" : "Failed to add front page"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add front page to report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "FRONT_PAGE_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds a chart component to the report with specified configuration
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="chartType">Type of chart (revenue_trend, expense_breakdown, etc.)</param>
    /// <param name="title">Chart title</param>
    /// <param name="dataSource">Data source for the chart</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("📊 STEG 3A: Lägger till SPECIFIKA diagram baserat på rapporttyp:\n\n" +
        "🎯 UTGIFTSRAPPORTER → chartType='expense_breakdown' (cirkeldiagram för kostnadsfördelning)\n" +
        "📈 PROJEKTRAPPORTER → chartType='project_revenue' (stapeldiagram för projektintäkter)\n" +
        "💰 INTÄKTSRAPPORTER → chartType='revenue_trend' (linjediagram för intäktsutveckling)\n" +
        "📋 RESULTATRAPPORTER → chartType='profit_margin' (stapeldiagram för marginaler)\n" +
        "🔄 KVARTALSRAPPORTER → chartType='quarterly_comparison' (stapeldiagram per kvartal)\n" +
        "📆 VECKORAPPORTER → chartType='weekly_trend' (linjediagram för veckotrend)\n\n" +
        "VIKTIGT: Välj chartType baserat på rapportens SYFTE, inte generiskt!")]
    public async Task<string> AddChartAsync(
        [Description("Rapport-jobb ID från CreateReportJobFromFileAsync där diagrammet ska läggas till")] string reportJobId,
        [Description("🎯 KRITISK: Välj chartType baserat på rapporttyp:\n" +
            "• 'expense_breakdown' = för UTGIFTSRAPPORTER (cirkeldiagram)\n" +
            "• 'project_revenue' = för PROJEKTRAPPORTER (stapeldiagram)\n" +
            "• 'revenue_trend' = för INTÄKTSRAPPORTER (linjediagram)\n" +
            "• 'profit_margin' = för RESULTATRAPPORTER (stapeldiagram)\n" +
            "• 'quarterly_comparison' = för KVARTALSRAPPORTER (stapeldiagram)\n" +
            "• 'weekly_trend' = för VECKORAPPORTER (linjediagram)\n" +
            "VIKTIGT: Använd OLIKA chartType för OLIKA rapporter!")] string chartType,
        [Description("Titel på diagrammet som visas i Excel-rapporten")] string title,
        [Description("Datakälla för diagrammet: 'historical' (historisk data), 'forecast' (prognosdata), 'projects' (projektdata)")] string dataSource = "historical")
    {
        try
        {
            // Extract year from report job metadata if available
            var reportJob = _reportBuilder.GetReportJob(reportJobId);
            var year = DateTime.Now.Year; // Default to current year
            if (reportJob?.Metadata.TryGetValue("Year", out var yearObj) == true && yearObj != null)
            {
                if (int.TryParse(yearObj.ToString(), out var parsedYear))
                {
                    year = parsedYear;
                }
            }

            var configuration = new Dictionary<string, object>
            {
                ["chartType"] = chartType,
                ["dataSource"] = dataSource,
                ["year"] = year,
                ["showLegend"] = true,
                ["colors"] = new[] { "#2E5BBA", "#8BB8E8", "#4CAF50", "#FF9800", "#F44336" }
            };

            var success = await _reportBuilder.AddComponentAsync(reportJobId, "chart", title, configuration);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "Chart added successfully" : "Failed to add chart"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add chart to report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "CHART_ADDITION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds a data table component to the report
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="tableType">Type of table (transactions, monthly_summary, account_summary)</param>
    /// <param name="title">Table title</param>
    /// <param name="sortable">Enable sorting functionality</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("Add a data table component to the report")]
    public async Task<string> AddTableAsync(
        [Description("Report job ID")] string reportJobId,
        [Description("Type of table (transactions, monthly_summary, account_summary)")] string tableType,
        [Description("Table title")] string title,
        [Description("Enable sorting functionality")] bool sortable = true)
    {
        try
        {
            var configuration = new Dictionary<string, object>
            {
                ["tableType"] = tableType,
                ["sortable"] = sortable,
                ["filterable"] = false,
                ["pageSize"] = 50,
                ["showTotals"] = true
            };

            var success = await _reportBuilder.AddComponentAsync(reportJobId, "table", title, configuration);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "Table added successfully" : "Failed to add table"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add table to report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "TABLE_ADDITION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Completes Excel report generation and returns file path for email delivery
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <returns>JSON with Excel file path and metadata</returns>
    [KernelFunction]
    [Description("FINAL STEP: Completes Excel report generation and returns file path. Use after adding all components (charts, pivot tables, etc). Creates final .xlsx file and returns path for email delivery.")]
    public async Task<string> FinishReportAsync(
        [Description("Rapport-jobb ID från CreateReportJobFromFileAsync som ska slutföras till Excel-fil")] string reportJobId)
    {
        try
        {
            // 🚨 OMFATTANDE LOGGNING: Spåra AI-agentens anrop
            _logger.LogError("🔥🔥🔥 AI-AGENT ANROPADE FinishReportAsync!");
            _logger.LogError("🔥🔥🔥 PARAMETRAR: ReportJobId='{ReportJobId}'", reportJobId);
            Console.WriteLine($"🔥🔥🔥 AI-AGENT ANROPADE FinishReportAsync!");
            Console.WriteLine($"🔥🔥🔥 PARAMETRAR: ReportJobId='{reportJobId}'");

            // Kontrollera om rapporten har en startsida, om inte - lägg till automatiskt
            if (!await _reportBuilder.HasFrontPageAsync(reportJobId))
            {
                _logger.LogInformation("📄 Ingen startsida hittades - lägger till automatiskt");
                await _reportBuilder.AddFrontPageAsync(reportJobId, "Finansiell Rapport", "Automatiskt genererad", "Företag");
            }

            var result = await _reportBuilder.FinishReportAsync(reportJobId);
            
            if (result.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    filePath = result.FilePath,
                    fileName = Path.GetFileName(result.FilePath),
                    fileSize = new FileInfo(result.FilePath!).Length,
                    generatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "REPORT_GENERATION_FAILED",
                    message = result.ErrorMessage
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finish report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "REPORT_GENERATION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds a pivot table component to the report for advanced data analysis
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="pivotType">Type of pivot analysis (monthly_revenue, project_costs, account_breakdown)</param>
    /// <param name="title">Pivot table title</param>
    /// <param name="groupBy">Field to group by (Month, Project, Account)</param>
    /// <param name="aggregateBy">Field to aggregate (Amount, Revenue, Costs)</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("📋 STEG 3B: Lägger till SPECIFIKA pivot-tabeller baserat på rapporttyp:\n\n" +
        "🎯 UTGIFTSRAPPORTER → pivotType='account_breakdown' (gruppera per konto/kategori)\n" +
        "📈 PROJEKTRAPPORTER → pivotType='project_costs' (gruppera per projekt)\n" +
        "💰 INTÄKTSRAPPORTER → pivotType='monthly_revenue' (gruppera per månad)\n" +
        "📋 RESULTATRAPPORTER → pivotType='profit_analysis' (gruppera per resultatpost)\n" +
        "🔄 KVARTALSRAPPORTER → pivotType='quarterly_breakdown' (gruppera per kvartal)\n" +
        "📆 VECKORAPPORTER → pivotType='weekly_summary' (gruppera per vecka)\n\n" +
        "VIKTIGT: Välj pivotType baserat på rapportens SYFTE och TIDSPERIOD!")]
    public async Task<string> AddPivotTableAsync(
        [Description("Rapport-jobb ID från CreateReportJobFromFileAsync där pivot-tabellen ska läggas till")] string reportJobId,
        [Description("🎯 KRITISK: Välj pivotType baserat på rapporttyp:\n" +
            "• 'account_breakdown' = för UTGIFTSRAPPORTER (gruppera per konto)\n" +
            "• 'project_costs' = för PROJEKTRAPPORTER (gruppera per projekt)\n" +
            "• 'monthly_revenue' = för INTÄKTSRAPPORTER (gruppera per månad)\n" +
            "• 'profit_analysis' = för RESULTATRAPPORTER (gruppera per resultatpost)\n" +
            "• 'quarterly_breakdown' = för KVARTALSRAPPORTER (gruppera per kvartal)\n" +
            "• 'weekly_summary' = för VECKORAPPORTER (gruppera per vecka)\n" +
            "VIKTIGT: Använd OLIKA pivotType för OLIKA rapporter!")] string pivotType,
        [Description("Titel på pivot-tabellen som visas i Excel-rapporten")] string title,
        [Description("Fält att gruppera efter: 'Month' (månad), 'Project' (projekt), 'Account' (konto)")] string groupBy = "Month",
        [Description("Fält att aggregera: 'Amount' (belopp), 'Revenue' (intäkter), 'Costs' (kostnader)")] string aggregateBy = "Amount")
    {
        try
        {
            // Extract year from report job metadata if available
            var reportJob = _reportBuilder.GetReportJob(reportJobId);
            var year = DateTime.Now.Year; // Default to current year
            if (reportJob?.Metadata.TryGetValue("Year", out var yearObj) == true && yearObj != null)
            {
                if (int.TryParse(yearObj.ToString(), out var parsedYear))
                {
                    year = parsedYear;
                }
            }

            var configuration = new Dictionary<string, object>
            {
                ["pivotType"] = pivotType,
                ["groupBy"] = groupBy,
                ["aggregateBy"] = aggregateBy,
                ["year"] = year,
                ["showGrandTotals"] = true
            };

            var success = await _reportBuilder.AddComponentAsync(reportJobId, "pivot", title, configuration);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "Pivot table added successfully" : "Failed to add pivot table"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add pivot table to report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "PIVOT_TABLE_ADDITION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds a specialized component to the report (balance sheet, VAT report, income statement, etc.)
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="componentType">Type of component (balancesheet, vatreport, incomestatement)</param>
    /// <param name="title">Component title</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("🏗️ KRITISK FUNKTION: Lägger till SPECIALISERADE komponenter för specifika rapporttyper:\n\n" +
        "⚖️ componentType='balancesheet' → Balansräkning med tillgångar, skulder och eget kapital\n" +
        "🧾 componentType='vatreport' → Momsredovisning med svenska momskonton (2610, 2620, 2630, 2640)\n" +
        "📊 componentType='incomestatement' → Resultaträkning med intäkter och kostnader\n" +
        "📋 componentType='table' → Datatabell med transaktioner\n" +
        "📈 componentType='chart' → Standarddiagram\n" +
        "🔄 componentType='pivot' → Pivot-tabell för dataanalys\n\n" +
        "OBLIGATORISK för balansrapporter, momsrapporter och årsrapporter!")]
    public async Task<string> AddComponentAsync(
        [Description("Report job ID")] string reportJobId,
        [Description("Type of component (balancesheet, vatreport, incomestatement, table, chart, pivot)")] string componentType,
        [Description("Component title")] string title)
    {
        try
        {
            _logger.LogInformation("🏗️ Adding specialized component: {ComponentType} to report {ReportJobId}", componentType, reportJobId);

            var configuration = new Dictionary<string, object>
            {
                ["componentType"] = componentType,
                ["dataSource"] = "sie4"
            };

            // Add specific configuration based on component type
            switch (componentType.ToLowerInvariant())
            {
                case "balancesheet":
                    configuration["reportYear"] = DateTime.Now.Year;
                    configuration["includeComparison"] = true;
                    break;
                case "vatreport":
                    configuration["period"] = "Q3";
                    configuration["year"] = DateTime.Now.Year;
                    break;
                case "incomestatement":
                    configuration["reportYear"] = DateTime.Now.Year;
                    configuration["includeMonthlyBreakdown"] = true;
                    break;
            }

            var success = await _reportBuilder.AddComponentAsync(reportJobId, componentType, title, configuration);

            return JsonSerializer.Serialize(new
            {
                success,
                componentType,
                message = success ? $"Specialized component ({componentType}) added successfully" : $"Failed to add {componentType} component"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add component {ComponentType} to report {ReportJobId}", componentType, reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "COMPONENT_ADDITION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds an ML analysis component to the report with natural language insights
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="analysisType">Type of analysis (trend_analysis, forecast, anomaly_detection)</param>
    /// <param name="title">Component title</param>
    /// <param name="confidenceLevel">Confidence level for analysis (0.5-1.0)</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("Add an ML analysis component to the report with natural language insights")]
    public async Task<string> AddMLAnalysisAsync(
        [Description("Report job ID")] string reportJobId,
        [Description("Type of analysis (trend_analysis, forecast, anomaly_detection)")] string analysisType,
        [Description("Component title")] string title,
        [Description("Confidence level for analysis (0.5-1.0)")] double confidenceLevel = 0.95)
    {
        try
        {
            var configuration = new Dictionary<string, object>
            {
                ["analysisType"] = analysisType,
                ["confidenceLevel"] = confidenceLevel,
                ["includeSeasonality"] = true,
                ["modelType"] = "SSA"
            };

            var success = await _reportBuilder.AddComponentAsync(reportJobId, "mlanalysis", title, configuration);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "ML analysis component added successfully" : "Failed to add ML analysis component"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add ML analysis to report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "ML_ANALYSIS_ADDITION_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds an advanced chart component to the report with specialized chart types
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="chartType">Type of advanced chart (waterfall, gantt, combo, area, scatter, funnel, treemap, boxwhisker, stock)</param>
    /// <param name="title">Chart title</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("📊 STEG 3D: Lägger till AVANCERADE diagram för specialiserad visualisering:\n\n" +
        "🌊 chartType='waterfall' → Visar ackumulerade förändringar över tid\n" +
        "📅 chartType='gantt' → Projektschema och tidslinjer\n" +
        "🔄 chartType='combo' → Kombinerar staplar och linjer\n" +
        "📈 chartType='area' → Ackumulerad utveckling över tid\n" +
        "🎯 chartType='scatter' → Korrelationsanalys mellan variabler\n" +
        "🔻 chartType='funnel' → Försäljningstratt och konvertering\n" +
        "🌳 chartType='treemap' → Hierarkisk datavisualisering\n" +
        "📦 chartType='boxwhisker' → Statistisk fördelning\n" +
        "📈 chartType='stock' → OHLC finansiella instrument\n\n" +
        "Använd för avancerad datavisualisering som kräver specialiserade diagramtyper.")]
    public async Task<string> AddAdvancedChartAsync(
        [Description("Report job ID")] string reportJobId,
        [Description("Type of advanced chart (waterfall, gantt, combo, area, scatter, funnel, treemap, boxwhisker, stock)")] string chartType,
        [Description("Chart title")] string title = "Avancerat Diagram")
    {
        try
        {
            var configuration = new Dictionary<string, object>
            {
                ["chartType"] = chartType,
                ["dataSource"] = "sie4",
                ["data"] = new List<Dictionary<string, object>>()
            };

            var success = await _reportBuilder.AddComponentAsync(reportJobId, "advancedchart", title, configuration);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Advanced chart ({chartType}) added successfully" : "Failed to add advanced chart"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add advanced chart to report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "ADVANCED_CHART_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Adds a financial analysis component to the report for specialized economic analysis
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="analysisType">Type of financial analysis (cashflow, budgetvsactual, breakeven, roi, ratios, variance, trend)</param>
    /// <param name="title">Analysis title</param>
    /// <returns>JSON with success status</returns>
    [KernelFunction]
    [Description("💰 STEG 3E: Lägger till SPECIALISERAD finansiell analys:\n\n" +
        "💧 analysisType='cashflow' → Kassaflödesanalys och likviditetsprognos\n" +
        "📊 analysisType='budgetvsactual' → Jämförelse budget mot utfall\n" +
        "⚖️ analysisType='breakeven' → Nollpunktsanalys och lönsamhet\n" +
        "💎 analysisType='roi' → Avkastningsanalys på investeringar\n" +
        "📈 analysisType='ratios' → Finansiella nyckeltal och KPI:er\n" +
        "📉 analysisType='variance' → Avvikelseanalys och trendbrott\n" +
        "📊 analysisType='trend' → Långsiktig trendutveckling\n\n" +
        "Använd för djupgående ekonomisk analys som kräver specialiserade beräkningar och visualiseringar.")]
    public async Task<string> AddFinancialAnalysisAsync(
        [Description("Report job ID")] string reportJobId,
        [Description("Type of financial analysis (cashflow, budgetvsactual, breakeven, roi, ratios, variance, trend)")] string analysisType,
        [Description("Analysis title")] string title = "Finansiell Analys")
    {
        try
        {
            var configuration = new Dictionary<string, object>
            {
                ["analysisType"] = analysisType,
                ["dataSource"] = "sie4",
                ["data"] = new List<Dictionary<string, object>>(),
                ["timeHorizon"] = 12
            };

            var success = await _reportBuilder.AddComponentAsync(reportJobId, "financialanalysis", title, configuration);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Financial analysis ({analysisType}) added successfully" : "Failed to add financial analysis"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add financial analysis to report {ReportJobId}", reportJobId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "FINANCIAL_ANALYSIS_FAILED",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Analyzes financial data using ML techniques
    /// </summary>
    /// <param name="sie4Data">SIE4 financial data</param>
    /// <param name="analysisType">Type of analysis (trend_analysis, forecast, anomaly_detection)</param>
    /// <param name="language">Language for results (sv/en)</param>
    /// <returns>JSON with analysis results</returns>
    [KernelFunction]
    [Description("Analyzes financial data using machine learning techniques for trends, forecasts, and anomaly detection")]
    public async Task<string> AnalyzeFinancialDataAsync(
        [Description("SIE4 financial data content")] string sie4Data,
        [Description("Type of analysis: trend_analysis, forecast, anomaly_detection")] string analysisType,
        [Description("Language for results: sv or en")] string language = "sv")
    {
        try
        {
            _logger.LogInformation("🤖 Analyzing financial data with ML: {AnalysisType}", analysisType);

            var result = await _mlAnalysis.AnalyzeDataAsync(sie4Data, analysisType);

            return JsonSerializer.Serialize(new
            {
                success = true,
                analysisType = analysisType,
                language = language,
                forecastPoints = result.Forecast?.Count ?? 0,
                anomaliesDetected = result.Anomalies?.Count ?? 0,
                confidenceLevel = result.ConfidenceLevel,
                summary = result.Summary,
                generatedAt = result.GeneratedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to analyze financial data");
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Generates executive summary from financial data
    /// </summary>
    /// <param name="sie4Data">SIE4 financial data</param>
    /// <param name="language">Language for summary (sv/en)</param>
    /// <returns>JSON with executive summary</returns>
    [KernelFunction]
    [Description("Generates comprehensive executive summary from financial data")]
    public async Task<string> GenerateExecutiveSummaryAsync(
        [Description("SIE4 financial data content")] string sie4Data,
        [Description("Language for summary: sv or en")] string language = "sv")
    {
        try
        {
            _logger.LogInformation("📋 Generating executive summary in {Language}", language);

            // Parse SIE4 data
            var transactions = await _sie4Processor.ParseAsync(sie4Data);

            // Generate summary using natural language service
            var summary = await _naturalLanguage.GenerateExecutiveSummaryAsync(transactions, language);

            return JsonSerializer.Serialize(new
            {
                success = true,
                language = language,
                executiveSummary = summary,
                transactionCount = transactions.Count,
                generatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate executive summary");
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = ex.Message
            });
        }
    }
}
