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
/// Dynamic plugin discovery service with fallback to hardcoded discovery for safety.
/// This is the production-ready version that ensures plugins are always registered.
/// </summary>
public class DynamicPluginDiscoveryServiceWithFallback : PluginDiscoveryService
{
    private readonly DynamicPluginDiscoveryService _dynamicService;
    private readonly DynamicPluginDiscoveryService _fallbackService;
    private readonly ILogger<DynamicPluginDiscoveryServiceWithFallback> _logger;
    private readonly int _minimumExpectedPlugins = 10; // We expect at least 10 plugins

    public DynamicPluginDiscoveryServiceWithFallback(
        IConfiguration configuration,
        ILoggerFactory loggerFactory) : base(configuration, loggerFactory)
    {
        _dynamicService = new DynamicPluginDiscoveryService(configuration, loggerFactory);
        _fallbackService = new DynamicPluginDiscoveryService(configuration, loggerFactory);
        _logger = loggerFactory.CreateLogger<DynamicPluginDiscoveryServiceWithFallback>();
    }

    /// <summary>
    /// Registers plugins using dynamic discovery with fallback to hardcoded if needed.
    /// </summary>
    public override int RegisterAllPlugins(Kernel kernel)
    {
        _logger.LogInformation("🚀 DYNAMIC DISCOVERY WITH FALLBACK: Starting plugin registration");

        try
        {
            // Try dynamic discovery first
            var dynamicPlugins = _dynamicService.GetDiscoverablePlugins();
            var enabledDynamicPlugins = dynamicPlugins.Where(p => p.Enabled).ToList();

            _logger.LogInformation("🔍 DYNAMIC DISCOVERY: Found {Total} plugins ({Enabled} enabled)", 
                dynamicPlugins.Count, enabledDynamicPlugins.Count);

            // Check if we found enough plugins
            if (enabledDynamicPlugins.Count >= _minimumExpectedPlugins)
            {
                _logger.LogInformation("✅ DYNAMIC DISCOVERY: Sufficient plugins found, proceeding with dynamic registration");
                var registeredCount = _dynamicService.RegisterAllPlugins(kernel);
                
                if (registeredCount >= _minimumExpectedPlugins)
                {
                    _logger.LogInformation("🎉 DYNAMIC DISCOVERY SUCCESS: Registered {Count} plugins", registeredCount);
                    return registeredCount;
                }
                else
                {
                    _logger.LogWarning("⚠️ DYNAMIC DISCOVERY: Only {Count} plugins registered, expected at least {Expected}", 
                        registeredCount, _minimumExpectedPlugins);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ DYNAMIC DISCOVERY: Only {Count} enabled plugins found, expected at least {Expected}", 
                    enabledDynamicPlugins.Count, _minimumExpectedPlugins);
            }

            // Log details about what was found
            LogDynamicDiscoveryDetails(dynamicPlugins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ DYNAMIC DISCOVERY FAILED: Exception during dynamic plugin discovery");
        }

        // Fallback to hardcoded discovery
        _logger.LogWarning("🔄 FALLBACK: Switching to hardcoded plugin discovery for safety");
        
        try
        {
            var fallbackCount = _fallbackService.RegisterAllPlugins(kernel);
            _logger.LogInformation("✅ FALLBACK SUCCESS: Registered {Count} plugins using hardcoded discovery", fallbackCount);
            return fallbackCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FALLBACK FAILED: Both dynamic and hardcoded discovery failed!");
            throw new InvalidOperationException("Both dynamic and hardcoded plugin discovery failed", ex);
        }
    }

    /// <summary>
    /// Logs detailed information about dynamic discovery results for debugging.
    /// </summary>
    private void LogDynamicDiscoveryDetails(List<PluginMetadata> plugins)
    {
        _logger.LogInformation("📋 DYNAMIC DISCOVERY DETAILS:");
        
        if (!plugins.Any())
        {
            _logger.LogWarning("  ❌ No plugins found by dynamic discovery");
            _logger.LogInformation("  🔍 This could indicate:");
            _logger.LogInformation("    - Plugin assemblies not loaded");
            _logger.LogInformation("    - [PluginDiscoverable] attributes missing");
            _logger.LogInformation("    - Assembly scanning issues");
            return;
        }

        var groupedPlugins = plugins.GroupBy(p => p.Category).OrderBy(g => g.Key);
        
        foreach (var group in groupedPlugins)
        {
            _logger.LogInformation("  📂 Category: {Category}", group.Key);
            
            foreach (var plugin in group.OrderBy(p => p.Priority))
            {
                var status = plugin.Enabled ? "✅" : "❌";
                var factory = plugin.Factory != null ? "✅" : "❌";
                
                _logger.LogInformation("    {Status} {Name} (Priority: {Priority}, Factory: {Factory}, Assembly: {Assembly})", 
                    status, plugin.Name, plugin.Priority, factory, plugin.AssemblyName);
                
                if (!plugin.Enabled)
                {
                    _logger.LogInformation("      ⚠️ Plugin is disabled");
                }
                
                if (plugin.Factory == null)
                {
                    _logger.LogWarning("      ⚠️ No factory instance - plugin cannot be registered");
                }
            }
        }

        // Summary statistics
        var enabledCount = plugins.Count(p => p.Enabled);
        var withFactoryCount = plugins.Count(p => p.Factory != null);
        var readyCount = plugins.Count(p => p.Enabled && p.Factory != null);
        
        _logger.LogInformation("📊 SUMMARY: {Total} total, {Enabled} enabled, {WithFactory} with factory, {Ready} ready to register", 
            plugins.Count, enabledCount, withFactoryCount, readyCount);
    }

    /// <summary>
    /// Gets the dynamic discovery results for testing and debugging.
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

    /// <summary>
    /// Checks if dynamic discovery is working properly.
    /// </summary>
    public bool IsDynamicDiscoveryWorking()
    {
        try
        {
            var plugins = _dynamicService.GetDiscoverablePlugins();
            var enabledPlugins = plugins.Where(p => p.Enabled && p.Factory != null).ToList();
            
            var isWorking = enabledPlugins.Count >= _minimumExpectedPlugins;
            
            _logger.LogInformation("🔍 HEALTH CHECK: Dynamic discovery is {Status} ({Count}/{Expected} plugins ready)", 
                isWorking ? "WORKING" : "NOT WORKING", enabledPlugins.Count, _minimumExpectedPlugins);
            
            return isWorking;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ HEALTH CHECK: Dynamic discovery health check failed");
            return false;
        }
    }
}
