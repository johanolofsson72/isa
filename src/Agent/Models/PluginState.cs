using System.ComponentModel.DataAnnotations;

namespace Agent.Data.Models;

/// <summary>
/// Entity representing the state of a plugin in the database
/// </summary>
public class PluginState
{
    /// <summary>
    /// Database primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for the plugin (matches IKernelPlugin.Id)
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the plugin for display purposes
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the plugin
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether the plugin is currently enabled/active
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// When the plugin state was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who/what modified the plugin state last
    /// </summary>
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }
}