using Agent.Configuration;
using Agent.Data;
using Agent.Models;
using Agent.Services;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agent.Tests.Services;

/// <summary>
/// Tests for memory persistence and decay behavior
/// </summary>
public class MemoryPersistenceTests : IDisposable
{
    private readonly MemoryDbContext _context;
    private readonly ProductionMemoryService _service;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;

    public MemoryPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MemoryDbContext(options);
        _embeddingService = A.Fake<IEmbeddingGenerator<string, Embedding<float>>>();
        
        // Setup fake embedding service
        var fakeEmbedding = new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f });
        A.CallTo(() => _embeddingService.GenerateAsync(A<IEnumerable<string>>._, A<EmbeddingGenerationOptions>._, A<CancellationToken>._))
            .Returns(new GeneratedEmbeddings<Embedding<float>>(new[] { fakeEmbedding }));

        var logger = A.Fake<ILogger<ProductionMemoryService>>();
        var llmOptions = Options.Create(new LiteLLMOptions { EmbeddingDimensions = 3 });
        _service = new ProductionMemoryService(_context, _embeddingService, llmOptions, logger);
    }

    [Fact]
    public async Task Mathematical_Facts_Should_Have_Longer_Retention_Than_Context()
    {
        // Arrange
        var mathMemory = new MemoryEntry
        {
            Content = "x = 77 and z = 34 and y = x - z",
            Type = MemoryType.Facts,
            OwnerUserId = "test-user",
            Score = 1.0,
            CreatedAt = DateTime.UtcNow.AddDays(-30) // 30 days old
        };

        var contextMemory = new MemoryEntry
        {
            Content = "User likes coffee in the morning",
            Type = MemoryType.EpisodicContext,
            OwnerUserId = "test-user",
            Score = 1.0,
            CreatedAt = DateTime.UtcNow.AddDays(-30) // 30 days old
        };

        await _context.Set<MemoryEntry>().AddRangeAsync(mathMemory, contextMemory);
        await _context.SaveChangesAsync();

        // Act - simulate decay after 30 days
        await _service.ConsolidateAndCleanupAsync();

        // Assert
        var remainingMemories = await _context.Set<MemoryEntry>()
            .Where(m => m.TombstonedAt == null)
            .ToListAsync();

        // Math facts should have higher score due to slower decay rate
        var mathAfterDecay = remainingMemories.First(m => m.Type == MemoryType.Facts);
        var contextAfterDecay = remainingMemories.First(m => m.Type == MemoryType.EpisodicContext);

        mathAfterDecay.Score.Should().BeGreaterThan(contextAfterDecay.Score);
    }

    [Fact]
    public async Task Identity_Information_Should_Have_Longest_Retention()
    {
        // Arrange
        var identityMemory = new MemoryEntry
        {
            Content = "mellannamn är Patrick",
            Type = MemoryType.Identity,
            OwnerUserId = "test-user",
            Score = 1.0,
            CreatedAt = DateTime.UtcNow.AddDays(-60) // 60 days old
        };

        var factMemory = new MemoryEntry
        {
            Content = "x = 42",
            Type = MemoryType.Facts,
            OwnerUserId = "test-user",
            Score = 1.0,
            CreatedAt = DateTime.UtcNow.AddDays(-60) // 60 days old
        };

        await _context.Set<MemoryEntry>().AddRangeAsync(identityMemory, factMemory);
        await _context.SaveChangesAsync();

        // Act
        await _service.ConsolidateAndCleanupAsync();

        // Assert
        var remainingMemories = await _context.Set<MemoryEntry>()
            .Where(m => m.TombstonedAt == null)
            .ToListAsync();

        var identityAfterDecay = remainingMemories.First(m => m.Type == MemoryType.Identity);
        var factAfterDecay = remainingMemories.First(m => m.Type == MemoryType.Facts);

        identityAfterDecay.Score.Should().BeGreaterThan(factAfterDecay.Score);
    }

    [Fact]
    public async Task TTL_Should_Be_Respected_Before_Score_Threshold()
    {
        // Arrange
        var memoryWithTTL = new MemoryEntry
        {
            Content = "Important fact that should not decay",
            Type = MemoryType.Facts,
            OwnerUserId = "test-user",
            Score = 0.02, // Below threshold but not expired
            ExpiresAt = DateTime.UtcNow.AddDays(30), // Still valid
            CreatedAt = DateTime.UtcNow.AddDays(-100)
        };

        await _context.Set<MemoryEntry>().AddAsync(memoryWithTTL);
        await _context.SaveChangesAsync();

        // Act
        await _service.ConsolidateAndCleanupAsync();

        // Assert
        var memory = await _context.Set<MemoryEntry>()
            .FirstOrDefaultAsync(m => m.Id == memoryWithTTL.Id);

        memory.Should().NotBeNull();
        memory!.TombstonedAt.Should().BeNull(); // Should not be removed despite low score
    }

    [Fact]
    public async Task Expired_Memories_Should_Be_Removed_Regardless_Of_Score()
    {
        // Arrange
        var expiredMemory = new MemoryEntry
        {
            Content = "Expired memory",
            Type = MemoryType.Facts,
            OwnerUserId = "test-user",
            Score = 0.9, // High score but expired
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        await _context.Set<MemoryEntry>().AddAsync(expiredMemory);
        await _context.SaveChangesAsync();

        // Act
        await _service.ConsolidateAndCleanupAsync();

        // Assert
        var memory = await _context.Set<MemoryEntry>()
            .FirstOrDefaultAsync(m => m.Id == expiredMemory.Id);

        memory.Should().NotBeNull();
        memory!.TombstonedAt.Should().NotBeNull(); // Should be tombstoned despite high score
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
