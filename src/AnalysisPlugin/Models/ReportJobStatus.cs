namespace AnalysisPlugin.Models;

/// <summary>
/// Status enumeration for report jobs
/// </summary>
public enum ReportJobStatus
{
    /// <summary>
    /// Report job has been created but not started
    /// </summary>
    Created = 0,

    /// <summary>
    /// Report job is currently being processed
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Report job has completed successfully
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Report job failed with an error
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Report job was cancelled
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Report job is currently in progress
    /// </summary>
    InProgress = 5,

    /// <summary>
    /// Report job has expired
    /// </summary>
    Expired = 6
}
