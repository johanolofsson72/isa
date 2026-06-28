using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace WhatsApp;

/// <summary>
/// Factory for creating WhatsApp plugin instances
/// </summary>
[PluginDiscoverable("WhatsApp", "Messaging using WhatsApp Business API", Category = "Communication", Priority = 13)]
public class WhatsAppPluginFactory : IKernelPluginFactory
{
    public string PluginName => "WhatsApp";
    
    public string? Description => "Plugin for sending WhatsApp messages";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}