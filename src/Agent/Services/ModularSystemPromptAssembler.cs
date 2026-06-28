using System.Text;
using System.Text.Json;
using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Service for assembling system prompts from modular components
/// </summary>
public class ModularSystemPromptAssembler
{
    private readonly ILogger<ModularSystemPromptAssembler> _logger;
    private readonly JsonPluginGenerator _pluginGenerator;
    private readonly JsonSchemaValidator _validator;
    private readonly string _contentRoot;
    private readonly string _systemPromptPath;
    private readonly string _usersJsonPath;
    private readonly string _pluginsJsonPath;

    // Cache for assembled prompt
    private string? _cachedSystemPrompt;
    private DateTime _cacheTimestamp = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

    public ModularSystemPromptAssembler(
        ILogger<ModularSystemPromptAssembler> logger,
        JsonPluginGenerator pluginGenerator,
        JsonSchemaValidator validator,
        IConfiguration configuration)
    {
        _logger = logger;
        _pluginGenerator = pluginGenerator;
        _validator = validator;
        _contentRoot = configuration["ContentRoot"] ?? Directory.GetCurrentDirectory();

        // Use environment variables for file paths if available, otherwise fall back to ContentRoot
        var envSystemPromptPath = Environment.GetEnvironmentVariable("ISA_SYSTEM_PROMPT_PATH");
        var envConfigPath = Environment.GetEnvironmentVariable("ISA_CONFIG_PATH");

        _systemPromptPath = !string.IsNullOrEmpty(envSystemPromptPath)
            ? envSystemPromptPath
            : Path.Combine(_contentRoot, "systemprompt.txt");

        // Use config path for users.json and plugins.json if available
        var configRoot = !string.IsNullOrEmpty(envConfigPath) ? envConfigPath : _contentRoot;
        _usersJsonPath = Path.Combine(configRoot, "users.json");
        _pluginsJsonPath = Path.Combine(configRoot, "plugins.json");

        _logger.LogInformation("📁 System prompt path configured as: {SystemPromptPath}", _systemPromptPath);
        _logger.LogInformation("📁 Users JSON path configured as: {UsersJsonPath}", _usersJsonPath);
        _logger.LogInformation("📁 Plugins JSON path configured as: {PluginsJsonPath}", _pluginsJsonPath);
        _logger.LogInformation("🔍 Environment variables - ISA_SYSTEM_PROMPT_PATH: {EnvSystemPrompt}, ISA_CONFIG_PATH: {EnvConfig}",
            Environment.GetEnvironmentVariable("ISA_SYSTEM_PROMPT_PATH"),
            Environment.GetEnvironmentVariable("ISA_CONFIG_PATH"));
        _logger.LogInformation("📂 Content root: {ContentRoot}, Current directory: {CurrentDir}",
            _contentRoot, Directory.GetCurrentDirectory());
    }

    /// <summary>
    /// Assembles the complete system prompt from all modular components
    /// </summary>
    public async Task<string> AssembleSystemPromptAsync()
    {
        try
        {
            // Check cache first
            if (IsCacheValid())
            {
                _logger.LogDebug("📋 Using cached system prompt");
                return _cachedSystemPrompt!;
            }

            _logger.LogInformation("🔧 Assembling modular system prompt...");

            // Ensure plugins.json is up to date
            await EnsurePluginsJsonIsCurrentAsync();

            // Load all components
            var coreInstructions = await LoadCoreInstructionsAsync();
            var usersCatalog = await LoadUsersCatalogAsync();
            var pluginsCatalog = await LoadPluginsCatalogAsync();

            // Assemble the complete prompt
            var assembledPrompt = AssemblePrompt(coreInstructions, usersCatalog, pluginsCatalog);

            // Cache the result
            _cachedSystemPrompt = assembledPrompt;
            _cacheTimestamp = DateTime.UtcNow;

            _logger.LogInformation("✅ System prompt assembled successfully ({Length} characters)", assembledPrompt.Length);
            return assembledPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to assemble system prompt");
            
            // Fallback to core instructions only
            try
            {
                var fallback = await LoadCoreInstructionsAsync();
                _logger.LogWarning("⚠️ Using fallback system prompt (core instructions only)");
                return fallback;
            }
            catch
            {
                _logger.LogError("💥 Complete system prompt assembly failure");
                return "Du är AccountingAgent. Hjälp användaren med bokföring och kommunikation.";
            }
        }
    }

    /// <summary>
    /// Ensures plugins.json is current and regenerates if needed
    /// </summary>
    private async Task EnsurePluginsJsonIsCurrentAsync()
    {
        if (_pluginGenerator.ShouldRegeneratePluginsJson())
        {
            _logger.LogInformation("🔄 Regenerating plugins.json...");
            await _pluginGenerator.GeneratePluginsJsonAsync();
        }
    }

    /// <summary>
    /// Loads core instructions from systemprompt.txt
    /// </summary>
    private async Task<string> LoadCoreInstructionsAsync()
    {
        // Try multiple possible locations for systemprompt.txt
        var possiblePaths = new[]
        {
            _systemPromptPath,
            "/config/systemprompt.txt",
            "/app/systemprompt.txt",
            Path.Combine(Directory.GetCurrentDirectory(), "systemprompt.txt"),
            Path.Combine(_contentRoot, "systemprompt.txt")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger.LogInformation("📄 Found system prompt file at: {Path}", path);
                var content = await File.ReadAllTextAsync(path);
                _logger.LogDebug("📄 Loaded core instructions ({Length} characters)", content.Length);
                return content;
            }
            else
            {
                _logger.LogDebug("❌ System prompt file not found at: {Path}", path);
            }
        }

        // If no file found, return a fallback prompt
        _logger.LogWarning("⚠️ No system prompt file found in any location, using fallback");
        return GetFallbackSystemPrompt();
    }

    /// <summary>
    /// Gets a fallback system prompt when no file is found
    /// </summary>
    private string GetFallbackSystemPrompt()
    {
        return @"# AccountingAgent System Prompt

Du är AccountingAgent, en AI-assistent specialiserad på bokföring och företagsadministration.

## Huvuduppgifter:
- Hjälpa med bokföring och redovisning
- Assistera med företagsadministration
- Svara på frågor om ekonomi och skatter
- Hjälpa med kommunikation och dokumentation

## Beteende:
- Var professionell och hjälpsam
- Ge tydliga och korrekta svar
- Fråga om förtydliganden när något är oklart
- Använd svenska som huvudspråk

Hur kan jag hjälpa dig idag?";
    }

    /// <summary>
    /// Loads users catalog from users.json
    /// </summary>
    private async Task<UsersCatalog?> LoadUsersCatalogAsync()
    {
        // Try multiple possible locations for users.json
        var possiblePaths = new[]
        {
            _usersJsonPath,
            "/config/users.json",
            "/app/users.json",
            Path.Combine(Directory.GetCurrentDirectory(), "users.json"),
            Path.Combine(_contentRoot, "users.json")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    _logger.LogInformation("👥 Found users.json file at: {Path}", path);
                    var json = await File.ReadAllTextAsync(path);
                    var catalog = JsonSerializer.Deserialize<UsersCatalog>(json);
                    _logger.LogDebug("👥 Loaded users catalog ({UserCount} users)", catalog?.Users?.Count ?? 0);
                    return catalog;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to parse users.json at {Path}", path);
                    continue;
                }
            }
            else
            {
                _logger.LogDebug("❌ users.json not found at: {Path}", path);
            }
        }

        _logger.LogWarning("⚠️ No users.json file found in any location");
        return null;
    }

    /// <summary>
    /// Loads plugins catalog from plugins.json
    /// </summary>
    private async Task<PluginsCatalog?> LoadPluginsCatalogAsync()
    {
        // Try multiple possible locations for plugins.json
        var possiblePaths = new[]
        {
            _pluginsJsonPath,
            "/config/plugins.json",
            "/app/plugins.json",
            Path.Combine(Directory.GetCurrentDirectory(), "plugins.json"),
            Path.Combine(_contentRoot, "plugins.json")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    _logger.LogInformation("🔌 Found plugins.json file at: {Path}", path);
                    var json = await File.ReadAllTextAsync(path);
                    var catalog = JsonSerializer.Deserialize<PluginsCatalog>(json);
                    _logger.LogDebug("🔌 Loaded plugins catalog ({PluginCount} plugins, {FunctionCount} functions)",
                catalog?.Plugins?.Count ?? 0, catalog?.Metadata?.TotalFunctions ?? 0);
                    return catalog;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to parse plugins.json at {Path}", path);
                    continue;
                }
            }
            else
            {
                _logger.LogDebug("❌ plugins.json not found at: {Path}", path);
            }
        }

        _logger.LogWarning("⚠️ No plugins.json file found in any location");
        return null;
    }

    /// <summary>
    /// Assembles the final system prompt from all components
    /// </summary>
    private string AssemblePrompt(string coreInstructions, UsersCatalog? usersCatalog, PluginsCatalog? pluginsCatalog)
    {
        var prompt = new StringBuilder();

        // 1. Core instructions (behavioral guidelines, rules, etc.)
        prompt.AppendLine(coreInstructions);

        // 2. Users section (if available)
        if (usersCatalog?.Users?.Any() == true)
        {
            prompt.AppendLine();
            prompt.AppendLine("---");
            prompt.AppendLine();
            prompt.AppendLine("# 👥 TILLGÄNGLIGA ANVÄNDARE");
            prompt.AppendLine();
            prompt.AppendLine("Användarinformation laddas från users.json:");
            prompt.AppendLine();

            foreach (var user in usersCatalog.Users)
            {
                AppendUserSection(prompt, user);
            }

            // Add mail server info if available
            if (usersCatalog.MailServers != null)
            {
                AppendMailServerSection(prompt, usersCatalog.MailServers);
            }
        }

        // 3. Plugins section (if available)
        if (pluginsCatalog?.Plugins?.Any() == true)
        {
            prompt.AppendLine();
            prompt.AppendLine("---");
            prompt.AppendLine();
            prompt.AppendLine("# 🔌 TILLGÄNGLIGA FUNKTIONER (Auto-Generated)");
            prompt.AppendLine();
            prompt.AppendLine("Plugin-funktioner laddas automatiskt från plugins.json:");
            prompt.AppendLine();

            // Group plugins by category
            var categorizedPlugins = pluginsCatalog.Plugins
                .GroupBy(p => p.Category ?? "Utilities")
                .OrderBy(g => g.Key);

            foreach (var categoryGroup in categorizedPlugins)
            {
                prompt.AppendLine($"## 📂 {categoryGroup.Key}");
                prompt.AppendLine();

                foreach (var plugin in categoryGroup.OrderBy(p => p.Name))
                {
                    AppendPluginSection(prompt, plugin);
                }
            }

            // Add metadata
            if (pluginsCatalog.Metadata != null)
            {
                prompt.AppendLine();
                prompt.AppendLine($"*Genererad: {pluginsCatalog.Metadata.GeneratedAt:yyyy-MM-dd HH:mm} UTC*");
                prompt.AppendLine($"*{pluginsCatalog.Metadata.TotalPlugins} plugins, {pluginsCatalog.Metadata.TotalFunctions} funktioner*");
            }
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Appends user information to the prompt
    /// </summary>
    private void AppendUserSection(StringBuilder prompt, JsonUser user)
    {
        prompt.AppendLine($"- **{user.Name}**");
        
        if (!string.IsNullOrEmpty(user.Role))
            prompt.AppendLine($"  - Roll: {user.Role}");
        
        if (!string.IsNullOrEmpty(user.Location))
            prompt.AppendLine($"  - Plats: {user.Location}");

        if (user.Preferences?.Likes?.Any() == true)
            prompt.AppendLine($"  - Intressen: {string.Join(", ", user.Preferences.Likes)}");

        if (!string.IsNullOrEmpty(user.Preferences?.TemperatureComfort))
            prompt.AppendLine($"  - Temperaturkomfort: {user.Preferences.TemperatureComfort}");

        // Address handling (can be string or array)
        if (user.Address != null)
        {
            if (user.Address is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    var addresses = element.EnumerateArray().Select(a => a.GetString()).Where(a => a != null);
                    prompt.AppendLine($"  - Adress: {string.Join(" / ", addresses)}");
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
                    prompt.AppendLine($"  - Adress: {element.GetString()}");
                }
            }
        }

        // Contacts
        prompt.AppendLine($"  - E-post: `{user.Contacts.Email}`");
        if (!string.IsNullOrEmpty(user.Contacts.EmailPassword))
            prompt.AppendLine($"  - E-post lösenord: `{user.Contacts.EmailPassword}`");
        if (!string.IsNullOrEmpty(user.Contacts.Phone))
            prompt.AppendLine($"  - Tel: `{user.Contacts.Phone}`");
        if (!string.IsNullOrEmpty(user.Contacts.Google))
            prompt.AppendLine($"  - Google: `{user.Contacts.Google}`");
        if (!string.IsNullOrEmpty(user.Contacts.GooglePassword))
            prompt.AppendLine($"  - Google lösenord: `{user.Contacts.GooglePassword}`");
        if (!string.IsNullOrEmpty(user.Contacts.Office365))
            prompt.AppendLine($"  - Office365: `{user.Contacts.Office365}`");
        if (!string.IsNullOrEmpty(user.Contacts.Office365Password))
            prompt.AppendLine($"  - Office365 lösenord: `{user.Contacts.Office365Password}`");
        if (!string.IsNullOrEmpty(user.Contacts.Slack))
            prompt.AppendLine($"  - Slack: {user.Contacts.Slack}");
        if (!string.IsNullOrEmpty(user.PriorityChannel))
            prompt.AppendLine($"  - Prioriterad kanal: {user.PriorityChannel}");

        prompt.AppendLine();
    }

    /// <summary>
    /// Appends mail server configuration
    /// </summary>
    private void AppendMailServerSection(StringBuilder prompt, MailServerConfig mailServers)
    {
        prompt.AppendLine("**Mailserver-konfiguration:**");
        
        if (mailServers.Incoming != null)
        {
            prompt.AppendLine($"- Inkommande: {mailServers.Incoming.Server}");
            if (mailServers.Incoming.ImapPorts?.Any() == true)
                prompt.AppendLine($"  - IMAP: {string.Join(", ", mailServers.Incoming.ImapPorts)}");
            if (mailServers.Incoming.Pop3Ports?.Any() == true)
                prompt.AppendLine($"  - POP3: {string.Join(", ", mailServers.Incoming.Pop3Ports)}");
        }

        if (mailServers.Outgoing != null)
        {
            prompt.AppendLine($"- Utgående: {mailServers.Outgoing.Server}");
            if (mailServers.Outgoing.SmtpPorts?.Any() == true)
                prompt.AppendLine($"  - SMTP: {string.Join(", ", mailServers.Outgoing.SmtpPorts)}");
        }

        prompt.AppendLine();
    }

    /// <summary>
    /// Appends plugin information to the prompt
    /// </summary>
    private void AppendPluginSection(StringBuilder prompt, PluginDefinition plugin)
    {
        prompt.AppendLine($"### 🔧 {plugin.Name}");
        prompt.AppendLine($"*{plugin.Description}*");
        prompt.AppendLine();

        if (plugin.Functions.Any())
        {
            prompt.AppendLine("**Tillgängliga funktioner:**");
            foreach (var function in plugin.Functions)
            {
                prompt.AppendLine($"- **{function.Name}**: {function.Description}");
                
                if (function.Parameters.Any())
                {
                    prompt.AppendLine("  Parametrar:");
                    foreach (var param in function.Parameters)
                    {
                        var required = param.Value.Required ? "obligatorisk" : "valfri";
                        var defaultValue = param.Value.Default != null ? $", default: {param.Value.Default}" : "";
                        prompt.AppendLine($"    - {param.Key} ({param.Value.Type}, {required}{defaultValue}): {param.Value.Description}");
                    }
                }
            }
        }

        prompt.AppendLine();
    }

    /// <summary>
    /// Checks if the cached prompt is still valid
    /// </summary>
    private bool IsCacheValid()
    {
        return _cachedSystemPrompt != null && 
               DateTime.UtcNow - _cacheTimestamp < _cacheLifetime;
    }

    /// <summary>
    /// Invalidates the cache (useful when files are updated)
    /// </summary>
    public void InvalidateCache()
    {
        _cachedSystemPrompt = null;
        _cacheTimestamp = DateTime.MinValue;
        _logger.LogDebug("🗑️ System prompt cache invalidated");
    }

    /// <summary>
    /// Gets the individual components for debugging/inspection
    /// </summary>
    public async Task<(string coreInstructions, UsersCatalog? users, PluginsCatalog? plugins)> GetComponentsAsync()
    {
        await EnsurePluginsJsonIsCurrentAsync();

        var coreInstructions = await LoadCoreInstructionsAsync();
        var usersCatalog = await LoadUsersCatalogAsync();
        var pluginsCatalog = await LoadPluginsCatalogAsync();

        return (coreInstructions, usersCatalog, pluginsCatalog);
    }

    /// <summary>
    /// Gets statistics about the assembled prompt
    /// </summary>
    public async Task<SystemPromptStats> GetStatsAsync()
    {
        var (core, users, plugins) = await GetComponentsAsync();

        return new SystemPromptStats
        {
            CoreInstructionsLength = core.Length,
            UserCount = users?.Users?.Count ?? 0,
            PluginCount = plugins?.Plugins?.Count ?? 0,
            FunctionCount = plugins?.Metadata?.TotalFunctions ?? 0,
            LastGenerated = plugins?.Metadata?.GeneratedAt ?? DateTime.MinValue
        };
    }

    /// <summary>
    /// Validates all components and returns validation summary
    /// </summary>
    public async Task<ValidationSummary> ValidateComponentsAsync()
    {
        return await _validator.ValidateAllAsync(_systemPromptPath, _usersJsonPath, _pluginsJsonPath);
    }
}

/// <summary>
/// Statistics about the system prompt components
/// </summary>
public class SystemPromptStats
{
    public int CoreInstructionsLength { get; set; }
    public int UserCount { get; set; }
    public int PluginCount { get; set; }
    public int FunctionCount { get; set; }
    public DateTime LastGenerated { get; set; }
}
