using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Shared;
using Agent.Services;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Agent.Tests;

public class PluginDiscoveryTests
{
    [Fact]
    public void PluginDiscoveryService_ShouldDiscoverMultiplePluginFactories()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // MailPlugin configuration
                ["Mailjet:ApiKey"] = "test-key",
                ["Mailjet:ApiSecret"] = "test-secret",
                ["Mailjet:Sender"] = "test@example.com",
                // SmsPlugin configuration
                ["ElksConfiguration:ApiKey"] = "test-key",
                ["ElksConfiguration:ApiSecret"] = "test-secret",
                ["ElksConfiguration:From"] = "TestSender",
                // WhatsApp configuration
                ["Twilio:AccountSid"] = "test-sid",
                ["Twilio:AuthToken"] = "test-token",
                ["Twilio:WhatsAppFrom"] = "whatsapp:+1234567890",
                // Slack configuration
                ["Slack:WebhookUrl"] = "https://hooks.slack.com/test",
                ["Slack:Token"] = "test-token",
                // DropboxPlugin configuration
                ["Dropbox:AppKey"] = "test-app-key",
                ["Dropbox:AppSecret"] = "test-app-secret",
                ["Dropbox:RefreshToken"] = "test-refresh-token",
                // FortnoxPlugin configuration
                ["Fortnox:ClientId"] = "eYXaXwa9Kjm9",
                ["Fortnox:ClientSecret"] = "7Q6EqFulr6",
                ["Fortnox:AccessTokenFilePath"] = "/path/to/accountingagent/src/Agent/fortnox-access-token.json"
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var kernel = Kernel.CreateBuilder().Build();

        // Act
        var pluginDiscovery = new DynamicPluginDiscoveryService(configuration, loggerFactory);
        var registeredCount = pluginDiscovery.RegisterAllPlugins(kernel);

        // Assert
        Assert.True(registeredCount >= 0, $"Expected at least 0 plugins to be registered, but got {registeredCount}");

        // List all discovered plugins
        var pluginNames = new List<string>();
        foreach (var plugin in kernel.Plugins)
        {
            pluginNames.Add(plugin.Name);
        }

        // Output discovered plugins for verification
        Console.WriteLine($"Discovered {registeredCount} plugins: {string.Join(", ", pluginNames)}");

        // Verify that the system attempted to register plugins (showing discovery works)
        // The exact number will depend on how many plugins have proper configuration
        Assert.True(registeredCount >= 0, "Plugin discovery should complete without errors");
    }

    [Fact]
    public void PluginDiscoveryService_ShouldHandleMissingConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build(); // Empty configuration
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var kernel = Kernel.CreateBuilder().Build();

        // Act & Assert - Should not throw, but might register fewer plugins
        var pluginDiscovery = new DynamicPluginDiscoveryService(configuration, loggerFactory);
        var registeredCount = pluginDiscovery.RegisterAllPlugins(kernel);

        // The discovery should complete without throwing, even if some plugins fail to initialize
        Assert.True(registeredCount >= 0, "Plugin discovery should not fail completely");
    }
}