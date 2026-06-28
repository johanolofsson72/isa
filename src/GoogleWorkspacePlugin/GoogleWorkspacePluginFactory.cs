using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace GoogleWorkspace;

/// <summary>
/// Factory for creating Google Workspace plugin instances
/// </summary>
[PluginDiscoverable("GoogleWorkspace", "Google Workspace integration for Gmail, Calendar, and Drive", Category = "Productivity", Priority = 61)]
public class GoogleWorkspacePluginFactory : IKernelPluginFactory
{
    public string PluginName => "GoogleWorkspace";
    
    public string? Description => "Plugin for Google Workspace operations (Drive, Calendar)";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}