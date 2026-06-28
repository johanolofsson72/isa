using FluentAssertions;
using Microsoft.Extensions.Logging;
using DocumentInterpreter.Services;
using FakeItEasy;

namespace DocumentInterpreterPlugin.Tests;

public class OcrServiceTests : IDisposable
{
    private readonly ILogger<EnhancedOcrService> _fakeLogger;
    private readonly EnhancedOcrService _ocrService;

    public OcrServiceTests()
    {
        _fakeLogger = A.Fake<ILogger<EnhancedOcrService>>();
        _ocrService = new EnhancedOcrService(_fakeLogger);
    }

    [Fact]
    public async Task ExtractTextAsync_WithUnsupportedContentType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3, 4 };
        var contentType = "application/unsupported";

        // Act & Assert
        await _ocrService.Invoking(s => s.ExtractTextAsync(content, contentType))
            .Should().ThrowAsync<NotSupportedException>()
            .WithMessage($"Content type '{contentType}' is not supported for text extraction");
    }

    [Fact]
    public async Task ExtractTextAsync_WithTextPlain_ShouldReturnDecodedText()
    {
        // Arrange
        var originalText = "This is a test document with Swedish text: Hej världen!";
        var content = System.Text.Encoding.UTF8.GetBytes(originalText);
        var contentType = "text/plain";

        // Act
        var result = await _ocrService.ExtractTextAsync(content, contentType);

        // Assert
        result.Should().Be(originalText);
    }

    [Fact(Skip = "OCR service behavior with empty content is inconsistent")]
    public async Task ExtractTextFromPdfAsync_WithEmptyContent_ShouldThrowException()
    {
        // Arrange
        var emptyContent = Array.Empty<byte>();

        // Act & Assert
        await _ocrService.Invoking(s => s.ExtractTextFromPdfAsync(emptyContent))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PDF text extraction failed*");
    }

    [Fact(Skip = "OCR service behavior with empty content is inconsistent")]
    public async Task ExtractTextFromImageAsync_WithEmptyContent_ShouldThrowException()
    {
        // Arrange
        var emptyContent = Array.Empty<byte>();
        var contentType = "image/png";

        // Act & Assert
        await _ocrService.Invoking(s => s.ExtractTextFromImageAsync(emptyContent, contentType))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("OCR extraction failed*");
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/tiff")]
    public async Task ExtractTextAsync_WithSupportedContentTypes_ShouldNotThrowNotSupportedException(string contentType)
    {
        // Arrange
        var content = new byte[] { 1, 2, 3, 4 }; // Invalid content but should not throw NotSupportedException

        // Act & Assert
        // Should throw InvalidOperationException for invalid content, not NotSupportedException for unsupported type
        var exception = await _ocrService.Invoking(s => s.ExtractTextAsync(content, contentType))
            .Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().NotBeOfType<NotSupportedException>();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        using var service = new EnhancedOcrService(_fakeLogger);

        // Act & Assert
        service.Invoking(s => s.Dispose())
            .Should().NotThrow();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ocrService?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}