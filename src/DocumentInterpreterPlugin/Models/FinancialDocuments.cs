namespace DocumentInterpreter.Models;

/// <summary>
/// Supplier invoice (inbound invoice) - invoice received from a supplier
/// </summary>
public class SupplierInvoice : DocumentBase
{
    /// <summary>
    /// Seller information (the supplier)
    /// </summary>
    public ContactInfo Seller { get; set; } = new();

    /// <summary>
    /// Invoice number from supplier
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Date when invoice was issued
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Due date for payment
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// VAT breakdown by rate
    /// </summary>
    public List<VatBreakdown> VatBreakdown { get; set; } = new();

    /// <summary>
    /// Payment information
    /// </summary>
    public PaymentInfo PaymentInfo { get; set; } = new();

    /// <summary>
    /// Invoice line items
    /// </summary>
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Purchase order reference, if any
    /// </summary>
    public string? PurchaseOrderReference { get; set; }

    /// <summary>
    /// Delivery date, if specified
    /// </summary>
    public DateTime? DeliveryDate { get; set; }
}

/// <summary>
/// Customer invoice (outbound invoice) - invoice sent to a customer
/// </summary>
public class CustomerInvoice : DocumentBase
{
    /// <summary>
    /// Customer information
    /// </summary>
    public ContactInfo Customer { get; set; } = new();

    /// <summary>
    /// Invoice number
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Date when invoice was issued
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Due date for payment
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Invoice line items
    /// </summary>
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Customer reference, if any
    /// </summary>
    public string? CustomerReference { get; set; }

    /// <summary>
    /// Order reference, if any
    /// </summary>
    public string? OrderReference { get; set; }
}

/// <summary>
/// Credit note - negative invoice for returns/adjustments
/// </summary>
public class CreditNote : DocumentBase
{
    /// <summary>
    /// Reference to original invoice
    /// </summary>
    public OriginalInvoiceReference OriginalInvoice { get; set; } = new();

    /// <summary>
    /// Credit note number
    /// </summary>
    public string CreditNoteNumber { get; set; } = string.Empty;

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Total amounts (typically negative)
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Reason for credit note
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Customer or supplier information
    /// </summary>
    public ContactInfo Contact { get; set; } = new();

    /// <summary>
    /// Credit note line items
    /// </summary>
    public List<InvoiceLine> Lines { get; set; } = new();
}

/// <summary>
/// Reference to original invoice for credit notes
/// </summary>
public class OriginalInvoiceReference
{
    /// <summary>
    /// Original invoice number
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Original invoice date
    /// </summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>
    /// Original invoice amount
    /// </summary>
    public decimal InvoiceAmount { get; set; }
}

/// <summary>
/// Receipt - proof of purchase from retail transactions
/// </summary>
public class Receipt : DocumentBase
{
    /// <summary>
    /// Purchase date and time
    /// </summary>
    public DateTime? PurchaseDateTime { get; set; }

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// VAT breakdown by rate
    /// </summary>
    public List<VatBreakdown> VatBreakdown { get; set; } = new();

    /// <summary>
    /// Payment method used
    /// </summary>
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Merchant/store information
    /// </summary>
    public ContactInfo Merchant { get; set; } = new();

    /// <summary>
    /// Receipt number or transaction ID
    /// </summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>
    /// Store location or branch
    /// </summary>
    public string? StoreLocation { get; set; }

    /// <summary>
    /// Receipt line items
    /// </summary>
    public List<ReceiptLine> Lines { get; set; } = new();
}

/// <summary>
/// Receipt line item
/// </summary>
public class ReceiptLine
{
    /// <summary>
    /// Item description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Quantity purchased
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit price including VAT
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Total price for this line including VAT
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// VAT rate as percentage
    /// </summary>
    public decimal VatRate { get; set; }

    /// <summary>
    /// Product code or barcode
    /// </summary>
    public string? ProductCode { get; set; }
}

/// <summary>
/// Payment advice or settlement report
/// </summary>
public class PaymentAdvice : DocumentBase
{
    /// <summary>
    /// Payment date
    /// </summary>
    public DateTime? PaymentDate { get; set; }

    /// <summary>
    /// Total payment amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public string Currency { get; set; } = "SEK";

    /// <summary>
    /// References to paid invoices
    /// </summary>
    public List<PaymentReference> References { get; set; } = new();

    /// <summary>
    /// Fees deducted
    /// </summary>
    public List<Fee> Fees { get; set; } = new();

    /// <summary>
    /// Taxes deducted
    /// </summary>
    public List<Tax> Taxes { get; set; } = new();

    /// <summary>
    /// Payment method or source
    /// </summary>
    public string? PaymentMethod { get; set; }
}

/// <summary>
/// Reference to a paid invoice
/// </summary>
public class PaymentReference
{
    /// <summary>
    /// Invoice number
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Invoice date
    /// </summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>
    /// Amount paid for this invoice
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Any discount applied
    /// </summary>
    public decimal? Discount { get; set; }
}

/// <summary>
/// Fee deduction
/// </summary>
public class Fee
{
    /// <summary>
    /// Fee description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Fee amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Fee type
    /// </summary>
    public string? FeeType { get; set; }
}

/// <summary>
/// Tax deduction
/// </summary>
public class Tax
{
    /// <summary>
    /// Tax description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tax amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Tax rate as percentage
    /// </summary>
    public decimal? TaxRate { get; set; }

    /// <summary>
    /// Tax type
    /// </summary>
    public string? TaxType { get; set; }
}

/// <summary>
/// Offert (Quote/Estimate) - Swedish business quote document
/// </summary>
public class Quote : DocumentBase
{
    /// <summary>
    /// Customer information  
    /// </summary>
    public ContactInfo Customer { get; set; } = new();

    /// <summary>
    /// Quote number
    /// </summary>
    public string QuoteNumber { get; set; } = string.Empty;

    /// <summary>
    /// Quote issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Quote expiry/valid until date
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Quote line items
    /// </summary>
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Quote status (pending, accepted, declined, expired)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Terms and conditions
    /// </summary>
    public string? Terms { get; set; }
}

/// <summary>
/// Orderbekräftelse (Order Confirmation) - Swedish order confirmation document
/// </summary>
public class OrderConfirmation : DocumentBase
{
    /// <summary>
    /// Customer information
    /// </summary>
    public ContactInfo Customer { get; set; } = new();

    /// <summary>
    /// Order number
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Order confirmation date
    /// </summary>
    public DateTime? ConfirmationDate { get; set; }

    /// <summary>
    /// Expected delivery date
    /// </summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>
    /// Order line items
    /// </summary>
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Customer reference/order reference
    /// </summary>
    public string? CustomerReference { get; set; }

    /// <summary>
    /// Delivery address if different from customer address
    /// </summary>
    public string? DeliveryAddress { get; set; }
}

/// <summary>
/// Följesedel (Delivery Note/Packing Slip) - Swedish delivery document
/// </summary>
public class DeliveryNote : DocumentBase
{
    /// <summary>
    /// Customer information
    /// </summary>
    public ContactInfo Customer { get; set; } = new();

    /// <summary>
    /// Delivery note number
    /// </summary>
    public string DeliveryNoteNumber { get; set; } = string.Empty;

    /// <summary>
    /// Delivery date
    /// </summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>
    /// Related order number
    /// </summary>
    public string? OrderNumber { get; set; }

    /// <summary>
    /// Delivered items
    /// </summary>
    public List<DeliveryLine> Lines { get; set; } = new();

    /// <summary>
    /// Delivery address
    /// </summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>
    /// Carrier/shipping method
    /// </summary>
    public string? Carrier { get; set; }

    /// <summary>
    /// Tracking number
    /// </summary>
    public string? TrackingNumber { get; set; }
}

/// <summary>
/// Delivery line item for följesedel
/// </summary>
public class DeliveryLine
{
    /// <summary>
    /// Item description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Quantity delivered
    /// </summary>
    public decimal QuantityDelivered { get; set; }

    /// <summary>
    /// Quantity ordered (for comparison)
    /// </summary>
    public decimal? QuantityOrdered { get; set; }

    /// <summary>
    /// Product code or item number
    /// </summary>
    public string? ProductCode { get; set; }

    /// <summary>
    /// Unit of measure
    /// </summary>
    public string? Unit { get; set; }
}

/// <summary>
/// Kontantkvitto (Cash Receipt) - Swedish cash transaction receipt
/// </summary>
public class CashReceipt : DocumentBase
{
    /// <summary>
    /// Purchase date and time
    /// </summary>
    public DateTime? PurchaseDateTime { get; set; }

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// VAT breakdown by rate
    /// </summary>
    public List<VatBreakdown> VatBreakdown { get; set; } = new();

    /// <summary>
    /// Always cash for this document type
    /// </summary>
    public string PaymentMethod { get; set; } = "Kontant";

    /// <summary>
    /// Merchant/store information
    /// </summary>
    public ContactInfo Merchant { get; set; } = new();

    /// <summary>
    /// Receipt number or transaction ID
    /// </summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>
    /// Store location or branch
    /// </summary>
    public string? StoreLocation { get; set; }

    /// <summary>
    /// Receipt line items
    /// </summary>
    public List<ReceiptLine> Lines { get; set; } = new();

    /// <summary>
    /// Cash given by customer
    /// </summary>
    public decimal? CashGiven { get; set; }

    /// <summary>
    /// Change given to customer
    /// </summary>
    public decimal? ChangeGiven { get; set; }
}

/// <summary>
/// Räntefaktura (Interest Invoice) - Swedish interest invoice for late payments
/// </summary>
public class InterestInvoice : DocumentBase
{
    /// <summary>
    /// Customer information
    /// </summary>
    public ContactInfo Customer { get; set; } = new();

    /// <summary>
    /// Interest invoice number
    /// </summary>
    public string InterestInvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Due date
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Reference to original overdue invoice(s)
    /// </summary>
    public List<OverdueInvoiceReference> OverdueInvoices { get; set; } = new();

    /// <summary>
    /// Interest rate applied
    /// </summary>
    public decimal InterestRate { get; set; }

    /// <summary>
    /// Interest period (days)
    /// </summary>
    public int InterestPeriodDays { get; set; }

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Administrative fee for interest invoice
    /// </summary>
    public decimal? AdministrativeFee { get; set; }
}

/// <summary>
/// Reference to overdue invoice for interest calculation
/// </summary>
public class OverdueInvoiceReference
{
    /// <summary>
    /// Original invoice number
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Original invoice date
    /// </summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>
    /// Original due date
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Outstanding amount
    /// </summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>
    /// Days overdue
    /// </summary>
    public int DaysOverdue { get; set; }

    /// <summary>
    /// Interest amount calculated for this invoice
    /// </summary>
    public decimal InterestAmount { get; set; }
}

/// <summary>
/// Beställning (Purchase Order) - Swedish purchase order document
/// </summary>
public class PurchaseOrder : DocumentBase
{
    /// <summary>
    /// Supplier information
    /// </summary>
    public ContactInfo Supplier { get; set; } = new();

    /// <summary>
    /// Purchase order number
    /// </summary>
    public string PurchaseOrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Order date
    /// </summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>
    /// Requested delivery date
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; set; }

    /// <summary>
    /// Order line items
    /// </summary>
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Delivery address if different from company address
    /// </summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>
    /// Purchase order status
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Contact person for the order
    /// </summary>
    public string? ContactPerson { get; set; }
}

/// <summary>
/// Självfaktura (Self-billing Invoice) - Swedish self-billing invoice
/// </summary>
public class SelfBillingInvoice : DocumentBase
{
    /// <summary>
    /// Supplier information (who would normally issue the invoice)
    /// </summary>
    public ContactInfo Supplier { get; set; } = new();

    /// <summary>
    /// Self-billing invoice number
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Due date for payment
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Invoice line items
    /// </summary>
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Reference to the agreement allowing self-billing
    /// </summary>
    public string? SelfBillingAgreementRef { get; set; }

    /// <summary>
    /// Original delivery/service reference
    /// </summary>
    public string? ServiceReference { get; set; }
}

/// <summary>
/// Projektfaktura (Project Invoice) - Swedish project-based invoice
/// </summary>
public class ProjectInvoice : DocumentBase
{
    /// <summary>
    /// Customer information
    /// </summary>
    public ContactInfo Customer { get; set; } = new();

    /// <summary>
    /// Project invoice number
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Issue date
    /// </summary>
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Due date
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Project information
    /// </summary>
    public ProjectInfo Project { get; set; } = new();

    /// <summary>
    /// Invoice line items with project references
    /// </summary>
    public List<ProjectInvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Total amounts
    /// </summary>
    public TotalAmounts Totals { get; set; } = new();

    /// <summary>
    /// Time period covered by this invoice
    /// </summary>
    public DatePeriod? InvoicePeriod { get; set; }
}

/// <summary>
/// Project information for project invoices
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// Project number or ID
    /// </summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>
    /// Project name
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Project manager or responsible person
    /// </summary>
    public string? ProjectManager { get; set; }

    /// <summary>
    /// Project start date
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Project end date (if applicable)
    /// </summary>
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Project invoice line with time tracking
/// </summary>
public class ProjectInvoiceLine : InvoiceLine
{
    /// <summary>
    /// Hours worked (for time-based billing)
    /// </summary>
    public decimal? HoursWorked { get; set; }

    /// <summary>
    /// Hourly rate (for time-based billing)
    /// </summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>
    /// Task or activity description
    /// </summary>
    public string? TaskDescription { get; set; }

    /// <summary>
    /// Work performed date
    /// </summary>
    public DateTime? WorkDate { get; set; }

    /// <summary>
    /// Employee or consultant who performed the work
    /// </summary>
    public string? Worker { get; set; }
}

/// <summary>
/// Date period helper class
/// </summary>
public class DatePeriod
{
    /// <summary>
    /// Period start date
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Period end date
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Period description (e.g., "January 2024", "Q1 2024")
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Lönespecifikation (Payslip) - Swedish payroll document
/// </summary>
public class Payslip : DocumentBase
{
    /// <summary>
    /// Employee information
    /// </summary>
    public EmployeeInfo Employee { get; set; } = new();

    /// <summary>
    /// Employer information
    /// </summary>
    public ContactInfo Employer { get; set; } = new();

    /// <summary>
    /// Pay period
    /// </summary>
    public DatePeriod PayPeriod { get; set; } = new();

    /// <summary>
    /// Payslip number
    /// </summary>
    public string? PayslipNumber { get; set; }

    /// <summary>
    /// Pay date
    /// </summary>
    public DateTime? PayDate { get; set; }

    /// <summary>
    /// Gross salary
    /// </summary>
    public decimal GrossSalary { get; set; }

    /// <summary>
    /// Net salary (after deductions)
    /// </summary>
    public decimal NetSalary { get; set; }

    /// <summary>
    /// Tax deductions
    /// </summary>
    public decimal TaxDeductions { get; set; }

    /// <summary>
    /// Social security contributions
    /// </summary>
    public decimal SocialSecurityContributions { get; set; }

    /// <summary>
    /// Pension contributions
    /// </summary>
    public decimal? PensionContributions { get; set; }

    /// <summary>
    /// Other deductions (insurance, union fees, etc.)
    /// </summary>
    public List<PayrollDeduction> OtherDeductions { get; set; } = new();

    /// <summary>
    /// Worked hours for the period
    /// </summary>
    public decimal? WorkedHours { get; set; }

    /// <summary>
    /// Currency (typically SEK for Swedish payslips)
    /// </summary>
    public string Currency { get; set; } = "SEK";
}

/// <summary>
/// Employee information for payslips
/// </summary>
public class EmployeeInfo
{
    /// <summary>
    /// Employee name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Employee ID or personnel number
    /// </summary>
    public string? EmployeeId { get; set; }

    /// <summary>
    /// Swedish personal number (personnummer)
    /// </summary>
    public string? PersonalNumber { get; set; }

    /// <summary>
    /// Department or cost center
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Job title or position
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// Employment start date
    /// </summary>
    public DateTime? EmploymentStartDate { get; set; }
}

/// <summary>
/// Payroll deduction item
/// </summary>
public class PayrollDeduction
{
    /// <summary>
    /// Deduction description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Deduction amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Deduction type (tax, insurance, union, etc.)
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Momsrapport (VAT Report) - Swedish VAT reporting document
/// </summary>
public class VatReport : DocumentBase
{
    /// <summary>
    /// Company information
    /// </summary>
    public ContactInfo Company { get; set; } = new();

    /// <summary>
    /// VAT report period
    /// </summary>
    public DatePeriod ReportPeriod { get; set; } = new();

    /// <summary>
    /// VAT registration number
    /// </summary>
    public string? VatRegistrationNumber { get; set; }

    /// <summary>
    /// Report reference number
    /// </summary>
    public string? ReportNumber { get; set; }

    /// <summary>
    /// Report submission date
    /// </summary>
    public DateTime? SubmissionDate { get; set; }

    /// <summary>
    /// Output VAT (outgoing VAT on sales)
    /// </summary>
    public VatSummary OutputVat { get; set; } = new();

    /// <summary>
    /// Input VAT (incoming VAT on purchases)
    /// </summary>
    public VatSummary InputVat { get; set; } = new();

    /// <summary>
    /// Net VAT amount (output VAT - input VAT)
    /// </summary>
    public decimal NetVatAmount { get; set; }

    /// <summary>
    /// VAT to pay (if positive) or to be refunded (if negative)
    /// </summary>
    public decimal VatToPay { get; set; }

    /// <summary>
    /// Detailed VAT breakdown by rate
    /// </summary>
    public List<VatRateBreakdown> VatBreakdown { get; set; } = new();

    /// <summary>
    /// Currency (typically SEK for Swedish VAT reports)
    /// </summary>
    public string Currency { get; set; } = "SEK";

    /// <summary>
    /// Report status (draft, submitted, approved)
    /// </summary>
    public string? Status { get; set; }
}

/// <summary>
/// VAT summary for input or output VAT
/// </summary>
public class VatSummary
{
    /// <summary>
    /// Total VAT amount
    /// </summary>
    public decimal TotalVatAmount { get; set; }

    /// <summary>
    /// Total taxable amount (excluding VAT)
    /// </summary>
    public decimal TotalTaxableAmount { get; set; }

    /// <summary>
    /// VAT breakdown by rate
    /// </summary>
    public List<VatBreakdown> Breakdown { get; set; } = new();
}

/// <summary>
/// VAT breakdown by rate for VAT reports
/// </summary>
public class VatRateBreakdown
{
    /// <summary>
    /// VAT rate as percentage (0%, 6%, 12%, 25% for Sweden)
    /// </summary>
    public decimal VatRate { get; set; }

    /// <summary>
    /// Taxable amount for this rate
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>
    /// VAT amount for this rate
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// Number of transactions at this rate
    /// </summary>
    public int? TransactionCount { get; set; }
}