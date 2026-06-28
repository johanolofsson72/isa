using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Agent.Models;

/// <summary>
/// Root structure for users.json file
/// </summary>
public class UsersCatalog
{
    [JsonPropertyName("users")]
    public List<JsonUser> Users { get; set; } = new();

    [JsonPropertyName("mailServers")]
    public MailServerConfig? MailServers { get; set; }
}

/// <summary>
/// Individual user definition from JSON
/// </summary>
public class JsonUser
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("preferences")]
    public UserPreferences? Preferences { get; set; }

    [JsonPropertyName("address")]
    public object? Address { get; set; } // Can be string or string[]

    [JsonPropertyName("contacts")]
    [Required]
    public UserContacts Contacts { get; set; } = new();

    [JsonPropertyName("priorityChannel")]
    public string? PriorityChannel { get; set; }
}

/// <summary>
/// User preferences and personal information
/// </summary>
public class UserPreferences
{
    [JsonPropertyName("likes")]
    public List<string>? Likes { get; set; }

    [JsonPropertyName("temperatureComfort")]
    public string? TemperatureComfort { get; set; }
}

/// <summary>
/// User contact information across different platforms
/// </summary>
public class UserContacts
{
    [JsonPropertyName("email")]
    [Required]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("email-password")]
    public string? EmailPassword { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("google")]
    public string? Google { get; set; }

    [JsonPropertyName("google-password")]
    public string? GooglePassword { get; set; }

    [JsonPropertyName("office365")]
    public string? Office365 { get; set; }

    [JsonPropertyName("office365-password")]
    public string? Office365Password { get; set; }

    [JsonPropertyName("slack")]
    public string? Slack { get; set; }
}

/// <summary>
/// Mail server configuration
/// </summary>
public class MailServerConfig
{
    [JsonPropertyName("incoming")]
    public MailServerIncoming? Incoming { get; set; }

    [JsonPropertyName("outgoing")]
    public MailServerOutgoing? Outgoing { get; set; }
}

public class MailServerIncoming
{
    [JsonPropertyName("server")]
    public string? Server { get; set; }

    [JsonPropertyName("imapPorts")]
    public List<int>? ImapPorts { get; set; }

    [JsonPropertyName("pop3Ports")]
    public List<int>? Pop3Ports { get; set; }
}

public class MailServerOutgoing
{
    [JsonPropertyName("server")]
    public string? Server { get; set; }

    [JsonPropertyName("smtpPorts")]
    public List<int>? SmtpPorts { get; set; }
}

/// <summary>
/// Root structure for plugins.json file
/// </summary>
public class PluginsCatalog
{
    [JsonPropertyName("plugins")]
    public List<PluginDefinition> Plugins { get; set; } = new();

    [JsonPropertyName("metadata")]
    public PluginMetadata? Metadata { get; set; }
}

/// <summary>
/// Individual plugin definition
/// </summary>
public class PluginDefinition
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("functions")]
    [Required]
    public List<PluginFunction> Functions { get; set; } = new();
}

/// <summary>
/// Individual function within a plugin
/// </summary>
public class PluginFunction
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, PluginParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Parameter definition for a plugin function
/// </summary>
public class PluginParameter
{
    [JsonPropertyName("type")]
    [Required]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("default")]
    public object? Default { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

/// <summary>
/// Metadata about the plugins catalog
/// </summary>
public class PluginMetadata
{
    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("totalPlugins")]
    public int TotalPlugins { get; set; }

    [JsonPropertyName("totalFunctions")]
    public int TotalFunctions { get; set; }
}
