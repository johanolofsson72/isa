using System;

namespace Shared.Attributes;

/// <summary>
/// Marks a plugin factory class as discoverable by the dynamic plugin discovery system.
/// This attribute enables automatic plugin registration without hardcoded lists.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PluginDiscoverableAttribute : Attribute
{
    /// <summary>
    /// Gets the unique name of the plugin.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the human-readable description of the plugin's functionality.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets or sets the category this plugin belongs to for organization purposes.
    /// Default is "General".
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Gets or sets the priority for plugin loading order. Lower numbers load first.
    /// Default is 100.
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether this plugin is enabled by default.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the version of this plugin.
    /// Default is "1.0.0".
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets additional tags for this plugin for filtering and search.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Initializes a new instance of the PluginDiscoverableAttribute class.
    /// </summary>
    /// <param name="name">The unique name of the plugin.</param>
    /// <param name="description">The human-readable description of the plugin's functionality.</param>
    /// <exception cref="ArgumentException">Thrown when name or description is null or empty.</exception>
    public PluginDiscoverableAttribute(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plugin name cannot be null or empty.", nameof(name));
        
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Plugin description cannot be null or empty.", nameof(description));

        Name = name.Trim();
        Description = description.Trim();
    }

    /// <summary>
    /// Returns a string representation of this attribute for debugging purposes.
    /// </summary>
    public override string ToString()
    {
        return $"[PluginDiscoverable(Name=\"{Name}\", Description=\"{Description}\", Category=\"{Category}\", Priority={Priority}, Enabled={Enabled})]";
    }
}
