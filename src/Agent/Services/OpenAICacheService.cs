using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agent.Services;

/// <summary>
/// Service for caching OpenAI API responses to reduce costs and improve performance
/// </summary>
public interface IOpenAICacheService
{
    Task<string?> GetCachedResponseAsync(ChatHistory chatHistory, object? executionSettings = null);
    Task CacheResponseAsync(
        ChatHistory chatHistory,
        string response,
        object? executionSettings = null
    );
    Task CleanupExpiredCacheAsync();
    Task<CacheStatistics> GetCacheStatisticsAsync();
}

public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int TotalHits { get; set; }
    public DateTime? OldestEntry { get; set; }
    public DateTime? NewestEntry { get; set; }
    public long TotalSizeBytes { get; set; }
}

public class OpenAICacheService : IOpenAICacheService
{
    private readonly AgentDbContext _dbContext;
    private readonly ILogger<OpenAICacheService> _logger;
    private readonly IConfiguration _configuration;

    public OpenAICacheService(
        AgentDbContext dbContext,
        ILogger<OpenAICacheService> logger,
        IConfiguration configuration
    )
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string?> GetCachedResponseAsync(
        ChatHistory chatHistory,
        object? executionSettings = null
    )
    {
        try
        {
            var hash = GenerateRequestHash(chatHistory, executionSettings);

            var cacheEntry = await _dbContext.OpenAICache.FirstOrDefaultAsync(c =>
                c.RequestHash == hash && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow)
            );

            if (cacheEntry != null)
            {
                // Update access statistics
                cacheEntry.LastAccessedAt = DateTime.UtcNow;
                cacheEntry.HitCount++;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Cache hit for request hash: {Hash}", hash);
                return cacheEntry.ResponseContent;
            }

            _logger.LogDebug("Cache miss for request hash: {Hash}", hash);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached response");
            return null;
        }
    }

    public async Task CacheResponseAsync(
        ChatHistory chatHistory,
        string response,
        object? executionSettings = null
    )
    {
        try
        {
            var hash = GenerateRequestHash(chatHistory, executionSettings);

            // Check if already exists (shouldn't happen, but just in case)
            var existing = await _dbContext.OpenAICache.FirstOrDefaultAsync(c =>
                c.RequestHash == hash
            );

            if (existing != null)
            {
                _logger.LogWarning(
                    "Attempting to cache response that already exists: {Hash}",
                    hash
                );
                return;
            }

            var cacheLifetimeHours = _configuration.GetValue<int>("OpenAI:CacheLifetimeHours", 24);
            var expiresAt =
                cacheLifetimeHours > 0
                    ? DateTime.UtcNow.AddHours(cacheLifetimeHours)
                    : (DateTime?)null;

            var cacheEntry = new OpenAICache
            {
                RequestHash = hash,
                ResponseContent = response,
                RequestData = SerializeChatHistory(chatHistory, executionSettings),
                ResponseData = JsonSerializer.Serialize(
                    new { Content = response, Timestamp = DateTime.UtcNow }
                ),
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                HitCount = 0,
                ExpiresAt = expiresAt
            };

            _dbContext.OpenAICache.Add(cacheEntry);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Cached response with hash: {Hash}", hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching response");
        }
    }

    public async Task CleanupExpiredCacheAsync()
    {
        try
        {
            var expiredEntries = await _dbContext
                .OpenAICache.Where(c => c.ExpiresAt != null && c.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expiredEntries.Any())
            {
                _dbContext.OpenAICache.RemoveRange(expiredEntries);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation(
                    "Cleaned up {Count} expired cache entries",
                    expiredEntries.Count
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired cache entries");
        }
    }

    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        try
        {
            var entries = await _dbContext.OpenAICache.ToListAsync();

            return new CacheStatistics
            {
                TotalEntries = entries.Count,
                TotalHits = entries.Sum(e => e.HitCount),
                OldestEntry = entries.MinBy(e => e.CreatedAt)?.CreatedAt,
                NewestEntry = entries.MaxBy(e => e.CreatedAt)?.CreatedAt,
                TotalSizeBytes = entries.Sum(e =>
                    Encoding.UTF8.GetByteCount(e.ResponseContent)
                    + (e.RequestData != null ? Encoding.UTF8.GetByteCount(e.RequestData) : 0)
                    + (e.ResponseData != null ? Encoding.UTF8.GetByteCount(e.ResponseData) : 0)
                )
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache statistics");
            return new CacheStatistics();
        }
    }

    private string GenerateRequestHash(ChatHistory chatHistory, object? executionSettings)
    {
        var requestData = SerializeChatHistory(chatHistory, executionSettings);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(requestData));
        return Convert.ToHexString(hashBytes);
    }

    private string SerializeChatHistory(ChatHistory chatHistory, object? executionSettings)
    {
        var data = new
        {
            Messages = chatHistory
                .Select(m => new { Role = m.Role.ToString(), Content = m.Content })
                .ToList(),
            Settings = executionSettings != null
                ? JsonSerializer.Serialize(executionSettings)
                : null,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd") // Include date to invalidate cache daily if needed
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
    }
}
