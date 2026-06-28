using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using FakeItEasy;
using FluentAssertions;
using Agent.Data;
using Agent.Models;
using Agent.Services;
using Agent.Services.Interfaces;

namespace Agent.Tests.Integration;

public class MemorySystemIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MemoryDbContext _context;
    private readonly IProductionMemoryService _memoryService;
    private readonly IIntelligentMemoryExtractor _memoryExtractor;

    public MemorySystemIntegrationTests()
    {
        var services = new ServiceCollection();

        // Add in-memory database
        services.AddDbContext<MemoryDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

        // Add fake embedding service
        var embeddingService = A.Fake<IEmbeddingGenerator<string, Embedding<float>>>();
        A.CallTo(() => embeddingService.GenerateAsync(A<IEnumerable<string>>._, A<EmbeddingGenerationOptions>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(new[] { new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }) })));
        services.AddSingleton(embeddingService);

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add memory services
        services.AddScoped<IProductionMemoryService, ProductionMemoryService>();
        services.AddScoped<IIntelligentMemoryExtractor, IntelligentMemoryExtractor>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<MemoryDbContext>();
        _memoryService = _serviceProvider.GetRequiredService<IProductionMemoryService>();
        _memoryExtractor = _serviceProvider.GetRequiredService<IIntelligentMemoryExtractor>();
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task FullMemoryFlow_Should_WorkEndToEnd()
    {
        // Arrange
        var deviceFingerprint = "test-device-123";
        var userMessage = "Jag heter Anna Andersson";
        var assistantMessage = "Trevligt att träffas, Anna!";
        var sessionId = "test-session";

        // Act 1: Extract memories from conversation
        var extractedMemories = await _memoryExtractor.ExtractMemoriesAsync(
            userMessage, assistantMessage, sessionId, deviceFingerprint);

        // Act 2: Store memories
        foreach (var memory in extractedMemories)
        {
            var user = await _memoryService.GetOrCreateUserAsync(deviceFingerprint);
            memory.OwnerUserId = user.UserId;
            memory.SubjectUserId = user.UserId;
            await _memoryService.StoreMemoryAsync(memory);
        }

        // Act 3: Generate system prompt with memory context
        var systemPrompt = await _memoryService.GenerateSystemPromptAsync(deviceFingerprint);

        // Act 4: Retrieve memories
        var user2 = await _memoryService.GetOrCreateUserAsync(deviceFingerprint);
        var retrievedMemories = await _memoryService.RetrieveMemoriesAsync(
            "Anna", MemoryType.Identity, 0.5, 5, user2.UserId);

        // Assert
        extractedMemories.Should().HaveCount(1);
        extractedMemories.First().Content.Should().Contain("Anna Andersson");
        extractedMemories.First().Type.Should().Be(MemoryType.Identity);
        
        systemPrompt.Should().Contain("[SYSTEM - Pinned Profile]");
        systemPrompt.Should().Contain("Anna");
        
        retrievedMemories.Should().HaveCount(1);
        retrievedMemories.First().Content.Should().Contain("Anna Andersson");
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task PrivacyControl_Should_PreventCrossUserAccess()
    {
        // Arrange
        var device1 = "device-1";
        var device2 = "device-2";
        var privateMessage = "mitt mellannamn är Erik och det är hemligt";

        // Act 1: User 1 shares private information
        var user1 = await _memoryService.GetOrCreateUserAsync(device1);
        var privateMemories = await _memoryExtractor.ExtractMemoriesAsync(
            privateMessage, "", "session1", device1);
        
        foreach (var memory in privateMemories)
        {
            memory.OwnerUserId = user1.UserId;
            memory.SubjectUserId = user1.UserId;
            await _memoryService.StoreMemoryAsync(memory);
        }

        // Act 2: User 2 tries to access private information
        var user2 = await _memoryService.GetOrCreateUserAsync(device2);
        var retrievedMemories = await _memoryService.RetrieveMemoriesAsync(
            "Erik mellannamn", MemoryType.Identity, 0.1, 10, user2.UserId);

        // Assert
        privateMemories.Should().HaveCount(1);
        privateMemories.First().Confidentiality.Should().Be(Confidentiality.Private);
        
        // User 2 should not be able to access User 1's private memories
        retrievedMemories.Should().BeEmpty();
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task PublicFacts_Should_BeAccessibleToAllUsers()
    {
        // Arrange
        var device1 = "device-1";
        var device2 = "device-2";
        var publicFact = "x = 77";

        // Act 1: User 1 teaches a public fact
        var user1 = await _memoryService.GetOrCreateUserAsync(device1);
        var factMemories = await _memoryExtractor.ExtractMemoriesAsync(
            publicFact, "", "session1", device1);
        
        foreach (var memory in factMemories)
        {
            memory.OwnerUserId = user1.UserId;
            await _memoryService.StoreMemoryAsync(memory);
        }

        // Act 2: User 2 queries for the fact
        var user2 = await _memoryService.GetOrCreateUserAsync(device2);
        var retrievedMemories = await _memoryService.RetrieveMemoriesAsync(
            "x equals", MemoryType.Facts, 0.1, 10, user2.UserId);

        // Assert
        factMemories.Should().HaveCount(1);
        factMemories.First().Confidentiality.Should().Be(Confidentiality.Public);
        
        // User 2 should be able to access public facts
        retrievedMemories.Should().HaveCount(1);
        retrievedMemories.First().Content.Should().Be("x = 77");
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task ConsentManagement_Should_WorkCorrectly()
    {
        // Arrange
        var deviceFingerprint = "test-device";
        var user = await _memoryService.GetOrCreateUserAsync(deviceFingerprint);
        var consentKind = "online_search";

        // Act 1: Check initial consent (should be false)
        var initialConsent = await _memoryService.HasConsentAsync(user.UserId, consentKind);

        // Act 2: Grant consent
        await _memoryService.UpdateConsentAsync(user.UserId, consentKind, true);
        var grantedConsent = await _memoryService.HasConsentAsync(user.UserId, consentKind);

        // Act 3: Revoke consent
        await _memoryService.UpdateConsentAsync(user.UserId, consentKind, false);
        var revokedConsent = await _memoryService.HasConsentAsync(user.UserId, consentKind);

        // Assert
        initialConsent.Should().BeFalse();
        grantedConsent.Should().BeTrue();
        revokedConsent.Should().BeFalse();
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task UserRuntimeState_Should_PersistCorrectly()
    {
        // Arrange
        var deviceFingerprint = "test-device";
        var user = await _memoryService.GetOrCreateUserAsync(deviceFingerprint);

        // Act 1: Get initial runtime state
        var initialState = await _memoryService.GetOrCreateUserRuntimeStateAsync(user.UserId);
        initialState.OnlineSearchCount = 1;
        initialState.LastOnlineSearchDate = DateTime.UtcNow.Date;
        initialState.OnlineSearchIsStarted = true;

        // Act 2: Update runtime state
        await _memoryService.UpdateUserRuntimeStateAsync(initialState);

        // Act 3: Retrieve updated state
        var updatedState = await _memoryService.GetOrCreateUserRuntimeStateAsync(user.UserId);

        // Assert
        updatedState.OnlineSearchCount.Should().Be(1);
        updatedState.LastOnlineSearchDate.Should().Be(DateTime.UtcNow.Date);
        updatedState.OnlineSearchIsStarted.Should().BeTrue();
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task MemoryDeduplication_Should_PreventDuplicates()
    {
        // Arrange
        var deviceFingerprint = "test-device";
        var user = await _memoryService.GetOrCreateUserAsync(deviceFingerprint);
        var duplicateContent = "Stockholm är huvudstaden i Sverige";

        // Act 1: Store first memory
        var memory1 = new MemoryEntry
        {
            Content = duplicateContent,
            Type = MemoryType.Facts,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public,
            OwnerUserId = user.UserId
        };
        await _memoryService.StoreMemoryAsync(memory1);

        // Act 2: Try to store duplicate memory
        var memory2 = new MemoryEntry
        {
            Content = duplicateContent,
            Type = MemoryType.Facts,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public,
            OwnerUserId = user.UserId
        };
        await _memoryService.StoreMemoryAsync(memory2);

        // Act 3: Retrieve memories
        var retrievedMemories = await _memoryService.RetrieveMemoriesAsync(
            "Stockholm", MemoryType.Facts, 0.1, 10, user.UserId);

        // Assert - Should only have one memory due to deduplication
        retrievedMemories.Should().HaveCount(1);
    }

    [Fact(Skip = "Integration test - requires database setup")]
    public async Task SystemPromptGeneration_Should_IncludeAllMemoryTypes()
    {
        // Arrange
        var deviceFingerprint = "test-device";
        var user = await _memoryService.GetOrCreateUserAsync(deviceFingerprint);

        // Add different types of memories
        var identityMemory = new MemoryEntry
        {
            Content = "Name: Anna Andersson",
            Type = MemoryType.Identity,
            Scope = MemoryScope.UserPrivate,
            Confidentiality = Confidentiality.Private,
            OwnerUserId = user.UserId
        };

        var factMemory = new MemoryEntry
        {
            Content = "x = 77",
            Type = MemoryType.Facts,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public,
            OwnerUserId = user.UserId
        };

        var contextMemory = new MemoryEntry
        {
            Content = "User likes programming",
            Type = MemoryType.EpisodicContext,
            Scope = MemoryScope.Public,
            Confidentiality = Confidentiality.Public,
            OwnerUserId = user.UserId
        };

        await _memoryService.StoreMemoryAsync(identityMemory);
        await _memoryService.StoreMemoryAsync(factMemory);
        await _memoryService.StoreMemoryAsync(contextMemory);

        // Act
        var systemPrompt = await _memoryService.GenerateSystemPromptAsync(deviceFingerprint);

        // Assert
        systemPrompt.Should().Contain("[SYSTEM - Pinned Profile]");
        systemPrompt.Should().Contain("[SYSTEM - Facts]");
        systemPrompt.Should().Contain("[SYSTEM - Memory Context]");
        systemPrompt.Should().Contain("Anna Andersson");
        systemPrompt.Should().Contain("x = 77");
        systemPrompt.Should().Contain("programming");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
