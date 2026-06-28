using Agent.Data;
using Agent.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Agent.Services;

public class SqliteIdempotencyStore(AgentDbContext db, ILogger<SqliteIdempotencyStore> logger) : IIdempotencyStore
{
    public async Task<IdempotentResponse?> TryReplayAsync(string key, CancellationToken ct = default)
    {
        var entry = await db.Set<IdempotencyEntry>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, ct);

        if (entry is null)
        {
            return null;
        }

        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }

        logger.LogInformation("Idempotency replay hit for key {Key}", key);
        return new IdempotentResponse(entry.StatusCode, entry.ContentType, entry.ResponseBody);
    }

    public async Task SaveAsync(string key, IdempotentResponse response, TimeSpan ttl, CancellationToken ct = default)
    {
        var existing = await db.Set<IdempotencyEntry>().FirstOrDefaultAsync(e => e.Key == key, ct);
        if (existing is not null)
        {
            existing.StatusCode = response.StatusCode;
            existing.ContentType = response.ContentType;
            existing.ResponseBody = response.Body;
            existing.ExpiresAt = DateTime.UtcNow.Add(ttl);
        }
        else
        {
            db.Set<IdempotencyEntry>().Add(new IdempotencyEntry
            {
                Key = key,
                StatusCode = response.StatusCode,
                ContentType = response.ContentType,
                ResponseBody = response.Body,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ttl),
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
