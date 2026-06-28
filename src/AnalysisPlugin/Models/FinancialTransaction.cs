using System.ComponentModel.DataAnnotations;

namespace AnalysisPlugin.Models;

/// <summary>
/// Represents a financial transaction parsed from SIE4 data with BAS account category mapping
/// </summary>
public class FinancialTransaction
{
    /// <summary>
    /// Unique identifier for the transaction
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Transaction date (must not be in the future)
    /// </summary>
    [Required]
    public DateTime Date { get; set; }

    /// <summary>
    /// Transaction description
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Transaction amount (can be positive or negative)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Account number according to BAS account plan
    /// </summary>
    [Required]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Account name/description
    /// </summary>
    [Required]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "SEK";

    /// <summary>
    /// Exchange rate to base currency (must be > 0)
    /// </summary>
    [Range(0.0001, double.MaxValue)]
    public decimal ExchangeRate { get; set; } = 1.0m;

    /// <summary>
    /// Project identifier (optional)
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Cost center identifier (optional)
    /// </summary>
    public string? CostCenter { get; set; }

    /// <summary>
    /// BAS account category based on account number
    /// </summary>
    public BASAccountCategory Category { get; set; } = BASAccountCategory.Unknown;

    /// <summary>
    /// Amount in base currency (SEK)
    /// </summary>
    public decimal BaseAmount => Amount * ExchangeRate;

    /// <summary>
    /// Validates the transaction data
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Id))
            return false;

        if (Date > DateTime.Now.Date)
            return false;

        if (string.IsNullOrWhiteSpace(Description))
            return false;

        if (string.IsNullOrWhiteSpace(AccountNumber))
            return false;

        if (string.IsNullOrWhiteSpace(AccountName))
            return false;

        if (Currency.Length != 3)
            return false;

        if (ExchangeRate <= 0)
            return false;

        return true;
    }

    /// <summary>
    /// Determines BAS account category from account number
    /// </summary>
    /// <param name="accountNumber">Account number to categorize</param>
    /// <returns>BAS account category</returns>
    public static BASAccountCategory GetBASCategory(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return BASAccountCategory.Unknown;

        var firstDigit = accountNumber[0];
        return firstDigit switch
        {
            '1' => BASAccountCategory.Assets,
            '2' => BASAccountCategory.Liabilities,
            '3' => BASAccountCategory.Revenue,
            '4' => BASAccountCategory.CostOfGoods,
            '5' => BASAccountCategory.ExternalCosts,
            '6' => BASAccountCategory.PersonnelCosts,
            '7' => BASAccountCategory.FinancialItems,
            '8' => BASAccountCategory.Extraordinary,
            _ => BASAccountCategory.Unknown
        };
    }

    /// <summary>
    /// Updates the BAS category based on the account number
    /// </summary>
    public void UpdateBASCategory()
    {
        Category = GetBASCategory(AccountNumber);
    }
}
