using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agent.Services;

/// <summary>
/// Service responsible for automatically updating systemprompt.txt when new plugins are loaded
/// </summary>
public class SystemPromptUpdater
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemPromptUpdater> _logger;
    private readonly string _systemPromptPath;

    public SystemPromptUpdater(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = loggerFactory?.CreateLogger<SystemPromptUpdater>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        
        // Get system prompt file path from configuration
        _systemPromptPath = _configuration.GetValue<string>("LLM:SystemPromptFile") ?? "systemprompt.txt";
        
        if (!Path.IsPathRooted(_systemPromptPath))
        {
            _systemPromptPath = Path.Combine(Directory.GetCurrentDirectory(), _systemPromptPath);
        }
    }

    /// <summary>
    /// Updates system prompt with plugin documentation if not already present
    /// </summary>
    /// <param name="pluginName">Name of the plugin</param>
    /// <param name="pluginCapabilitiesPath">Path to plugin capabilities file (optional)</param>
    /// <returns>True if system prompt was updated, false if plugin was already documented</returns>
    public async Task<bool> EnsurePluginDocumentedAsync(string pluginName, string? pluginCapabilitiesPath = null)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
        {
            throw new ArgumentException("Plugin name cannot be null or empty", nameof(pluginName));
        }

        try
        {
            // Check if systemprompt.txt exists
            if (!File.Exists(_systemPromptPath))
            {
                _logger.LogWarning("System prompt file not found at: {Path}", _systemPromptPath);
                return false;
            }

            // Read current system prompt content
            var currentContent = await File.ReadAllTextAsync(_systemPromptPath);

            // Check if plugin is already documented
            if (IsPluginDocumented(currentContent, pluginName))
            {
                _logger.LogDebug("Plugin {PluginName} is already documented in system prompt", pluginName);
                return false;
            }

            // Load plugin capabilities
            var capabilities = await LoadPluginCapabilitiesAsync(pluginName, pluginCapabilitiesPath);
            if (capabilities == null)
            {
                _logger.LogWarning("Could not load capabilities for plugin {PluginName}", pluginName);
                return false;
            }

            // Generate plugin documentation
            var pluginDocumentation = GeneratePluginDocumentation(capabilities);
            
            // Update system prompt
            var updatedContent = AppendPluginDocumentation(currentContent, pluginDocumentation);
            
            // Write back to file
            await File.WriteAllTextAsync(_systemPromptPath, updatedContent);
            
            _logger.LogInformation("Added documentation for plugin {PluginName} to system prompt", pluginName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update system prompt for plugin {PluginName}", pluginName);
            return false;
        }
    }

    /// <summary>
    /// Checks if a plugin is already documented in the system prompt
    /// </summary>
    private bool IsPluginDocumented(string content, string pluginName)
    {
        // Look for plugin references in various formats that might exist in systemprompt.txt
        var searchPatterns = new[]
        {
            $@"\b{Regex.Escape(pluginName)}\b",                    // Exact plugin name
            $@"\b{Regex.Escape(pluginName.Replace("Plugin", ""))}\b", // Plugin name without "Plugin" suffix
            $@"\b{Regex.Escape(pluginName.ToLower())}\b",          // Lowercase version
        };

        foreach (var pattern in searchPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogDebug("Found existing documentation for plugin {PluginName} using pattern: {Pattern}", pluginName, pattern);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Loads plugin capabilities from capabilities file or discovers it automatically
    /// </summary>
    private async Task<PluginCapabilities?> LoadPluginCapabilitiesAsync(string pluginName, string? capabilitiesPath)
    {
        try
        {
            // If no path provided, try to find it automatically
            if (string.IsNullOrWhiteSpace(capabilitiesPath))
            {
                capabilitiesPath = FindPluginCapabilitiesFile(pluginName);
            }

            if (string.IsNullOrWhiteSpace(capabilitiesPath) || !File.Exists(capabilitiesPath))
            {
                _logger.LogDebug("Plugin capabilities file not found for {PluginName} at {Path}", pluginName, capabilitiesPath);
                return null;
            }

            var json = await File.ReadAllTextAsync(capabilitiesPath);
            var capabilities = JsonSerializer.Deserialize<PluginCapabilities>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogDebug("Loaded capabilities for plugin {PluginName} from {Path}", pluginName, capabilitiesPath);
            return capabilities;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load capabilities for plugin {PluginName}", pluginName);
            return null;
        }
    }

    /// <summary>
    /// Attempts to find the plugin capabilities file automatically
    /// </summary>
    private string? FindPluginCapabilitiesFile(string pluginName)
    {
        // Try common locations relative to the current directory
        var possiblePaths = new[]
        {
            Path.Combine("src", pluginName, "plugin.capabilities"),
            Path.Combine("..", pluginName, "plugin.capabilities"),
            Path.Combine(".", pluginName, "plugin.capabilities"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                _logger.LogDebug("Found capabilities file for {PluginName} at {Path}", pluginName, fullPath);
                return fullPath;
            }
        }

        _logger.LogDebug("Could not find capabilities file for plugin {PluginName}", pluginName);
        return null;
    }

    /// <summary>
    /// Generates Swedish documentation for a plugin based on its capabilities
    /// </summary>
    private string GeneratePluginDocumentation(PluginCapabilities capabilities)
    {
        var documentation = new List<string>();
        
        // Add header
        documentation.Add($"📦 {capabilities.Name} - {capabilities.Description}");
        documentation.Add($"När användaren vill använda {capabilities.Name.Replace("Plugin", "").ToLower()}:");

        // Add functions
        foreach (var function in capabilities.Functions ?? [])
        {
            documentation.Add($"- För att {function.Description.ToLower()}: Anropa funktionen {capabilities.Name.Replace("Plugin", "")}.{function.Name}");
            
            if (function.Parameters?.Any() == true)
            {
                foreach (var param in function.Parameters)
                {
                    documentation.Add($"  - {param.Key}: {param.Value}");
                }
            }
        }

        documentation.Add(""); // Empty line for spacing
        documentation.Add("---");
        documentation.Add(""); // Empty line after separator

        return string.Join(Environment.NewLine, documentation);
    }

    /// <summary>
    /// Appends plugin documentation to the system prompt content
    /// </summary>
    private string AppendPluginDocumentation(string currentContent, string pluginDocumentation)
    {
        // Find a good place to insert the documentation
        // Look for the end of the plugins section, typically before "---" or similar separators
        var lines = currentContent.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.None).ToList();
        
        // Find the last plugin section or a suitable insertion point
        var insertIndex = FindPluginInsertionPoint(lines);
        
        // Insert the new plugin documentation
        var docLines = pluginDocumentation.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.None);
        lines.InsertRange(insertIndex, docLines);
        
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Finds the best location to insert new plugin documentation
    /// </summary>
    private int FindPluginInsertionPoint(List<string> lines)
    {
        // Look for the last plugin section or similar patterns
        int lastPluginSection = -1;
        
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            
            // Look for typical plugin section headers (with emojis and plugin names)
            if (line.StartsWith("📦") || line.StartsWith("🔍") || line.StartsWith("📂") || 
                line.StartsWith("📅") || line.StartsWith("☁️") || line.StartsWith("📊") ||
                line.StartsWith("👥") || line.StartsWith("🌍") || line.StartsWith("🕐") ||
                line.StartsWith("🛒"))
            {
                // Look for the end of this plugin section (next section or separator)
                for (int j = i + 1; j < lines.Count; j++)
                {
                    var nextLine = lines[j].Trim();
                    if (nextLine.StartsWith("---") || nextLine.StartsWith("📦") || 
                        nextLine.StartsWith("🔍") || nextLine.StartsWith("📂") ||
                        nextLine.StartsWith("✅") || nextLine.StartsWith("🟢"))
                    {
                        lastPluginSection = j;
                        break;
                    }
                }
                break;
            }
        }
        
        // If we found a plugin section, insert after it
        if (lastPluginSection > 0)
        {
            return lastPluginSection;
        }
        
        // Look for specific markers indicating end of plugins section
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("✅ Slutnot:") || line.StartsWith("🟢 Kör nu.") || 
                line.Contains("Slutnot") || line.Contains("Kör nu"))
            {
                return i; // Insert before the end marker
            }
        }
        
        // If no good insertion point found, insert near the end but before final content
        if (lines.Count > 10)
        {
            return lines.Count - 5; // Insert a few lines before the end
        }
        
        return lines.Count;
    }

    /// <summary>
    /// Data structure for plugin capabilities
    /// </summary>
    public class PluginCapabilities
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<PluginFunction>? Functions { get; set; }
    }

    /// <summary>
    /// Data structure for plugin function
    /// </summary>
    public class PluginFunction
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string>? Parameters { get; set; }
    }
}