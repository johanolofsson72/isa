using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using WebChat.Services;
using WebChat.Services.Interfaces;
using FakeItEasy;

namespace WebChat.Tests;

public class SessionFileServiceTests
{
    [Fact]
    public async Task UploadFileAsync_ValidFile_ReturnsFileId()
    {
        // Arrange
        var httpClientFactory = A.Fake<IHttpClientFactory>();
        var logger = A.Fake<ILogger<SessionFileService>>();
        var service = new SessionFileService(httpClientFactory, logger);

        var mockFile = A.Fake<IFormFile>();
        A.CallTo(() => mockFile.FileName).Returns("test.pdf");
        A.CallTo(() => mockFile.Length).Returns(1024);
        A.CallTo(() => mockFile.ContentType).Returns("application/pdf");
        A.CallTo(() => mockFile.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));
        
        var sessionId = "test-session";

        // Act
        var fileId = await service.UploadFileAsync(sessionId, mockFile);

        // Assert
        Assert.NotNull(fileId);
        Assert.True(Guid.TryParse(fileId, out _));
        
        var files = await service.GetSessionFilesAsync(sessionId);
        Assert.Single(files);
        Assert.Equal("test.pdf", files[0].FileName);
    }

    [Fact]
    public async Task ClearSessionFilesAsync_ExistingFiles_RemovesAllFiles()
    {
        // Arrange
        var httpClientFactory = A.Fake<IHttpClientFactory>();
        var logger = A.Fake<ILogger<SessionFileService>>();
        var service = new SessionFileService(httpClientFactory, logger);

        var mockFile = A.Fake<IFormFile>();
        A.CallTo(() => mockFile.FileName).Returns("test.pdf");
        A.CallTo(() => mockFile.Length).Returns(1024);
        A.CallTo(() => mockFile.ContentType).Returns("application/pdf");
        A.CallTo(() => mockFile.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));

        var sessionId = "test-session";
        await service.UploadFileAsync(sessionId, mockFile);

        // Act
        await service.ClearSessionFilesAsync(sessionId);

        // Assert
        var files = await service.GetSessionFilesAsync(sessionId);
        Assert.Empty(files);
    }

    [Fact]
    public async Task DeleteFileAsync_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var httpClientFactory = A.Fake<IHttpClientFactory>();
        var logger = A.Fake<ILogger<SessionFileService>>();
        var service = new SessionFileService(httpClientFactory, logger);

        var mockFile = A.Fake<IFormFile>();
        A.CallTo(() => mockFile.FileName).Returns("test.pdf");
        A.CallTo(() => mockFile.Length).Returns(1024);
        A.CallTo(() => mockFile.ContentType).Returns("application/pdf");
        A.CallTo(() => mockFile.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));

        var sessionId = "test-session";
        var fileId = await service.UploadFileAsync(sessionId, mockFile);

        // Act
        var result = await service.DeleteFileAsync(sessionId, fileId);

        // Assert
        Assert.True(result);
        var files = await service.GetSessionFilesAsync(sessionId);
        Assert.Empty(files);
    }
}