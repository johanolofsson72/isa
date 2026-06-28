using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agent.Services;

/// <summary>
/// Service for discovering and managing MCP client connections
/// Supports VS Code, GitHub Copilot, Augment Code, and other MCP-compatible clients
/// </summary>
public class McpClientDiscoveryService
{
    private readonly ILogger<McpClientDiscoveryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, McpClientInfo> _connectedClients = new();
    private readonly object _clientsLock = new();

    public McpClientDiscoveryService(
        ILogger<McpClientDiscoveryService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Registers a new MCP client connection
    /// </summary>
    public void RegisterClient(string clientId, string userAgent, string? origin = null)
    {
        lock (_clientsLock)
        {
            var clientType = DetectClientType(userAgent, origin);
            var clientInfo = new McpClientInfo
            {
                ClientId = clientId,
                ClientType = clientType,
                UserAgent = userAgent,
                Origin = origin,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            _connectedClients[clientId] = clientInfo;
            
            _logger.LogInformation("🔌 MCP Client connected: {ClientType} ({ClientId})", 
                clientType, clientId);
        }
    }

    /// <summary>
    /// Unregisters an MCP client connection
    /// </summary>
    public void UnregisterClient(string clientId)
    {
        lock (_clientsLock)
        {
            if (_connectedClients.TryGetValue(clientId, out var clientInfo))
            {
                _connectedClients.Remove(clientId);
                _logger.LogInformation("🔌 MCP Client disconnected: {ClientType} ({ClientId})", 
                    clientInfo.ClientType, clientId);
            }
        }
    }

    /// <summary>
    /// Updates client activity timestamp
    /// </summary>
    public void UpdateClientActivity(string clientId)
    {
        lock (_clientsLock)
        {
            if (_connectedClients.TryGetValue(clientId, out var clientInfo))
            {
                clientInfo.LastActivity = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Gets all connected clients
    /// </summary>
    public List<McpClientInfo> GetConnectedClients()
    {
        lock (_clientsLock)
        {
            return _connectedClients.Values.ToList();
        }
    }

    /// <summary>
    /// Gets client statistics
    /// </summary>
    public McpClientStats GetClientStats()
    {
        lock (_clientsLock)
        {
            var clients = _connectedClients.Values.ToList();
            var stats = new McpClientStats
            {
                TotalClients = clients.Count,
                ClientsByType = clients.GroupBy(c => c.ClientType)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                ActiveClients = clients.Count(c => DateTime.UtcNow - c.LastActivity < TimeSpan.FromMinutes(5)),
                OldestConnection = clients.Any() ? clients.Min(c => c.ConnectedAt) : DateTime.MinValue,
                NewestConnection = clients.Any() ? clients.Max(c => c.ConnectedAt) : DateTime.MinValue
            };

            return stats;
        }
    }

    /// <summary>
    /// Detects client type from User-Agent and origin
    /// </summary>
    private McpClientType DetectClientType(string userAgent, string? origin)
    {
        var ua = userAgent?.ToLowerInvariant() ?? "";
        var org = origin?.ToLowerInvariant() ?? "";

        // VS Code detection
        if (ua.Contains("vscode") || ua.Contains("visual studio code") || org.Contains("vscode://"))
        {
            return McpClientType.VSCode;
        }

        // GitHub Copilot detection
        if (ua.Contains("copilot") || ua.Contains("github") || org.Contains("github.com") || org.Contains("copilot.github.com"))
        {
            return McpClientType.GitHubCopilot;
        }

        // Augment Code detection
        if (ua.Contains("augment") || org.Contains("augmentcode.com"))
        {
            return McpClientType.AugmentCode;
        }

        // Claude Desktop detection
        if (ua.Contains("claude") || ua.Contains("anthropic"))
        {
            return McpClientType.ClaudeDesktop;
        }

        // Generic MCP client detection
        if (ua.Contains("mcp") || ua.Contains("model-context-protocol"))
        {
            return McpClientType.GenericMcp;
        }

        return McpClientType.Unknown;
    }

    /// <summary>
    /// Cleans up inactive clients
    /// </summary>
    public void CleanupInactiveClients(TimeSpan inactivityThreshold)
    {
        lock (_clientsLock)
        {
            var cutoff = DateTime.UtcNow - inactivityThreshold;
            var inactiveClients = _connectedClients.Values
                .Where(c => c.LastActivity < cutoff)
                .ToList();

            foreach (var client in inactiveClients)
            {
                _connectedClients.Remove(client.ClientId);
                _logger.LogInformation("🧹 Cleaned up inactive MCP client: {ClientType} ({ClientId})", 
                    client.ClientType, client.ClientId);
            }
        }
    }

    /// <summary>
    /// Gets client-specific configuration
    /// </summary>
    public JsonNode? GetClientConfiguration(McpClientType clientType)
    {
        var configKey = $"Mcp:Clients:{clientType}";
        var config = _configuration.GetSection(configKey);
        
        if (!config.Exists())
        {
            return null;
        }

        var json = new JsonObject();
        foreach (var item in config.GetChildren())
        {
            json[item.Key] = JsonValue.Create(item.Value);
        }

        return json;
    }
}

/// <summary>
/// Information about a connected MCP client
/// </summary>
public class McpClientInfo
{
    public string ClientId { get; set; } = string.Empty;
    public McpClientType ClientType { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public string? Origin { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
}

/// <summary>
/// Statistics about connected MCP clients
/// </summary>
public class McpClientStats
{
    public int TotalClients { get; set; }
    public Dictionary<string, int> ClientsByType { get; set; } = new();
    public int ActiveClients { get; set; }
    public DateTime OldestConnection { get; set; }
    public DateTime NewestConnection { get; set; }
}

/// <summary>
/// Types of MCP clients
/// </summary>
public enum McpClientType
{
    Unknown,
    VSCode,
    GitHubCopilot,
    AugmentCode,
    ClaudeDesktop,
    GenericMcp
}
