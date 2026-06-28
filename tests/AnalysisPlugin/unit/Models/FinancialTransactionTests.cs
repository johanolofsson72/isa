using Xunit;
using FluentAssertions;
using AnalysisPlugin.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;

namespace AnalysisPlugin.Tests.Unit.Models;

[Trait("Category", "CI")]
public class FinancialTransactionTests
{
    [Fact]
    public void FinancialTransaction_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid().ToString(),
            Date = DateTime.Now.Date,
            Description = "Test transaction",
            Amount = 1000.50m,
            AccountNumber = "1910",
            AccountName = "Kundfordringar",
            ProjectId = "PROJ001",
            Currency = "SEK",
            ExchangeRate = 1.0m
        };

        // Act
        var validationResults = ValidateModel(transaction);

        // Assert
        validationResults.Should().BeEmpty("Valid transaction should pass validation");
        transaction.Amount.Should().Be(1000.50m);
        transaction.Currency.Should().Be("SEK");
        transaction.ExchangeRate.Should().Be(1.0m);
    }

    [Fact]
    public void FinancialTransaction_WithNegativeAmount_ShouldBeValid()
    {
        // Arrange - Negative amounts are valid for credits/expenses
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid().ToString(),
            Date = DateTime.Now.Date,
            Description = "Credit transaction",
            Amount = -500.25m,
            AccountNumber = "3010",
            AccountName = "Försäljning",
            Currency = "SEK",
            ExchangeRate = 1.0m
        };

        // Act
        var validationResults = ValidateModel(transaction);

        // Assert
        validationResults.Should().BeEmpty("Negative amounts should be valid for credits");
        transaction.Amount.Should().Be(-500.25m);
    }

    [Fact]
    public void FinancialTransaction_WithForeignCurrency_ShouldRequireExchangeRate()
    {
        // Arrange
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid().ToString(),
            Date = DateTime.Now.Date,
            Description = "USD transaction",
            Amount = 100.00m,
            AccountNumber = "1910",
            AccountName = "Kundfordringar",
            Currency = "USD",
            ExchangeRate = 10.50m // SEK per USD
        };

        // Act
        var validationResults = ValidateModel(transaction);

        // Assert
        validationResults.Should().BeEmpty("Foreign currency with exchange rate should be valid");
        transaction.Currency.Should().Be("USD");
        transaction.ExchangeRate.Should().Be(10.50m);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void FinancialTransaction_WithInvalidId_ShouldFailValidation(string invalidId)
    {
        // Arrange
        var transaction = new FinancialTransaction
        {
            Id = invalidId,
            Date = DateTime.Now.Date,
            Description = "Test transaction",
            Amount = 1000.50m,
            AccountNumber = "1910",
            AccountName = "Kundfordringar",
            Currency = "SEK",
            ExchangeRate = 1.0m
        };

        // Act
        var validationResults = ValidateModel(transaction);

        // Assert
        validationResults.Should().NotBeEmpty("Invalid ID should fail validation");
        validationResults.Should().Contain(r => r.MemberNames.Contains("Id"));
    }

    [Fact]
    public void FinancialTransaction_WithFutureDate_ShouldBeValid()
    {
        // Arrange - Future dates might be valid for planned transactions
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid().ToString(),
            Date = DateTime.Now.Date.AddDays(30),
            Description = "Future transaction",
            Amount = 1000.00m,
            AccountNumber = "1910",
            AccountName = "Kundfordringar",
            Currency = "SEK",
            ExchangeRate = 1.0m
        };

        // Act
        var validationResults = ValidateModel(transaction);

        // Assert
        validationResults.Should().BeEmpty("Future dates should be valid for planned transactions");
    }

    [Theory]
    [InlineData("1000", "Tillgångar")]
    [InlineData("2000", "Skulder")]
    [InlineData("3000", "Intäkter")]
    [InlineData("4000", "Kostnader")]
    [InlineData("5000", "Kostnader")]
    [InlineData("6000", "Kostnader")]
    [InlineData("7000", "Kostnader")]
    [InlineData("8000", "Resultatdisposition")]
    public void FinancialTransaction_WithBASAccountNumbers_ShouldBeValid(string accountNumber, string expectedCategory)
    {
        // Arrange
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid().ToString(),
            Date = DateTime.Now.Date,
            Description = $"BAS account {accountNumber} transaction",
            Amount = 1000.00m,
            AccountNumber = accountNumber,
            AccountName = expectedCategory,
            Currency = "SEK",
            ExchangeRate = 1.0m
        };

        // Act
        var validationResults = ValidateModel(transaction);

        // Assert
        validationResults.Should().BeEmpty($"BAS account {accountNumber} should be valid");
        transaction.AccountNumber.Should().Be(accountNumber);
    }

    [Fact]
    public void FinancialTransaction_WithZeroAmount_ShouldBeValid()
    {
        // Arrange - Zero amounts might be valid for certain transaction types
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid().ToString(),
            Date = DateTime.Now.Date,
            Description = "Zero amount transaction",
            Amount = 0.00m,
            AccountNumber = "1910",
            AccountName = "Kundfordringar",
            Currency = "SEK",
            ExchangeRate = 1.0m
        };

        // Act
        var validationResults = ValidateModel(transaction);

        // Assert
        validationResults.Should().BeEmpty("Zero amounts should be valid");
        transaction.Amount.Should().Be(0.00m);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, validationResults, true);
        return validationResults;
    }
}
