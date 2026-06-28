using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace OpenCage;

/// <summary>
/// Factory for creating OpenCage plugin instances
/// </summary>
[PluginDiscoverable("Geocoding", "Location and geocoding services using OpenCage API", Category = "Information", Priority = 41)]
public class OpenCagePluginFactory : IKernelPluginFactory
{
    public string PluginName => "Geocoding";
    
    public string? Description => "Plugin for geocoding and address search using OpenCage";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}