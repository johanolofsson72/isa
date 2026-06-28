using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using FakeItEasy;
using FluentAssertions;
using Agent.Models;
using Agent.Services;
using Agent.Services.Interfaces;

namespace Agent.Tests.Services;

public class OnlineSearchServiceTests
{
    private readonly IProductionMemoryService _memoryService;
    private readonly Kernel _kernel;
    private readonly ILogger<OnlineSearchService> _logger;
    private readonly IOnlineSearchService _service;

    public OnlineSearchServiceTests()
    {
        _memoryService = A.Fake<IProductionMemoryService>();

        // Create a real Kernel instance for testing since Kernel is sealed and cannot be faked
        var kernelBuilder = Kernel.CreateBuilder();
        _kernel = kernelBuilder.Build();

        _logger = A.Fake<ILogger<OnlineSearchService>>();

        _service = new OnlineSearchService(_memoryService, _kernel, _logger);
    }

    [Fact]
    public async Task CanSearchTodayAsync_Should_ReturnFalseWhenNoConsent()
    {
        // Arrange
        var userId = "test-user";
        A.CallTo(() => _memoryService.HasConsentAsync(userId, "online_search"))
            .Returns(false);

        // Act
        var canSearch = await _service.CanSearchTodayAsync(userId);

        // Assert
        canSearch.Should().BeFalse();
    }

    [Fact]
    public async Task CanSearchTodayAsync_Should_ReturnTrueWhenConsentAndUnderLimit()
    {
        // Arrange
        var userId = "test-user";
        var runtimeState = new UserRuntimeState
        {
            UserId = userId,
            OnlineSearchCount = 0,
            LastOnlineSearchDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        A.CallTo(() => _memoryService.HasConsentAsync(userId, "online_search"))
            .Returns(true);
        A.CallTo(() => _memoryService.GetOrCreateUserRuntimeStateAsync(userId))
            .Returns(runtimeState);

        // Act
        var canSearch = await _service.CanSearchTodayAsync(userId);

        // Assert
        canSearch.Should().BeTrue();
    }

    [Fact]
    public async Task CanSearchTodayAsync_Should_ReturnFalseWhenLimitReached()
    {
        // Arrange
        var userId = "test-user";
        var runtimeState = new UserRuntimeState
        {
            UserId = userId,
            OnlineSearchCount = 1, // Already at limit
            LastOnlineSearchDate = DateTime.UtcNow.Date
        };

        A.CallTo(() => _memoryService.HasConsentAsync(userId, "online_search"))
            .Returns(true);
        A.CallTo(() => _memoryService.GetOrCreateUserRuntimeStateAsync(userId))
            .Returns(runtimeState);

        // Act
        var canSearch = await _service.CanSearchTodayAsync(userId);

        // Assert
        canSearch.Should().BeFalse();
    }

    [Fact]
    public async Task RequestOnlineSearchConsentAsync_Should_ReturnConsentMessage()
    {
        // Arrange
        var userId = "test-user";
        A.CallTo(() => _memoryService.HasConsentAsync(userId, "online_search"))
            .Returns(false);

        // Act
        var message = await _service.RequestOnlineSearchConsentAsync(userId);

        // Assert
        message.Should().Contain("Online-sökning med samtycke");
        message.Should().Contain("Vill du ge samtycke");
    }

    [Fact]
    public async Task RequestOnlineSearchConsentAsync_Should_ReturnAlreadyGrantedMessage()
    {
        // Arrange
        var userId = "test-user";
        A.CallTo(() => _memoryService.HasConsentAsync(userId, "online_search"))
            .Returns(true);

        // Act
        var message = await _service.RequestOnlineSearchConsentAsync(userId);

        // Assert
        message.Should().Contain("redan gett samtycke");
    }

    [Fact]
    public async Task ProcessConsentResponseAsync_Should_GrantConsentForYes()
    {
        // Arrange
        var userId = "test-user";
        var response = "ja";

        // Act
        var message = await _service.ProcessConsentResponseAsync(userId, response);

        // Assert
        message.Should().Contain("Tack! Du har gett samtycke");
        A.CallTo(() => _memoryService.UpdateConsentAsync(userId, "online_search", true))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessConsentResponseAsync_Should_DenyConsentForNo()
    {
        // Arrange
        var userId = "test-user";
        var response = "nej";

        // Act
        var message = await _service.ProcessConsentResponseAsync(userId, response);

        // Assert
        message.Should().Contain("kommer inte att söka online");
        A.CallTo(() => _memoryService.UpdateConsentAsync(userId, "online_search", false))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("ja")]
    [InlineData("yes")]
    [InlineData("ok")]
    [InlineData("godkänn")]
    public async Task ProcessConsentResponseAsync_Should_RecognizePositiveResponses(string response)
    {
        // Arrange
        var userId = "test-user";

        // Act
        var message = await _service.ProcessConsentResponseAsync(userId, response);

        // Assert
        message.Should().Contain("Tack! Du har gett samtycke");
        A.CallTo(() => _memoryService.UpdateConsentAsync(userId, "online_search", true))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ConfirmSearchResultsAsync_Should_ConfirmPendingMemories()
    {
        // Arrange
        var userId = "test-user";
        var pendingMemories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Content = "Search result 1",
                Provenance = Provenance.ExternalSearch,
                Confirmation = ConfirmationState.Pending
            }
        };

        A.CallTo(() => _memoryService.RetrieveMemoriesAsync(
            A<string>._, MemoryType.Facts, A<double>._, A<int>._, userId))
            .Returns(pendingMemories);

        // Act
        var message = await _service.ConfirmSearchResultsAsync(userId, true);

        // Assert
        message.Should().Contain("Bekräftat 1 sökresultat");
        pendingMemories.First().Confirmation.Should().Be(ConfirmationState.Confirmed);
        A.CallTo(() => _memoryService.StoreMemoryAsync(A<MemoryEntry>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ConfirmSearchResultsAsync_Should_RejectPendingMemories()
    {
        // Arrange
        var userId = "test-user";
        var pendingMemories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Content = "Search result 1",
                Provenance = Provenance.ExternalSearch,
                Confirmation = ConfirmationState.Pending
            }
        };

        A.CallTo(() => _memoryService.RetrieveMemoriesAsync(
            A<string>._, MemoryType.Facts, A<double>._, A<int>._, userId))
            .Returns(pendingMemories);

        // Act
        var message = await _service.ConfirmSearchResultsAsync(userId, false);

        // Assert
        message.Should().Contain("Avvisat 1 sökresultat");
        pendingMemories.First().Confirmation.Should().Be(ConfirmationState.Rejected);
        pendingMemories.First().TombstonedAt.Should().NotBeNull();
        A.CallTo(() => _memoryService.StoreMemoryAsync(A<MemoryEntry>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetSearchStatusAsync_Should_ReturnInactiveWhenNoConsent()
    {
        // Arrange
        var userId = "test-user";
        A.CallTo(() => _memoryService.HasConsentAsync(userId, "online_search"))
            .Returns(false);

        // Act
        var status = await _service.GetSearchStatusAsync(userId);

        // Assert
        status.Should().Contain("Inaktiverad (inget samtycke)");
    }

    [Fact]
    public async Task GetSearchStatusAsync_Should_ReturnActiveWithSearchesLeft()
    {
        // Arrange
        var userId = "test-user";
        var runtimeState = new UserRuntimeState
        {
            UserId = userId,
            OnlineSearchCount = 0,
            LastOnlineSearchDate = DateTime.UtcNow.Date
        };

        A.CallTo(() => _memoryService.HasConsentAsync(userId, "online_search"))
            .Returns(true);
        A.CallTo(() => _memoryService.GetOrCreateUserRuntimeStateAsync(userId))
            .Returns(runtimeState);

        // Act
        var status = await _service.GetSearchStatusAsync(userId);

        // Assert
        status.Should().Contain("Aktiverad (1/1 sökningar kvar idag)");
    }
}
