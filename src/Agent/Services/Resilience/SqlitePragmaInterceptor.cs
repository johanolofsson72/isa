using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agent.Services.Resilience;

/// <summary>
/// EF Core interceptor that applies SQLite PRAGMAs on every new connection.
///
/// Profile is config-driven via Sqlite:UseWal (default true):
///   - true  → WAL profile (local disk only). journal_mode=WAL + mmap.
///   - false → NFS-safe profile (network filesystems). journal_mode=DELETE,
///             synchronous=FULL, no mmap. WAL on NFS corrupts the DB because
///             cross-host -shm mmap consistency is not guaranteed.
/// busy_timeout=5000 and foreign_keys=ON are mandatory in both profiles.
/// </summary>
public class SqlitePragmaInterceptor(IConfiguration configuration, ILogger<SqlitePragmaInterceptor> logger) : DbConnectionInterceptor
{
    private readonly bool _useWal = configuration.GetValue("Sqlite:UseWal", true);

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);
        return Task.CompletedTask;
    }

    private void ApplyPragmas(DbConnection connection)
    {
        if (connection is not SqliteConnection)
        {
            return;
        }

        var pragmas = _useWal
            ? """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA busy_timeout=5000;
                PRAGMA foreign_keys=ON;
                PRAGMA temp_store=MEMORY;
                PRAGMA mmap_size=268435456;
            """
            : """
                PRAGMA journal_mode=DELETE;
                PRAGMA synchronous=FULL;
                PRAGMA busy_timeout=5000;
                PRAGMA foreign_keys=ON;
                PRAGMA temp_store=MEMORY;
            """;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = pragmas;
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply SQLite PRAGMAs on connection {DataSource}", connection.DataSource);
        }
    }
}
