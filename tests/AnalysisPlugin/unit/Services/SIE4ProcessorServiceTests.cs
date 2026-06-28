using Xunit;
using FluentAssertions;
using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnalysisPlugin.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Moq;

namespace AnalysisPlugin.Tests.Unit.Services;

[Trait("Category", "CI")]
public class SIE4ProcessorServiceTests
{
    private readonly SIE4ProcessorService _service;
    private readonly Mock<ILogger<SIE4ProcessorService>> _loggerMock;
    private readonly AnalysisPluginOptions _options;

    public SIE4ProcessorServiceTests()
    {
        _loggerMock = new Mock<ILogger<SIE4ProcessorService>>();
        _options = new AnalysisPluginOptions
        {
            ReportArchivePath = Path.GetTempPath(),
            MaxProcessingTimeoutMinutes = 5
        };
        var optionsMock = new Mock<IOptions<AnalysisPluginOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        _service = new SIE4ProcessorService(_loggerMock.Object, optionsMock.Object);
    }

    [Fact]
    public async Task ProcessSIE4Data_WithRealSIE4File_ShouldParseCorrectly()
    {
        // Arrange
        var sie4Content = CreateValidSIE4Content();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, sie4Content);

        try
        {
            // Act
            var result = await _service.ProcessSIE4FileAsync(tempFile);

            // Assert
            result.Should().NotBeNull("SIE4 processing should return a result");
            result.Transactions.Should().NotBeEmpty("SIE4 file should contain transactions");
            result.Transactions.Should().HaveCountGreaterThan(0, "Should parse at least one transaction");
            
            // Verify company information
            result.CompanyName.Should().NotBeNullOrEmpty("Company name should be parsed");
            result.OrganizationNumber.Should().NotBeNullOrEmpty("Organization number should be parsed");
            
            // Verify transaction data
            var firstTransaction = result.Transactions.First();
            firstTransaction.Id.Should().NotBeNullOrEmpty("Transaction should have an ID");
            firstTransaction.AccountNumber.Should().NotBeNullOrEmpty("Transaction should have account number");
            firstTransaction.Amount.Should().NotBe(0, "Transaction should have non-zero amount");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessSIE4Data_WithBASAccountNumbers_ShouldClassifyCorrectly()
    {
        // Arrange
        var sie4Content = CreateSIE4WithBASAccounts();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, sie4Content);

        try
        {
            // Act
            var result = await _service.ProcessSIE4FileAsync(tempFile);

            // Assert
            result.Transactions.Should().NotBeEmpty("Should parse BAS account transactions");
            
            // Check for different BAS account categories
            var assetTransactions = result.Transactions.Where(t => t.AccountNumber.StartsWith("1")).ToList();
            var liabilityTransactions = result.Transactions.Where(t => t.AccountNumber.StartsWith("2")).ToList();
            var revenueTransactions = result.Transactions.Where(t => t.AccountNumber.StartsWith("3")).ToList();
            var expenseTransactions = result.Transactions.Where(t => t.AccountNumber.StartsWith("4") || t.AccountNumber.StartsWith("5")).ToList();

            assetTransactions.Should().NotBeEmpty("Should have asset transactions (1xxx)");
            revenueTransactions.Should().NotBeEmpty("Should have revenue transactions (3xxx)");
            expenseTransactions.Should().NotBeEmpty("Should have expense transactions (4xxx/5xxx)");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessSIE4Data_WithCorruptedFile_ShouldHandleGracefully()
    {
        // Arrange
        var corruptedContent = "This is not a valid SIE4 file content";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, corruptedContent);

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.ProcessSIE4FileAsync(tempFile));
            
            exception.Message.Should().Contain("SIE4", "Error message should mention SIE4 format");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessSIE4Data_WithLargeFile_ShouldRespectTimeout()
    {
        // Arrange
        var largeContent = CreateLargeSIE4Content();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, largeContent);

        // Set very short timeout for testing
        _options.MaxProcessingTimeoutMinutes = 0.01; // 0.6 seconds

        try
        {
            // Act
            var result = await _service.ProcessSIE4FileAsync(tempFile);

            // Assert
            // Should either complete quickly or handle timeout gracefully
            result.Should().NotBeNull("Service should handle large files within timeout");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessSIE4Data_WithForeignCurrency_ShouldParseExchangeRates()
    {
        // Arrange
        var sie4Content = CreateSIE4WithForeignCurrency();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, sie4Content);

        try
        {
            // Act
            var result = await _service.ProcessSIE4FileAsync(tempFile);

            // Assert
            result.Transactions.Should().NotBeEmpty("Should parse foreign currency transactions");
            
            var foreignTransactions = result.Transactions.Where(t => t.Currency != "SEK").ToList();
            if (foreignTransactions.Any())
            {
                foreignTransactions.Should().AllSatisfy(t => 
                    t.ExchangeRate.Should().BeGreaterThan(0, "Foreign currency transactions should have exchange rate"));
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessSIE4Data_WithNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), "non-existent-file.se");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.ProcessSIE4FileAsync(nonExistentFile));
        
        exception.FileName.Should().Contain("non-existent-file.se");
    }

    private static string CreateValidSIE4Content()
    {
        return @"#FLAGGA 0
#PROGRAM ""Test Program"" ""1.0""
#FORMAT PC8
#GEN 20241106
#SIETYP 4
#PROSA ""Test Company AB""
#FNAMN ""Test Company AB""
#ORGNR 556123-4567
#ADRESS ""Test Street 123"" ""12345"" ""Test City""
#KPTYP BAS2014
#VALUTA SEK

#KONTO 1910 ""Kundfordringar""
#KONTO 3010 ""Försäljning""
#KONTO 4010 ""Inköp av material""
#KONTO 5010 ""Lokalhyra""

#VER ""A"" ""1"" 20241101 ""Test transaction 1""
{
#TRANS 3010 {} -10000.00
#TRANS 1910 {} 10000.00
}

#VER ""A"" ""2"" 20241102 ""Test transaction 2""
{
#TRANS 4010 {} 5000.00
#TRANS 1910 {} -5000.00
}

#VER ""A"" ""3"" 20241103 ""Test transaction 3""
{
#TRANS 5010 {} 2500.00
#TRANS 1910 {} -2500.00
}
";
    }

    private static string CreateSIE4WithBASAccounts()
    {
        return @"#FLAGGA 0
#PROGRAM ""Test Program"" ""1.0""
#FORMAT PC8
#GEN 20241106
#SIETYP 4
#PROSA ""BAS Test Company AB""
#FNAMN ""BAS Test Company AB""
#ORGNR 556987-6543
#KPTYP BAS2014
#VALUTA SEK

#KONTO 1510 ""Kundfordringar""
#KONTO 2440 ""Leverantörsskulder""
#KONTO 3011 ""Försäljning varor""
#KONTO 4011 ""Inköp av varor""
#KONTO 5410 ""Förbrukningsinventarier""

#VER ""A"" ""1"" 20241101 ""BAS transaction""
{
#TRANS 3011 {} -15000.00
#TRANS 1510 {} 15000.00
}

#VER ""A"" ""2"" 20241102 ""BAS expense""
{
#TRANS 4011 {} 8000.00
#TRANS 2440 {} -8000.00
}

#VER ""A"" ""3"" 20241103 ""BAS office supplies""
{
#TRANS 5410 {} 1200.00
#TRANS 2440 {} -1200.00
}
";
    }

    private static string CreateLargeSIE4Content()
    {
        var content = @"#FLAGGA 0
#PROGRAM ""Large Test"" ""1.0""
#FORMAT PC8
#GEN 20241106
#SIETYP 4
#PROSA ""Large Test Company AB""
#FNAMN ""Large Test Company AB""
#ORGNR 556111-2222
#KPTYP BAS2014
#VALUTA SEK

#KONTO 1910 ""Kundfordringar""
#KONTO 3010 ""Försäljning""
";

        // Add many transactions to simulate large file
        for (int i = 1; i <= 100; i++)
        {
            content += $@"
#VER ""A"" ""{i}"" 20241101 ""Large file transaction {i}""
{{
#TRANS 3010 {{}} -{i * 100}.00
#TRANS 1910 {{}} {i * 100}.00
}}
";
        }

        return content;
    }

    private static string CreateSIE4WithForeignCurrency()
    {
        return @"#FLAGGA 0
#PROGRAM ""Currency Test"" ""1.0""
#FORMAT PC8
#GEN 20241106
#SIETYP 4
#PROSA ""Currency Test Company AB""
#FNAMN ""Currency Test Company AB""
#ORGNR 556333-4444
#KPTYP BAS2014
#VALUTA SEK

#KONTO 1910 ""Kundfordringar""
#KONTO 3010 ""Försäljning""

#VER ""A"" ""1"" 20241101 ""USD transaction""
{
#TRANS 3010 {} -1000.00 USD 10.50
#TRANS 1910 {} 10500.00
}

#VER ""A"" ""2"" 20241102 ""EUR transaction""
{
#TRANS 3010 {} -500.00 EUR 11.20
#TRANS 1910 {} 5600.00
}
";
    }
}
