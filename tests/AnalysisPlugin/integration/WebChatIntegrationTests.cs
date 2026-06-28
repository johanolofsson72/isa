using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using AnalysisPlugin.Services;
using System.Threading.Tasks;
using System;
using Moq;

namespace AnalysisPlugin.Tests.Integration;

/// <summary>
/// Integration tests för WebChat-flödet som genererar riktiga rapporter
/// </summary>
[Trait("Category", "Integration")]
public class WebChatIntegrationTests
{
    private readonly Mock<ILogger<MinimalWebChatService>> _loggerMock;
    private readonly MinimalWebChatService _webChatService;

    public WebChatIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<MinimalWebChatService>>();
        _webChatService = new MinimalWebChatService(_loggerMock.Object);
    }

    [Fact]
    public async Task ProcessSwedishRequest_ProjectSummary_ShouldGenerateRealReport()
    {
        // Arrange
        var swedishRequest = "Jag vill att du skapar en otroligt snygg projektsammanfattning och skickar till demo@example.com som kan se samtliga projekt och som summerar intäkter och kostnader";
        var recipientEmail = "demo@example.com";

        // Act
        var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(swedishRequest, recipientEmail);

        // Assert
        result.Should().NotBeNull("Resultatet ska inte vara null");
        result.Should().Contain("✅", "Resultatet ska indikera framgång");
        result.Should().Contain("Projektsammanfattning", "Rätt rapporttyp ska identifieras");
        result.Should().Contain("demo@example.com", "E-postadressen ska inkluderas");
        result.Should().Contain(".xlsx", "En Excel-fil ska skapas");

        Console.WriteLine($"✅ RESULTAT: {result}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_PivotReport_ShouldGenerateRealReport()
    {
        // Arrange
        var swedishRequest = "Jag vill att du skapar en otroligt snygg pivot-rapport och skickar till demo@example.com som visar omsättning, varukostnad, övriga externa kostnader uppdelat på kvartal";
        var recipientEmail = "demo@example.com";

        // Act
        var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(swedishRequest, recipientEmail);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("✅");
        result.Should().Contain("Pivotrapport");
        result.Should().Contain("demo@example.com");

        Console.WriteLine($"✅ RESULTAT: {result}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_WeeklySummary_ShouldGenerateRealReport()
    {
        // Arrange
        var swedishRequest = "Jag vill att du skapar en otroligt snygg veckorapport och skickar till demo@example.com som visar omsättning, varukostnad, bruttovinst för förra veckan";
        var recipientEmail = "demo@example.com";

        // Act
        var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(swedishRequest, recipientEmail);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("✅");
        result.Should().Contain("Veckorapport");
        result.Should().Contain("demo@example.com");

        Console.WriteLine($"✅ RESULTAT: {result}");
    }

    [Theory]
    [InlineData("Skapa en projektsammanfattning för demo@example.com", "Projektsammanfattning")]
    [InlineData("Jag behöver en pivotrapport till demo@example.com", "Pivotrapport")]
    [InlineData("Kan du göra en månadsrapport för demo@example.com", "Månadsrapport")]
    [InlineData("Skicka en årsrapport till demo@example.com", "Årsrapport")]
    public async Task ProcessSwedishRequest_VariousReportTypes_ShouldIdentifyCorrectType(string request, string expectedType)
    {
        // Arrange
        var recipientEmail = "demo@example.com";

        // Act
        var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(request, recipientEmail);

        // Assert
        result.Should().Contain(expectedType, $"Ska identifiera rapporttyp '{expectedType}' från begäran '{request}'");
        result.Should().Contain("✅", "Ska lyckas generera rapport");

        Console.WriteLine($"✅ BEGÄRAN: {request}");
        Console.WriteLine($"✅ IDENTIFIERAD TYP: {expectedType}");
        Console.WriteLine($"✅ RESULTAT: {result}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_CompleteWorkflow_ShouldLogAllSteps()
    {
        // Arrange
        var swedishRequest = "Skapa en finansiell sammanfattning och skicka till demo@example.com";
        var recipientEmail = "demo@example.com";

        // Act
        var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(swedishRequest, recipientEmail);

        // Assert
        result.Should().Contain("✅");
        
        // Verifiera att alla steg loggades
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Bearbetar svensk begäran")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);

        Console.WriteLine($"✅ KOMPLETT WORKFLOW TESTAT: {result}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_ErrorHandling_ShouldReturnErrorMessage()
    {
        // Arrange - Skapa en service som kommer att kasta ett fel
        var faultyLogger = new Mock<ILogger<MinimalWebChatService>>();
        faultyLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Throws(new InvalidOperationException("Test error"));

        var faultyService = new MinimalWebChatService(faultyLogger.Object);

        // Act
        var result = await faultyService.ProcessSwedishRequestAndGenerateReportAsync("test", "test@example.com");

        // Assert
        result.Should().Contain("❌", "Ska returnera felmeddelande");
        result.Should().Contain("Test error", "Ska innehålla det ursprungliga felmeddelandet");

        Console.WriteLine($"✅ FELHANTERING TESTAD: {result}");
    }
}
