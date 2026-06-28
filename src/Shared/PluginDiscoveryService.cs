using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Shared;

/// <summary>
/// Service responsible for discovering and registering kernel plugins automatically
/// This is a placeholder interface - actual implementation should be in the Agent project
/// </summary>
public class PluginDiscoveryService
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginDiscoveryService> _logger;

    public PluginDiscoveryService(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PluginDiscoveryService>();
    }

    /// <summary>
    /// Discovers and registers all available plugins with the kernel
    /// This method should be overridden in the Agent project where plugin references are available
    /// </summary>
    /// <param name="kernel">The semantic kernel to register plugins with</param>
    /// <returns>The number of plugins successfully registered</returns>
    public virtual int RegisterAllPlugins(Kernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        
        _logger.LogWarning("PluginDiscoveryService.RegisterAllPlugins called from Shared project - no plugins will be registered. This should be overridden in Agent project.");
        return 0;
    }
}