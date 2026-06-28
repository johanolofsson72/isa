using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Agent.Services;

/// <summary>
/// Service for debugging and monitoring memory system behavior
/// </summary>
public interface IMemoryDebugService
{
    Task<MemorySystemStats> GetMemoryStatsAsync(string? userId = null);
    Task<List<MemoryEntry>> GetMemoriesByTypeAsync(string? userId, MemoryType type, int limit = 50);
    Task<MemoryLifecycleInfo> GetMemoryLifecycleAsync(string memoryId);
    Task<List<MemoryEntry>> GetExpiredMemoriesAsync(int limit = 50);
    Task<List<MemoryEntry>> GetLowScoreMemoriesAsync(double threshold = 0.1, int limit = 50);
}

public class MemoryDebugService : IMemoryDebugService
{
    private readonly MemoryDbContext _context;
    private readonly ILogger<MemoryDebugService> _logger;

    public MemoryDebugService(MemoryDbContext context, ILogger<MemoryDebugService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MemorySystemStats> GetMemoryStatsAsync(string? userId = null)
    {
        var query = _context.Set<MemoryEntry>().Where(m => m.TombstonedAt == null);
        
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(m => m.OwnerUserId == userId);
        }

        var stats = new MemorySystemStats
        {
            TotalMemories = await query.CountAsync(),
            IdentityMemories = await query.CountAsync(m => m.Type == MemoryType.Identity),
            FactsMemories = await query.CountAsync(m => m.Type == MemoryType.Facts),
            ContextMemories = await query.CountAsync(m => m.Type == MemoryType.EpisodicContext),
            ExpiredMemories = await query.CountAsync(m => m.ExpiresAt.HasValue && m.ExpiresAt.Value < DateTime.UtcNow),
            LowScoreMemories = await query.CountAsync(m => m.Score < 0.1),
            AverageScore = await query.AverageAsync(m => m.Score),
            OldestMemory = await query.MinAsync(m => m.CreatedAt),
            NewestMemory = await query.MaxAsync(m => m.CreatedAt)
        };

        return stats;
    }

    public async Task<List<MemoryEntry>> GetMemoriesByTypeAsync(string? userId, MemoryType type, int limit = 50)
    {
        var query = _context.Set<MemoryEntry>()
            .Where(m => m.Type == type && m.TombstonedAt == null);

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(m => m.OwnerUserId == userId);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<MemoryLifecycleInfo> GetMemoryLifecycleAsync(string memoryId)
    {
        var memory = await _context.Set<MemoryEntry>()
            .FirstOrDefaultAsync(m => m.Id == memoryId);

        if (memory == null)
        {
            throw new ArgumentException($"Memory with ID {memoryId} not found");
        }

        var now = DateTime.UtcNow;
        var daysOld = (now - memory.CreatedAt).TotalDays;
        var daysUntilExpiry = memory.ExpiresAt.HasValue ? (double?)(memory.ExpiresAt.Value - now).TotalDays : null;

        // Calculate what the original score would have been
        var decayRate = memory.Type switch
        {
            MemoryType.Identity => 0.999,
            MemoryType.Facts => 0.998,
            MemoryType.EpisodicContext => 0.995,
            _ => 0.995
        };

        var estimatedOriginalScore = memory.Score / Math.Pow(decayRate, daysOld);

        return new MemoryLifecycleInfo
        {
            Memory = memory,
            DaysOld = daysOld,
            DaysUntilExpiry = daysUntilExpiry,
            EstimatedOriginalScore = estimatedOriginalScore,
            CurrentDecayRate = decayRate,
            IsExpired = memory.ExpiresAt.HasValue && memory.ExpiresAt.Value < now,
            IsTombstoned = memory.TombstonedAt.HasValue,
            WillBeRemovedByDecay = memory.Score < 0.05,
            WillBeRemovedByExpiry = memory.ExpiresAt.HasValue && memory.ExpiresAt.Value < now
        };
    }

    public async Task<List<MemoryEntry>> GetExpiredMemoriesAsync(int limit = 50)
    {
        return await _context.Set<MemoryEntry>()
            .Where(m => m.ExpiresAt.HasValue && m.ExpiresAt.Value < DateTime.UtcNow && m.TombstonedAt == null)
            .OrderBy(m => m.ExpiresAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<MemoryEntry>> GetLowScoreMemoriesAsync(double threshold = 0.1, int limit = 50)
    {
        return await _context.Set<MemoryEntry>()
            .Where(m => m.Score < threshold && m.TombstonedAt == null)
            .OrderBy(m => m.Score)
            .Take(limit)
            .ToListAsync();
    }
}

public class MemorySystemStats
{
    public int TotalMemories { get; set; }
    public int IdentityMemories { get; set; }
    public int FactsMemories { get; set; }
    public int ContextMemories { get; set; }
    public int ExpiredMemories { get; set; }
    public int LowScoreMemories { get; set; }
    public double AverageScore { get; set; }
    public DateTime OldestMemory { get; set; }
    public DateTime NewestMemory { get; set; }
}

public class MemoryLifecycleInfo
{
    public MemoryEntry Memory { get; set; } = null!;
    public double DaysOld { get; set; }
    public double? DaysUntilExpiry { get; set; }
    public double EstimatedOriginalScore { get; set; }
    public double CurrentDecayRate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsTombstoned { get; set; }
    public bool WillBeRemovedByDecay { get; set; }
    public bool WillBeRemovedByExpiry { get; set; }
}
