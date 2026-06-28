using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace Slack;

/// <summary>
/// Factory for creating Slack plugin instances
/// </summary>
[PluginDiscoverable("Slack", "Team communication and messaging using Slack API", Category = "Communication", Priority = 12)]
public class SlackPluginFactory : IKernelPluginFactory
{
    public string PluginName => "Slack";
    
    public string? Description => "Plugin for sending Slack messages";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}