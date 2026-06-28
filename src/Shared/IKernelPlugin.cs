using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared;

/// <summary>
/// Interface that all kernel plugins must implement for auto-registration
/// </summary>
public interface IKernelPlugin
{
    /// <summary>
    /// The unique name of the plugin that will be used for registration
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// Optional description of what the plugin does
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Factory method to create an instance of the plugin
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger instance for the plugin</param>
    /// <returns>The plugin instance</returns>
    static abstract object CreateInstance(IConfiguration configuration, ILogger logger);
}

/// <summary>
/// Base interface for plugin factory methods
/// </summary>
public interface IKernelPluginFactory
{
    /// <summary>
    /// Creates an instance of the plugin
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="loggerFactory">Logger factory for creating plugin-specific loggers</param>
    /// <returns>The plugin instance</returns>
    object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory);

    /// <summary>
    /// The unique name of the plugin
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// Optional description of the plugin
    /// </summary>
    string? Description { get; }
}