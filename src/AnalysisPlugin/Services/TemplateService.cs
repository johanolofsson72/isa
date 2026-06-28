using System.Text.Json;
using Microsoft.Extensions.Logging;
using AnalysisPlugin.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace AnalysisPlugin.Services;

/// <summary>
/// Service for managing report templates
/// </summary>
public class TemplateService
{
    private readonly ILogger<TemplateService> _logger;
    private readonly string _templatesPath;
    private readonly Dictionary<string, ReportTemplate> _templates;

    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger;
        _templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
        _templates = new Dictionary<string, ReportTemplate>();

        LoadTemplates();
    }

    /// <summary>
    /// Gets available template styles
    /// </summary>
    public List<string> GetAvailableStyles()
    {
        return _templates.Keys.ToList();
    }

    /// <summary>
    /// Gets a template by style name
    /// </summary>
    public ReportTemplate? GetTemplate(string style, string type = "excel")
    {
        var key = $"{type}_{style}";
        return _templates.TryGetValue(key, out var template) ? template : null;
    }

    /// <summary>
    /// Gets the default template for a given type
    /// </summary>
    public ReportTemplate GetDefaultTemplate(string type = "excel")
    {
        var defaultTemplate = GetTemplate("corporate", type);
        if (defaultTemplate != null)
            return defaultTemplate;

        // Fallback to built-in template
        return CreateBuiltInTemplate(type);
    }

    /// <summary>
    /// Applies template styling to Excel package
    /// </summary>
    public void ApplyExcelTemplate(ExcelPackage package, ReportTemplate template, string companyName = "")
    {
        try
        {
            _logger.LogInformation("Applying Excel template: {TemplateName}", template.Name);

            foreach (var worksheet in package.Workbook.Worksheets)
            {
                ApplyExcelWorksheetStyling(worksheet, template, companyName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply Excel template: {TemplateName}", template.Name);
        }
    }

    /// <summary>
    /// Creates a styled Excel package from template
    /// </summary>
    public ExcelPackage CreateStyledPackage(ReportTemplate template, string reportTitle, string companyName)
    {
        // 🚨 OMFATTANDE LOGGNING: Spåra CreateStyledPackage
        _logger.LogError("🔥🔥🔥 TemplateService.CreateStyledPackage() KÖRS!");
        _logger.LogError("🔥🔥🔥 PARAMETRAR: ReportTitle='{ReportTitle}', CompanyName='{CompanyName}'", reportTitle, companyName);
        Console.WriteLine($"🔥🔥🔥 TemplateService.CreateStyledPackage() KÖRS!");
        Console.WriteLine($"🔥🔥🔥 PARAMETRAR: ReportTitle='{reportTitle}', CompanyName='{companyName}'");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var package = new ExcelPackage();

        // Create main worksheet
        var worksheet = package.Workbook.Worksheets.Add("Rapport");

        // Apply template styling
        ApplyExcelWorksheetStyling(worksheet, template, companyName);

        // Add header
        AddReportHeader(worksheet, template, reportTitle, companyName);

        return package;
    }

    private void LoadTemplates()
    {
        try
        {
            if (!Directory.Exists(_templatesPath))
            {
                _logger.LogWarning("Templates directory not found: {Path}. Creating built-in templates.", _templatesPath);
                CreateBuiltInTemplates();
                return;
            }

            var templateDirs = Directory.GetDirectories(_templatesPath, "*", SearchOption.AllDirectories);

            foreach (var dir in templateDirs)
            {
                var configFile = Path.Combine(dir, "config.json");
                if (File.Exists(configFile))
                {
                    try
                    {
                        var json = File.ReadAllText(configFile);
                        var template = JsonSerializer.Deserialize<ReportTemplate>(json);

                        if (template != null)
                        {
                            var key = $"{template.Type}_{template.Style}";
                            _templates[key] = template;
                            _logger.LogInformation("Loaded template: {Key}", key);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load template config: {ConfigFile}", configFile);
                    }
                }
            }

            if (_templates.Count == 0)
            {
                _logger.LogWarning("No templates loaded. Creating built-in templates.");
                CreateBuiltInTemplates();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load templates");
            CreateBuiltInTemplates();
        }
    }

    private void CreateBuiltInTemplates()
    {
        // Corporate Excel template
        var corporateExcel = CreateBuiltInTemplate("excel", "corporate");
        _templates["excel_corporate"] = corporateExcel;

        // Modern Excel template
        var modernExcel = CreateBuiltInTemplate("excel", "modern");
        _templates["excel_modern"] = modernExcel;

        // Classic Excel template
        var classicExcel = CreateBuiltInTemplate("excel", "classic");
        _templates["excel_classic"] = classicExcel;

        _logger.LogInformation("Created {Count} built-in templates", _templates.Count);
    }

    private ReportTemplate CreateBuiltInTemplate(string type, string style = "corporate")
    {
        var template = new ReportTemplate
        {
            Name = $"{char.ToUpper(style[0])}{style[1..]} {type.ToUpper()} Template",
            Description = $"Professional {style} style template for {type} reports",
            Type = type,
            Style = style
        };

        // Configure colors based on style
        template.Colors = style switch
        {
            "corporate" => new TemplateColors
            {
                Primary = "#1f4e79",      // Professional blue
                Secondary = "#70ad47",    // Corporate green
                Accent = "#ffc000",       // Gold accent
                Background = "#ffffff",
                Text = "#333333",
                HeaderBackground = "#f2f2f2",
                BorderColor = "#d0d0d0"
            },
            "modern" => new TemplateColors
            {
                Primary = "#2e86ab",      // Modern blue
                Secondary = "#a23b72",    // Modern purple
                Accent = "#f18f01",       // Orange accent
                Background = "#ffffff",
                Text = "#2c3e50",
                HeaderBackground = "#ecf0f1",
                BorderColor = "#bdc3c7"
            },
            _ => new TemplateColors()  // Classic/default
        };

        return template;
    }

    private void ApplyExcelWorksheetStyling(ExcelWorksheet worksheet, ReportTemplate template, string companyName)
    {
        try
        {
            // Set default font
            worksheet.Cells.Style.Font.Name = template.Fonts.Body;
            worksheet.Cells.Style.Font.Size = template.Fonts.BodySize;
            worksheet.Cells.Style.Font.Color.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Text));

            // Set background
            worksheet.Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells.Style.Fill.BackgroundColor.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Background));

            // Configure column widths for better layout
            worksheet.Column(1).Width = 20;  // Labels
            worksheet.Column(2).Width = 15;  // Values
            worksheet.Column(3).Width = 15;  // Additional data
            worksheet.Column(4).Width = 20;  // Comments/notes

            _logger.LogDebug("Applied worksheet styling for template: {TemplateName}", template.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply worksheet styling");
        }
    }

    private void AddReportHeader(ExcelWorksheet worksheet, ReportTemplate template, string reportTitle, string companyName)
    {
        try
        {
            // Company name (row 1)
            if (!string.IsNullOrEmpty(companyName))
            {
                worksheet.Cells[1, 1].Value = companyName;
                worksheet.Cells[1, 1].Style.Font.Size = template.Fonts.HeadingSize + 2;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Primary));
            }

            // Report title (row 2)
            worksheet.Cells[2, 1].Value = reportTitle;
            worksheet.Cells[2, 1].Style.Font.Size = template.Fonts.HeadingSize;
            worksheet.Cells[2, 1].Style.Font.Bold = true;
            worksheet.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Secondary));

            // Date (row 3)
            worksheet.Cells[3, 1].Value = $"Genererad: {DateTime.Now:yyyy-MM-dd HH:mm}";
            worksheet.Cells[3, 1].Style.Font.Size = template.Fonts.BodySize - 1;
            worksheet.Cells[3, 1].Style.Font.Color.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Text));

            // Add separator line (row 4)
            var headerRange = worksheet.Cells[4, 1, 4, 10];
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
            headerRange.Style.Border.Bottom.Color.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Primary));

            _logger.LogDebug("Added report header: {Title}", reportTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add report header");
        }
    }

    /// <summary>
    /// Applies professional table styling
    /// </summary>
    public void ApplyTableStyling(ExcelRange tableRange, ReportTemplate template, bool hasHeaders = true)
    {
        try
        {
            if (hasHeaders)
            {
                // Style header row
                var headerRow = tableRange.Worksheet.Cells[tableRange.Start.Row, tableRange.Start.Column, tableRange.Start.Row, tableRange.End.Column];
                headerRow.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRow.Style.Fill.BackgroundColor.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Primary));
                headerRow.Style.Font.Color.SetColor(System.Drawing.Color.White);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Font.Size = template.Fonts.BodySize;
            }

            // Style data rows with alternating colors
            for (int row = tableRange.Start.Row + (hasHeaders ? 1 : 0); row <= tableRange.End.Row; row++)
            {
                var dataRow = tableRange.Worksheet.Cells[row, tableRange.Start.Column, row, tableRange.End.Column];

                if ((row - tableRange.Start.Row) % 2 == 0)
                {
                    dataRow.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    dataRow.Style.Fill.BackgroundColor.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.HeaderBackground));
                }

                dataRow.Style.Font.Size = template.Fonts.DataSize;
                dataRow.Style.Font.Color.SetColor(System.Drawing.ColorTranslator.FromHtml(template.Colors.Text));
            }

            // Add borders
            tableRange.Style.Border.BorderAround(ExcelBorderStyle.Medium, System.Drawing.ColorTranslator.FromHtml(template.Colors.BorderColor));
            tableRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            tableRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            tableRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            tableRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            _logger.LogDebug("Applied table styling to range: {Range}", tableRange.Address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply table styling");
        }
    }
}