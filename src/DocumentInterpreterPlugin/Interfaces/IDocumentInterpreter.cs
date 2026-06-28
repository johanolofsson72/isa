using DocumentInterpreter.Models;

namespace DocumentInterpreter.Interfaces;

/// <summary>
/// Interface for document interpretation services
/// </summary>
public interface IDocumentInterpreter
{
    /// <summary>
    /// Interprets a document from raw text content
    /// </summary>
    /// <param name="content">Raw text content or file path</param>
    /// <param name="documentType">Expected document type (optional)</param>
    /// <param name="source">Source information (email, file, etc.)</param>
    /// <returns>Interpreted document object</returns>
    Task<DocumentBase> InterpretDocumentAsync(string content, string? documentType = null, string? source = null);

    /// <summary>
    /// Interprets a document from binary content (PDF, image, etc.)
    /// </summary>
    /// <param name="content">Binary content</param>
    /// <param name="contentType">MIME type of the content</param>
    /// <param name="filename">Original filename</param>
    /// <param name="documentType">Expected document type (optional)</param>
    /// <param name="source">Source information</param>
    /// <returns>Interpreted document object</returns>
    Task<DocumentBase> InterpretDocumentAsync(byte[] content, string contentType, string filename, string? documentType = null, string? source = null);

    /// <summary>
    /// Detects the document type from content
    /// </summary>
    /// <param name="content">Document content</param>
    /// <returns>Detected document type and confidence</returns>
    Task<(string DocumentType, double Confidence)> DetectDocumentTypeAsync(string content);

    /// <summary>
    /// Validates an interpreted document
    /// </summary>
    /// <param name="document">Document to validate</param>
    /// <returns>Validation results</returns>
    Task<ValidationResult> ValidateDocumentAsync(DocumentBase document);

    /// <summary>
    /// Gets supported document types
    /// </summary>
    /// <returns>List of supported document types</returns>
    IEnumerable<string> GetSupportedDocumentTypes();
}

/// <summary>
/// Document interpretation result with quality metrics
/// </summary>
public class InterpretationResult
{
    /// <summary>
    /// The interpreted document
    /// </summary>
    public DocumentBase Document { get; set; } = null!;

    /// <summary>
    /// Quality metrics for this interpretation
    /// </summary>
    public QualityMetrics QualityMetrics { get; set; } = new();

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Any warnings or issues found during interpretation
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Document validation result
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the document passed validation
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Overall validation score (0-1)
    /// </summary>
    public double ValidationScore { get; set; }
}

/// <summary>
/// Configuration for document interpretation
/// </summary>
public class DocumentInterpreterConfig
{
    /// <summary>
    /// Minimum confidence threshold (0-1)
    /// </summary>
    public double MinConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// Whether to enable verbose logging
    /// </summary>
    public bool VerboseLogging { get; set; }

    /// <summary>
    /// Timeout for operations in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}