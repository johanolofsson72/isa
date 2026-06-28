using System;
using System.Collections.Generic;
using Shared;

namespace Shared.Models;

/// <summary>
/// Contains comprehensive metadata about a discovered plugin.
/// </summary>
public class PluginMetadata
{
    /// <summary>
    /// Gets or sets the unique name of the plugin.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable description of the plugin's functionality.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category this plugin belongs to.
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Gets or sets the priority for plugin loading order. Lower numbers load first.
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether this plugin is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the version of this plugin.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets additional tags for this plugin.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the list of functions exposed by this plugin.
    /// </summary>
    public List<FunctionMetadata> Functions { get; set; } = new();

    /// <summary>
    /// Gets or sets the Type of the plugin factory.
    /// </summary>
    public Type? FactoryType { get; set; }

    /// <summary>
    /// Gets or sets the plugin factory instance.
    /// </summary>
    public IKernelPluginFactory? Factory { get; set; }

    /// <summary>
    /// Gets or sets the assembly name where this plugin is defined.
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this metadata was extracted.
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Returns a string representation of this plugin metadata.
    /// </summary>
    public override string ToString()
    {
        return $"Plugin: {Name} ({Functions.Count} functions, Category: {Category}, Enabled: {Enabled})";
    }
}

/// <summary>
/// Contains metadata about a function exposed by a plugin.
/// </summary>
public class FunctionMetadata
{
    /// <summary>
    /// Gets or sets the name of the function.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what this function does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of parameters this function accepts.
    /// </summary>
    public List<ParameterMetadata> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the return type of this function.
    /// </summary>
    public Type? ReturnType { get; set; }

    /// <summary>
    /// Gets or sets whether this function is asynchronous.
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about this function.
    /// </summary>
    public Dictionary<string, object> AdditionalMetadata { get; set; } = new();

    /// <summary>
    /// Returns a string representation of this function metadata.
    /// </summary>
    public override string ToString()
    {
        return $"Function: {Name} ({Parameters.Count} parameters)";
    }
}

/// <summary>
/// Contains metadata about a parameter of a plugin function.
/// </summary>
public class ParameterMetadata
{
    /// <summary>
    /// Gets or sets the name of the parameter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this parameter.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of this parameter.
    /// </summary>
    public Type? Type { get; set; }

    /// <summary>
    /// Gets or sets whether this parameter is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the default value for this parameter, if any.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets additional constraints or validation rules for this parameter.
    /// </summary>
    public Dictionary<string, object> Constraints { get; set; } = new();

    /// <summary>
    /// Returns a string representation of this parameter metadata.
    /// </summary>
    public override string ToString()
    {
        var required = IsRequired ? "required" : "optional";
        return $"Parameter: {Name} ({Type?.Name ?? "unknown"}, {required})";
    }
}
