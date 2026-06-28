using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace CompanyCheckupPlugin;

/// <summary>
/// Factory for creating Company Checkup plugin instances
/// </summary>
[PluginDiscoverable("CompanyCheckup", "Company information lookup using OpenCorporates and VIES", Category = "Business", Priority = 60)]
public class CompanyCheckupPluginFactory : IKernelPluginFactory
{
    public string PluginName => "CompanyCheckup";
    
    public string? Description => "Plugin for company information lookup and validation";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}