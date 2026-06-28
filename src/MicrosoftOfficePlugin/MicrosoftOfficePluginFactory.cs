using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace MicrosoftOffice;

/// <summary>
/// Factory for creating Microsoft Office plugin instances
/// </summary>
[PluginDiscoverable("MicrosoftOffice", "Microsoft Office integration for documents and productivity", Category = "Productivity", Priority = 60)]
public class MicrosoftOfficePluginFactory : IKernelPluginFactory
{
    public string PluginName => "MicrosoftOffice";
    
    public string? Description => "Plugin for Microsoft Office/Microsoft Graph operations";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}