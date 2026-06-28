using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AnalysisPlugin.Configuration;
using AnalysisPlugin.Models;
using AnalysisPlugin.Components;

namespace AnalysisPlugin.Services;

/// <summary>
/// Core report building orchestration service with concurrent job management and TTL cleanup
/// </summary>
public class ReportBuilderService
{
    private readonly AnalysisPluginOptions _options;
    private readonly ILogger<ReportBuilderService> _logger;
    private readonly ConcurrentDictionary<string, ReportJob> _activeJobs = new();
    private readonly Timer _cleanupTimer;
    private readonly ExcelGeneratorService _excelGenerator;
    private readonly ComponentFactory _componentFactory;

    public ReportBuilderService(AnalysisPluginOptions options, ILogger<ReportBuilderService> logger)
    {
        _options = options;
        _logger = logger;

        // Initialize services (in production these would be injected)
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var templateService = new TemplateService(loggerFactory.CreateLogger<TemplateService>());
        _excelGenerator = new ExcelGeneratorService(loggerFactory.CreateLogger<ExcelGeneratorService>(), templateService);
        _componentFactory = new ComponentFactory(loggerFactory.CreateLogger<ComponentFactory>());

        // Setup cleanup timer to run every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredJobs, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Creates a new report job
    /// </summary>
    /// <param name="sie4Data">SIE4 financial data</param>
    /// <param name="language">Report language</param>
    /// <returns>Report job ID</returns>
    public async Task<string> CreateReportJobAsync(string sie4Data, string language = "sv")
    {
        var jobId = Guid.NewGuid().ToString("N")[..8];

        // Extract year from SIE4 data
        var year = ExtractYearFromSIE4(sie4Data);

        var job = new ReportJob
        {
            Id = jobId,
            CreatedAt = DateTime.UtcNow,
            SIE4Data = sie4Data,
            Language = language,
            Status = ReportJobStatus.Created,
            Metadata = new Dictionary<string, object>
            {
                ["Year"] = year,
                ["ExtractedAt"] = DateTime.UtcNow
            }
        };

        _activeJobs.TryAdd(jobId, job);
        _logger.LogInformation("Created report job {JobId} for year {Year}", jobId, year);

        return await Task.FromResult(jobId);
    }

    /// <summary>
    /// Gets a report job by ID
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <returns>Report job or null if not found</returns>
    public ReportJob? GetReportJob(string reportJobId)
    {
        _activeJobs.TryGetValue(reportJobId, out var job);
        return job;
    }

    /// <summary>
    /// Adds a front page component to the report
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="title">Report title</param>
    /// <param name="subtitle">Report subtitle</param>
    /// <param name="companyName">Company name</param>
    /// <returns>True if successful</returns>
    public async Task<bool> AddFrontPageAsync(string reportJobId, string title, string subtitle, string companyName)
    {
        // 🚨 EXTREMT TYDLIG DEBUG-LOGGNING
        _logger.LogError("🔥🔥🔥 AddFrontPageAsync() KÖRS! JobId: {JobId}, Title: {Title}", reportJobId, title);
        Console.WriteLine($"🔥🔥🔥 AddFrontPageAsync() KÖRS! JobId: {reportJobId}, Title: {title}");

        if (!_activeJobs.TryGetValue(reportJobId, out var job))
        {
            _logger.LogWarning("Report job {JobId} not found", reportJobId);
            return false;
        }

        // Create front page component using the professional implementation
        var frontPageComponent = new FrontPageComponent
        {
            Title = title,
            Configuration = new Dictionary<string, object>
            {
                ["subtitle"] = subtitle,
                ["companyName"] = companyName,
                ["reportDate"] = DateTime.Now.ToString("yyyy-MM-dd")
            }
        };

        // 🔥 KRITISK FIX: Sätt SIE4-data på komponenten så den kan använda riktig finansiell data
        frontPageComponent.SIE4Data = job.SIE4Data;

        // Convert to ReportComponent for job storage
        var reportComponent = new ReportComponent
        {
            Type = frontPageComponent.ComponentType,
            Title = frontPageComponent.Title,
            Configuration = frontPageComponent.Configuration
        };

        job.Components.Add(reportComponent);
        job.TransitionTo(ReportJobStatus.InProgress);

        _logger.LogInformation("Added front page to report job {JobId}", reportJobId);
        return await Task.FromResult(true);
    }

    public async Task<bool> HasFrontPageAsync(string reportJobId)
    {
        if (!_activeJobs.TryGetValue(reportJobId, out var job))
        {
            _logger.LogError("🔥 HasFrontPageAsync: Job {JobId} not found", reportJobId);
            return false;
        }

        _logger.LogError("🔥 HasFrontPageAsync: Job {JobId} has {Count} components", reportJobId, job.Components.Count);
        foreach (var component in job.Components)
        {
            _logger.LogError("🔥 Component: Type='{Type}', ComponentType='{ComponentType}', Title='{Title}'",
                component.Type, component.ComponentType, component.Title);
        }

        var hasFrontPage = job.Components.Any(c => c.ComponentType == "FrontPage");
        _logger.LogError("🔥 HasFrontPageAsync: Job {JobId} has front page: {HasFrontPage}", reportJobId, hasFrontPage);

        return await Task.FromResult(hasFrontPage);
    }

    /// <summary>
    /// Adds a component to the report using ComponentFactory
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <param name="componentType">Component type</param>
    /// <param name="title">Component title</param>
    /// <param name="configuration">Component configuration</param>
    /// <returns>True if successful</returns>
    public async Task<bool> AddComponentAsync(string reportJobId, string componentType, string title, Dictionary<string, object> configuration)
    {
        if (!_activeJobs.TryGetValue(reportJobId, out var job))
        {
            _logger.LogWarning("Report job {JobId} not found", reportJobId);
            return false;
        }

        var component = _componentFactory.CreateComponent(componentType, title, configuration);
        if (component == null)
        {
            _logger.LogWarning("Failed to create component of type {ComponentType}", componentType);
            return false;
        }

        // 🔥 KRITISK FIX: Sätt SIE4-data på ALLA komponenter så de kan använda riktig finansiell data
        if (component is ReportComponentBase baseComponent)
        {
            baseComponent.SIE4Data = job.SIE4Data;
            _logger.LogInformation("Set SIE4 data on {ComponentType} component", componentType);
        }

        // Convert IReportComponent to ReportComponent for job storage
        var reportComponent = new ReportComponent
        {
            Type = component.ComponentType,
            Title = component.Title,
            Configuration = component.Configuration
        };

        job.Components.Add(reportComponent);
        job.TransitionTo(ReportJobStatus.InProgress);

        _logger.LogInformation("Added {ComponentType} component to report job {JobId}", componentType, reportJobId);
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Finishes report generation and returns file path
    /// </summary>
    /// <param name="reportJobId">Report job ID</param>
    /// <returns>Report generation result</returns>
    public async Task<ReportGenerationResult> FinishReportAsync(string reportJobId)
    {
        // 🚨 DEBUG: Logga att FinishReportAsync körs
        _logger.LogError("🔥🔥🔥 FinishReportAsync() KÖRS! JobId: {JobId}", reportJobId);
        Console.WriteLine($"🔥🔥🔥 FinishReportAsync() KÖRS! JobId: {reportJobId}");

        if (!_activeJobs.TryGetValue(reportJobId, out var job))
        {
            return new ReportGenerationResult
            {
                Success = false,
                ErrorMessage = "Report job not found"
            };
        }

        try
        {
            // 🚨 UNIKA FILNAMN: Använd REPORT_TYPE från miljövariabel för att skapa unika filnamn
            var reportType = Environment.GetEnvironmentVariable("REPORT_TYPE") ?? "financial_report";
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{reportType}_{timestamp}.xlsx";
            var filePath = Path.Combine(_options.ReportArchiveDirectory, fileName);

            _logger.LogError("🔥🔥🔥 SKAPAR UNIK RAPPORT: {FileName} (ReportType: {ReportType})", fileName, reportType);
            Console.WriteLine($"🔥🔥🔥 SKAPAR UNIK RAPPORT: {fileName} (ReportType: {reportType})");
            
            // Ensure directory exists
            Directory.CreateDirectory(_options.ReportArchiveDirectory);
            
            // Generate Excel report using ExcelGeneratorService
            // Get template style from job metadata
            var templateStyle = job.Metadata.TryGetValue("TemplateStyle", out var style) ? style?.ToString() ?? "corporate" : "corporate";

            // Generate Excel file with template
            var success = await _excelGenerator.GenerateReportAsync(job, filePath, templateStyle);
            if (!success)
            {
                throw new InvalidOperationException("Failed to generate Excel report");
            }

            // 🚨 KRITISK VALIDATOR: Validera och fixa rapporten innan den markeras som klar
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var validator = new ReportValidator(loggerFactory.CreateLogger<ReportValidator>());
            var validationResult = await validator.ValidateAndFixReportAsync(filePath, job.SIE4Data);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("⚠️ RAPPORT VALIDERING MISSLYCKADES: {Issues}", string.Join(", ", validationResult.Issues));
                // Fortsätt ändå men logga problemen
            }

            if (validationResult.Fixes.Any())
            {
                _logger.LogInformation("✅ RAPPORT FIXAD: {Fixes}", string.Join(", ", validationResult.Fixes));
            }

            // 🚨 AFFÄRSKRITISK VALIDERING: Obligatorisk validering innan rapport kan användas
            await RunMandatoryValidation(filePath, job.SIE4Data);

            job.GeneratedFilePath = filePath;
            job.TransitionTo(ReportJobStatus.Completed);
            
            _logger.LogInformation("Completed report job {JobId}, file: {FilePath}", reportJobId, filePath);
            
            return new ReportGenerationResult
            {
                Success = true,
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finish report job {JobId}", reportJobId);
            job.TransitionTo(ReportJobStatus.Failed);
            job.ErrorMessage = ex.Message;
            
            return new ReportGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Sets the template style for a report job
    /// </summary>
    public async Task<bool> SetTemplateStyleAsync(string reportJobId, string templateStyle)
    {
        if (!_activeJobs.TryGetValue(reportJobId, out var job))
        {
            _logger.LogWarning("Report job not found: {ReportJobId}", reportJobId);
            return false;
        }

        // Store template style in job metadata
        job.Metadata["TemplateStyle"] = templateStyle;
        _logger.LogInformation("Set template style '{TemplateStyle}' for report job {ReportJobId}", templateStyle, reportJobId);

        return await Task.FromResult(true);
    }

    private async Task CreateStubExcelFile(string filePath, ReportJob job)
    {
        // Stub implementation - create a simple text file for now
        var content = $"Financial Report\nGenerated: {DateTime.UtcNow}\nJob ID: {job.Id}\nComponents: {job.Components.Count}";
        await File.WriteAllTextAsync(filePath.Replace(".xlsx", ".txt"), content);
    }

    private void CleanupExpiredJobs(object? state)
    {
        var expiredJobs = _activeJobs.Where(kvp =>
            kvp.Value.IsExpired).ToList(); // Use 1 hour TTL

        foreach (var kvp in expiredJobs)
        {
            var jobId = kvp.Key;
            var job = kvp.Value;
            job.TransitionTo(ReportJobStatus.Expired);
            _activeJobs.TryRemove(jobId, out _);
            _logger.LogInformation("Cleaned up expired report job {JobId}", jobId);
        }
    }

    /// <summary>
    /// Extracts the year from SIE4 data by looking at the #OMFATTN (period) field
    /// </summary>
    private int ExtractYearFromSIE4(string sie4Data)
    {
        try
        {
            var lines = sie4Data.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Look for #OMFATTN line which contains the period (e.g., #OMFATTN 20230101 20231231)
            var omfattnLine = lines.FirstOrDefault(line => line.Trim().StartsWith("#OMFATTN"));
            if (omfattnLine != null)
            {
                var parts = omfattnLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var startDate = parts[1];
                    if (startDate.Length >= 4 && int.TryParse(startDate[..4], out var year))
                    {
                        return year;
                    }
                }
            }

            // Fallback: look for #GEN line which contains generation date
            var genLine = lines.FirstOrDefault(line => line.Trim().StartsWith("#GEN"));
            if (genLine != null)
            {
                var parts = genLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var genDate = parts[1];
                    if (genDate.Length >= 4 && int.TryParse(genDate[..4], out var year))
                    {
                        return year;
                    }
                }
            }

            // Final fallback: current year
            return DateTime.Now.Year;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract year from SIE4 data, using current year");
            return DateTime.Now.Year;
        }
    }

    private async Task RunMandatoryValidation(string filePath, string sie4Data)
    {
        try
        {
            _logger.LogError("🚨🚨🚨 KÖR AFFÄRSKRITISK VALIDERING i ReportBuilderService för: {FilePath}", filePath);

            // Skapa validator med dependencies
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            var sie4Processor = new SIE4ProcessorService(loggerFactory.CreateLogger<SIE4ProcessorService>());
            var validator = new MandatoryReportValidator(
                loggerFactory.CreateLogger<MandatoryReportValidator>(),
                sie4Processor);

            // Kör obligatorisk validering
            var validationResult = await validator.ValidateReportAsync(filePath, sie4Data);

            if (!validationResult.IsValid)
            {
                // 🚨 BLOCKERANDE FEL: Kasta exception för att stoppa processen
                var errorMessage = $"AFFÄRSKRITISK VALIDERING MISSLYCKADES: {string.Join("; ", validationResult.ValidationErrors)}";
                _logger.LogError("🚨🚨🚨 {ErrorMessage}", errorMessage);

                // Flytta rapporten till karantän
                var quarantinePath = filePath.Replace(".xlsx", "_INVALID_DO_NOT_USE.xlsx");
                if (File.Exists(filePath))
                {
                    File.Move(filePath, quarantinePath);
                }

                throw new InvalidOperationException($"RAPPORTEN ÄR BLOCKERAD: {errorMessage}");
            }
            else
            {
                _logger.LogError("✅ AFFÄRSKRITISK VALIDERING GODKÄND i ReportBuilderService");
            }
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw validation failures
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kritiskt fel under affärskritisk validering i ReportBuilderService");
            throw new InvalidOperationException($"Kritiskt fel under validering: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Result of report generation
/// </summary>
public class ReportGenerationResult
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
}


