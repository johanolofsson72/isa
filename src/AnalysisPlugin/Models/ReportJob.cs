using OfficeOpenXml;

namespace AnalysisPlugin.Models;

/// <summary>
/// Simplified report job model for in-memory report processing
/// </summary>
public class ReportJob
{
    /// <summary>
    /// Unique identifier for the report job
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// SIE4 data content for the report
    /// </summary>
    public string SIE4Data { get; set; } = string.Empty;

    /// <summary>
    /// Report language (sv/en)
    /// </summary>
    public string Language { get; set; } = "sv";

    /// <summary>
    /// Report components to include
    /// </summary>
    public List<ReportComponent> Components { get; set; } = new();

    /// <summary>
    /// Report metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Template style for the report
    /// </summary>
    public string TemplateStyle { get; set; } = "corporate";

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current status of the report job
    /// </summary>
    public ReportJobStatus Status { get; set; } = ReportJobStatus.Created;

    /// <summary>
    /// Generated file path when report is completed
    /// </summary>
    public string? GeneratedFilePath { get; set; }

    /// <summary>
    /// Error message if report generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Transitions the job to a new status
    /// </summary>
    /// <param name="newStatus">New status to transition to</param>
    public void TransitionTo(ReportJobStatus newStatus)
    {
        Status = newStatus;
    }

    /// <summary>
    /// Checks if the report job has expired (older than 1 hour)
    /// </summary>
    public bool IsExpired => DateTime.UtcNow - CreatedAt > TimeSpan.FromHours(1);
}

/// <summary>
/// Report component definition that implements IReportComponent
/// </summary>
public class ReportComponent : IReportComponent
{
    /// <summary>
    /// Component type (frontpage, chart, pivot, table, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Component type alias for compatibility
    /// </summary>
    public string ComponentType => Type;

    /// <summary>
    /// Component title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Component configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Renders the component to an Excel worksheet (stub implementation)
    /// </summary>
    /// <param name="worksheet">Target Excel worksheet</param>
    /// <param name="startRow">Starting row for rendering</param>
    /// <returns>Result of the rendering operation</returns>
    public virtual Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        // Basic stub implementation
        worksheet.Cells[startRow, 1].Value = Title;
        worksheet.Cells[startRow + 1, 1].Value = $"Type: {ComponentType}";

        return Task.FromResult(ComponentResult.CreateSuccess(3));
    }
}
