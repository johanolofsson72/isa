using AnalysisPlugin.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using System.Text;
using Xunit;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class FortnoxPluginIntegrationTests
{
    private readonly SIE4ProcessorService _service;
    private readonly Mock<ILogger<SIE4ProcessorService>> _mockLogger;
    private readonly Mock<Kernel> _mockKernel;

    public FortnoxPluginIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<SIE4ProcessorService>>();
        _mockKernel = new Mock<Kernel>();
        _service = new SIE4ProcessorService(_mockLogger.Object, _mockKernel.Object);
    }

    [Fact]
    public async Task RetrieveAndProcessSIE4DataAsync_WithValidFortnoxData_ReturnsTransactions()
    {
        // Arrange
        var mockSIE4Data = CreateMockSIE4Data();
        var mockKernelResult = new Mock<FunctionResult>();
        mockKernelResult.Setup(r => r.GetValue<string>()).Returns(mockSIE4Data);

        var mockFunction = new Mock<KernelFunction>();
        var mockPlugin = new Mock<KernelPlugin>();
        mockPlugin.Setup(p => p["GetSIEAsync"]).Returns(mockFunction.Object);
        
        _mockKernel.Setup(k => k.Plugins["FortnoxPlugin"]).Returns(mockPlugin.Object);
        _mockKernel.Setup(k => k.InvokeAsync(It.IsAny<KernelFunction>(), It.IsAny<KernelArguments>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(mockKernelResult.Object);

        // Act
        var result = await _service.RetrieveAndProcessSIE4DataAsync(2024);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var transactions = result.ToList();
        Assert.Equal(2, transactions.Count); // Based on mock data
        
        // Verify BAS classification
        var assetTransaction = transactions.FirstOrDefault(t => t.AccountCode == "1930");
        Assert.NotNull(assetTransaction);
        Assert.Equal("Assets", assetTransaction.AccountCategory);
        Assert.Equal("Current Assets", assetTransaction.AccountSubCategory);
        
        var revenueTransaction = transactions.FirstOrDefault(t => t.AccountCode == "3001");
        Assert.NotNull(revenueTransaction);
        Assert.Equal("Revenue", revenueTransaction.AccountCategory);
        Assert.Equal("Sales Revenue", revenueTransaction.AccountSubCategory);
    }

    [Fact]
    public async Task RetrieveAndProcessSIE4DataAsync_FortnoxError_ReturnsEmpty()
    {
        // Arrange
        var mockKernelResult = new Mock<FunctionResult>();
        mockKernelResult.Setup(r => r.GetValue<string>()).Returns("ERROR: Authentication failed");

        var mockFunction = new Mock<KernelFunction>();
        var mockPlugin = new Mock<KernelPlugin>();
        mockPlugin.Setup(p => p["GetSIEAsync"]).Returns(mockFunction.Object);
        
        _mockKernel.Setup(k => k.Plugins["FortnoxPlugin"]).Returns(mockPlugin.Object);
        _mockKernel.Setup(k => k.InvokeAsync(It.IsAny<KernelFunction>(), It.IsAny<KernelArguments>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(mockKernelResult.Object);

        // Act
        var result = await _service.RetrieveAndProcessSIE4DataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ProcessSIE4DataAsync_WithTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var largeSIE4Data = CreateLargeMockSIE4Data(10000); // Large dataset
        var shortTimeout = 1; // 1 second timeout

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(
            () => _service.ProcessSIE4DataAsync(largeSIE4Data, shortTimeout));
    }

    [Fact]
    public async Task ProcessSIE4DataAsync_WithMemoryLimit_UsesStreamingApproach()
    {
        // Arrange
        var largeSIE4Data = CreateLargeMockSIE4Data(1000);
        var lowMemoryLimit = 1; // 1MB limit

        // Act
        var result = await _service.ProcessSIE4DataAsync(largeSIE4Data, 60, lowMemoryLimit);

        // Assert
        Assert.NotNull(result);
        // Should complete without throwing OutOfMemoryException
    }

    [Fact]
    public void ValidateSIE4Format_WithCorruptedData_ReturnsValidationErrors()
    {
        // Arrange
        var corruptedData = @"
#FLAGGA 0
#PROGRAM ""Fortnox"" ""4.0""
#FORMAT PC8
#VER ""A"" ""1"" 20240101 ""Test""
#TRANS ""1930"" invalid_amount
#TRANS ""3001"" 1000.00
";

        // Act
        var result = _service.ValidateSIE4Format(corruptedData);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid date formats"));
    }

    private string CreateMockSIE4Data()
    {
        return @"
#FLAGGA 0
#PROGRAM ""Fortnox"" ""4.0""
#FORMAT PC8
#KONTO ""1930"" ""Bank""
#KONTO ""3001"" ""Sales""
#VER ""A"" ""1"" 20240101 ""Opening balance""
#TRANS ""1930"" 10000.00
#TRANS ""3001"" -10000.00
#VER ""A"" ""2"" 20240102 ""Sales transaction""
#TRANS ""1930"" 1000.00
#TRANS ""3001"" -1000.00
";
    }

    private string CreateLargeMockSIE4Data(int transactionCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#FLAGGA 0");
        sb.AppendLine("#PROGRAM \"Test\" \"1.0\"");
        sb.AppendLine("#FORMAT PC8");
        sb.AppendLine("#KONTO \"1930\" \"Bank\"");
        sb.AppendLine("#KONTO \"3001\" \"Sales\"");

        for (int i = 1; i <= transactionCount; i++)
        {
            sb.AppendLine($"#VER \"A\" \"{i}\" 20240101 \"Transaction {i}\"");
            sb.AppendLine($"#TRANS \"1930\" {i * 100}.00");
            sb.AppendLine($"#TRANS \"3001\" -{i * 100}.00");
        }

        return sb.ToString();
    }
}
