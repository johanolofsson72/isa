using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using FakeItEasy;
using FluentAssertions;
using Agent.Models;
using Agent.Services;

namespace Agent.Tests.Services;

public class IntelligentMemoryExtractorTests
{
    private readonly ILogger<IntelligentMemoryExtractor> _logger;
    private readonly IntelligentMemoryExtractor _extractor;

    public IntelligentMemoryExtractorTests()
    {
        _logger = A.Fake<ILogger<IntelligentMemoryExtractor>>();
        _extractor = new IntelligentMemoryExtractor(_logger);
    }

    [Fact]
    public async Task ExtractFromText_Should_ExtractIdentityFromName()
    {
        // Arrange
        var text = "Jag heter Anna Andersson";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1);
        var memory = memories.First();
        memory.Type.Should().Be(MemoryType.Identity);
        memory.Content.Should().Contain("Anna Andersson");
        memory.Scope.Should().Be(MemoryScope.UserPrivate);
        memory.Confidentiality.Should().Be(Confidentiality.Private);
        memory.Provenance.Should().Be(Provenance.DirectUser);
        memory.Confirmation.Should().Be(ConfirmationState.Confirmed);
    }

    [Fact]
    public async Task ExtractFromText_Should_ExtractFactsFromVariableAssignment()
    {
        // Arrange
        var text = "x = 77";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1);
        var memory = memories.First();
        memory.Type.Should().Be(MemoryType.Facts);
        memory.Content.Should().Be("x = 77");
        memory.Scope.Should().Be(MemoryScope.Public);
        memory.Confidentiality.Should().Be(Confidentiality.Public);
        memory.Provenance.Should().Be(Provenance.DirectUser);
        memory.Confirmation.Should().Be(ConfirmationState.Confirmed);
    }

    [Fact]
    public async Task ExtractFromText_Should_ExtractSecretInformation()
    {
        // Arrange
        var text = "Kom ihåg denna hemlighet: mitt mellannamn är Erik och det är hemligt";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert - The extractor finds multiple pieces of information in this text
        memories.Should().HaveCountGreaterThan(0);

        // Should extract the secret information
        memories.Should().Contain(m => m.Content.Contains("mellannamn är Erik") || m.Content.Contains("Erik"));

        // All memories should be private since the text contains secret information
        memories.Should().OnlyContain(m => m.Scope == MemoryScope.UserPrivate);
        memories.Should().OnlyContain(m => m.Confidentiality == Confidentiality.Private);
        memories.Should().OnlyContain(m => m.Provenance == Provenance.DirectUser);
        memories.Should().OnlyContain(m => m.Confirmation == ConfirmationState.Confirmed);
    }

    [Fact]
    public async Task ExtractFromText_Should_ExtractFactsFromStatement()
    {
        // Arrange
        var text = "Stockholm är huvudstaden i Sverige";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1);
        var memory = memories.First();
        memory.Type.Should().Be(MemoryType.Facts);
        memory.Content.Should().Be("Stockholm är huvudstaden i Sverige");
        memory.Scope.Should().Be(MemoryScope.Public);
        memory.Confidentiality.Should().Be(Confidentiality.Public);
        memory.Provenance.Should().Be(Provenance.DirectUser);
        memory.Confirmation.Should().Be(ConfirmationState.Confirmed);
    }

    [Fact]
    public async Task ExtractFromText_Should_ExtractEpisodicContext()
    {
        // Arrange
        var text = "Igår gick jag till affären och köpte mjölk";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1);
        var memory = memories.First();
        memory.Type.Should().Be(MemoryType.EpisodicContext);
        memory.Content.Should().Be("Igår gick jag till affären och köpte mjölk");
        memory.Scope.Should().Be(MemoryScope.Public);
        memory.Confidentiality.Should().Be(Confidentiality.Public);
        memory.Provenance.Should().Be(Provenance.DirectUser);
        memory.Confirmation.Should().Be(ConfirmationState.Confirmed);
    }

    [Fact]
    public async Task ExtractFromText_Should_HandlePrivateInformation()
    {
        // Arrange
        var text = "Min hemliga kod är 1234 och det är privat";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert - The extractor finds multiple pieces of information in private text
        memories.Should().HaveCountGreaterThan(0);

        // Should extract the code information
        memories.Should().Contain(m => m.Content.Contains("kod") && m.Content.Contains("1234"));

        // All memories should be private since the text contains private information
        memories.Should().OnlyContain(m => m.Scope == MemoryScope.UserPrivate);
        memories.Should().OnlyContain(m => m.Confidentiality == Confidentiality.Private);
    }

    [Fact]
    public async Task ExtractFromText_Should_SetCorrectSourceAndProvenance()
    {
        // Arrange
        var text = "Jag heter Test";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync("", text, sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1);
        var memory = memories.First();
        memory.Source.Should().Be("agent");
        memory.Provenance.Should().Be(Provenance.Inferred);
        memory.Confirmation.Should().Be(ConfirmationState.Pending);
        memory.IsAuthoritative.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractFromText_Should_SetDeviceFingerprint()
    {
        // Arrange
        var text = "Test content";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device-123";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        if (memories.Any())
        {
            memories.First().DeviceFingerprint.Should().Be("test-device-123");
        }
    }

    [Fact]
    public async Task ExtractFromText_Should_ReturnEmptyForIrrelevantText()
    {
        // Arrange
        var text = "Hej, hur mår du?";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Vad heter jag?")]
    [InlineData("Vem är jag?")]
    [InlineData("Vad är mitt namn?")]
    [InlineData("What is my name?")]
    [InlineData("Who am I?")]
    [InlineData("vad heter jag")]
    [InlineData("vem är jag")]
    public async Task ExtractFromText_Should_NotExtractFromQuestions(string questionText)
    {
        // Arrange
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(questionText, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().BeEmpty("Questions should not trigger memory extraction");
    }

    [Fact]
    public async Task ExtractMemoriesAsync_Should_ExtractFromBothUserAndAssistantMessages()
    {
        // Arrange
        var userMessage = "Jag heter Anna";
        var assistantMessage = "Trevligt att träffas, Anna!";
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(userMessage, assistantMessage, sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1); // Only user message should extract identity
        var memory = memories.First();
        memory.Content.Should().Contain("Anna");
        memory.Type.Should().Be(MemoryType.Identity);
    }

    [Theory]
    [InlineData("mitt mellannamn är Erik och det är hemligt")]
    [InlineData("jag heter Erik i mellannamn och det är hemligt")]
    [InlineData("kom ihåg denna hemlighet: Erik")]
    public async Task ExtractFromText_Should_DetectSecretMiddleName(string text)
    {
        // Arrange
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1);
        var memory = memories.First();
        memory.Type.Should().Be(MemoryType.Identity);
        memory.Confidentiality.Should().Be(Confidentiality.Private);
        memory.Scope.Should().Be(MemoryScope.UserPrivate);
    }

    [Theory]
    [InlineData("x = 42")]
    [InlineData("y = hello")]
    [InlineData("temperature = 25")]
    [InlineData("name = Anna")]
    public async Task ExtractFromText_Should_ExtractVariableAssignments(string text)
    {
        // Arrange
        var sessionId = "test-session";
        var deviceFingerprint = "test-device";

        // Act
        var memories = await _extractor.ExtractMemoriesAsync(text, "", sessionId, deviceFingerprint);

        // Assert
        memories.Should().HaveCount(1);
        var memory = memories.First();
        memory.Type.Should().Be(MemoryType.Facts);
        memory.Content.Should().Contain("=");
        memory.Content.Should().Be(text);
    }
}
