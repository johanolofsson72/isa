using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Agent.Services;

/// <summary>
/// Service for generating dynamic system prompts by combining static content 
/// with automatically generated plugin descriptions from code metadata.
/// </summary>
public interface IDynamicSystemPromptGenerator
{
    /// <summary>
    /// Generate the complete system prompt by combining static content with dynamic plugin sections.
    /// </summary>
    Task<string> GenerateSystemPromptAsync();
    
    /// <summary>
    /// Generate only the plugin sections for testing or debugging.
    /// </summary>
    Task<string> GeneratePluginSectionsAsync();
    
    /// <summary>
    /// Get the static system prompt content without plugin sections.
    /// </summary>
    Task<string> GetStaticSystemPromptAsync();
}

public class DynamicSystemPromptGenerator : IDynamicSystemPromptGenerator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DynamicSystemPromptGenerator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PluginMetadataExtractor _metadataExtractor;
    private readonly DynamicPluginDiscoveryServiceWithFallback _pluginDiscovery;

    public DynamicSystemPromptGenerator(
        IConfiguration configuration,
        ILogger<DynamicSystemPromptGenerator> logger,
        ILoggerFactory loggerFactory,
        PluginMetadataExtractor metadataExtractor,
        DynamicPluginDiscoveryServiceWithFallback pluginDiscovery)
    {
        _configuration = configuration;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _metadataExtractor = metadataExtractor;
        _pluginDiscovery = pluginDiscovery;
    }

    public async Task<string> GenerateSystemPromptAsync()
    {
        try
        {
            var staticPrompt = await GetStaticSystemPromptAsync();
            var pluginSections = await GeneratePluginSectionsAsync();
            var usersData = await GetUsersDataAsync();

            var combinedPrompt = new StringBuilder();
            combinedPrompt.AppendLine(staticPrompt);
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine("---");
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine("# 👥 USERS DATA (Auto-Loaded)");
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine("The following user data is available from users.json:");
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine("```json");
            combinedPrompt.AppendLine(usersData);
            combinedPrompt.AppendLine("```");
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine("---");
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine("# 🔌 AVAILABLE PLUGINS (Auto-Generated)");
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine("The following plugins are automatically discovered and available for use:");
            combinedPrompt.AppendLine();
            combinedPrompt.AppendLine(pluginSections);

            var result = combinedPrompt.ToString();

            _logger.LogInformation(
                "Generated dynamic system prompt: {StaticLength} chars static + {UsersLength} chars users + {PluginLength} chars plugins = {TotalLength} chars total",
                staticPrompt.Length,
                usersData.Length,
                pluginSections.Length,
                result.Length
            );



            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dynamic system prompt, falling back to static");
            return await GetStaticSystemPromptAsync();
        }
    }

    public Task<string> GeneratePluginSectionsAsync()
    {
        try
        {
            var plugins = _pluginDiscovery.GetDynamicDiscoveryResults();
            var sections = new StringBuilder();
            
            // Group plugins by category
            var pluginsByCategory = plugins
                .GroupBy(p => p.Category ?? "Other")
                .OrderBy(g => GetCategoryOrder(g.Key));
            
            foreach (var categoryGroup in pluginsByCategory)
            {
                sections.AppendLine($"## 📂 {categoryGroup.Key}");
                sections.AppendLine();
                
                foreach (var plugin in categoryGroup.OrderBy(p => p.Priority).ThenBy(p => p.Name))
                {
                    AppendPluginSection(sections, plugin);
                    sections.AppendLine();
                }
            }
            
            return Task.FromResult(sections.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate plugin sections");
            return Task.FromResult("<!-- Plugin sections could not be generated -->");
        }
    }

    public async Task<string> GetStaticSystemPromptAsync()
    {
        try
        {
            var promptPath = _configuration.GetValue<string>("LLM:SystemPromptFile")
                ?? throw new InvalidOperationException("LLM:SystemPromptFile configuration is missing");

            if (!File.Exists(promptPath))
            {
                _logger.LogWarning("Static system prompt file not found at {Path}", promptPath);
                return GetFallbackSystemPrompt();
            }

            var content = await File.ReadAllTextAsync(promptPath);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Static system prompt file is empty at {Path}", promptPath);
                return GetFallbackSystemPrompt();
            }

            return content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load static system prompt");
            return GetFallbackSystemPrompt();
        }
    }

    private void AppendPluginSection(StringBuilder sections, PluginMetadata plugin)
    {
        try
        {
            // Create plugin instance for metadata extraction
            if (plugin.Factory == null)
            {
                _logger.LogWarning("Plugin {PluginName} has no factory, skipping metadata extraction", plugin.Name);
                return;
            }

            var pluginInstance = plugin.Factory.CreatePlugin(_configuration, _loggerFactory);
            if (pluginInstance == null)
            {
                _logger.LogWarning("Could not create instance of plugin {PluginName}", plugin.Name);
                return;
            }

            // Extract function metadata
            var functions = _metadataExtractor.ExtractFunctionMetadata(pluginInstance);
            
            sections.AppendLine($"### 🔧 {plugin.Name}");
            if (!string.IsNullOrEmpty(plugin.Description))
            {
                sections.AppendLine($"*{plugin.Description}*");
                sections.AppendLine();
            }
            
            if (functions.Any())
            {
                sections.AppendLine("**Available functions:**");
                
                foreach (var function in functions.OrderBy(f => f.Name))
                {
                    sections.AppendLine($"- **{function.Name}**: {function.Description ?? "No description available"}");
                    
                    if (function.Parameters.Any())
                    {
                        sections.AppendLine("  Parameters:");
                        foreach (var param in function.Parameters)
                        {
                            var required = param.IsRequired ? "required" : "optional";
                            var defaultValue = !param.IsRequired && param.DefaultValue != null 
                                ? $", default: {param.DefaultValue}" 
                                : "";
                            
                            sections.AppendLine($"    - {param.Name} ({param.Type?.Name ?? "unknown"}, {required}{defaultValue}): {param.Description ?? "No description"}");
                        }
                    }
                    sections.AppendLine();
                }
            }
            else
            {
                sections.AppendLine("*No functions available*");
                sections.AppendLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate section for plugin {PluginName}", plugin.Name);
            sections.AppendLine($"### ❌ {plugin.Name}");
            sections.AppendLine("*Error generating plugin documentation*");
            sections.AppendLine();
        }
    }

    private static int GetCategoryOrder(string category)
    {
        return category switch
        {
            "Communication" => 1,
            "FileManagement" => 2,
            "Calendar" => 3,
            "Search" => 4,
            "Weather" => 5,
            "Location" => 6,
            "Accounting" => 7,
            "Testing" => 8,
            _ => 9
        };
    }

    private async Task<string> GetUsersDataAsync()
    {
        try
        {
            var configPath = _configuration["ISA_CONFIG_PATH"];
            var usersPath = !string.IsNullOrEmpty(configPath)
                ? Path.Combine(configPath, "users.json")
                : Path.Combine(Directory.GetCurrentDirectory(), "users.json");

            if (!File.Exists(usersPath))
            {
                _logger.LogWarning("Users data file not found at {Path}", usersPath);
                return "{}";
            }

            var content = await File.ReadAllTextAsync(usersPath);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Users data file is empty at {Path}", usersPath);
                return "{}";
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read users data");
            return "{}";
        }
    }

    private static string GetFallbackSystemPrompt()
    {
        return @"🎯 You are a helpful AI assistant for AccountingAgent.

You have access to various plugins and functions to help users with:
- Communication (email, SMS, messaging)
- File management and cloud storage
- Calendar and scheduling
- Search and information lookup
- Weather and location services
- Accounting and business tasks

Use the available functions to assist users effectively and professionally.";
    }
}
