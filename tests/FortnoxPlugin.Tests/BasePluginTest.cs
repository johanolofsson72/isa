using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FortnoxPlugin.Tests;

/// <summary>
/// Base class for FortnoxPlugin tests providing common functionality
/// </summary>
public abstract class BasePluginTest : IDisposable
{
    protected IConfiguration Configuration { get; private set; }
    protected ILoggerFactory LoggerFactory { get; private set; }

    protected BasePluginTest()
    {
        // Setup configuration
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // FortnoxPlugin configuration
                ["Fortnox:ApiUrl"] = "https://api.fortnox.se/3",
                ["Fortnox:ClientId"] = "eYXaXwa9Kjm9",
                ["Fortnox:ClientSecret"] = "7Q6EqFulr6",
                ["Fortnox:AccessTokenFilePath"] = "/path/to/accountingagent/src/Agent/fortnox-access-token.json"
            });

        Configuration = configBuilder.Build();

        // Setup logging
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    protected ILogger<T> CreateLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }

    protected void ValidatePluginInstantiation<T>(Func<IConfiguration, ILogger<T>, T> factory)
    {
        // Arrange
        var logger = CreateLogger<T>();

        // Act & Assert - Should not throw
        var plugin = factory(Configuration, logger);
        Assert.NotNull(plugin);
    }

    protected void ValidateSuccessResult(object result, string expectedStatus)
    {
        Assert.NotNull(result);
        
        // Use reflection to check if result has a Status property
        var statusProperty = result.GetType().GetProperty("Status");
        if (statusProperty != null)
        {
            var status = statusProperty.GetValue(result)?.ToString();
            Assert.Equal(expectedStatus, status);
        }
    }

    protected virtual void Cleanup()
    {
        LoggerFactory?.Dispose();
    }

    public virtual void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
