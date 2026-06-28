using System.Net.Http.Json;
using System.Text.Json;

namespace WebChat.Services;

/// <summary>
/// Service for managing plugin states through the Agent API
/// </summary>
public class PluginManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PluginManagementService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Event for plugin status changes
    public event Action<string, bool>? OnPluginStatusChanged;

    public PluginManagementService(
        IHttpClientFactory httpClientFactory,
        ILogger<PluginManagementService> logger
    )
    {
        _httpClient = httpClientFactory.CreateClient("agent");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
    }

    /// <summary>
    /// Gets all plugin states from the Agent API
    /// </summary>
    /// <returns>List of plugin states</returns>
    public async Task<List<PluginStateDto>?> GetAllPluginsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching all plugins from API");
            var response = await _httpClient.GetAsync("/api/plugins");
            
            if (response.IsSuccessStatusCode)
            {
                var plugins = await response.Content.ReadFromJsonAsync<List<PluginStateDto>>(_jsonOptions);
                _logger.LogInformation($"Successfully retrieved {plugins?.Count ?? 0} plugins");
                return plugins;
            }
            else
            {
                _logger.LogError($"Failed to get plugins: {response.StatusCode} - {response.ReasonPhrase}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugins from API");
            return null;
        }
    }

    /// <summary>
    /// Gets a specific plugin state by name
    /// </summary>
    /// <param name="pluginName">The plugin name</param>
    /// <returns>Plugin state or null if not found</returns>
    public async Task<PluginStateDto?> GetPluginAsync(string pluginName)
    {
        try
        {
            _logger.LogInformation($"Fetching plugin {pluginName} from API");
            var response = await _httpClient.GetAsync($"/api/plugins/{pluginName}");

            if (response.IsSuccessStatusCode)
            {
                var plugin = await response.Content.ReadFromJsonAsync<PluginStateDto>(_jsonOptions);
                _logger.LogInformation($"Successfully retrieved plugin {pluginName}");
                return plugin;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Plugin {pluginName} not found");
                return null;
            }
            else
            {
                _logger.LogError($"Failed to get plugin {pluginName}: {response.StatusCode} - {response.ReasonPhrase}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting plugin {pluginName} from API");
            return null;
        }
    }

    /// <summary>
    /// Toggles the enabled state of a plugin
    /// </summary>
    /// <param name="pluginName">The plugin name</param>
    /// <param name="modifiedBy">Who is making the change</param>
    /// <returns>True if successful</returns>
    public async Task<bool> TogglePluginAsync(string pluginName, string? modifiedBy = null)
    {
        if (string.IsNullOrEmpty(pluginName))
        {
            _logger.LogWarning("TogglePluginAsync called with null or empty pluginName");
            return false;
        }

        try
        {
            _logger.LogInformation("Toggling plugin {PluginName} by {ModifiedBy}", pluginName, modifiedBy ?? "WebChat User");

            var requestBody = new { modifiedBy = modifiedBy ?? "WebChat User" };

            // Add timeout for the request
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _httpClient.PostAsJsonAsync($"/api/plugins/{pluginName}/toggle", requestBody, _jsonOptions, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TogglePluginResponse>(_jsonOptions, cts.Token);
                _logger.LogInformation("Successfully toggled plugin {PluginName} to {IsEnabled}", pluginName, result?.IsEnabled == true ? "enabled" : "disabled");

                // Notify subscribers of the change
                OnPluginStatusChanged?.Invoke(pluginName, result?.IsEnabled ?? false);

                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to toggle plugin {PluginName}: {StatusCode} - {ReasonPhrase}. Response: {ErrorContent}",
                    pluginName, response.StatusCode, response.ReasonPhrase, errorContent);
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout occurred while toggling plugin {PluginName}", pluginName);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while toggling plugin {PluginName}", pluginName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while toggling plugin {PluginName}", pluginName);
            return false;
        }
    }

    /// <summary>
    /// Updates a plugin state
    /// </summary>
    /// <param name="pluginName">The plugin name</param>
    /// <param name="description">The plugin description</param>
    /// <param name="isEnabled">Whether the plugin should be enabled</param>
    /// <param name="modifiedBy">Who is making the change</param>
    /// <returns>True if successful</returns>
    public async Task<bool> UpdatePluginAsync(string pluginName, string? description, bool isEnabled, string? modifiedBy = null)
    {
        try
        {
            _logger.LogInformation($"Updating plugin {pluginName}");

            var requestBody = new UpdatePluginStateRequestDto
            {
                PluginName = pluginName,
                Description = description,
                IsEnabled = isEnabled,
                ModifiedBy = modifiedBy ?? "WebChat User"
            };

            var response = await _httpClient.PutAsJsonAsync($"/api/plugins/{pluginName}", requestBody, _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully updated plugin {pluginName}");

                // Notify subscribers of the change
                OnPluginStatusChanged?.Invoke(pluginName, isEnabled);

                return true;
            }
            else
            {
                _logger.LogError($"Failed to update plugin {pluginName}: {response.StatusCode} - {response.ReasonPhrase}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating plugin {pluginName}");
            return false;
        }
    }
}

/// <summary>
/// DTO for plugin state information
/// </summary>
public class PluginStateDto
{
    public int Id { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime LastModified { get; set; }
    public string? ModifiedBy { get; set; }
    public int FunctionCount { get; set; }
    public bool IsDiscovered { get; set; }
    public List<PluginFunctionInfo> Functions { get; set; } = new();
}

/// <summary>
/// Information about a plugin function
/// </summary>
public class PluginFunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
}

/// <summary>
/// DTO for updating plugin state
/// </summary>
public class UpdatePluginStateRequestDto
{
    public string PluginName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Response model for toggle plugin operation
/// </summary>
public class TogglePluginResponse
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Message { get; set; } = string.Empty;
}

