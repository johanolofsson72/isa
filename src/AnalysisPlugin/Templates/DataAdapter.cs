using AnalysisPlugin.Models;

namespace AnalysisPlugin.Templates;

/// <summary>
/// 🔄 Data Adapter - Konverterar SIE-data och annan verklig data till template-format
/// Ingen hårdkodning - bara transformation av riktig finansiell data!
/// </summary>
public static class DataAdapter
{
    /// <summary>
    /// Konverterar SIE-data till Financial Planning Template format (e1)
    /// </summary>
    public static Dictionary<string, object> ConvertToFinancialTemplate(
        List<SieTransaction> transactions, 
        string companyName,
        DateTime startDate,
        DateTime endDate)
    {
        var data = new Dictionary<string, object>
        {
            ["title"] = $"{companyName} - Financial Planning Report",
            ["period"] = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            ["templateType"] = "financial"
        };

        // Gruppera transaktioner per år för 5-års projektion
        var yearlyRevenue = GroupTransactionsByYear(transactions, "3", startDate, endDate); // Konto 3xxx = Intäkter
        var yearlyExpenses = GroupTransactionsByYear(transactions, "4,5,6,7", startDate, endDate); // Konto 4-7xxx = Kostnader

        data["revenueData"] = ConvertToChartData(yearlyRevenue, "Revenue");
        data["expenseData"] = ConvertToChartData(yearlyExpenses, "Expenses");

        // Payroll data (löner - konto 7xxx)
        var payrollData = GroupTransactionsByAccount(transactions, "70,71,72", startDate, endDate);
        data["payrollData"] = ConvertToTableData(payrollData, "Payroll");

        // Balance Sheet data
        var assets = GroupTransactionsByAccount(transactions, "1", startDate, endDate); // Tillgångar
        var liabilities = GroupTransactionsByAccount(transactions, "2", startDate, endDate); // Skulder
        
        data["balanceSheetData"] = new Dictionary<string, object>
        {
            ["assets"] = ConvertToTableData(assets, "Assets"),
            ["liabilities"] = ConvertToTableData(liabilities, "Liabilities")
        };

        return data;
    }

    /// <summary>
    /// Konverterar SIE-data till Business Budget Template format (e2)
    /// </summary>
    public static Dictionary<string, object> ConvertToBudgetTemplate(
        List<SieTransaction> transactions,
        string companyName,
        DateTime startDate,
        DateTime endDate)
    {
        var data = new Dictionary<string, object>
        {
            ["title"] = $"{companyName} - Business Budget Report",
            ["period"] = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            ["templateType"] = "budget"
        };

        // Månadsvis data
        var monthlyData = new List<Dictionary<string, object>>();
        
        for (var date = startDate; date <= endDate; date = date.AddMonths(1))
        {
            var monthStart = new DateTime(date.Year, date.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            
            var monthTransactions = transactions.Where(t => 
                t.Date >= monthStart && t.Date <= monthEnd).ToList();

            var income = CalculateTotalForAccounts(monthTransactions, "3"); // Intäkter
            var expenses = CalculateTotalForAccounts(monthTransactions, "4,5,6,7"); // Kostnader
            var netProfit = income - expenses;

            monthlyData.Add(new Dictionary<string, object>
            {
                ["month"] = date.ToString("MMMM yyyy"),
                ["income"] = income,
                ["expenses"] = expenses,
                ["netProfit"] = netProfit,
                ["expenseRatio"] = income > 0 ? expenses / income : 0
            });
        }

        data["monthlyData"] = monthlyData;

        // Income breakdown
        var incomeByAccount = GroupTransactionsByAccount(transactions, "3", startDate, endDate);
        data["incomeData"] = ConvertToTableData(incomeByAccount, "Income");

        // Expense breakdown  
        var expensesByAccount = GroupTransactionsByAccount(transactions, "4,5,6,7", startDate, endDate);
        data["expenseData"] = ConvertToTableData(expensesByAccount, "Expenses");

        return data;
    }

    /// <summary>
    /// Konverterar projekt-data till Project Template format (e3/e4)
    /// </summary>
    public static Dictionary<string, object> ConvertToProjectTemplate(
        List<ProjectPhase> phases,
        string projectName)
    {
        var data = new Dictionary<string, object>
        {
            ["title"] = $"{projectName} - Project Schedule",
            ["templateType"] = "project"
        };

        var phaseData = phases.Select(phase => new Dictionary<string, object>
        {
            ["name"] = phase.Name,
            ["startDate"] = phase.StartDate,
            ["endDate"] = phase.EndDate,
            ["progress"] = phase.Progress,
            ["status"] = phase.Status,
            ["lead"] = phase.Lead
        }).ToList();

        data["phases"] = phaseData;
        return data;
    }

    /// <summary>
    /// Konverterar KPI-data till KPI Dashboard format (e5)
    /// </summary>
    public static Dictionary<string, object> ConvertToKpiTemplate(
        List<KpiMetric> metrics,
        string companyName)
    {
        var data = new Dictionary<string, object>
        {
            ["title"] = $"{companyName} - KPI Dashboard",
            ["templateType"] = "kpi"
        };

        var kpiData = metrics.Select(metric => new Dictionary<string, object>
        {
            ["name"] = metric.Name,
            ["actual"] = metric.ActualValue,
            ["target"] = metric.TargetValue,
            ["variance"] = metric.ActualValue - metric.TargetValue,
            ["unit"] = metric.Unit,
            ["trend"] = metric.Trend
        }).ToList();

        data["kpiData"] = kpiData;
        return data;
    }

    // Hjälpmetoder för databearbetning
    private static List<Dictionary<string, object>> GroupTransactionsByYear(
        List<SieTransaction> transactions, 
        string accountPrefix, 
        DateTime startDate, 
        DateTime endDate)
    {
        var accountPrefixes = accountPrefix.Split(',');
        
        return transactions
            .Where(t => accountPrefixes.Any(prefix => t.Account.StartsWith(prefix)))
            .GroupBy(t => t.Date.Year)
            .Select(g => new Dictionary<string, object>
            {
                ["year"] = g.Key,
                ["amount"] = g.Sum(t => Math.Abs(t.Amount))
            })
            .OrderBy(x => (int)x["year"])
            .ToList();
    }

    private static List<Dictionary<string, object>> GroupTransactionsByAccount(
        List<SieTransaction> transactions,
        string accountPrefix,
        DateTime startDate,
        DateTime endDate)
    {
        var accountPrefixes = accountPrefix.Split(',');
        
        return transactions
            .Where(t => accountPrefixes.Any(prefix => t.Account.StartsWith(prefix)))
            .GroupBy(t => t.Account)
            .Select(g => new Dictionary<string, object>
            {
                ["account"] = g.Key,
                ["description"] = g.First().Description ?? $"Account {g.Key}",
                ["amount"] = g.Sum(t => Math.Abs(t.Amount))
            })
            .OrderBy(x => x["account"].ToString())
            .ToList();
    }

    private static double CalculateTotalForAccounts(List<SieTransaction> transactions, string accountPrefix)
    {
        var accountPrefixes = accountPrefix.Split(',');
        return transactions
            .Where(t => accountPrefixes.Any(prefix => t.Account.StartsWith(prefix)))
            .Sum(t => Math.Abs(t.Amount));
    }

    private static List<Dictionary<string, object>> ConvertToChartData(
        List<Dictionary<string, object>> groupedData, 
        string dataType)
    {
        return groupedData.Select(item => new Dictionary<string, object>
        {
            ["label"] = $"Year {item["year"]}",
            ["value"] = item["amount"],
            ["type"] = dataType
        }).ToList();
    }

    private static List<Dictionary<string, object>> ConvertToTableData(
        List<Dictionary<string, object>> groupedData,
        string dataType)
    {
        return groupedData.Select(item => new Dictionary<string, object>
        {
            ["category"] = item.GetValueOrDefault("description", item["account"]),
            ["amount"] = item["amount"],
            ["type"] = dataType
        }).ToList();
    }
}

// Hjälpklasser för olika datatyper
public class ProjectPhase
{
    public string Name { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double Progress { get; set; }
    public string Status { get; set; } = "";
    public string Lead { get; set; } = "";
}

public class KpiMetric
{
    public string Name { get; set; } = "";
    public double ActualValue { get; set; }
    public double TargetValue { get; set; }
    public string Unit { get; set; } = "";
    public string Trend { get; set; } = "";
}
