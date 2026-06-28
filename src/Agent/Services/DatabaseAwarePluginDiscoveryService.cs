using Agent.Data;
using Agent.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Shared;
using Shared.Models;

namespace Agent.Services;

/// <summary>
/// Plugin discovery service that respects the PluginStates database for enabled/disabled status.
/// This ensures that disabled plugins are NOT loaded into the Semantic Kernel.
/// </summary>
public class DatabaseAwarePluginDiscoveryService : PluginDiscoveryService
{
    private readonly DynamicPluginDiscoveryService _dynamicService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DatabaseAwarePluginDiscoveryService> _logger;

    public DatabaseAwarePluginDiscoveryService(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        DynamicPluginDiscoveryService dynamicService,
        IServiceProvider serviceProvider) : base(configuration, loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _dynamicService = dynamicService;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<DatabaseAwarePluginDiscoveryService>();
    }

    /// <summary>
    /// Registers plugins with the kernel, respecting the PluginStates database for enabled/disabled status.
    /// </summary>
    public override int RegisterAllPlugins(Kernel kernel)
    {
        _logger.LogInformation("🔍 DATABASE-AWARE DISCOVERY: Starting plugin registration with database state checking");

        var discoveredPlugins = _dynamicService.GetDiscoverablePlugins();
        var registeredCount = 0;

        // Get database states
        var pluginStates = GetPluginStatesFromDatabase();
        var statesByName = pluginStates.ToDictionary(ps => ps.PluginName, ps => ps);

        _logger.LogInformation("📊 Found {DiscoveredCount} discovered plugins and {DatabaseCount} database states", 
            discoveredPlugins.Count, pluginStates.Count);

        // Process each discovered plugin
        foreach (var plugin in discoveredPlugins.OrderBy(p => p.Priority))
        {
            try
            {
                // Check database state first
                var isEnabledInDatabase = true; // Default to enabled if not in database
                if (statesByName.TryGetValue(plugin.Name, out var dbState))
                {
                    isEnabledInDatabase = dbState.IsEnabled;
                    _logger.LogDebug("Plugin {PluginName}: Database state = {IsEnabled}", plugin.Name, isEnabledInDatabase);
                }
                else
                {
                    _logger.LogDebug("Plugin {PluginName}: No database state found, defaulting to enabled", plugin.Name);
                }

                // Skip if disabled in database
                if (!isEnabledInDatabase)
                {
                    _logger.LogInformation("⏭️ SKIPPING plugin {PluginName}: Disabled in database", plugin.Name);
                    continue;
                }

                // Skip if disabled in attribute (fallback)
                if (!plugin.Enabled)
                {
                    _logger.LogInformation("⏭️ SKIPPING plugin {PluginName}: Disabled in attribute", plugin.Name);
                    continue;
                }

                // Skip if no factory
                if (plugin.Factory == null)
                {
                    _logger.LogWarning("⚠️ SKIPPING plugin {PluginName}: No factory instance", plugin.Name);
                    continue;
                }

                // Register the plugin
                _logger.LogDebug("Registering plugin: {PluginName} (Priority: {Priority})", plugin.Name, plugin.Priority);

                var instance = plugin.Factory.CreatePlugin(_configuration, _loggerFactory);
                kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(instance, plugin.Name));
                registeredCount++;

                _logger.LogInformation("✅ Registered plugin: {PluginName} ({FunctionCount} functions)", 
                    plugin.Name, plugin.Functions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to register plugin: {PluginName}", plugin.Name);
            }
        }

        _logger.LogInformation("🎉 DATABASE-AWARE DISCOVERY: Registered {Count} plugins (respecting database states)", registeredCount);
        return registeredCount;
    }

    /// <summary>
    /// Gets all discoverable plugins (delegates to dynamic service)
    /// </summary>
    public List<PluginMetadata> GetDiscoverablePlugins()
    {
        return _dynamicService.GetDiscoverablePlugins();
    }

    /// <summary>
    /// Gets plugin states from the database
    /// </summary>
    private List<PluginState> GetPluginStatesFromDatabase()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
            
            var states = dbContext.PluginStates.ToList();
            _logger.LogDebug("Retrieved {Count} plugin states from database", states.Count);
            
            return states;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to retrieve plugin states from database, proceeding without database filtering");
            return new List<PluginState>();
        }
    }

    /// <summary>
    /// Forces a refresh of the plugin cache
    /// </summary>
    public void RefreshDiscovery()
    {
        _dynamicService.RefreshDiscovery();
    }
}
