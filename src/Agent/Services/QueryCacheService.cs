using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Agent.Services;

/// <summary>
/// Service for caching OpenAI queries to reduce API calls and costs
/// </summary>
public interface IQueryCacheService
{
    Task<string?> GetCachedResponseAsync(string query, string model, object? parameters = null);
    Task CacheResponseAsync(
        string query,
        string model,
        string response,
        object? parameters = null,
        int? tokenCount = null,
        int? responseTimeMs = null
    );
    Task<bool> IsCachedAsync(string query, string model, object? parameters = null);
    Task CleanupOldCacheAsync(TimeSpan maxAge);
    Task<int> GetCacheCountAsync();
    Task<long> GetCacheSizeBytesAsync();
    Task ClearAllCacheAsync();
}

/// <summary>
/// Implementation of query cache service using SQLite
/// </summary>
public class QueryCacheService : IQueryCacheService
{
    private readonly AgentDbContext _context;
    private readonly ILogger<QueryCacheService> _logger;

    public QueryCacheService(AgentDbContext context, ILogger<QueryCacheService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> GetCachedResponseAsync(
        string query,
        string model,
        object? parameters = null
    )
    {
        try
        {
            var queryHash = ComputeQueryHash(query, model, parameters);

            var cachedQuery = await _context.OpenAICache.FirstOrDefaultAsync(c =>
                c.RequestHash == queryHash
            );

            if (cachedQuery != null)
            {
                // Update access statistics
                cachedQuery.LastAccessedAt = DateTime.UtcNow;
                cachedQuery.HitCount++;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Cache hit for query hash: {QueryHash} (accessed {HitCount} times)",
                    queryHash,
                    cachedQuery.HitCount
                );

                return cachedQuery.ResponseContent;
            }

            _logger.LogDebug("Cache miss for query hash: {QueryHash}", queryHash);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached response");
            return null;
        }
    }

    public async Task CacheResponseAsync(
        string query,
        string model,
        string response,
        object? parameters = null,
        int? tokenCount = null,
        int? responseTimeMs = null
    )
    {
        try
        {
            var queryHash = ComputeQueryHash(query, model, parameters);

            // Check if already cached (shouldn't happen, but handle gracefully)
            var existingCache = await _context.OpenAICache.FirstOrDefaultAsync(c =>
                c.RequestHash == queryHash
            );

            if (existingCache != null)
            {
                _logger.LogWarning(
                    "Attempted to cache already existing query hash: {QueryHash}",
                    queryHash
                );
                return;
            }

            var cachedQuery = new OpenAICache
            {
                RequestHash = queryHash,
                ResponseContent = response,
                RequestData = JsonSerializer.Serialize(
                    new
                    {
                        query,
                        model,
                        parameters
                    }
                ),
                ResponseData = response,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                HitCount = 0
            };

            _context.OpenAICache.Add(cachedQuery);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Cached response for query hash: {QueryHash} (model: {Model}, tokens: {TokenCount})",
                queryHash,
                model,
                tokenCount
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching response");
        }
    }

    public async Task<bool> IsCachedAsync(string query, string model, object? parameters = null)
    {
        try
        {
            var queryHash = ComputeQueryHash(query, model, parameters);
            return await _context.OpenAICache.AnyAsync(c => c.RequestHash == queryHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence");
            return false;
        }
    }

    public async Task CleanupOldCacheAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - maxAge;

            var oldEntries = await _context
                .OpenAICache.Where(c => c.LastAccessedAt < cutoffDate)
                .ToListAsync();

            if (oldEntries.Any())
            {
                _context.OpenAICache.RemoveRange(oldEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Cleaned up {Count} old cache entries older than {MaxAge}",
                    oldEntries.Count,
                    maxAge
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old cache entries");
        }
    }

    public async Task<int> GetCacheCountAsync()
    {
        try
        {
            return await _context.OpenAICache.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache count");
            return 0;
        }
    }

    public async Task<long> GetCacheSizeBytesAsync()
    {
        try
        {
            var totalSize = await _context.OpenAICache.SumAsync(c =>
                (c.RequestData ?? "").Length
                + c.ResponseContent.Length
                + (c.ResponseData ?? "").Length
            );

            return totalSize * sizeof(char); // Approximate size in bytes
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating cache size");
            return 0;
        }
    }

    public async Task ClearAllCacheAsync()
    {
        try
        {
            var allEntries = await _context.OpenAICache.ToListAsync();

            if (allEntries.Any())
            {
                _context.OpenAICache.RemoveRange(allEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleared all {Count} cache entries", allEntries.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all cache entries");
        }
    }

    private static string ComputeQueryHash(string query, string model, object? parameters)
    {
        var input =
            $"{query}|{model}|{(parameters != null ? JsonSerializer.Serialize(parameters) : "")}";
        var inputBytes = Encoding.UTF8.GetBytes(input);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(inputBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
