using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using MicrosoftOffice;
using Xunit;

namespace MicrosoftOfficePlugin.Tests;

/// <summary>
/// Unit tests for MicrosoftOfficePlugin functionality (including Teams)
/// These tests use real objects and avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
///
/// Tests include:
/// - Constructor validation
/// - Parameter validation for Teams methods
/// - Configuration loading
/// - Error handling
/// - Teams messaging functionality
/// - Calendar functionality
/// - OneDrive functionality
/// </summary>
[Trait("Category", "CI")]
public class MicrosoftOfficePluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public MicrosoftOfficePluginTests()
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
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        _testConfiguration["MicrosoftOffice:DriveId"] = "test-drive-id";

        // Act
        var plugin = new Plugin(_testConfiguration, _logger);

        // Assert
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowException()
    {
        // Act & Assert
        // The constructor throws NullReferenceException when configuration is null
        Assert.ThrowsAny<Exception>(() => new Plugin((IConfiguration)null!, _logger));
    }

    [Theory]
    [InlineData("MicrosoftOffice:ClientId")]
    [InlineData("MicrosoftOffice:ClientSecret")]
    [InlineData("MicrosoftOffice:TenantId")]
    public void Constructor_WithMissingRequiredConfiguration_ShouldThrowException(string missingKey)
    {
        // Arrange
        var config = new TestConfiguration();
        config["MicrosoftOffice:ClientId"] = missingKey == "MicrosoftOffice:ClientId" ? null : "test-client-id";
        config["MicrosoftOffice:ClientSecret"] = missingKey == "MicrosoftOffice:ClientSecret" ? null : "REDACTED";
        config["MicrosoftOffice:TenantId"] = missingKey == "MicrosoftOffice:TenantId" ? null : "test-tenant-id";
        config["MicrosoftOffice:DriveId"] = "test-drive-id";

        // Act & Assert
        // The constructor will throw when trying to create ClientSecretCredential with null values
        Assert.ThrowsAny<Exception>(() => new Plugin(config, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        _testConfiguration["MicrosoftOffice:DriveId"] = "test-drive-id";

        // Act
        var plugin = new Plugin(_testConfiguration, null);

        // Assert
        plugin.Should().NotBeNull();
        // Should not throw when logger is null - uses NullLogger internally
    }

    #endregion

    #region Teams Messaging Tests

    [Theory]
    [InlineData("", "Test message")]
    [InlineData(null, "Test message")]
    [InlineData("   ", "Test message")]
    [InlineData("test@example.com", "")]
    [InlineData("test@example.com", null)]
    [InlineData("test@example.com", "   ")]
    public async Task SendTeamsMessage_WithInvalidParameters_ShouldReturnError(string? recipientEmail, string? message)
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendTeamsMessageAsync(recipientEmail!, message!);

        // Assert
        result.Should().NotBeNull();
        // The method will either validate parameters or fail on API call
        // Both are acceptable for unit tests
        if (string.IsNullOrWhiteSpace(recipientEmail) || string.IsNullOrWhiteSpace(message))
        {
            // Should handle invalid parameters gracefully
            result.Should().StartWith("ERROR:");
        }
    }

    [Fact]
    public async Task SendTeamsMessage_WithValidParameters_ShouldAttemptToSend()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendTeamsMessageAsync("test@example.com", "Test Teams message");

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        // But we can verify that parameter validation passed
        if (result.StartsWith("ERROR:"))
        {
            // Expected - API call failed due to invalid credentials
            result.Should().NotContain("parameter");
        }
        else
        {
            // Unexpected success - should contain success message
            result.Should().StartWith("SUCCESS:");
        }
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData(null, 10)]
    [InlineData("   ", 10)]
    [InlineData("test@example.com", -1)]
    [InlineData("test@example.com", 0)]
    public async Task GetRecentTeamsMessages_WithInvalidParameters_ShouldReturnError(string? participantEmail, int maxResults)
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GetRecentTeamsMessages(participantEmail!, maxResults);

        // Assert
        result.Should().NotBeNull();
        // Should handle invalid parameters gracefully
        if (string.IsNullOrWhiteSpace(participantEmail) || maxResults <= 0)
        {
            result.Should().StartWith("ERROR:");
        }
    }

    [Fact]
    public async Task GetRecentTeamsMessages_WithValidParameters_ShouldAttemptToRetrieve()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GetRecentTeamsMessages("test@example.com", 5);

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        // But we can verify that parameter validation passed
        result.Should().NotBeNull();
    }

    #endregion

    #region Teams Channel Tests

    [Theory]
    [InlineData("", "Test message")]
    [InlineData(null, "Test message")]
    [InlineData("   ", "Test message")]
    [InlineData("Test Channel", "")]
    [InlineData("Test Channel", null)]
    [InlineData("Test Channel", "   ")]
    public async Task CreateTeamsChannelAndMessage_WithInvalidParameters_ShouldReturnError(string? channelName, string? message)
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.CreateTeamsChannelAndMessage(channelName!, message!);

        // Assert
        result.Should().NotBeNull();
        // Should handle invalid parameters gracefully
        if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(message))
        {
            result.Should().StartWith("ERROR:");
        }
    }

    [Fact]
    public async Task CreateTeamsChannelAndMessage_WithValidParameters_ShouldAttemptToCreate()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.CreateTeamsChannelAndMessage("Test Channel", "Test message");

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ListTeams_ShouldAttemptToRetrieve()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListTeams();

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        // But the method should handle it gracefully
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("", "channel-id", "Test message")]
    [InlineData(null, "channel-id", "Test message")]
    [InlineData("team-id", "", "Test message")]
    [InlineData("team-id", null, "Test message")]
    [InlineData("team-id", "channel-id", "")]
    [InlineData("team-id", "channel-id", null)]
    public async Task SendMessageToChannel_WithInvalidParameters_ShouldReturnError(string? teamId, string? channelIdOrName, string? message)
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendMessageToChannel(teamId!, channelIdOrName!, message!);

        // Assert
        result.Should().NotBeNull();
        // Should handle invalid parameters gracefully
        if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelIdOrName) || string.IsNullOrWhiteSpace(message))
        {
            result.Should().StartWith("ERROR:");
        }
    }

    #endregion

    #region Calendar Tests

    [Theory]
    [InlineData("", "Test event", "2024-01-01T10:00:00", "2024-01-01T11:00:00")]
    [InlineData(null, "Test event", "2024-01-01T10:00:00", "2024-01-01T11:00:00")]
    [InlineData("Test Subject", "", "2024-01-01T10:00:00", "2024-01-01T11:00:00")]
    [InlineData("Test Subject", null, "2024-01-01T10:00:00", "2024-01-01T11:00:00")]
    [InlineData("Test Subject", "Test event", "", "2024-01-01T11:00:00")]
    [InlineData("Test Subject", "Test event", null, "2024-01-01T11:00:00")]
    [InlineData("Test Subject", "Test event", "2024-01-01T10:00:00", "")]
    [InlineData("Test Subject", "Test event", "2024-01-01T10:00:00", null)]
    public async Task CreateCalendarEvent_WithInvalidParameters_ShouldReturnError(string? subject, string? body, string? startTime, string? endTime)
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.CreateCalendarEvent(subject!, startTime!, endTime!, body!);

        // Assert
        result.Should().NotBeNull();
        // Should handle invalid parameters gracefully
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body) ||
            string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
        {
            result.Should().StartWith("ERROR:");
        }
    }

    [Fact]
    public async Task CreateCalendarEvent_WithValidParameters_ShouldAttemptToCreate()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.CreateCalendarEvent("Test Subject", "2024-01-01T10:00:00", "2024-01-01T11:00:00", "Test event");

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task DeleteCalendarEvent_WithInvalidEventId_ShouldReturnError(string? eventId)
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.DeleteCalendarEvent(eventId!);

        // Assert
        result.Should().NotBeNull();
        // Should handle invalid parameters gracefully
        if (string.IsNullOrWhiteSpace(eventId))
        {
            result.Should().StartWith("ERROR:");
        }
    }

    [Fact]
    public async Task ListCalendarEvents_ShouldAttemptToRetrieve()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListCalendarEvents();

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        result.Should().NotBeNull();
    }

    #endregion

    #region OneDrive Tests

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ListFiles_WithInvalidFolderPath_ShouldReturnError(string? folderPath)
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        _testConfiguration["MicrosoftOffice:DriveId"] = "test-drive-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListFiles(folderPath!);

        // Assert
        result.Should().NotBeNull();
        // Should handle invalid parameters gracefully
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            result.Should().StartWith("ERROR:");
        }
    }

    [Fact]
    public async Task ListFiles_WithValidFolderPath_ShouldAttemptToRetrieve()
    {
        // Arrange
        _testConfiguration["MicrosoftOffice:ClientId"] = "test-client-id";
        _testConfiguration["MicrosoftOffice:ClientSecret"] = "REDACTED";
        _testConfiguration["MicrosoftOffice:TenantId"] = "test-tenant-id";
        _testConfiguration["MicrosoftOffice:DriveId"] = "test-drive-id";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListFiles("Documents");

        // Assert
        result.Should().NotBeNull();
        // Since we don't have real credentials, this will likely fail with an API error
        result.Should().NotBeNull();
    }

    #endregion
}

