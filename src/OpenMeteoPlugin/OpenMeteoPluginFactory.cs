using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace OpenMeteo;

/// <summary>
/// Factory for creating OpenMeteo plugin instances
/// </summary>
[PluginDiscoverable("Weather", "Weather information and forecasts using OpenMeteo API", Category = "Information", Priority = 40)]
public class OpenMeteoPluginFactory : IKernelPluginFactory
{
    public string PluginName => "Weather";
    
    public string? Description => "Plugin for weather data from OpenMeteo API";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}