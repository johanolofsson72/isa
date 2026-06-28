using Xunit;
using FluentAssertions;
using FakeItEasy;
using WebChat.Services;
using WebChat.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace WebChat.Tests;

public class WebChatTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Placeholder_ShouldPass_WhenProjectBuilds()
    {
        true.Should().BeTrue();
    }
}

public class MemoryQueryServiceTests
{
    [Fact]
    public async Task GetPersonalInfoAsync_Should_ReturnNull_WhenNoUserFound()
    {
        // Arrange
        var httpClientFactory = A.Fake<IHttpClientFactory>();
        var logger = A.Fake<ILogger<MemoryQueryService>>();
        var httpClient = A.Fake<HttpClient>();

        A.CallTo(() => httpClientFactory.CreateClient("agent")).Returns(httpClient);

        var service = new MemoryQueryService(httpClientFactory, logger);

        // Act
        var result = await service.GetPersonalInfoAsync("test-device-fingerprint");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void PersonalInfo_Should_HaveCorrectProperties()
    {
        // Arrange & Act
        var personalInfo = new PersonalInfo
        {
            UserId = "test-user-id",
            DisplayName = "Anna Andersson",
            FirstName = "Anna"
        };

        // Assert
        personalInfo.UserId.Should().Be("test-user-id");
        personalInfo.DisplayName.Should().Be("Anna Andersson");
        personalInfo.FirstName.Should().Be("Anna");
    }
}

