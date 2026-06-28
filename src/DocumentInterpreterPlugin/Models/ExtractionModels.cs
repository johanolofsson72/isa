using System;
using System.Collections.Generic;

namespace DocumentInterpreter.Models
{
    /// <summary>
    /// Represents the result of a single extraction technique
    /// </summary>
    public class ExtractionResult
    {
        /// <summary>
        /// Name of the extraction technique used
        /// </summary>
        public string TechniqueName { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score of this extraction (0.0 to 1.0)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Raw extracted text
        /// </summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Whether the extraction was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if extraction failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Extracted structured data
        /// </summary>
        public ExtractedData ExtractedData { get; set; } = new();

        /// <summary>
        /// Additional metadata from the extraction technique
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Structured data extracted from document
    /// </summary>
    public class ExtractedData
    {
        /// <summary>
        /// Extracted amounts with confidence scores
        /// </summary>
        public List<ExtractedAmount> Amounts { get; set; } = new();

        /// <summary>
        /// Extracted dates with confidence scores
        /// </summary>
        public List<ExtractedDate> Dates { get; set; } = new();

        /// <summary>
        /// Extracted text fields with confidence scores
        /// </summary>
        public List<ExtractedField> Fields { get; set; } = new();

        /// <summary>
        /// Detected document type
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// Document type confidence
        /// </summary>
        public double DocumentTypeConfidence { get; set; }
    }

    /// <summary>
    /// Extracted monetary amount with metadata
    /// </summary>
    public class ExtractedAmount
    {
        /// <summary>
        /// The extracted amount value
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Currency code
        /// </summary>
        public string Currency { get; set; } = "SEK";

        /// <summary>
        /// Type of amount (Total, Net, VAT, Shipping, etc.)
        /// </summary>
        public AmountType Type { get; set; }

        /// <summary>
        /// Confidence score for this amount
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Position in document where amount was found
        /// </summary>
        public DocumentPosition Position { get; set; } = new();

        /// <summary>
        /// Raw text that was parsed to extract this amount
        /// </summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// Context around the amount (nearby text)
        /// </summary>
        public string Context { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of monetary amounts
    /// </summary>
    public enum AmountType
    {
        Unknown,
        Total,
        Net,
        VAT,
        VAT6,
        VAT12,
        VAT25,
        Shipping,
        Fee,
        Discount,
        Interest,
        Penalty
    }

    /// <summary>
    /// Extracted date with metadata
    /// </summary>
    public class ExtractedDate
    {
        /// <summary>
        /// The extracted date value
        /// </summary>
        public DateTime Value { get; set; }

        /// <summary>
        /// Type of date (Invoice, Due, Order, etc.)
        /// </summary>
        public DateType Type { get; set; }

        /// <summary>
        /// Confidence score for this date
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Position in document where date was found
        /// </summary>
        public DocumentPosition Position { get; set; } = new();

        /// <summary>
        /// Raw text that was parsed to extract this date
        /// </summary>
        public string RawText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of dates
    /// </summary>
    public enum DateType
    {
        Unknown,
        Invoice,
        Due,
        Order,
        Delivery,
        Payment,
        Created,
        Modified
    }

    /// <summary>
    /// Extracted text field with metadata
    /// </summary>
    public class ExtractedField
    {
        /// <summary>
        /// Field name/type
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Extracted value
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this field
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Position in document where field was found
        /// </summary>
        public DocumentPosition Position { get; set; } = new();
    }

    /// <summary>
    /// Position information within a document
    /// </summary>
    public class DocumentPosition
    {
        /// <summary>
        /// Page number (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Line number within page (1-based)
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Character position within line (0-based)
        /// </summary>
        public int Character { get; set; }

        /// <summary>
        /// Bounding box coordinates (for image-based extraction)
        /// </summary>
        public BoundingBox? BoundingBox { get; set; }
    }

    /// <summary>
    /// Bounding box coordinates
    /// </summary>
    public class BoundingBox
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>
    /// Consolidated result from multiple extraction techniques
    /// </summary>
    public class ConsolidatedExtractionResult
    {
        /// <summary>
        /// Individual results from each technique
        /// </summary>
        public List<ExtractionResult> TechniqueResults { get; set; } = new();

        /// <summary>
        /// Final consolidated extracted data
        /// </summary>
        public ExtractedData ConsolidatedData { get; set; } = new();

        /// <summary>
        /// Overall confidence score
        /// </summary>
        public double OverallConfidence { get; set; }

        /// <summary>
        /// Fusion decisions made during consolidation
        /// </summary>
        public List<FusionDecision> FusionDecisions { get; set; } = new();

        /// <summary>
        /// Total processing time for all techniques
        /// </summary>
        public long TotalProcessingTimeMs { get; set; }

        /// <summary>
        /// Quality metrics for the extraction
        /// </summary>
        public ExtractionQualityMetrics QualityMetrics { get; set; } = new();
    }

    /// <summary>
    /// Represents a decision made during result fusion
    /// </summary>
    public class FusionDecision
    {
        /// <summary>
        /// Type of data being fused
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Techniques that provided conflicting results
        /// </summary>
        public List<string> ConflictingTechniques { get; set; } = new();

        /// <summary>
        /// Chosen technique and reason
        /// </summary>
        public string ChosenTechnique { get; set; } = string.Empty;

        /// <summary>
        /// Reason for the choice
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Confidence in the fusion decision
        /// </summary>
        public double DecisionConfidence { get; set; }
    }

    /// <summary>
    /// Quality metrics for extraction results
    /// </summary>
    public class ExtractionQualityMetrics
    {
        /// <summary>
        /// Number of techniques that succeeded
        /// </summary>
        public int SuccessfulTechniques { get; set; }

        /// <summary>
        /// Number of techniques that failed
        /// </summary>
        public int FailedTechniques { get; set; }

        /// <summary>
        /// Cross-validation score (how well techniques agree)
        /// </summary>
        public double CrossValidationScore { get; set; }

        /// <summary>
        /// Data completeness score
        /// </summary>
        public double CompletenessScore { get; set; }

        /// <summary>
        /// Consistency score across techniques
        /// </summary>
        public double ConsistencyScore { get; set; }
    }
}
