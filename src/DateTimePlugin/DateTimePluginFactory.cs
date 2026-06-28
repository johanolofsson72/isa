using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace DateTimePlugin;

/// <summary>
/// Factory for creating DateTime plugin instances
/// </summary>
[PluginDiscoverable("DateTime", "Date and time operations, timezone conversions, and scheduling", Category = "Utilities", Priority = 20)]
public class DateTimePluginFactory : IKernelPluginFactory
{
    public string PluginName => "DateTime";
    
    public string? Description => "Plugin for date and time operations";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}