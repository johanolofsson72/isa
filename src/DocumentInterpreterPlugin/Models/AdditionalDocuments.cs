namespace DocumentInterpreter.Models;

/// <summary>
/// Bank statement with transactions
/// </summary>
public class BankStatement : DocumentBase
{
    /// <summary>
    /// Bank account information
    /// </summary>
    public BankAccount Account { get; set; } = new();

    /// <summary>
    /// Statement period start
    /// </summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>
    /// Statement period end
    /// </summary>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// Opening balance
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// Closing balance
    /// </summary>
    public decimal ClosingBalance { get; set; }

    /// <summary>
    /// List of transactions
    /// </summary>
    public List<BankTransaction> Transactions { get; set; } = new();
}

/// <summary>
/// Bank account information
/// </summary>
public class BankAccount
{
    /// <summary>
    /// Account number
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Account name/description
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Bank name
    /// </summary>
    public string? BankName { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public string Currency { get; set; } = "SEK";

    /// <summary>
    /// IBAN if applicable
    /// </summary>
    public string? Iban { get; set; }

    /// <summary>
    /// BIC/SWIFT code
    /// </summary>
    public string? Bic { get; set; }
}

/// <summary>
/// Bank transaction
/// </summary>
public class BankTransaction
{
    /// <summary>
    /// Transaction date
    /// </summary>
    public DateTime? TransactionDate { get; set; }

    /// <summary>
    /// Value date (when the transaction takes effect)
    /// </summary>
    public DateTime? ValueDate { get; set; }

    /// <summary>
    /// Transaction amount (positive for credit, negative for debit)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Transaction description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Reference number
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Transaction type (e.g., "Direct Debit", "Card Payment", etc.)
    /// </summary>
    public string? TransactionType { get; set; }

    /// <summary>
    /// Balance after this transaction
    /// </summary>
    public decimal? Balance { get; set; }
}

/// <summary>
/// Customs document for import/export
/// </summary>
public class CustomsDocument : DocumentBase
{
    /// <summary>
    /// MRN (Movement Reference Number)
    /// </summary>
    public string Mrn { get; set; } = string.Empty;

    /// <summary>
    /// Customs value of goods
    /// </summary>
    public decimal CustomsValue { get; set; }

    /// <summary>
    /// Duty amount
    /// </summary>
    public decimal Duty { get; set; }

    /// <summary>
    /// Import VAT amount
    /// </summary>
    public decimal ImportVat { get; set; }

    /// <summary>
    /// Reference to related invoice
    /// </summary>
    public string? InvoiceRef { get; set; }

    /// <summary>
    /// Country of origin
    /// </summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>
    /// Country of destination
    /// </summary>
    public string? CountryOfDestination { get; set; }

    /// <summary>
    /// Commodity code
    /// </summary>
    public string? CommodityCode { get; set; }

    /// <summary>
    /// Gross weight in kg
    /// </summary>
    public decimal? GrossWeight { get; set; }

    /// <summary>
    /// Net weight in kg
    /// </summary>
    public decimal? NetWeight { get; set; }
}

/// <summary>
/// Expense report for travel/business expenses
/// </summary>
public class ExpenseReport : DocumentBase
{
    /// <summary>
    /// Employee or person submitting the expense report
    /// </summary>
    public ContactInfo Employee { get; set; } = new();

    /// <summary>
    /// Report period start
    /// </summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>
    /// Report period end
    /// </summary>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// Expense items
    /// </summary>
    public List<ExpenseItem> Items { get; set; } = new();

    /// <summary>
    /// Mileage expenses
    /// </summary>
    public List<MileageExpense> Mileage { get; set; } = new();

    /// <summary>
    /// Per diem expenses
    /// </summary>
    public List<PerDiemExpense> PerDiem { get; set; } = new();

    /// <summary>
    /// Total amount claimed
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Report status
    /// </summary>
    public string? Status { get; set; }
}

/// <summary>
/// Individual expense item
/// </summary>
public class ExpenseItem
{
    /// <summary>
    /// Expense date
    /// </summary>
    public DateTime? ExpenseDate { get; set; }

    /// <summary>
    /// Expense type (meals, transport, accommodation, etc.)
    /// </summary>
    public string ExpenseType { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// VAT amount
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// VAT rate
    /// </summary>
    public decimal VatRate { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public string Currency { get; set; } = "SEK";

    /// <summary>
    /// Receipt reference or attachment
    /// </summary>
    public string? ReceiptReference { get; set; }
}

/// <summary>
/// Mileage expense
/// </summary>
public class MileageExpense
{
    /// <summary>
    /// Travel date
    /// </summary>
    public DateTime? TravelDate { get; set; }

    /// <summary>
    /// From location
    /// </summary>
    public string FromLocation { get; set; } = string.Empty;

    /// <summary>
    /// To location
    /// </summary>
    public string ToLocation { get; set; } = string.Empty;

    /// <summary>
    /// Distance in kilometers
    /// </summary>
    public decimal Distance { get; set; }

    /// <summary>
    /// Rate per kilometer
    /// </summary>
    public decimal RatePerKm { get; set; }

    /// <summary>
    /// Total amount
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Purpose of travel
    /// </summary>
    public string? Purpose { get; set; }
}

/// <summary>
/// Per diem expense
/// </summary>
public class PerDiemExpense
{
    /// <summary>
    /// Date
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Location/destination
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Per diem rate
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Number of days
    /// </summary>
    public decimal Days { get; set; }

    /// <summary>
    /// Total amount
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Meal deductions (if meals were provided)
    /// </summary>
    public decimal MealDeductions { get; set; }
}

/// <summary>
/// Reminder or dunning notice
/// </summary>
public class ReminderNotice : DocumentBase
{
    /// <summary>
    /// Customer information
    /// </summary>
    public ContactInfo Customer { get; set; } = new();

    /// <summary>
    /// Reference to original invoice
    /// </summary>
    public string InvoiceRef { get; set; } = string.Empty;

    /// <summary>
    /// Original invoice date
    /// </summary>
    public DateTime? OriginalInvoiceDate { get; set; }

    /// <summary>
    /// Original due date
    /// </summary>
    public DateTime? OriginalDueDate { get; set; }

    /// <summary>
    /// Reminder fees
    /// </summary>
    public List<Fee> Fees { get; set; } = new();

    /// <summary>
    /// New due date
    /// </summary>
    public DateTime? NewDueDate { get; set; }

    /// <summary>
    /// Outstanding amount
    /// </summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>
    /// Total amount including fees
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Reminder level (1st, 2nd, final, etc.)
    /// </summary>
    public string? ReminderLevel { get; set; }
}

/// <summary>
/// Vendor onboarding document with master data
/// </summary>
public class VendorOnboarding : DocumentBase
{
    /// <summary>
    /// Vendor information
    /// </summary>
    public ContactInfo Vendor { get; set; } = new();

    /// <summary>
    /// Bank account details
    /// </summary>
    public BankAccount BankAccount { get; set; } = new();

    /// <summary>
    /// Tax information
    /// </summary>
    public TaxInfo TaxInfo { get; set; } = new();

    /// <summary>
    /// Business categories or types
    /// </summary>
    public List<string> BusinessCategories { get; set; } = new();

    /// <summary>
    /// Preferred payment terms
    /// </summary>
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Credit limit
    /// </summary>
    public decimal? CreditLimit { get; set; }
}

/// <summary>
/// Customer onboarding document for creating new customers in Fortnox
/// </summary>
public class CustomerOnboarding : DocumentBase
{
    /// <summary>
    /// Customer information extracted from the document
    /// </summary>
    public CustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// Delivery address if different from billing address
    /// </summary>
    public DeliveryAddressInfo? DeliveryAddress { get; set; }

    /// <summary>
    /// Additional customer preferences and settings
    /// </summary>
    public CustomerPreferences Preferences { get; set; } = new();
}

/// <summary>
/// Customer information for onboarding
/// </summary>
public class CustomerInfo
{
    /// <summary>
    /// Customer name (required)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Customer type: COMPANY or PRIVATE (required)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Email address (required)
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Billing address (required)
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// City (required)
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Postal code (required)
    /// </summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// Country code (required)
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Country name (optional)
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Phone number (optional)
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Organization number (optional)
    /// </summary>
    public string? OrganizationNumber { get; set; }

    /// <summary>
    /// Currency (optional, defaults to SEK)
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Website URL (optional)
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Comments about the customer (optional)
    /// </summary>
    public string? Comments { get; set; }
}

/// <summary>
/// Delivery address information if different from billing
/// </summary>
public class DeliveryAddressInfo
{
    /// <summary>
    /// Delivery address
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Delivery city
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Delivery postal code
    /// </summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// Delivery country code
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Delivery country name
    /// </summary>
    public string? Country { get; set; }
}

/// <summary>
/// Customer preferences and business terms
/// </summary>
public class CustomerPreferences
{
    /// <summary>
    /// Payment terms (optional)
    /// </summary>
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Delivery terms (optional)
    /// </summary>
    public string? DeliveryTerms { get; set; }

    /// <summary>
    /// Whether customer is active (defaults to true)
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Vendor status (active, pending, inactive)
    /// </summary>
    public string Status { get; set; } = "Pending";
}

/// <summary>
/// Tax information for vendor onboarding
/// </summary>
public class TaxInfo
{
    /// <summary>
    /// VAT registration number
    /// </summary>
    public string? VatNumber { get; set; }

    /// <summary>
    /// Tax registration number
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Whether VAT reverse charge applies
    /// </summary>
    public bool ReverseChargeApplicable { get; set; }

    /// <summary>
    /// Tax residence country
    /// </summary>
    public string? TaxResidenceCountry { get; set; }

    /// <summary>
    /// Tax exemption details, if applicable
    /// </summary>
    public string? TaxExemptionDetails { get; set; }
}