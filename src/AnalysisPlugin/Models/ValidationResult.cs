namespace AnalysisPlugin.Models;

/// <summary>
/// Result of a validation operation with detailed feedback
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// List of validation issues found
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// List of fixes that were applied
    /// </summary>
    public List<string> Fixes { get; set; } = new();

    /// <summary>
    /// Additional metadata about the validation
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <returns>ValidationResult indicating success</returns>
    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result with issues
    /// </summary>
    /// <param name="issues">List of validation issues</param>
    /// <returns>ValidationResult indicating failure</returns>
    public static ValidationResult Failure(params string[] issues)
    {
        return new ValidationResult 
        { 
            IsValid = false, 
            Issues = issues.ToList() 
        };
    }

    /// <summary>
    /// Creates a validation result with fixes applied
    /// </summary>
    /// <param name="fixes">List of fixes that were applied</param>
    /// <returns>ValidationResult with fixes</returns>
    public static ValidationResult WithFixes(params string[] fixes)
    {
        return new ValidationResult 
        { 
            IsValid = true, 
            Fixes = fixes.ToList() 
        };
    }
}
