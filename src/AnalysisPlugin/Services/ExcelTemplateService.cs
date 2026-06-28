using OfficeOpenXml;
using Microsoft.Extensions.Logging;
using AnalysisPlugin.Models;

namespace AnalysisPlugin.Services;

/// <summary>
/// Service for managing Excel-based report templates
/// Handles loading and applying pre-designed Excel templates for specific report types
/// </summary>
public class ExcelTemplateService
{
    private readonly ILogger<ExcelTemplateService> _logger;
    private readonly string _templatesPath;
    private readonly Dictionary<string, string> _templateFiles;

    public ExcelTemplateService(ILogger<ExcelTemplateService> logger)
    {
        _logger = logger;
        _templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Excel");
        _templateFiles = new Dictionary<string, string>();
        
        LoadTemplateFiles();
    }

    /// <summary>
    /// Gets available Excel template types
    /// </summary>
    public List<string> GetAvailableTemplateTypes()
    {
        return _templateFiles.Keys.ToList();
    }

    /// <summary>
    /// Creates an Excel package from a template file
    /// </summary>
    public ExcelPackage? CreateFromTemplate(string templateType, string reportTitle, string companyName = "")
    {
        try
        {
            if (!_templateFiles.TryGetValue(templateType.ToLowerInvariant(), out var templatePath))
            {
                _logger.LogWarning("Template type {TemplateType} not found", templateType);
                return null;
            }

            if (!File.Exists(templatePath))
            {
                _logger.LogError("Template file not found: {TemplatePath}", templatePath);
                return null;
            }

            _logger.LogInformation("Loading Excel template: {TemplateType} from {TemplatePath}", templateType, templatePath);

            // Load template file
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var templateBytes = File.ReadAllBytes(templatePath);
            var package = new ExcelPackage(new MemoryStream(templateBytes));

            // Customize template with report-specific data
            CustomizeTemplate(package, reportTitle, companyName);

            return package;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Excel package from template {TemplateType}", templateType);
            return null;
        }
    }

    /// <summary>
    /// Checks if a template exists for the given report type
    /// </summary>
    public bool HasTemplate(string reportType)
    {
        return _templateFiles.ContainsKey(reportType.ToLowerInvariant());
    }

    /// <summary>
    /// Gets the template file path for a report type
    /// </summary>
    public string? GetTemplatePath(string reportType)
    {
        return _templateFiles.TryGetValue(reportType.ToLowerInvariant(), out var path) ? path : null;
    }

    private void LoadTemplateFiles()
    {
        try
        {
            if (!Directory.Exists(_templatesPath))
            {
                _logger.LogWarning("Excel templates directory not found: {Path}", _templatesPath);
                return;
            }

            // Map specific template files to report types
            var templateMappings = new Dictionary<string, string>
            {
                { "momsrapport", "Momsrapport.xlsx" },
                { "vat", "Momsrapport.xlsx" },
                { "moms", "Momsrapport.xlsx" },
                { "balansrapport", "Balansrapport.xlsx" },
                { "balance", "Balansrapport.xlsx" },
                { "balans", "Balansrapport.xlsx" },
                { "resultatrapport", "Balansrapport.xlsx" }, // Can use balance template for income statement
                { "income", "Balansrapport.xlsx" },
                { "resultat", "Balansrapport.xlsx" }
            };

            foreach (var mapping in templateMappings)
            {
                var filePath = Path.Combine(_templatesPath, mapping.Value);
                if (File.Exists(filePath))
                {
                    _templateFiles[mapping.Key] = filePath;
                    _logger.LogInformation("Loaded Excel template: {ReportType} -> {FilePath}", mapping.Key, filePath);
                }
                else
                {
                    _logger.LogWarning("Template file not found: {FilePath}", filePath);
                }
            }

            _logger.LogInformation("Loaded {Count} Excel template mappings", _templateFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Excel template files");
        }
    }

    private void CustomizeTemplate(ExcelPackage package, string reportTitle, string companyName)
    {
        try
        {
            // Customize each worksheet in the template
            foreach (var worksheet in package.Workbook.Worksheets)
            {
                CustomizeWorksheet(worksheet, reportTitle, companyName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to customize template with title: {ReportTitle}", reportTitle);
        }
    }

    private void CustomizeWorksheet(ExcelWorksheet worksheet, string reportTitle, string companyName)
    {
        // Look for placeholder cells and replace them
        if (worksheet.Dimension != null)
        {
            for (int row = 1; row <= Math.Min(worksheet.Dimension.Rows, 20); row++)
            {
                for (int col = 1; col <= Math.Min(worksheet.Dimension.Columns, 10); col++)
                {
                    var cell = worksheet.Cells[row, col];
                    var value = cell.Value?.ToString() ?? "";

                    // Replace common placeholders
                    if (value.Contains("{REPORT_TITLE}"))
                    {
                        cell.Value = value.Replace("{REPORT_TITLE}", reportTitle);
                    }
                    if (value.Contains("{COMPANY_NAME}"))
                    {
                        cell.Value = value.Replace("{COMPANY_NAME}", companyName);
                    }
                    if (value.Contains("{DATE}"))
                    {
                        cell.Value = value.Replace("{DATE}", DateTime.Now.ToString("yyyy-MM-dd"));
                    }
                    if (value.Contains("{GENERATED_DATE}"))
                    {
                        cell.Value = value.Replace("{GENERATED_DATE}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    }
                }
            }
        }
    }
}
