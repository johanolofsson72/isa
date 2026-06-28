using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace Dropbox;

/// <summary>
/// Factory for creating Dropbox plugin instances
/// </summary>
[PluginDiscoverable("Dropbox", "File storage and sharing operations using Dropbox API", Category = "Storage", Priority = 30)]
public class DropboxPluginFactory : IKernelPluginFactory
{
    public string PluginName => "Dropbox";
    
    public string? Description => "Plugin for Dropbox file management - list, upload, download, and manage files";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}