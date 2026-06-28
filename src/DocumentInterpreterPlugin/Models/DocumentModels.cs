using System.ComponentModel;

namespace DocumentInterpreter.Models;

/// <summary>
/// Base class for all financial documents with common interpretation data
/// </summary>
public abstract class DocumentBase
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Type of document (SupplierInvoice, Receipt, etc.)
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Source of the document (email, file path, etc.)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Original filename or identifier from source
    /// </summary>
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>
    /// Quality metrics for the interpretation
    /// </summary>
    public QualityMetrics QualityMetrics { get; set; } = new();

    /// <summary>
    /// When the document was processed
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Raw extracted text from the document
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score of the interpretation (0-1)
    /// </summary>
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Quality metrics for document interpretation
/// </summary>
public class QualityMetrics
{
    /// <summary>
    /// Overall confidence score (0-1)
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Text extraction quality (0-1)
    /// </summary>
    public double TextQuality { get; set; }

    /// <summary>
    /// Field extraction accuracy (0-1)
    /// </summary>
    public double FieldAccuracy { get; set; }

    /// <summary>
    /// Whether all required fields were found
    /// </summary>
    public bool RequiredFieldsComplete { get; set; }

    /// <summary>
    /// List of any validation errors or warnings
    /// </summary>
    public List<string> ValidationMessages { get; set; } = new();
}

/// <summary>
/// Common monetary amounts and totals
/// </summary>
public class TotalAmounts
{
    /// <summary>
    /// Net amount (excluding VAT)
    /// </summary>
    public decimal NetAmount { get; set; }

    /// <summary>
    /// VAT/Tax amount
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// Fee amount
    /// </summary>
    public decimal FeeAmount { get; set; }

    /// <summary>
    /// Shipping amount
    /// </summary>
    public decimal ShippingAmount { get; set; }

    /// <summary>
    /// Gross total amount (including VAT)
    /// </summary>
    public decimal GrossAmount { get; set; }

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string Currency { get; set; } = "SEK";
}

/// <summary>
/// VAT breakdown by rate
/// </summary>
public class VatBreakdown
{
    /// <summary>
    /// VAT rate as percentage (e.g., 25.0 for 25%)
    /// </summary>
    public decimal VatRate { get; set; }

    /// <summary>
    /// Net amount for this VAT rate
    /// </summary>
    public decimal NetAmount { get; set; }

    /// <summary>
    /// VAT amount for this rate
    /// </summary>
    public decimal VatAmount { get; set; }
}

/// <summary>
/// Payment information for invoices
/// </summary>
public class PaymentInfo
{
    /// <summary>
    /// Bankgiro number
    /// </summary>
    public string? BankGiro { get; set; }

    /// <summary>
    /// Plusgiro number
    /// </summary>
    public string? PlusGiro { get; set; }

    /// <summary>
    /// IBAN account number
    /// </summary>
    public string? Iban { get; set; }

    /// <summary>
    /// BIC/SWIFT code
    /// </summary>
    public string? Bic { get; set; }

    /// <summary>
    /// OCR reference number
    /// </summary>
    public string? OcrNumber { get; set; }

    /// <summary>
    /// Payment method (bank transfer, card, cash, etc.)
    /// </summary>
    public string? PaymentMethod { get; set; }
}

/// <summary>
/// Contact/company information
/// </summary>
public class ContactInfo
{
    /// <summary>
    /// Company or person name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Organization number (Swedish: organisationsnummer)
    /// </summary>
    public string? OrganizationNumber { get; set; }

    /// <summary>
    /// VAT registration number
    /// </summary>
    public string? VatNumber { get; set; }

    /// <summary>
    /// Full address
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    public string? Phone { get; set; }
}

/// <summary>
/// Line item for invoices
/// </summary>
public class InvoiceLine
{
    /// <summary>
    /// Description of the item/service
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Quantity
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit price (excluding VAT)
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// VAT rate for this line (percentage)
    /// </summary>
    public decimal VatRate { get; set; }

    /// <summary>
    /// Total amount for this line (excluding VAT)
    /// </summary>
    public decimal NetAmount { get; set; }

    /// <summary>
    /// VAT amount for this line
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Product code or SKU
    /// </summary>
    public string? ProductCode { get; set; }
}