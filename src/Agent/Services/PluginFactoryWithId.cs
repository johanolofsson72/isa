using Shared;

namespace Agent.Services;

/// <summary>
/// Wrapper for plugin factories that includes a unique ID for database tracking
/// </summary>
public class PluginFactoryWithId : IKernelPluginFactory
{
    private readonly IKernelPluginFactory _innerFactory;

    public PluginFactoryWithId(IKernelPluginFactory innerFactory, string id)
    {
        _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    public string Id { get; }

    public string PluginName => _innerFactory.PluginName;

    public string? Description => _innerFactory.Description;

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return _innerFactory.CreatePlugin(configuration, loggerFactory);
    }
}