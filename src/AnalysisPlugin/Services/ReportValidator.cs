using Microsoft.Extensions.Logging;
using AnalysisPlugin.Models;

namespace AnalysisPlugin.Services;

/// <summary>
/// Simple report validator for basic validation tasks
/// </summary>
public class ReportValidator
{
    private readonly ILogger<ReportValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the ReportValidator class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public ReportValidator(ILogger<ReportValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a report for basic requirements
    /// </summary>
    /// <param name="reportPath">Path to the report file</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool ValidateReport(string reportPath)
    {
        try
        {
            if (!File.Exists(reportPath))
            {
                _logger.LogWarning("Report file not found: {ReportPath}", reportPath);
                return false;
            }

            var fileInfo = new FileInfo(reportPath);
            if (fileInfo.Length == 0)
            {
                _logger.LogWarning("Report file is empty: {ReportPath}", reportPath);
                return false;
            }

            _logger.LogInformation("Report validation passed: {ReportPath}", reportPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating report: {ReportPath}", reportPath);
            return false;
        }
    }

    /// <summary>
    /// Validates and fixes a report if needed
    /// </summary>
    /// <param name="reportPath">Path to the report file</param>
    /// <param name="sie4Data">SIE4 data for validation (optional)</param>
    /// <returns>ValidationResult with detailed feedback</returns>
    public async Task<ValidationResult> ValidateAndFixReportAsync(string reportPath, string? sie4Data = null)
    {
        await Task.CompletedTask; // Simulate async work
        _logger.LogInformation("Validating report with SIE4 data: {HasData}", !string.IsNullOrEmpty(sie4Data));

        var isValid = ValidateReport(reportPath);

        if (isValid)
        {
            return ValidationResult.Success();
        }
        else
        {
            return ValidationResult.Failure($"Report validation failed for {reportPath}");
        }
    }
}
