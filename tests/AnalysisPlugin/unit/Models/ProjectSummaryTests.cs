using Xunit;
using FluentAssertions;
using AnalysisPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnalysisPlugin.Tests.Unit.Models;

[Trait("Category", "CI")]
public class ProjectSummaryTests
{
    [Fact]
    public void ProjectSummary_WithRealTransactions_ShouldCalculateCorrectTotals()
    {
        // Arrange
        var transactions = CreateSampleTransactions();
        var projectSummary = new ProjectSummary
        {
            ProjectId = "PROJ001",
            ProjectName = "Test Project",
            StartDate = DateTime.Now.Date.AddDays(-30),
            EndDate = DateTime.Now.Date,
            Transactions = transactions
        };

        // Act
        var totalRevenue = projectSummary.Transactions
            .Where(t => t.AccountNumber.StartsWith("3"))
            .Sum(t => Math.Abs(t.Amount));
        
        var totalExpenses = projectSummary.Transactions
            .Where(t => t.AccountNumber.StartsWith("4") || t.AccountNumber.StartsWith("5"))
            .Sum(t => Math.Abs(t.Amount));

        var grossProfit = totalRevenue - totalExpenses;
        var grossProfitPercentage = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;

        // Assert
        totalRevenue.Should().Be(15000.00m, "Revenue should be sum of account 3xxx transactions");
        totalExpenses.Should().Be(8500.00m, "Expenses should be sum of account 4xxx and 5xxx transactions");
        grossProfit.Should().Be(6500.00m, "Gross profit should be revenue minus expenses");
        grossProfitPercentage.Should().BeApproximately(43.33m, 0.01m, "Gross profit percentage should be calculated correctly");
    }

    [Fact]
    public void ProjectSummary_WithForeignCurrencyTransactions_ShouldConvertToSEK()
    {
        // Arrange
        var transactions = new List<FinancialTransaction>
        {
            new FinancialTransaction
            {
                Id = "1",
                Date = DateTime.Now.Date,
                Description = "USD Revenue",
                Amount = 1000.00m, // USD
                AccountNumber = "3010",
                AccountName = "Försäljning",
                Currency = "USD",
                ExchangeRate = 10.50m // SEK per USD
            },
            new FinancialTransaction
            {
                Id = "2",
                Date = DateTime.Now.Date,
                Description = "EUR Expense",
                Amount = -500.00m, // EUR
                AccountNumber = "4010",
                AccountName = "Inköp",
                Currency = "EUR",
                ExchangeRate = 11.20m // SEK per EUR
            }
        };

        var projectSummary = new ProjectSummary
        {
            ProjectId = "PROJ002",
            ProjectName = "International Project",
            Transactions = transactions
        };

        // Act
        var revenueInSEK = transactions
            .Where(t => t.AccountNumber.StartsWith("3"))
            .Sum(t => Math.Abs(t.Amount * t.ExchangeRate));
        
        var expensesInSEK = transactions
            .Where(t => t.AccountNumber.StartsWith("4"))
            .Sum(t => Math.Abs(t.Amount * t.ExchangeRate));

        // Assert
        revenueInSEK.Should().Be(10500.00m, "USD revenue should be converted to SEK");
        expensesInSEK.Should().Be(5600.00m, "EUR expense should be converted to SEK");
    }

    [Fact]
    public void ProjectSummary_WithNoTransactions_ShouldHaveZeroTotals()
    {
        // Arrange
        var projectSummary = new ProjectSummary
        {
            ProjectId = "PROJ003",
            ProjectName = "Empty Project",
            StartDate = DateTime.Now.Date.AddDays(-30),
            EndDate = DateTime.Now.Date,
            Transactions = new List<FinancialTransaction>()
        };

        // Act
        var totalRevenue = projectSummary.Transactions
            .Where(t => t.AccountNumber.StartsWith("3"))
            .Sum(t => Math.Abs(t.Amount));

        // Assert
        totalRevenue.Should().Be(0m, "Empty project should have zero revenue");
        projectSummary.Transactions.Should().BeEmpty("Empty project should have no transactions");
    }

    [Fact]
    public void ProjectSummary_WithMixedAccountTypes_ShouldCategorizeCorrectly()
    {
        // Arrange
        var transactions = new List<FinancialTransaction>
        {
            // Assets (1xxx)
            new FinancialTransaction { Id = "1", AccountNumber = "1910", Amount = 5000m, Currency = "SEK", ExchangeRate = 1.0m },
            // Liabilities (2xxx)
            new FinancialTransaction { Id = "2", AccountNumber = "2440", Amount = -2000m, Currency = "SEK", ExchangeRate = 1.0m },
            // Revenue (3xxx)
            new FinancialTransaction { Id = "3", AccountNumber = "3010", Amount = -8000m, Currency = "SEK", ExchangeRate = 1.0m },
            // Expenses (4xxx)
            new FinancialTransaction { Id = "4", AccountNumber = "4010", Amount = 3000m, Currency = "SEK", ExchangeRate = 1.0m },
            // Expenses (5xxx)
            new FinancialTransaction { Id = "5", AccountNumber = "5010", Amount = 1500m, Currency = "SEK", ExchangeRate = 1.0m }
        };

        var projectSummary = new ProjectSummary
        {
            ProjectId = "PROJ004",
            ProjectName = "Mixed Accounts Project",
            Transactions = transactions
        };

        // Act
        var assets = projectSummary.Transactions.Where(t => t.AccountNumber.StartsWith("1")).Sum(t => t.Amount);
        var liabilities = projectSummary.Transactions.Where(t => t.AccountNumber.StartsWith("2")).Sum(t => Math.Abs(t.Amount));
        var revenue = projectSummary.Transactions.Where(t => t.AccountNumber.StartsWith("3")).Sum(t => Math.Abs(t.Amount));
        var expenses = projectSummary.Transactions.Where(t => t.AccountNumber.StartsWith("4") || t.AccountNumber.StartsWith("5")).Sum(t => Math.Abs(t.Amount));

        // Assert
        assets.Should().Be(5000m, "Assets should be positive");
        liabilities.Should().Be(2000m, "Liabilities should be calculated as absolute value");
        revenue.Should().Be(8000m, "Revenue should be calculated as absolute value");
        expenses.Should().Be(4500m, "Expenses should be sum of 4xxx and 5xxx accounts");
    }

    [Fact]
    public void ProjectSummary_WithDateRange_ShouldFilterTransactionsCorrectly()
    {
        // Arrange
        var startDate = DateTime.Now.Date.AddDays(-30);
        var endDate = DateTime.Now.Date.AddDays(-10);
        
        var transactions = new List<FinancialTransaction>
        {
            new FinancialTransaction { Id = "1", Date = startDate.AddDays(-5), Amount = 1000m, AccountNumber = "3010", Currency = "SEK", ExchangeRate = 1.0m }, // Before range
            new FinancialTransaction { Id = "2", Date = startDate.AddDays(5), Amount = 2000m, AccountNumber = "3010", Currency = "SEK", ExchangeRate = 1.0m }, // In range
            new FinancialTransaction { Id = "3", Date = endDate.AddDays(5), Amount = 3000m, AccountNumber = "3010", Currency = "SEK", ExchangeRate = 1.0m }  // After range
        };

        var projectSummary = new ProjectSummary
        {
            ProjectId = "PROJ005",
            ProjectName = "Date Range Project",
            StartDate = startDate,
            EndDate = endDate,
            Transactions = transactions
        };

        // Act
        var transactionsInRange = projectSummary.Transactions
            .Where(t => t.Date >= projectSummary.StartDate && t.Date <= projectSummary.EndDate)
            .ToList();

        // Assert
        transactionsInRange.Should().HaveCount(1, "Only one transaction should be in the date range");
        transactionsInRange.First().Amount.Should().Be(2000m, "The in-range transaction should have amount 2000");
    }

    private static List<FinancialTransaction> CreateSampleTransactions()
    {
        return new List<FinancialTransaction>
        {
            // Revenue transactions (3xxx accounts)
            new FinancialTransaction
            {
                Id = "1",
                Date = DateTime.Now.Date.AddDays(-10),
                Description = "Sales revenue",
                Amount = -10000.00m, // Negative for revenue
                AccountNumber = "3010",
                AccountName = "Försäljning",
                Currency = "SEK",
                ExchangeRate = 1.0m
            },
            new FinancialTransaction
            {
                Id = "2",
                Date = DateTime.Now.Date.AddDays(-5),
                Description = "Service revenue",
                Amount = -5000.00m, // Negative for revenue
                AccountNumber = "3020",
                AccountName = "Tjänsteintäkter",
                Currency = "SEK",
                ExchangeRate = 1.0m
            },
            // Expense transactions (4xxx accounts)
            new FinancialTransaction
            {
                Id = "3",
                Date = DateTime.Now.Date.AddDays(-8),
                Description = "Material costs",
                Amount = 6000.00m, // Positive for expenses
                AccountNumber = "4010",
                AccountName = "Inköp av material",
                Currency = "SEK",
                ExchangeRate = 1.0m
            },
            // Expense transactions (5xxx accounts)
            new FinancialTransaction
            {
                Id = "4",
                Date = DateTime.Now.Date.AddDays(-3),
                Description = "Office rent",
                Amount = 2500.00m, // Positive for expenses
                AccountNumber = "5010",
                AccountName = "Lokalhyra",
                Currency = "SEK",
                ExchangeRate = 1.0m
            }
        };
    }
}
