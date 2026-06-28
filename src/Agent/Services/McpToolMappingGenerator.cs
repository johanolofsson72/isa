using System.Text.Json;
using Microsoft.SemanticKernel;
using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Generates MCP tool mapping for hybrid mode (individual tools + execute_task)
/// </summary>
public class McpToolMappingGenerator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpToolMappingGenerator> _logger;
    private readonly string _toolMappingPath;

    public McpToolMappingGenerator(
        IServiceProvider serviceProvider,
        ILogger<McpToolMappingGenerator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _toolMappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mcp-tool-mapping.json");
    }

    /// <summary>
    /// Checks if tool mapping needs regeneration
    /// </summary>
    public bool ShouldRegenerateToolMapping()
    {
        if (!File.Exists(_toolMappingPath))
        {
            _logger.LogInformation("🔧 Tool mapping file not found, regeneration needed");
            return true;
        }

        var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(_toolMappingPath);
        if (fileAge.TotalHours > 24)
        {
            _logger.LogInformation("🔧 Tool mapping file is older than 24 hours, regeneration needed");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates tool mapping from Semantic Kernel plugins
    /// </summary>
    public async Task<bool> GenerateToolMappingAsync()
    {
        try
        {
            _logger.LogInformation("🔧 Generating MCP tool mapping...");

            using var scope = _serviceProvider.CreateScope();
            var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();

            var toolMapping = new ToolMapping
            {
                GeneratedAt = DateTime.UtcNow,
                Tools = new Dictionary<string, ToolMappingEntry>()
            };

            int toolCount = 0;
            int pluginCount = 0;

            foreach (var plugin in kernel.Plugins)
            {
                pluginCount++;
                _logger.LogDebug("🔧 Processing plugin: {PluginName}", plugin.Name);

                foreach (var function in plugin)
                {
                    var toolName = $"{plugin.Name}_{function.Name}".ToLowerInvariant();
                    
                    var parameters = function.Metadata.Parameters.Select(p => new ToolParameter
                    {
                        Name = p.Name,
                        Type = MapParameterType(p.ParameterType),
                        Description = p.Description ?? "",
                        Required = p.IsRequired
                    }).ToList();

                    toolMapping.Tools[toolName] = new ToolMappingEntry
                    {
                        ToolName = toolName,
                        PluginName = plugin.Name,
                        FunctionName = function.Name,
                        Description = function.Description ?? $"Execute {function.Name} from {plugin.Name}",
                        Parameters = parameters
                    };

                    toolCount++;
                }
            }

            toolMapping.TotalPlugins = pluginCount;
            toolMapping.TotalTools = toolCount;

            var json = JsonSerializer.Serialize(toolMapping, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(_toolMappingPath, json);

            _logger.LogInformation("✅ Generated tool mapping with {ToolCount} tools from {PluginCount} plugins", 
                toolCount, pluginCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate tool mapping");
            return false;
        }
    }

    /// <summary>
    /// Loads existing tool mapping
    /// </summary>
    public async Task<ToolMapping?> LoadToolMappingAsync()
    {
        try
        {
            if (!File.Exists(_toolMappingPath))
            {
                _logger.LogWarning("⚠️ Tool mapping file not found: {Path}", _toolMappingPath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_toolMappingPath);
            var toolMapping = JsonSerializer.Deserialize<ToolMapping>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogDebug("📋 Loaded tool mapping with {ToolCount} tools", toolMapping?.Tools?.Count ?? 0);
            return toolMapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load tool mapping");
            return null;
        }
    }

    /// <summary>
    /// Maps .NET parameter types to JSON schema types
    /// </summary>
    private static string MapParameterType(Type? parameterType)
    {
        if (parameterType == null) return "string";

        return parameterType.Name.ToLowerInvariant() switch
        {
            "string" => "string",
            "int32" or "int64" or "int16" => "integer",
            "double" or "float" or "decimal" => "number",
            "boolean" => "boolean",
            "datetime" => "string",
            _ => "string"
        };
    }
}
