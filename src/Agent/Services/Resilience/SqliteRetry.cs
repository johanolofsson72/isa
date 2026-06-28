using Microsoft.Data.Sqlite;

namespace Agent.Services.Resilience;

/// <summary>
/// Lightweight retry wrapper for SQLite transient errors:
///   5  = SQLITE_BUSY, 6  = SQLITE_LOCKED, 10 = SQLITE_IOERR
/// Exponential backoff 100ms * 2^attempt, capped at 5 attempts. Does NOT retry
/// SQLITE_CORRUPT (11) — that is permanent and must surface as a fatal error.
/// </summary>
public static class SqliteRetry
{
    private static readonly int[] TransientCodes = { 5, 6, 10 };

    public static async Task ExecuteAsync(Func<Task> action, int maxAttempts = 5, ILogger? logger = null, CancellationToken ct = default)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch (SqliteException ex) when (TransientCodes.Contains(ex.SqliteErrorCode) && ++attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                logger?.LogDebug("SQLite transient error {Code}; retrying in {Delay} (attempt {Attempt}/{Max})", ex.SqliteErrorCode, delay, attempt, maxAttempts);
                await Task.Delay(delay, ct);
            }
        }
    }

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, int maxAttempts = 5, ILogger? logger = null, CancellationToken ct = default)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await action();
            }
            catch (SqliteException ex) when (TransientCodes.Contains(ex.SqliteErrorCode) && ++attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                logger?.LogDebug("SQLite transient error {Code}; retrying in {Delay} (attempt {Attempt}/{Max})", ex.SqliteErrorCode, delay, attempt, maxAttempts);
                await Task.Delay(delay, ct);
            }
        }
    }
}
