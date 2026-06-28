using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace Fortnox;

/// <summary>
/// Factory for creating Fortnox plugin instances
/// </summary>
[PluginDiscoverable("Fortnox", "Plugin for Fortnox accounting integration - manage customers, invoices, and financial data", Category = "Utilities", Priority = 30)]
public class FortnoxPluginFactory : IKernelPluginFactory
{
    public string PluginName => "FortnoxPlugin";
    
    public string? Description => "Plugin for Fortnox accounting integration - manage customers, invoices, and financial data";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}