using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Generates MCP capabilities dynamically from plugin metadata according to Microsoft MCP 2025-06-18 standards
/// </summary>
public class DynamicMcpCapabilitiesGenerator
{
    private readonly ILogger<DynamicMcpCapabilitiesGenerator> _logger;
    private readonly ModularSystemPromptAssembler _assembler;

    public DynamicMcpCapabilitiesGenerator(
        ILogger<DynamicMcpCapabilitiesGenerator> logger,
        ModularSystemPromptAssembler assembler)
    {
        _logger = logger;
        _assembler = assembler;
    }

    /// <summary>
    /// Generates MCP server capabilities from plugins.json according to MCP 2025-06-18 specification
    /// </summary>
    public async Task<JsonNode> GenerateServerCapabilitiesAsync()
    {
        try
        {
            _logger.LogInformation("🔧 Generating MCP server capabilities from plugins.json...");

            var (_, _, pluginsCatalog) = await _assembler.GetComponentsAsync();
            
            if (pluginsCatalog?.Plugins == null)
            {
                _logger.LogWarning("⚠️ No plugins found in plugins.json, returning minimal capabilities");
                return CreateMinimalCapabilities();
            }

            var capabilities = new JsonObject
            {
                ["capabilities"] = new JsonObject
                {
                    ["tools"] = new JsonObject
                    {
                        ["listChanged"] = true
                    }
                }
            };

            _logger.LogInformation("✅ Generated MCP server capabilities for {PluginCount} plugins", 
                pluginsCatalog.Plugins.Count);

            return capabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate MCP server capabilities");
            return CreateMinimalCapabilities();
        }
    }

    /// <summary>
    /// Generates MCP tools list from plugins.json according to MCP 2025-06-18 specification
    /// </summary>
    public async Task<JsonNode> GenerateToolsListAsync()
    {
        try
        {
            _logger.LogInformation("🔧 Generating MCP tools list from plugins.json...");

            var (_, _, pluginsCatalog) = await _assembler.GetComponentsAsync();
            
            if (pluginsCatalog?.Plugins == null)
            {
                _logger.LogWarning("⚠️ No plugins found in plugins.json, returning empty tools list");
                return new JsonObject { ["tools"] = new JsonArray() };
            }

            var tools = new JsonArray();
            var totalFunctions = 0;

            foreach (var plugin in pluginsCatalog.Plugins)
            {
                if (plugin.Functions?.Any() == true)
                {
                    foreach (var function in plugin.Functions)
                    {
                        var tool = CreateMcpTool(plugin, function);
                        tools.Add(tool);
                        totalFunctions++;
                    }
                }
            }

            var result = new JsonObject
            {
                ["tools"] = tools
            };

            _logger.LogInformation("✅ Generated {FunctionCount} MCP tools from {PluginCount} plugins", 
                totalFunctions, pluginsCatalog.Plugins.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate MCP tools list");
            return new JsonObject { ["tools"] = new JsonArray() };
        }
    }

    /// <summary>
    /// Creates an MCP tool definition according to MCP 2025-06-18 specification
    /// </summary>
    private JsonObject CreateMcpTool(PluginDefinition plugin, PluginFunction function)
    {
        // Create unique tool name: PluginName.FunctionName
        var toolName = $"{plugin.Name}.{function.Name}";

        var tool = new JsonObject
        {
            ["name"] = toolName,
            ["description"] = function.Description ?? $"Function {function.Name} from {plugin.Name}",
            ["inputSchema"] = CreateInputSchema(function)
        };

        // Add optional title for better display
        if (!string.IsNullOrEmpty(plugin.Description))
        {
            tool["title"] = $"{plugin.Name}: {function.Name}";
        }

        // Add annotations for security and trust (MCP 2025-06-18 requirement)
        tool["annotations"] = new JsonObject
        {
            ["audience"] = new JsonArray { "assistant" },
            ["priority"] = 0.5
        };

        return tool;
    }

    /// <summary>
    /// Creates JSON Schema for function input parameters according to MCP specification
    /// </summary>
    private JsonObject CreateInputSchema(PluginFunction function)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
            ["required"] = new JsonArray()
        };

        var properties = schema["properties"]!.AsObject();
        var required = schema["required"]!.AsArray();

        if (function.Parameters?.Any() == true)
        {
            foreach (var param in function.Parameters)
            {
                var paramSchema = new JsonObject
                {
                    ["type"] = MapTypeToJsonSchemaType(param.Value.Type),
                    ["description"] = param.Value.Description ?? $"Parameter {param.Key}"
                };

                // Add default value if specified
                if (param.Value.Default != null)
                {
                    paramSchema["default"] = JsonValue.Create(param.Value.Default);
                }

                // Add format if specified
                if (!string.IsNullOrEmpty(param.Value.Format))
                {
                    paramSchema["format"] = param.Value.Format;
                }

                properties[param.Key] = paramSchema;

                // Add to required array if parameter is required
                if (param.Value.Required)
                {
                    required.Add(param.Key);
                }
            }
        }

        return schema;
    }

    /// <summary>
    /// Maps .NET types to JSON Schema types according to MCP specification
    /// </summary>
    private string MapTypeToJsonSchemaType(string dotNetType)
    {
        return dotNetType.ToLowerInvariant() switch
        {
            "string" => "string",
            "integer" or "int32" or "int64" or "int" => "integer",
            "number" or "double" or "float" or "decimal" => "number",
            "boolean" or "bool" => "boolean",
            "array" => "array",
            "object" => "object",
            _ => "string" // Default to string for unknown types
        };
    }

    /// <summary>
    /// Creates minimal MCP capabilities for fallback scenarios
    /// </summary>
    private JsonObject CreateMinimalCapabilities()
    {
        return new JsonObject
        {
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject
                {
                    ["listChanged"] = true
                }
            }
        };
    }

    /// <summary>
    /// Validates that generated capabilities conform to MCP 2025-06-18 specification
    /// </summary>
    public bool ValidateMcpCompliance(JsonNode capabilities)
    {
        try
        {
            // Check required top-level structure
            if (capabilities["capabilities"] == null)
            {
                _logger.LogWarning("⚠️ MCP capabilities missing required 'capabilities' field");
                return false;
            }

            var caps = capabilities["capabilities"]!.AsObject();
            
            // Check tools capability structure
            if (caps["tools"] != null)
            {
                var tools = caps["tools"]!.AsObject();
                if (!tools.ContainsKey("listChanged"))
                {
                    _logger.LogWarning("⚠️ MCP tools capability missing required 'listChanged' field");
                    return false;
                }
            }

            _logger.LogDebug("✅ MCP capabilities validation passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MCP capabilities validation failed");
            return false;
        }
    }

    /// <summary>
    /// Gets statistics about generated MCP capabilities
    /// </summary>
    public async Task<McpCapabilitiesStats> GetStatsAsync()
    {
        var (_, _, pluginsCatalog) = await _assembler.GetComponentsAsync();
        
        var stats = new McpCapabilitiesStats
        {
            PluginCount = pluginsCatalog?.Plugins?.Count ?? 0,
            FunctionCount = pluginsCatalog?.Plugins?.Sum(p => p.Functions?.Count ?? 0) ?? 0,
            LastGenerated = pluginsCatalog?.Metadata?.GeneratedAt ?? DateTime.MinValue,
            McpVersion = "2025-06-18"
        };

        return stats;
    }
}

/// <summary>
/// Statistics about generated MCP capabilities
/// </summary>
public class McpCapabilitiesStats
{
    public int PluginCount { get; set; }
    public int FunctionCount { get; set; }
    public DateTime LastGenerated { get; set; }
    public string McpVersion { get; set; } = string.Empty;
}
