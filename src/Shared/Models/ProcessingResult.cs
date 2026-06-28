using System.ComponentModel.DataAnnotations;

namespace Shared.Models;

/// <summary>
/// Represents the result of processing a file
/// </summary>
public class ProcessingResult
{
    /// <summary>
    /// Unique identifier for the processing result
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the FileRecord
    /// </summary>
    [Required]
    public Guid FileId { get; set; }

    /// <summary>
    /// Type of processing performed (e.g., "OCR", "DocumentInterpreter", "AI_Analysis")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ProcessingType { get; set; } = string.Empty;

    /// <summary>
    /// Processing status
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Processing result data as JSON
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace if processing failed
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// When processing started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When processing completed
    /// </summary>
    [Required]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Version of the processor used
    /// </summary>
    [MaxLength(20)]
    public string? ProcessorVersion { get; set; }

    /// <summary>
    /// Additional processing metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Quality score of the processing result (0-100)
    /// </summary>
    public int? QualityScore { get; set; }

    /// <summary>
    /// Confidence level of the processing result (0-100)
    /// </summary>
    public int? ConfidenceLevel { get; set; }
}

/// <summary>
/// Common processing status values
/// </summary>
public static class ProcessingStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Skipped = "Skipped";
}

/// <summary>
/// Common processing types
/// </summary>
public static class ProcessingType
{
    public const string OCR = "OCR";
    public const string DocumentInterpreter = "DocumentInterpreter";
    public const string AIAnalysis = "AI_Analysis";
    public const string TextExtraction = "TextExtraction";
    public const string ImageProcessing = "ImageProcessing";
    public const string PDFProcessing = "PDFProcessing";
}
