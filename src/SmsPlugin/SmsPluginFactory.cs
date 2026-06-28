using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace Sms;

/// <summary>
/// Factory for creating SMS plugin instances
/// </summary>
[PluginDiscoverable("SMS", "Send SMS messages using 46elks API", Category = "Communication", Priority = 11)]
public class SmsPluginFactory : IKernelPluginFactory
{
    public string PluginName => "SMS";
    
    public string? Description => "Plugin for sending SMS messages using 46elks";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}