using Microsoft.Data.Sqlite;

namespace Agent.BackgroundServices;

/// <summary>
/// Runs PRAGMA wal_checkpoint(TRUNCATE) against every configured SQLite database when
/// the host shuts down. Without this, a hard-killed container leaves the WAL un-merged
/// and the next opener has to recover, which is the corruption window on shared storage.
/// </summary>
public sealed class SqliteCheckpointOnStop(IConfiguration configuration, ILogger<SqliteCheckpointOnStop> logger) : IHostedService
{
    private static readonly string[] ConnectionNames =
    {
        "DefaultConnection",
        "MemoryConnection",
        "FilesConnection",
    };

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No-op when WAL is disabled (NFS-safe DELETE journaling has no -wal file to checkpoint).
        var useWal = configuration.GetValue("Sqlite:UseWal", true);
        if (!useWal)
        {
            logger.LogDebug("WAL checkpoint skipped: Sqlite:UseWal=false (NFS-safe DELETE journal in use)");
            return Task.CompletedTask;
        }

        foreach (var name in ConnectionNames)
        {
            var cs = configuration.GetConnectionString(name);
            if (string.IsNullOrWhiteSpace(cs))
            {
                continue;
            }

            try
            {
                using var conn = new SqliteConnection(cs);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                cmd.ExecuteNonQuery();
                logger.LogInformation("WAL checkpoint completed for {Connection}", name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WAL checkpoint failed for {Connection}", name);
            }
        }
        return Task.CompletedTask;
    }
}
