using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using FakeItEasy;
using FluentAssertions;
using Agent.Configuration;
using Agent.Data;
using Agent.Models;
using Agent.Services;
using Agent.Services.Interfaces;

namespace Agent.Tests.Services;

public class ProductionMemoryServiceTests : IDisposable
{
    private readonly MemoryDbContext _context;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
    private readonly ILogger<ProductionMemoryService> _logger;
    private readonly ProductionMemoryService _service;

    public ProductionMemoryServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MemoryDbContext(options);
        _embeddingService = A.Fake<IEmbeddingGenerator<string, Embedding<float>>>();
        _logger = A.Fake<ILogger<ProductionMemoryService>>();

        var llmOptions = Options.Create(new LiteLLMOptions { EmbeddingDimensions = 3 });
        _service = new ProductionMemoryService(_context, _embeddingService, llmOptions, _logger);
        
        // Setup fake embedding service
        A.CallTo(() => _embeddingService.GenerateAsync(A<IEnumerable<string>>._, A<EmbeddingGenerationOptions>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(new[] { new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f }) })));
    }

    [Fact]
    public async Task StoreMemoryAsync_Should_StoreMemoryWithEmbedding()
    {
        // Arrange
        var memory = new MemoryEntry
        {
            Content = "Test memory content",
            Type = MemoryType.Facts,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public
        };

        // Act
        await _service.StoreMemoryAsync(memory);

        // Assert
        var storedMemory = await _context.Set<MemoryEntry>().FirstOrDefaultAsync();
        storedMemory.Should().NotBeNull();
        storedMemory!.Content.Should().Be("Test memory content");
        storedMemory.EmbeddingVector.Should().NotBeNullOrEmpty();
        storedMemory.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RetrieveMemoriesAsync_Should_ReturnMemoriesAboveThreshold()
    {
        // Arrange
        var memory1 = new MemoryEntry
        {
            Content = "Similar content",
            Type = MemoryType.Facts,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public,
            EmbeddingVector = "[0.1, 0.2, 0.3]"
        };
        
        var memory2 = new MemoryEntry
        {
            Content = "Different content",
            Type = MemoryType.Facts,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public,
            EmbeddingVector = "[0.9, 0.8, 0.7]"
        };

        await _context.Set<MemoryEntry>().AddRangeAsync(memory1, memory2);
        await _context.SaveChangesAsync();

        // Act
        var results = await _service.RetrieveMemoriesAsync("test query", MemoryType.Facts, 0.5, 5);

        // Assert
        results.Should().NotBeEmpty();
        A.CallTo(() => _embeddingService.GenerateAsync(A<IEnumerable<string>>._, A<EmbeddingGenerationOptions>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetOrCreateUserAsync_Should_CreateNewUserForNewDevice()
    {
        // Arrange
        var deviceFingerprint = "test-device-123";

        // Act
        var user = await _service.GetOrCreateUserAsync(deviceFingerprint);

        // Assert
        user.Should().NotBeNull();
        user.UserId.Should().NotBeEmpty();
        
        var device = await _context.Set<Device>().FirstOrDefaultAsync(d => d.DeviceId == deviceFingerprint);
        device.Should().NotBeNull();
        device!.PrimaryUserId.Should().Be(user.UserId);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_Should_ReturnExistingUserForKnownDevice()
    {
        // Arrange
        var deviceFingerprint = "test-device-123";
        var existingUser = new User();
        var existingDevice = new Device { DeviceId = deviceFingerprint, PrimaryUserId = existingUser.UserId };
        var deviceUser = new DeviceUser
        {
            DeviceId = deviceFingerprint,
            UserId = existingUser.UserId,
            User = existingUser,
            Confidence = 1.0
        };

        await _context.Set<User>().AddAsync(existingUser);
        await _context.Set<Device>().AddAsync(existingDevice);
        await _context.Set<DeviceUser>().AddAsync(deviceUser);
        await _context.SaveChangesAsync();

        // Act
        var user = await _service.GetOrCreateUserAsync(deviceFingerprint);

        // Assert
        user.UserId.Should().Be(existingUser.UserId);
    }

    [Fact]
    public async Task HasConsentAsync_Should_ReturnTrueWhenConsentGranted()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var consent = new Consent
        {
            UserId = userId,
            Kind = "test_consent",
            Granted = true
        };
        
        await _context.Set<Consent>().AddAsync(consent);
        await _context.SaveChangesAsync();

        // Act
        var hasConsent = await _service.HasConsentAsync(userId, "test_consent");

        // Assert
        hasConsent.Should().BeTrue();
    }

    [Fact]
    public async Task HasConsentAsync_Should_ReturnFalseWhenConsentNotGranted()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act
        var hasConsent = await _service.HasConsentAsync(userId, "test_consent");

        // Assert
        hasConsent.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateConsentAsync_Should_CreateNewConsentWhenNotExists()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act
        await _service.UpdateConsentAsync(userId, "test_consent", true);

        // Assert
        var consent = await _context.Set<Consent>().FirstOrDefaultAsync();
        consent.Should().NotBeNull();
        consent!.UserId.Should().Be(userId);
        consent.Kind.Should().Be("test_consent");
        consent.Granted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConsentAsync_Should_UpdateExistingConsent()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var existingConsent = new Consent
        {
            UserId = userId,
            Kind = "test_consent",
            Granted = false
        };
        
        await _context.Set<Consent>().AddAsync(existingConsent);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateConsentAsync(userId, "test_consent", true);

        // Assert
        var consent = await _context.Set<Consent>().FirstOrDefaultAsync();
        consent.Should().NotBeNull();
        consent!.Granted.Should().BeTrue();
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task GenerateSystemPromptAsync_Should_IncludeMemoryContext()
    {
        // Arrange
        var deviceFingerprint = "test-device-123";
        var user = new User { DisplayName = "Anna Andersson" };
        var device = new Device { DeviceId = deviceFingerprint, PrimaryUserId = user.UserId };
        var deviceUser = new DeviceUser
        {
            DeviceId = deviceFingerprint,
            UserId = user.UserId,
            User = user,
            Confidence = 1.0
        };

        await _context.Set<User>().AddAsync(user);
        await _context.Set<Device>().AddAsync(device);
        await _context.Set<DeviceUser>().AddAsync(deviceUser);

        var memory = new MemoryEntry
        {
            Content = "User name is Anna",
            Type = MemoryType.Identity,
            Scope = MemoryScope.UserPrivate,
            Confidentiality = Confidentiality.Private,
            OwnerUserId = user.UserId,
            EmbeddingVector = "[0.1, 0.2, 0.3]"
        };

        await _context.Set<MemoryEntry>().AddAsync(memory);
        await _context.SaveChangesAsync();

        // Act
        var systemPrompt = await _service.GenerateSystemPromptAsync(deviceFingerprint);

        // Assert
        systemPrompt.Should().NotBeNullOrEmpty();
        systemPrompt.Should().Contain("[SYSTEM - Pinned Profile]");
        systemPrompt.Should().Contain("Anna");
    }

    [Fact]
    public async Task RetrieveMemoriesAsync_Should_RespectAccessControl()
    {
        // Arrange
        var user1Id = Guid.NewGuid().ToString();
        var user2Id = Guid.NewGuid().ToString();
        
        var privateMemory = new MemoryEntry
        {
            Content = "Private information",
            Type = MemoryType.Facts,
            Scope = MemoryScope.UserPrivate,
            Confidentiality = Confidentiality.Private,
            OwnerUserId = user1Id,
            EmbeddingVector = "[0.1, 0.2, 0.3]"
        };
        
        var publicMemory = new MemoryEntry
        {
            Content = "Public information",
            Type = MemoryType.Facts,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public,
            OwnerUserId = user1Id,
            EmbeddingVector = "[0.1, 0.2, 0.3]"
        };

        await _context.Set<MemoryEntry>().AddRangeAsync(privateMemory, publicMemory);
        await _context.SaveChangesAsync();

        // Act - User2 tries to access memories
        var results = await _service.RetrieveMemoriesAsync("information", MemoryType.Facts, 0.1, 5, user2Id);

        // Assert - Should only get public memory, not private
        results.Should().HaveCount(1);
        results.First().Content.Should().Be("Public information");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
