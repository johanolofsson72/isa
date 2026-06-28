namespace AnalysisPlugin.Models;

/// <summary>
/// BAS (Bas Account Schema) account categories according to Swedish accounting standards
/// </summary>
public enum BASAccountCategory
{
    /// <summary>
    /// 1xxx - Assets (Tillgångar)
    /// </summary>
    Assets = 1,

    /// <summary>
    /// 2xxx - Liabilities and Equity (Skulder och eget kapital)
    /// </summary>
    Liabilities = 2,

    /// <summary>
    /// 3xxx - Revenue (Intäkter)
    /// </summary>
    Revenue = 3,

    /// <summary>
    /// 4xxx - Cost of Goods Sold (Varukostnader)
    /// </summary>
    CostOfGoods = 4,

    /// <summary>
    /// 5xxx - External Costs (Externa kostnader)
    /// </summary>
    ExternalCosts = 5,

    /// <summary>
    /// 6xxx - Personnel Costs (Personalkostnader)
    /// </summary>
    PersonnelCosts = 6,

    /// <summary>
    /// 7xxx - Financial Items (Finansiella poster)
    /// </summary>
    FinancialItems = 7,

    /// <summary>
    /// 8xxx - Extraordinary Items (Extraordinära poster)
    /// </summary>
    Extraordinary = 8,

    /// <summary>
    /// Unknown or uncategorized account
    /// </summary>
    Unknown = 0
}
