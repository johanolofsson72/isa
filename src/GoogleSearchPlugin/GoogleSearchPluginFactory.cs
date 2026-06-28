using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace GoogleSearchPlugin;

/// <summary>
/// Factory for creating Google Search plugin instances
/// </summary>
[PluginDiscoverable("GoogleSearch", "Web search using Google Custom Search API", Category = "Information", Priority = 50)]
public class GoogleSearchPluginFactory : IKernelPluginFactory
{
    public string PluginName => "GoogleSearch";
    
    public string? Description => "Plugin for Google Search operations";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}