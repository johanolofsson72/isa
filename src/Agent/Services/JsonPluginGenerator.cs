using System.Reflection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Agent.Models;
using Agent.Services;

namespace Agent.Services;

/// <summary>
/// Service for generating plugins.json from discovered plugins
/// </summary>
public class JsonPluginGenerator
{
    private readonly ILogger<JsonPluginGenerator> _logger;
    private readonly DynamicPluginDiscoveryService _discoveryService;
    private readonly PluginMetadataExtractor _metadataExtractor;
    private readonly string _pluginsJsonPath;

    public JsonPluginGenerator(
        ILogger<JsonPluginGenerator> logger,
        DynamicPluginDiscoveryService discoveryService,
        PluginMetadataExtractor metadataExtractor,
        IConfiguration configuration)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _metadataExtractor = metadataExtractor;
        _pluginsJsonPath = Path.Combine(
            configuration["ContentRoot"] ?? Directory.GetCurrentDirectory(),
            "plugins.json");
    }

    /// <summary>
    /// Generates or updates plugins.json file from discovered plugins
    /// </summary>
    public async Task<bool> GeneratePluginsJsonAsync()
    {
        try
        {
            _logger.LogInformation("🔄 Starting plugins.json generation...");

            // Discover all available plugins
            var discoveredPlugins = _discoveryService.GetDiscoverablePlugins();
            _logger.LogInformation("📦 Discovered {Count} plugins for JSON generation", discoveredPlugins.Count);

            // Convert to JSON structure
            var pluginsCatalog = await ConvertToPluginsCatalogAsync(discoveredPlugins);

            // Write to file
            await WritePluginsJsonAsync(pluginsCatalog);

            _logger.LogInformation("✅ Successfully generated plugins.json with {PluginCount} plugins and {FunctionCount} functions",
                pluginsCatalog.Plugins.Count, pluginsCatalog.Metadata?.TotalFunctions ?? 0);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate plugins.json");
            return false;
        }
    }

    /// <summary>
    /// Checks if plugins.json needs to be regenerated
    /// </summary>
    public bool ShouldRegeneratePluginsJson()
    {
        try
        {
            if (!File.Exists(_pluginsJsonPath))
            {
                _logger.LogInformation("📄 plugins.json does not exist, regeneration needed");
                return true;
            }

            var fileInfo = new FileInfo(_pluginsJsonPath);
            var ageInHours = (DateTime.Now - fileInfo.LastWriteTime).TotalHours;

            // Regenerate if older than 1 hour or if any plugin assembly is newer
            if (ageInHours > 1)
            {
                _logger.LogInformation("⏰ plugins.json is {Age:F1} hours old, regeneration needed", ageInHours);
                return true;
            }

            // Check if any plugin assemblies are newer than the JSON file
            var pluginAssemblies = GetPluginAssemblyFiles();
            foreach (var assembly in pluginAssemblies)
            {
                if (File.GetLastWriteTime(assembly) > fileInfo.LastWriteTime)
                {
                    _logger.LogInformation("🔄 Plugin assembly {Assembly} is newer than plugins.json, regeneration needed", 
                        Path.GetFileName(assembly));
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Error checking if plugins.json needs regeneration, assuming yes");
            return true;
        }
    }

    /// <summary>
    /// Converts discovered plugins to PluginsCatalog structure
    /// </summary>
    private Task<PluginsCatalog> ConvertToPluginsCatalogAsync(List<Shared.Models.PluginMetadata> discoveredPlugins)
    {
        var catalog = new PluginsCatalog();
        var totalFunctions = 0;

        foreach (var pluginMetadata in discoveredPlugins)
        {
            try
            {
                var pluginDefinition = new PluginDefinition
                {
                    Name = pluginMetadata.Name,
                    Description = pluginMetadata.Description,
                    Category = pluginMetadata.Category,
                    Version = pluginMetadata.Version,
                    Functions = new List<PluginFunction>()
                };

                // Convert functions
                foreach (var function in pluginMetadata.Functions)
                {
                    var pluginFunction = new PluginFunction
                    {
                        Name = function.Name,
                        Description = function.Description,
                        Parameters = new Dictionary<string, PluginParameter>()
                    };

                    // Convert parameters
                    foreach (var param in function.Parameters)
                    {
                        pluginFunction.Parameters[param.Name] = new PluginParameter
                        {
                            Type = MapTypeToJsonType(param.Type?.Name ?? "string"),
                            Required = param.IsRequired,
                            Description = param.Description,
                            Default = param.DefaultValue
                        };
                    }

                    pluginDefinition.Functions.Add(pluginFunction);
                    totalFunctions++;
                }

                catalog.Plugins.Add(pluginDefinition);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to convert plugin {PluginName} to JSON", pluginMetadata.Name);
            }
        }

        // Add metadata
        catalog.Metadata = new Models.PluginMetadata
        {
            GeneratedAt = DateTime.UtcNow,
            Version = "1.0",
            TotalPlugins = catalog.Plugins.Count,
            TotalFunctions = totalFunctions
        };

        return Task.FromResult(catalog);
    }

    /// <summary>
    /// Writes the plugins catalog to JSON file
    /// </summary>
    private async Task WritePluginsJsonAsync(PluginsCatalog catalog)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(catalog, options);
        await File.WriteAllTextAsync(_pluginsJsonPath, json);

        _logger.LogDebug("📝 Written plugins.json to {Path} ({Size} bytes)", _pluginsJsonPath, json.Length);
    }

    /// <summary>
    /// Gets plugin assembly files for timestamp comparison
    /// </summary>
    private List<string> GetPluginAssemblyFiles()
    {
        var files = new List<string>();
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            files.AddRange(Directory.GetFiles(baseDirectory, "*Plugin.dll", SearchOption.TopDirectoryOnly));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to get plugin assembly files");
        }

        return files;
    }



    /// <summary>
    /// Maps .NET types to JSON schema types
    /// </summary>
    private string MapTypeToJsonType(string dotNetType)
    {
        return dotNetType.ToLowerInvariant() switch
        {
            "string" => "string",
            "int32" or "int64" or "int" => "integer",
            "double" or "float" or "decimal" => "number",
            "boolean" or "bool" => "boolean",
            "datetime" => "string",
            _ => "string"
        };
    }
}
