using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace AnalysisPlugin;

/// <summary>
/// Factory for creating AnalysisPlugin instances
/// </summary>
[PluginDiscoverable("AnalysisPlugin", "AI-driven financial analysis and reporting for Swedish accounting firms with BAS compliance, ML forecasting, and automated report generation", Category = "Financial", Priority = 10)]
public class AnalysisPluginFactory : IKernelPluginFactory
{
    public string PluginName => "AnalysisPlugin";
    
    public string? Description => "AI-driven financial analysis and reporting for Swedish accounting firms with BAS compliance, ML forecasting, and automated report generation";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new AnalysisPlugin(configuration, loggerFactory.CreateLogger<AnalysisPlugin>());
    }
}
