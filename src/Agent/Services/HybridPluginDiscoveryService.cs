using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Shared;
using Shared.Models;

namespace Agent.Services;

/// <summary>
/// Hybrid plugin discovery service that compares hardcoded and dynamic discovery results.
/// This is used during Phase 1 to ensure consistency before switching to dynamic-only.
/// </summary>
public class HybridPluginDiscoveryService : PluginDiscoveryService
{
    private readonly DynamicPluginDiscoveryService _hardcodedService;
    private readonly DynamicPluginDiscoveryService _dynamicService;
    private readonly ILogger<HybridPluginDiscoveryService> _logger;

    public HybridPluginDiscoveryService(
        IConfiguration configuration,
        ILoggerFactory loggerFactory) : base(configuration, loggerFactory)
    {
        _hardcodedService = new DynamicPluginDiscoveryService(configuration, loggerFactory);
        _dynamicService = new DynamicPluginDiscoveryService(configuration, loggerFactory);
        _logger = loggerFactory.CreateLogger<HybridPluginDiscoveryService>();
    }

    /// <summary>
    /// Registers plugins using hardcoded method while comparing with dynamic discovery.
    /// </summary>
    public override int RegisterAllPlugins(Kernel kernel)
    {
        _logger.LogInformation("🔄 HYBRID DISCOVERY: Starting comparison between hardcoded and dynamic discovery");

        // Get results from both methods
        var hardcodedCount = _hardcodedService.RegisterAllPlugins(kernel);
        var dynamicPlugins = _dynamicService.GetDiscoverablePlugins();

        // Compare results
        CompareDiscoveryResults(hardcodedCount, dynamicPlugins);

        _logger.LogInformation("🎯 HYBRID DISCOVERY: Using hardcoded registration result: {Count} plugins", hardcodedCount);
        return hardcodedCount;
    }

    /// <summary>
    /// Compares the results between hardcoded and dynamic discovery methods.
    /// </summary>
    private void CompareDiscoveryResults(int hardcodedCount, List<PluginMetadata> dynamicPlugins)
    {
        var enabledDynamicCount = dynamicPlugins.Count(p => p.Enabled);

        _logger.LogInformation("📊 DISCOVERY COMPARISON:");
        _logger.LogInformation("  Hardcoded plugins registered: {HardcodedCount}", hardcodedCount);
        _logger.LogInformation("  Dynamic plugins discovered: {DynamicTotal} (enabled: {DynamicEnabled})", 
            dynamicPlugins.Count, enabledDynamicCount);

        // Check if counts match
        if (hardcodedCount == enabledDynamicCount)
        {
            _logger.LogInformation("✅ DISCOVERY MATCH: Plugin counts are consistent!");
        }
        else
        {
            _logger.LogWarning("⚠️ DISCOVERY MISMATCH: Hardcoded ({Hardcoded}) vs Dynamic ({Dynamic}) plugin counts differ!", 
                hardcodedCount, enabledDynamicCount);
        }

        // Log detailed comparison
        LogDetailedComparison(dynamicPlugins);

        // Check for potential issues
        CheckForIssues(dynamicPlugins);
    }

    /// <summary>
    /// Logs detailed information about discovered plugins.
    /// </summary>
    private void LogDetailedComparison(List<PluginMetadata> dynamicPlugins)
    {
        _logger.LogInformation("📋 DYNAMIC DISCOVERY DETAILS:");

        var groupedPlugins = dynamicPlugins.GroupBy(p => p.Category).OrderBy(g => g.Key);

        foreach (var group in groupedPlugins)
        {
            _logger.LogInformation("  Category: {Category}", group.Key);
            
            foreach (var plugin in group.OrderBy(p => p.Priority))
            {
                var status = plugin.Enabled ? "✅" : "❌";
                _logger.LogInformation("    {Status} {Name} (Priority: {Priority}, Assembly: {Assembly})", 
                    status, plugin.Name, plugin.Priority, plugin.AssemblyName);
                
                if (plugin.Factory == null)
                {
                    _logger.LogWarning("      ⚠️ No factory instance created for {PluginName}", plugin.Name);
                }
            }
        }
    }

    /// <summary>
    /// Checks for potential issues in the dynamic discovery results.
    /// </summary>
    private void CheckForIssues(List<PluginMetadata> dynamicPlugins)
    {
        var issues = new List<string>();

        // Check for plugins without factories
        var pluginsWithoutFactories = dynamicPlugins.Where(p => p.Factory == null).ToList();
        if (pluginsWithoutFactories.Any())
        {
            issues.Add($"{pluginsWithoutFactories.Count} plugins have no factory instances");
            foreach (var plugin in pluginsWithoutFactories)
            {
                _logger.LogWarning("🚨 Plugin {PluginName} has no factory instance", plugin.Name);
            }
        }

        // Check for disabled plugins
        var disabledPlugins = dynamicPlugins.Where(p => !p.Enabled).ToList();
        if (disabledPlugins.Any())
        {
            issues.Add($"{disabledPlugins.Count} plugins are disabled");
            foreach (var plugin in disabledPlugins)
            {
                _logger.LogInformation("ℹ️ Plugin {PluginName} is disabled", plugin.Name);
            }
        }

        // Check for duplicate names
        var duplicateNames = dynamicPlugins.GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        
        if (duplicateNames.Any())
        {
            issues.Add($"{duplicateNames.Count} duplicate plugin names found");
            foreach (var name in duplicateNames)
            {
                _logger.LogError("🚨 Duplicate plugin name found: {PluginName}", name);
            }
        }

        // Check for missing expected plugins (based on hardcoded list)
        var expectedPlugins = new[]
        {
            "Mail", "SMS", "DateTime", "Dropbox", "Weather", "Slack", 
            "WhatsApp", "Geocoding", "GoogleSearch", "MicrosoftOffice", "GoogleWorkspace"
        };

        var discoveredNames = dynamicPlugins.Select(p => p.Name).ToHashSet();
        var missingPlugins = expectedPlugins.Where(name => !discoveredNames.Contains(name)).ToList();
        
        if (missingPlugins.Any())
        {
            issues.Add($"{missingPlugins.Count} expected plugins are missing");
            foreach (var name in missingPlugins)
            {
                _logger.LogError("🚨 Expected plugin not found: {PluginName}", name);
            }
        }

        // Summary
        if (issues.Any())
        {
            _logger.LogWarning("⚠️ DISCOVERY ISSUES FOUND:");
            foreach (var issue in issues)
            {
                _logger.LogWarning("  - {Issue}", issue);
            }
        }
        else
        {
            _logger.LogInformation("✅ No issues found in dynamic discovery!");
        }
    }

    /// <summary>
    /// Gets the dynamic discovery results for testing purposes.
    /// </summary>
    public List<PluginMetadata> GetDynamicDiscoveryResults()
    {
        return _dynamicService.GetDiscoverablePlugins();
    }

    /// <summary>
    /// Forces a re-scan of plugins by clearing caches.
    /// </summary>
    public void RefreshDiscovery()
    {
        _dynamicService.ClearCache();
        _logger.LogInformation("🔄 Plugin discovery cache refreshed");
    }
}
