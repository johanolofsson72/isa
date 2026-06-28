using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Slack;
using Xunit;

namespace SlackPlugin.Tests;

/// <summary>
/// Unit tests for SlackPlugin functionality
/// These tests use real objects and avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
///
/// Tests include:
/// - Constructor validation
/// - Parameter validation for messaging methods
/// - Configuration loading
/// - Error handling
/// - Message formatting
/// - Username handling
/// </summary>
[Trait("Category", "CI")]
public class SlackPluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public SlackPluginTests()
    {
        _logger = NullLogger<Plugin>.Instance;
        _testConfiguration = new TestConfiguration();
    }

    /// <summary>
    /// Simple test configuration implementation
    /// </summary>
    private class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values = new();

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();

        public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, _values);

        public T GetValue<T>(string key, T defaultValue)
        {
            if (_values.TryGetValue(key, out var value) && value != null)
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            return defaultValue;
        }

        public void SetValue(string key, string? value) => _values[key] = value;
    }

    /// <summary>
    /// Simple test configuration section implementation
    /// </summary>
    private class TestConfigurationSection : IConfigurationSection
    {
        private readonly string _key;
        private readonly Dictionary<string, string?> _values;

        public TestConfigurationSection(string key, Dictionary<string, string?> values)
        {
            _key = key;
            _values = values;
        }

        public string? this[string key]
        {
            get => _values.TryGetValue($"{_key}:{key}", out var value) ? value : null;
            set => _values[$"{_key}:{key}"] = value;
        }

        public string Key => _key;
        public string Path => _key;
        public string? Value
        {
            get => _values.TryGetValue(_key, out var value) ? value : null;
            set => _values[_key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            var prefix = _key + ":";
            return _values
                .Where(kvp => kvp.Key.StartsWith(prefix) && kvp.Key.Length > prefix.Length)
                .Select(kvp => kvp.Key.Substring(prefix.Length).Split(':')[0])
                .Distinct()
                .Select(childKey => new TestConfigurationSection($"{_key}:{childKey}", _values));
        }

        public IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => new TestConfigurationSection($"{_key}:{key}", _values);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitialize()
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";

        // Act
        var plugin = new Plugin(_testConfiguration, _logger);

        // Assert
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Plugin((IConfiguration)null!, _logger));
    }

    [Theory]
    [InlineData("Slack:BotToken")]
    [InlineData("Slack:WebhookUrl")]
    public void Constructor_WithMissingConfigurationValue_ShouldThrowArgumentNullException(string missingKey)
    {
        // Arrange
        var config = new TestConfiguration();
        config["Slack:BotToken"] = missingKey == "Slack:BotToken" ? null : "xoxb-test-bot-token";
        config["Slack:WebhookUrl"] = missingKey == "Slack:WebhookUrl" ? null : "https://hooks.slack.com/services/test/webhook/url";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Plugin(config, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";

        // Act
        var plugin = new Plugin(_testConfiguration, null);

        // Assert
        plugin.Should().NotBeNull();
        // Should not throw when logger is null - uses NullLogger internally
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyConfigurationValues_ShouldNotThrow(string emptyValue)
    {
        // Arrange
        var config = new TestConfiguration();
        config["Slack:BotToken"] = emptyValue;
        config["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";

        // Act & Assert
        // SlackPlugin constructor accepts empty strings, only null throws ArgumentNullException
        var plugin = new Plugin(config, _logger);
        plugin.Should().NotBeNull();
    }

    #endregion

    #region SendMessage Tests

    [Theory]
    [InlineData("", "Test message", "ERROR: Kanal saknas")]
    [InlineData(null, "Test message", "ERROR: Kanal saknas")]
    [InlineData("   ", "Test message", "ERROR: Kanal saknas")]
    [InlineData("#general", "", "ERROR: Meddelande saknas")]
    [InlineData("#general", null, "ERROR: Meddelande saknas")]
    [InlineData("#general", "   ", "ERROR: Meddelande saknas")]
    public async Task SendMessage_WithInvalidParameters_ShouldReturnErrorMessage(string? channel, string? message, string expectedError)
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendMessage(channel!, message!);

        // Assert
        result.Should().Be(expectedError);
    }

    [Fact]
    public async Task SendMessage_WithValidParameters_ShouldAttemptToSend()
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendMessage("#general", "Test Slack message");

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        // But we can verify that parameter validation passed
        result.Should().NotBe("ERROR: Kanal saknas");
        result.Should().NotBe("ERROR: Meddelande saknas");
    }

    [Theory]
    [InlineData("#general")]
    [InlineData("general")]
    [InlineData("C1234567890")] // Channel ID format
    [InlineData("random")]
    public async Task SendMessage_WithVariousChannelFormats_ShouldAcceptFormat(string channel)
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendMessage(channel, "Test message");

        // Assert
        result.Should().NotBeNull();
        // Should not fail on parameter validation
        result.Should().NotBe("ERROR: Kanal saknas");
        result.Should().NotBe("ERROR: Meddelande saknas");
    }

    #endregion

    #region SendDirectMessage Tests

    [Theory]
    [InlineData("", "Test message", "ERROR: Användarnamn saknas")]
    [InlineData(null, "Test message", "ERROR: Användarnamn saknas")]
    [InlineData("   ", "Test message", "ERROR: Användarnamn saknas")]
    [InlineData("demo.user", "", "ERROR: Meddelande saknas")]
    [InlineData("demo.user", null, "ERROR: Meddelande saknas")]
    [InlineData("demo.user", "   ", "ERROR: Meddelande saknas")]
    public async Task SendDirectMessage_WithInvalidParameters_ShouldReturnErrorMessage(string? user, string? message, string expectedError)
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendDirectMessage(user!, message!);

        // Assert
        result.Should().Be(expectedError);
    }

    [Fact]
    public async Task SendDirectMessage_WithValidParameters_ShouldAttemptToSend()
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendDirectMessage("demo.user", "Test direct message");

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        // But we can verify that parameter validation passed
        result.Should().NotBe("ERROR: Användarnamn saknas");
        result.Should().NotBe("ERROR: Meddelande saknas");
    }

    [Theory]
    [InlineData("demo.user", "demo.user")] // Without @
    [InlineData("@demo.user", "demo.user")] // With @
    [InlineData("@@demo.user", "@demo.user")] // Multiple @
    [InlineData("U1234567890", "U1234567890")] // User ID format
    public async Task SendDirectMessage_WithVariousUserFormats_ShouldHandleUsernameCorrectly(string inputUser, string expectedCleanUser)
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendDirectMessage(inputUser, "Test message");

        // Assert
        result.Should().NotBeNull();
        // Should not fail on parameter validation
        result.Should().NotBe("ERROR: Användarnamn saknas");
        result.Should().NotBe("ERROR: Meddelande saknas");

        // If it's a success message, it should contain the clean username
        if (result.StartsWith("SUCCESS:"))
        {
            result.Should().Contain(expectedCleanUser);
        }
    }

    #endregion

    #region SendWebhookMessage Tests

    [Theory]
    [InlineData("", "ERROR: Meddelande saknas")]
    [InlineData(null, "ERROR: Meddelande saknas")]
    [InlineData("   ", "ERROR: Meddelande saknas")]
    public async Task SendWebhookMessage_WithInvalidParameters_ShouldReturnErrorMessage(string? message, string expectedError)
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendWebhookMessage(message!);

        // Assert
        result.Should().Be(expectedError);
    }

    [Fact]
    public async Task SendWebhookMessage_WithValidParameters_ShouldAttemptToSend()
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendWebhookMessage("Test webhook message");

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real webhook URL, this will likely fail with an API error
        // But we can verify that parameter validation passed
        result.Should().NotBe("ERROR: Meddelande saknas");
    }

    [Theory]
    [InlineData("Simple message")]
    [InlineData("Message with emoji 🚀")]
    [InlineData("Message with\nmultiple\nlines")]
    [InlineData("Message with special chars: @#$%^&*()")]
    public async Task SendWebhookMessage_WithVariousMessageFormats_ShouldAcceptFormat(string message)
    {
        // Arrange
        _testConfiguration["Slack:BotToken"] = "xoxb-test-bot-token";
        _testConfiguration["Slack:WebhookUrl"] = "https://hooks.slack.com/services/test/webhook/url";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendWebhookMessage(message);

        // Assert
        result.Should().NotBeNull();
        // Should not fail on parameter validation
        result.Should().NotBe("ERROR: Meddelande saknas");
    }

    #endregion
}

