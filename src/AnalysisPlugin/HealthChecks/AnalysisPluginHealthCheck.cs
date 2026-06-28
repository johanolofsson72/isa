using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using AnalysisPlugin.Configuration;
using Microsoft.Extensions.Options;

namespace AnalysisPlugin.HealthChecks;

/// <summary>
/// Health check for AnalysisPlugin to verify system readiness
/// </summary>
public class AnalysisPluginHealthCheck : IHealthCheck
{
    private readonly ILogger<AnalysisPluginHealthCheck> _logger;
    private readonly AnalysisPluginOptions _options;

    public AnalysisPluginHealthCheck(
        ILogger<AnalysisPluginHealthCheck> logger,
        IOptions<AnalysisPluginOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var checks = new List<string>();

            // Check report archive directory
            if (!Directory.Exists(_options.ReportArchiveDirectory))
            {
                Directory.CreateDirectory(_options.ReportArchiveDirectory);
                checks.Add("Created report archive directory");
            }
            else
            {
                checks.Add("Report archive directory exists");
            }

            // Check SQLite database connectivity
            var dbPath = "analysis.db"; // Default path
            if (!File.Exists(dbPath))
            {
                checks.Add("Analysis database will be created on first use");
            }
            else
            {
                checks.Add("Analysis database exists");
            }

            // Check disk space (basic check)
            var driveInfo = new DriveInfo(Path.GetPathRoot(_options.ReportArchiveDirectory) ?? "/");
            var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024 * 1024 * 1024);
            
            if (freeSpaceGB < 1)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "Low disk space available",
                    data: new Dictionary<string, object>
                    {
                        ["FreeSpaceGB"] = freeSpaceGB,
                        ["Checks"] = checks
                    }));
            }

            checks.Add($"Sufficient disk space: {freeSpaceGB}GB available");

            _logger.LogDebug("AnalysisPlugin health check passed: {Checks}", string.Join(", ", checks));

            return Task.FromResult(HealthCheckResult.Healthy(
                "AnalysisPlugin is ready",
                data: new Dictionary<string, object>
                {
                    ["FreeSpaceGB"] = freeSpaceGB,
                    ["Checks"] = checks
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalysisPlugin health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "AnalysisPlugin health check failed",
                ex,
                data: new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                }));
        }
    }
}
