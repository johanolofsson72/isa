using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using DocumentInterpreter;

namespace DocumentInterpreterPlugin.Tests;

public class DocumentInterpreterPluginFactoryTests
{
    [Fact]
    public void CreatePlugin_WithValidConfiguration_ShouldReturnPluginInstance()
    {
        // Arrange
        var factory = new DocumentInterpreterPluginFactory();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentInterpreter:ModelName"] = "gpt-4o"
            })
            .Build();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Act
        var plugin = factory.CreatePlugin(configuration, loggerFactory);

        // Assert
        Assert.NotNull(plugin);
        Assert.IsType<Plugin>(plugin);
    }

    [Fact]
    public void PluginName_ShouldReturnCorrectName()
    {
        // Arrange
        var factory = new DocumentInterpreterPluginFactory();

        // Act
        var name = factory.PluginName;

        // Assert
        Assert.Equal("DocumentInterpreter", name);
    }

    [Fact]
    public void Description_ShouldNotBeNullOrEmpty()
    {
        // Arrange
        var factory = new DocumentInterpreterPluginFactory();

        // Act
        var description = factory.Description;

        // Assert
        Assert.NotNull(description);
        Assert.NotEmpty(description);
        Assert.Contains("financial documents", description);
    }
}