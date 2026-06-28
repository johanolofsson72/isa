using DocumentInterpreter.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentInterpreter.Interfaces
{
    /// <summary>
    /// Interface for document extraction techniques
    /// </summary>
    public interface IDocumentExtractionTechnique
    {
        /// <summary>
        /// Name of the extraction technique
        /// </summary>
        string TechniqueName { get; }

        /// <summary>
        /// Supported file formats
        /// </summary>
        string[] SupportedFormats { get; }

        /// <summary>
        /// Priority of this technique (higher = more preferred)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Whether this technique is available/configured
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Extract text and structured data from a document
        /// </summary>
        /// <param name="filePath">Path to the document file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extraction result</returns>
        Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract text and structured data from a document stream
        /// </summary>
        /// <param name="stream">Document stream</param>
        /// <param name="fileName">Original file name for format detection</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extraction result</returns>
        Task<ExtractionResult> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if this technique can handle the given file format
        /// </summary>
        /// <param name="fileName">File name with extension</param>
        /// <returns>True if supported</returns>
        bool CanHandle(string fileName);

        /// <summary>
        /// Get estimated processing time for a file
        /// </summary>
        /// <param name="fileSizeBytes">File size in bytes</param>
        /// <returns>Estimated processing time in milliseconds</returns>
        long EstimateProcessingTime(long fileSizeBytes);
    }

    /// <summary>
    /// Interface for the main extraction orchestrator
    /// </summary>
    public interface IDocumentExtractionOrchestrator
    {
        /// <summary>
        /// Extract using all available techniques in parallel
        /// </summary>
        /// <param name="filePath">Path to the document file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Consolidated extraction result</returns>
        Task<ConsolidatedExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract using all available techniques in parallel from stream
        /// </summary>
        /// <param name="stream">Document stream</param>
        /// <param name="fileName">Original file name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Consolidated extraction result</returns>
        Task<ConsolidatedExtractionResult> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Register an extraction technique
        /// </summary>
        /// <param name="technique">Technique to register</param>
        void RegisterTechnique(IDocumentExtractionTechnique technique);

        /// <summary>
        /// Get all registered techniques
        /// </summary>
        /// <returns>List of registered techniques</returns>
        IDocumentExtractionTechnique[] GetRegisteredTechniques();
    }

    /// <summary>
    /// Interface for result fusion algorithm
    /// </summary>
    public interface IExtractionResultFusion
    {
        /// <summary>
        /// Fuse multiple extraction results into a single consolidated result
        /// </summary>
        /// <param name="results">Results from different techniques</param>
        /// <returns>Consolidated result</returns>
        ConsolidatedExtractionResult FuseResults(ExtractionResult[] results);

        /// <summary>
        /// Calculate cross-validation score between results
        /// </summary>
        /// <param name="results">Results to compare</param>
        /// <returns>Cross-validation score (0.0 to 1.0)</returns>
        double CalculateCrossValidationScore(ExtractionResult[] results);

        /// <summary>
        /// Detect and resolve conflicts between extraction results
        /// </summary>
        /// <param name="results">Results with potential conflicts</param>
        /// <returns>Fusion decisions made</returns>
        FusionDecision[] ResolveConflicts(ExtractionResult[] results);
    }

    /// <summary>
    /// Interface for Swedish number format handling
    /// </summary>
    public interface ISwedishNumberParser
    {
        /// <summary>
        /// Parse Swedish formatted number (space thousands separator, comma decimal)
        /// </summary>
        /// <param name="text">Text containing the number</param>
        /// <returns>Parsed decimal value or null if parsing failed</returns>
        decimal? ParseSwedishNumber(string text);

        /// <summary>
        /// Extract all Swedish formatted numbers from text
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <returns>List of extracted amounts with positions</returns>
        ExtractedAmount[] ExtractSwedishAmounts(string text);

        /// <summary>
        /// Normalize Swedish number format to standard decimal
        /// </summary>
        /// <param name="swedishNumber">Swedish formatted number</param>
        /// <returns>Normalized decimal string</returns>
        string NormalizeSwedishNumber(string swedishNumber);

        /// <summary>
        /// Detect currency in text context
        /// </summary>
        /// <param name="context">Text context around amount</param>
        /// <returns>Detected currency code</returns>
        string DetectCurrency(string context);
    }

    /// <summary>
    /// Interface for document type detection
    /// </summary>
    public interface IDocumentTypeDetector
    {
        /// <summary>
        /// Detect document type from extracted text
        /// </summary>
        /// <param name="text">Extracted text</param>
        /// <returns>Detected document type and confidence</returns>
        (string DocumentType, double Confidence) DetectDocumentType(string text);

        /// <summary>
        /// Get extraction technique priorities for a document type
        /// </summary>
        /// <param name="documentType">Document type</param>
        /// <returns>Dictionary of technique names and their priorities</returns>
        Dictionary<string, int> GetTechniquePriorities(string documentType);

        /// <summary>
        /// Check if document is likely to be an invoice
        /// </summary>
        /// <param name="text">Document text</param>
        /// <returns>True if likely an invoice</returns>
        bool IsLikelyInvoice(string text);

        /// <summary>
        /// Check if document is likely to be a receipt
        /// </summary>
        /// <param name="text">Document text</param>
        /// <returns>True if likely a receipt</returns>
        bool IsLikelyReceipt(string text);
    }
}
