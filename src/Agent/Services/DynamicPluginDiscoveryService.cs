using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Shared;
using Shared.Attributes;
using Shared.Models;

namespace Agent.Services;

/// <summary>
/// Dynamic plugin discovery service that scans assemblies for [PluginDiscoverable] attributes.
/// This replaces hardcoded plugin lists with automatic discovery.
/// </summary>
public class DynamicPluginDiscoveryService : PluginDiscoveryService
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPluginMetadataExtractor _metadataExtractor;
    private readonly ILogger<DynamicPluginDiscoveryService> _logger;
    private List<PluginMetadata>? _cachedPlugins;
    private readonly object _cacheLock = new();

    public DynamicPluginDiscoveryService(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IPluginMetadataExtractor? metadataExtractor = null) : base(configuration, loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _metadataExtractor = metadataExtractor ?? new PluginMetadataExtractor(configuration, loggerFactory);
        _logger = loggerFactory.CreateLogger<DynamicPluginDiscoveryService>();
    }

    /// <summary>
    /// Registers all discoverable plugins with the kernel.
    /// </summary>
    public override int RegisterAllPlugins(Kernel kernel)
    {
        var plugins = GetDiscoverablePlugins();
        var registeredCount = 0;

        _logger.LogInformation("🔍 DYNAMIC DISCOVERY: Starting plugin registration for {Count} discovered plugins", plugins.Count);

        foreach (var plugin in plugins.Where(p => p.Enabled).OrderBy(p => p.Priority))
        {
            try
            {
                _logger.LogDebug("Registering plugin: {PluginName} (Priority: {Priority})", plugin.Name, plugin.Priority);

                if (plugin.Factory == null)
                {
                    _logger.LogWarning("Plugin {PluginName} has no factory instance, skipping", plugin.Name);
                    continue;
                }

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

        _logger.LogInformation("🎉 DYNAMIC DISCOVERY: Registered {RegisteredCount}/{TotalCount} plugins successfully", 
            registeredCount, plugins.Count(p => p.Enabled));

        return registeredCount;
    }

    /// <summary>
    /// Gets all discoverable plugins from assemblies.
    /// </summary>
    public List<PluginMetadata> GetDiscoverablePlugins()
    {
        lock (_cacheLock)
        {
            if (_cachedPlugins != null)
            {
                _logger.LogDebug("Returning cached plugin metadata ({Count} plugins)", _cachedPlugins.Count);
                return _cachedPlugins;
            }

            _logger.LogInformation("🔍 DYNAMIC DISCOVERY: Scanning assemblies for [PluginDiscoverable] attributes");

            var plugins = new List<PluginMetadata>();
            var assemblies = GetReferencedAssemblies();

            _logger.LogInformation("📦 ASSEMBLY SCAN: Found {Count} assemblies to scan", assemblies.Length);
            foreach (var assembly in assemblies)
            {
                _logger.LogInformation("  📄 Assembly: {AssemblyName}", assembly.GetName().Name);
            }

            foreach (var assembly in assemblies)
            {
                try
                {
                    var pluginTypes = ScanAssemblyForPlugins(assembly);
                    plugins.AddRange(pluginTypes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to scan assembly {AssemblyName} for plugins", assembly.FullName);
                }
            }

            _cachedPlugins = plugins;
            _logger.LogInformation("🎉 DYNAMIC DISCOVERY: Found {Count} discoverable plugins", plugins.Count);

            return plugins;
        }
    }

    /// <summary>
    /// Scans a single assembly for plugin types with [PluginDiscoverable] attributes.
    /// </summary>
    private List<PluginMetadata> ScanAssemblyForPlugins(Assembly assembly)
    {
        var plugins = new List<PluginMetadata>();

        _logger.LogDebug("🔍 Scanning assembly {AssemblyName} for plugin types", assembly.GetName().Name);

        var allTypes = assembly.GetTypes();
        var typesWithAttribute = allTypes.Where(t => t.GetCustomAttribute<PluginDiscoverableAttribute>() != null).ToList();
        var factoryTypes = typesWithAttribute.Where(t => typeof(IKernelPluginFactory).IsAssignableFrom(t)).ToList();
        var pluginTypes = factoryTypes.Where(t => !t.IsAbstract && !t.IsInterface).ToList();

        _logger.LogDebug("  📊 Assembly {AssemblyName}: {TotalTypes} total types, {WithAttribute} with [PluginDiscoverable], {FactoryTypes} implementing IKernelPluginFactory, {PluginTypes} valid plugin types",
            assembly.GetName().Name, allTypes.Length, typesWithAttribute.Count, factoryTypes.Count, pluginTypes.Count);

        foreach (var type in pluginTypes)
        {
            try
            {
                var metadata = ExtractPluginMetadata(type);
                plugins.Add(metadata);
                
                _logger.LogDebug("Found plugin: {PluginName} in {AssemblyName}", metadata.Name, assembly.GetName().Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract metadata for plugin type {TypeName}", type.FullName);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Extracts metadata from a plugin type with [PluginDiscoverable] attribute.
    /// </summary>
    private PluginMetadata ExtractPluginMetadata(Type factoryType)
    {
        try
        {
            return _metadataExtractor.ExtractMetadata(factoryType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract metadata for {FactoryType}, creating basic metadata", factoryType.Name);

            // Fallback to basic metadata if extraction fails
            var attribute = factoryType.GetCustomAttribute<PluginDiscoverableAttribute>()!;
            return new PluginMetadata
            {
                Name = attribute.Name,
                Description = attribute.Description,
                Category = attribute.Category,
                Priority = attribute.Priority,
                Enabled = attribute.Enabled,
                Version = attribute.Version,
                Tags = attribute.Tags,
                FactoryType = factoryType,
                Factory = null,
                AssemblyName = factoryType.Assembly.GetName().Name ?? "Unknown",
                Functions = new List<FunctionMetadata>()
            };
        }
    }

    /// <summary>
    /// Gets assemblies to scan for plugins.
    /// </summary>
    private Assembly[] GetReferencedAssemblies()
    {
        var assemblies = new HashSet<Assembly>();

        // Add current assembly
        assemblies.Add(Assembly.GetExecutingAssembly());

        // Get all loaded assemblies in the current AppDomain
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in loadedAssemblies)
        {
            try
            {
                var assemblyName = assembly.GetName().Name;
                if (assemblyName != null && (
                    assemblyName.EndsWith("Plugin", StringComparison.OrdinalIgnoreCase) ||
                    assemblyName.Contains("Plugin", StringComparison.OrdinalIgnoreCase) ||
                    assemblyName.Equals("Shared", StringComparison.OrdinalIgnoreCase) ||
                    assemblyName.Equals("Agent", StringComparison.OrdinalIgnoreCase)))
                {
                    assemblies.Add(assembly);
                    _logger.LogDebug("Added loaded assembly for plugin scanning: {AssemblyName}", assemblyName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process loaded assembly {AssemblyName}", assembly.FullName);
            }
        }

        // Also try to load plugin assemblies from disk if not already loaded
        try
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var pluginFiles = Directory.GetFiles(baseDirectory, "*Plugin.dll", SearchOption.TopDirectoryOnly);

            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(pluginFile);

                    // Check if already loaded
                    if (assemblies.Any(a => a.GetName().Name?.Equals(assemblyName, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        continue;
                    }

                    var assembly = Assembly.LoadFrom(pluginFile);
                    assemblies.Add(assembly);
                    _logger.LogDebug("Loaded plugin assembly from disk: {AssemblyName}", assemblyName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load plugin assembly from {PluginFile}", pluginFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan for plugin assemblies on disk");
        }

        _logger.LogInformation("Scanning {Count} assemblies for plugins: {AssemblyNames}",
            assemblies.Count, string.Join(", ", assemblies.Select(a => a.GetName().Name)));

        return assemblies.ToArray();
    }

    /// <summary>
    /// Clears the plugin cache to force re-discovery.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedPlugins = null;
            _logger.LogInformation("Plugin discovery cache cleared");
        }
    }

    /// <summary>
    /// Forces a refresh of the plugin cache (alias for ClearCache)
    /// </summary>
    public void RefreshDiscovery()
    {
        ClearCache();
    }
}
