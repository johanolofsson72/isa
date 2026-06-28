using Agent.Data;
using Agent.Data.Models;
using Agent.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel.DataAnnotations;

namespace Agent.Extensions;

/// <summary>
/// Extension methods for plugin management API endpoints
/// </summary>
public static class PluginManagementEndpointExtensions
{
    /// <summary>
    /// Maps plugin management endpoints
    /// </summary>
    public static WebApplication MapPluginManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/plugins")
            .WithTags("Plugin Management")
            .WithOpenApi();

        // GET: Get all plugins with their states
        group.MapGet("/", GetAllPluginsAsync)
            .WithName("GetAllPlugins")
            .WithSummary("Get all plugins with their current states")
            .WithDescription("Returns a list of all discovered plugins with their enabled/disabled states from the database");

        // GET: Get specific plugin by name
        group.MapGet("/{pluginName}", GetPluginByNameAsync)
            .WithName("GetPluginByName")
            .WithSummary("Get a specific plugin by name")
            .WithDescription("Returns detailed information about a specific plugin");

        // POST: Toggle plugin enabled/disabled state
        group.MapPost("/{pluginName}/toggle", TogglePluginAsync)
            .WithName("TogglePlugin")
            .WithSummary("Toggle plugin enabled/disabled state")
            .WithDescription("Toggles the enabled state of a plugin and updates the database");

        // PUT: Update plugin state
        group.MapPut("/{pluginName}", UpdatePluginAsync)
            .WithName("UpdatePlugin")
            .WithSummary("Update plugin state")
            .WithDescription("Updates the plugin state including name, description, and enabled status");

        // GET: Get plugin discovery status
        group.MapGet("/discovery/status", GetDiscoveryStatusAsync)
            .WithName("GetPluginDiscoveryStatus")
            .WithSummary("Get plugin discovery system status")
            .WithDescription("Returns information about the plugin discovery system and its health");

        // POST: Refresh plugin discovery
        group.MapPost("/discovery/refresh", RefreshPluginDiscoveryAsync)
            .WithName("RefreshPluginDiscovery")
            .WithSummary("Refresh plugin discovery")
            .WithDescription("Forces a refresh of the plugin discovery system and updates the database");

        return app;
    }

    /// <summary>
    /// Get all plugins with their states
    /// </summary>
    private static async Task<IResult> GetAllPluginsAsync(
        AgentDbContext dbContext,
        DynamicPluginDiscoveryService discoveryService,
        ILogger<object> logger)
    {
        try
        {
            logger.LogInformation("🔍 Getting all plugins with states");

            // Get discovered plugins
            var discoveredPlugins = discoveryService.GetDiscoverablePlugins();
            
            // Get plugin states from database
            var pluginStates = await dbContext.PluginStates.ToListAsync();
            
            // Combine discovered plugins with database states
            var result = new List<PluginStateResponse>();
            
            foreach (var discovered in discoveredPlugins)
            {
                var state = pluginStates.FirstOrDefault(ps => ps.PluginName == discovered.Name);
                
                result.Add(new PluginStateResponse
                {
                    Id = state?.Id ?? 0,
                    PluginId = state?.PluginId ?? Guid.NewGuid().ToString(),
                    PluginName = discovered.Name,
                    Description = state?.Description ?? discovered.Description,
                    IsEnabled = state?.IsEnabled ?? discovered.Enabled,
                    LastModified = state?.LastModified ?? DateTime.UtcNow,
                    ModifiedBy = state?.ModifiedBy,
                    FunctionCount = discovered.Functions?.Count ?? 0,
                    IsDiscovered = true
                });
            }
            
            // Add any database-only plugins (not currently discovered)
            var discoveredNames = discoveredPlugins.Select(p => p.Name).ToHashSet();
            var databaseOnlyStates = pluginStates.Where(ps => !discoveredNames.Contains(ps.PluginName));
            
            foreach (var dbState in databaseOnlyStates)
            {
                result.Add(new PluginStateResponse
                {
                    Id = dbState.Id,
                    PluginId = dbState.PluginId,
                    PluginName = dbState.PluginName,
                    Description = dbState.Description,
                    IsEnabled = dbState.IsEnabled,
                    LastModified = dbState.LastModified,
                    ModifiedBy = dbState.ModifiedBy,
                    FunctionCount = 0,
                    IsDiscovered = false
                });
            }

            logger.LogInformation("✅ Retrieved {Count} plugins ({Discovered} discovered, {DatabaseOnly} database-only)", 
                result.Count, discoveredPlugins.Count, databaseOnlyStates.Count());

            return Results.Ok(result.OrderBy(p => p.PluginName).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting all plugins");
            return Results.Problem($"Error getting plugins: {ex.Message}");
        }
    }

    /// <summary>
    /// Get specific plugin by name
    /// </summary>
    private static async Task<IResult> GetPluginByNameAsync(
        string pluginName,
        AgentDbContext dbContext,
        DynamicPluginDiscoveryService discoveryService,
        ILogger<object> logger)
    {
        try
        {
            logger.LogInformation("🔍 Getting plugin {PluginName}", pluginName);

            // Find by PluginName (primary identifier)
            var state = await dbContext.PluginStates
                .FirstOrDefaultAsync(ps => ps.PluginName == pluginName);

            var discoveredPlugins = discoveryService.GetDiscoverablePlugins();
            var discovered = discoveredPlugins.FirstOrDefault(p => p.Name == pluginName);

            if (state == null && discovered == null)
            {
                logger.LogWarning("⚠️ Plugin {PluginName} not found", pluginName);
                return Results.NotFound($"Plugin '{pluginName}' not found");
            }

            var result = new PluginStateResponse
            {
                Id = state?.Id ?? 0,
                PluginId = state?.PluginId ?? Guid.NewGuid().ToString(),
                PluginName = discovered?.Name ?? state?.PluginName ?? pluginName,
                Description = state?.Description ?? discovered?.Description,
                IsEnabled = state?.IsEnabled ?? discovered?.Enabled ?? false,
                LastModified = state?.LastModified ?? DateTime.UtcNow,
                ModifiedBy = state?.ModifiedBy,
                FunctionCount = discovered?.Functions?.Count ?? 0,
                IsDiscovered = discovered != null,
                Functions = discovered?.Functions?.Select(f => new PluginFunctionInfo
                {
                    Name = f.Name,
                    Description = f.Description,
                    ParameterCount = f.Parameters?.Count ?? 0
                }).ToList() ?? new List<PluginFunctionInfo>()
            };

            logger.LogInformation("✅ Retrieved plugin {PluginName}", result.PluginName);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting plugin {PluginName}", pluginName);
            return Results.Problem($"Error getting plugin: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggle plugin enabled/disabled state
    /// </summary>
    private static async Task<IResult> TogglePluginAsync(
        string pluginName,
        TogglePluginRequest request,
        AgentDbContext dbContext,
        DynamicPluginDiscoveryService discoveryService,
        ILogger<object> logger)
    {
        try
        {
            logger.LogInformation("🔄 Toggling plugin {PluginName} by {ModifiedBy}", pluginName, request.ModifiedBy);

            // Find existing state by PluginName
            var state = await dbContext.PluginStates
                .FirstOrDefaultAsync(ps => ps.PluginName == pluginName);

            if (state == null)
            {
                // Check if plugin exists in discovery
                var discoveredPlugins = discoveryService.GetDiscoverablePlugins();
                var discovered = discoveredPlugins.FirstOrDefault(p => p.Name == pluginName);

                if (discovered == null)
                {
                    logger.LogWarning("⚠️ Plugin {PluginName} not found in discovery", pluginName);
                    return Results.NotFound($"Plugin '{pluginName}' not found");
                }

                // Create new state
                state = new PluginState
                {
                    PluginId = Guid.NewGuid().ToString(),
                    PluginName = discovered.Name,
                    Description = discovered.Description,
                    IsEnabled = !discovered.Enabled, // Toggle from current discovered state
                    LastModified = DateTime.UtcNow,
                    ModifiedBy = request.ModifiedBy
                };

                dbContext.PluginStates.Add(state);
            }
            else
            {
                // Toggle existing state
                state.IsEnabled = !state.IsEnabled;
                state.LastModified = DateTime.UtcNow;
                state.ModifiedBy = request.ModifiedBy;
            }

            await dbContext.SaveChangesAsync();

            var response = new TogglePluginResponse
            {
                PluginId = state.PluginId,
                PluginName = state.PluginName,
                IsEnabled = state.IsEnabled,
                Message = $"Plugin '{state.PluginName}' {(state.IsEnabled ? "enabled" : "disabled")} successfully"
            };

            logger.LogInformation("✅ Plugin {PluginName} toggled to {State}", state.PluginName, state.IsEnabled ? "enabled" : "disabled");
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error toggling plugin {PluginName}", pluginName);
            return Results.Problem($"Error toggling plugin: {ex.Message}");
        }
    }

    /// <summary>
    /// Update plugin state
    /// </summary>
    private static async Task<IResult> UpdatePluginAsync(
        string pluginName,
        UpdatePluginRequest request,
        AgentDbContext dbContext,
        ILogger<object> logger)
    {
        try
        {
            logger.LogInformation("🔄 Updating plugin {PluginName}", pluginName);

            var state = await dbContext.PluginStates
                .FirstOrDefaultAsync(ps => ps.PluginName == pluginName);

            if (state == null)
            {
                return Results.NotFound($"Plugin '{pluginName}' not found");
            }

            // Update fields
            state.PluginName = request.PluginName;
            state.Description = request.Description;
            state.IsEnabled = request.IsEnabled;
            state.LastModified = DateTime.UtcNow;
            state.ModifiedBy = request.ModifiedBy;

            await dbContext.SaveChangesAsync();

            logger.LogInformation("✅ Plugin {PluginName} updated successfully", state.PluginName);
            return Results.Ok(new { Message = "Plugin updated successfully", PluginName = state.PluginName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error updating plugin {PluginName}", pluginName);
            return Results.Problem($"Error updating plugin: {ex.Message}");
        }
    }

    /// <summary>
    /// Get plugin discovery status
    /// </summary>
    private static async Task<IResult> GetDiscoveryStatusAsync(
        DynamicPluginDiscoveryServiceWithFallback discoveryService,
        AgentDbContext dbContext,
        ILogger<object> logger)
    {
        try
        {
            logger.LogInformation("🔍 Getting plugin discovery status");

            var isWorking = discoveryService.IsDynamicDiscoveryWorking();
            var discoveredPlugins = discoveryService.GetDynamicDiscoveryResults();
            var pluginStatesCount = await dbContext.PluginStates.CountAsync();

            var status = new PluginDiscoveryStatus
            {
                IsWorking = isWorking,
                DiscoveredPluginCount = discoveredPlugins.Count,
                DatabasePluginCount = pluginStatesCount,
                LastChecked = DateTime.UtcNow,
                DiscoveredPlugins = discoveredPlugins.Select(p => new DiscoveredPluginInfo
                {
                    Name = p.Name,
                    Description = p.Description,
                    IsEnabled = p.Enabled,
                    FunctionCount = p.Functions?.Count ?? 0
                }).ToList()
            };

            logger.LogInformation("✅ Discovery status: {Status} ({Count} plugins discovered)",
                isWorking ? "Working" : "Not Working", discoveredPlugins.Count);

            return Results.Ok(status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error getting discovery status");
            return Results.Problem($"Error getting discovery status: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresh plugin discovery
    /// </summary>
    private static async Task<IResult> RefreshPluginDiscoveryAsync(
        HybridPluginDiscoveryService discoveryService,
        AgentDbContext dbContext,
        ILogger<object> logger)
    {
        try
        {
            logger.LogInformation("🔄 Refreshing plugin discovery");

            // Refresh discovery cache
            discoveryService.RefreshDiscovery();

            // Get fresh discovery results
            var discoveredPlugins = discoveryService.GetDynamicDiscoveryResults();

            // Sync with database - ensure all discovered plugins have database entries
            foreach (var discovered in discoveredPlugins)
            {
                var existing = await dbContext.PluginStates
                    .FirstOrDefaultAsync(ps => ps.PluginName == discovered.Name);

                if (existing == null)
                {
                    var newState = new PluginState
                    {
                        PluginId = Guid.NewGuid().ToString(),
                        PluginName = discovered.Name,
                        Description = discovered.Description,
                        IsEnabled = discovered.Enabled,
                        LastModified = DateTime.UtcNow,
                        ModifiedBy = "System"
                    };

                    dbContext.PluginStates.Add(newState);
                }
            }

            await dbContext.SaveChangesAsync();

            var result = new RefreshDiscoveryResponse
            {
                Success = true,
                Message = "Plugin discovery refreshed successfully",
                DiscoveredPluginCount = discoveredPlugins.Count,
                RefreshedAt = DateTime.UtcNow
            };

            logger.LogInformation("✅ Plugin discovery refreshed: {Count} plugins discovered", discoveredPlugins.Count);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error refreshing plugin discovery");
            return Results.Problem($"Error refreshing discovery: {ex.Message}");
        }
    }
}

/// <summary>
/// Request model for toggling plugin state
/// </summary>
public class TogglePluginRequest
{
    [Required]
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request model for updating plugin state
/// </summary>
public class UpdatePluginRequest
{
    [Required]
    [MaxLength(100)]
    public string PluginName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; }

    [Required]
    [MaxLength(100)]
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response model for plugin state
/// </summary>
public class PluginStateResponse
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
/// Response model for toggle plugin operation
/// </summary>
public class TogglePluginResponse
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Plugin discovery status information
/// </summary>
public class PluginDiscoveryStatus
{
    public bool IsWorking { get; set; }
    public int DiscoveredPluginCount { get; set; }
    public int DatabasePluginCount { get; set; }
    public DateTime LastChecked { get; set; }
    public List<DiscoveredPluginInfo> DiscoveredPlugins { get; set; } = new();
}

/// <summary>
/// Information about a discovered plugin
/// </summary>
public class DiscoveredPluginInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int FunctionCount { get; set; }
}

/// <summary>
/// Response model for refresh discovery operation
/// </summary>
public class RefreshDiscoveryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DiscoveredPluginCount { get; set; }
    public DateTime RefreshedAt { get; set; }
}
