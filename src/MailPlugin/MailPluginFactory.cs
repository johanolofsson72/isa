using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace Mail;

/// <summary>
/// Factory for creating Mail plugin instances
/// </summary>
[PluginDiscoverable("Mail", "Send and read emails using Mailjet API", Category = "Communication", Priority = 10)]
public class MailPluginFactory : IKernelPluginFactory
{
    public string PluginName => "Mail";
    
    public string? Description => "Plugin for sending and reading emails using Mailjet";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}