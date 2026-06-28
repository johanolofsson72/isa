using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WebChatDemo.Services;
using System.Threading.Tasks;
using System;
using Moq;
using System.IO;

namespace WebChatDemo.Tests;

/// <summary>
/// Integration tests för WebChat-flödet som genererar riktiga rapporter till demo@example.com
/// </summary>
[Trait("Category", "Integration")]
public class WebChatIntegrationTests
{
    private readonly Mock<ILogger<WebChatService>> _loggerMock;
    private readonly WebChatService _webChatService;

    public WebChatIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<WebChatService>>();
        _webChatService = new WebChatService(_loggerMock.Object);
    }

    [Fact]
    public async Task ProcessSwedishRequest_ProjectSummary_ShouldGenerateRealReportForJool()
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

        // Verifiera att filen faktiskt skapades
        var filePath = ExtractFilePathFromResult(result);
        File.Exists(filePath).Should().BeTrue("Excel-filen ska finnas på disk");

        Console.WriteLine($"✅ RESULTAT: {result}");
        Console.WriteLine($"📁 FIL SKAPAD: {filePath}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_PivotReport_ShouldGenerateRealReportForJool()
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

        var filePath = ExtractFilePathFromResult(result);
        File.Exists(filePath).Should().BeTrue("Excel-filen ska finnas på disk");

        Console.WriteLine($"✅ RESULTAT: {result}");
        Console.WriteLine($"📁 FIL SKAPAD: {filePath}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_WeeklySummary_ShouldGenerateRealReportForJool()
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

        var filePath = ExtractFilePathFromResult(result);
        File.Exists(filePath).Should().BeTrue("Excel-filen ska finnas på disk");

        Console.WriteLine($"✅ RESULTAT: {result}");
        Console.WriteLine($"📁 FIL SKAPAD: {filePath}");
    }

    [Theory]
    [InlineData("Skapa en projektsammanfattning för demo@example.com", "Projektsammanfattning")]
    [InlineData("Jag behöver en pivotrapport till demo@example.com", "Pivotrapport")]
    [InlineData("Kan du göra en månadsrapport för demo@example.com", "Månadsrapport")]
    [InlineData("Skicka en årsrapport till demo@example.com", "Årsrapport")]
    public async Task ProcessSwedishRequest_VariousReportTypes_ShouldIdentifyCorrectTypeAndCreateFiles(string request, string expectedType)
    {
        // Arrange
        var recipientEmail = "demo@example.com";

        // Act
        var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(request, recipientEmail);

        // Assert
        result.Should().Contain(expectedType, $"Ska identifiera rapporttyp '{expectedType}' från begäran '{request}'");
        result.Should().Contain("✅", "Ska lyckas generera rapport");

        var filePath = ExtractFilePathFromResult(result);
        File.Exists(filePath).Should().BeTrue($"Excel-fil ska skapas för {expectedType}");

        Console.WriteLine($"✅ BEGÄRAN: {request}");
        Console.WriteLine($"✅ IDENTIFIERAD TYP: {expectedType}");
        Console.WriteLine($"✅ RESULTAT: {result}");
        Console.WriteLine($"📁 FIL SKAPAD: {filePath}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_PerformanceTest_ShouldCompleteUnder5Seconds()
    {
        // Arrange
        var swedishRequest = "Skapa en finansiell sammanfattning och skicka till demo@example.com";
        var recipientEmail = "demo@example.com";

        // Act
        var startTime = DateTime.Now;
        var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(swedishRequest, recipientEmail);
        var duration = DateTime.Now - startTime;

        // Assert
        result.Should().Contain("✅");
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5), "Rapportgenerering ska ta mindre än 5 sekunder");

        Console.WriteLine($"✅ PRESTANDA TEST: {duration.TotalSeconds:F2} sekunder");
        Console.WriteLine($"✅ RESULTAT: {result}");
    }

    [Fact]
    public async Task ProcessSwedishRequest_MultipleReports_ShouldCreateUniqueFiles()
    {
        // Arrange
        var requests = new[]
        {
            "Skapa projektsammanfattning för demo@example.com",
            "Skapa månadsrapport för demo@example.com",
            "Skapa årsrapport för demo@example.com"
        };

        var filePaths = new List<string>();

        // Act
        foreach (var request in requests)
        {
            var result = await _webChatService.ProcessSwedishRequestAndGenerateReportAsync(request, "demo@example.com");
            result.Should().Contain("✅");
            
            var filePath = ExtractFilePathFromResult(result);
            filePaths.Add(filePath);
        }

        // Assert
        filePaths.Should().HaveCount(3, "Tre rapporter ska skapas");
        filePaths.Should().OnlyHaveUniqueItems("Alla filer ska ha unika namn");
        
        foreach (var filePath in filePaths)
        {
            File.Exists(filePath).Should().BeTrue($"Fil ska finnas: {filePath}");
        }

        Console.WriteLine($"✅ SKAPADE {filePaths.Count} UNIKA RAPPORTER:");
        foreach (var filePath in filePaths)
        {
            Console.WriteLine($"📁 {Path.GetFileName(filePath)}");
        }
    }

    private string ExtractFilePathFromResult(string result)
    {
        // Extrahera filsökvägen från resultatsträngen
        var startIndex = result.LastIndexOf("Fil: ") + 5;
        if (startIndex < 5) return string.Empty;

        var filePath = result.Substring(startIndex);

        // Verifiera att filen finns i analysis_reports mappen
        if (filePath.Contains("/path/to/accountingagent/analysis_reports/"))
        {
            Console.WriteLine($"📁 Rapport sparad i korrekt mapp: {Path.GetFileName(filePath)}");
        }

        return filePath;
    }
}
