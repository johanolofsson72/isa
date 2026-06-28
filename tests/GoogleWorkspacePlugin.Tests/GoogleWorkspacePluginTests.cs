using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using GoogleWorkspace;
using Xunit;

namespace GoogleWorkspacePlugin.Tests;

/// <summary>
/// Unit tests for GoogleWorkspacePlugin functionality
/// These tests use real objects and test mode to avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
/// </summary>
[Trait("Category", "CI")]
public class GoogleWorkspacePluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public GoogleWorkspacePluginTests()
    {
        _logger = NullLogger<Plugin>.Instance;
        _testConfiguration = new TestConfiguration();
    }

    private class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values = new();
        public string? this[string key] { get => _values.TryGetValue(key, out var value) ? value : null; set => _values[key] = value; }
        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, _values);
        public T GetValue<T>(string key, T defaultValue) => _values.TryGetValue(key, out var value) && value != null ? (T)Convert.ChangeType(value, typeof(T)) : defaultValue;
        public void SetValue(string key, string? value) => _values[key] = value;
    }

    private class TestConfigurationSection : IConfigurationSection
    {
        private readonly string _key;
        private readonly Dictionary<string, string?> _values;
        public TestConfigurationSection(string key, Dictionary<string, string?> values) { _key = key; _values = values; }
        public string? this[string key] { get => _values.TryGetValue($"{_key}:{key}", out var value) ? value : null; set => _values[$"{_key}:{key}"] = value; }
        public string Key => _key;
        public string Path => _key;
        public string? Value { get => _values.TryGetValue(_key, out var value) ? value : null; set => _values[_key] = value; }
        public IEnumerable<IConfigurationSection> GetChildren() => _values.Where(kvp => kvp.Key.StartsWith(_key + ":") && kvp.Key.Length > _key.Length + 1).Select(kvp => kvp.Key.Substring(_key.Length + 1).Split(':')[0]).Distinct().Select(childKey => new TestConfigurationSection($"{_key}:{childKey}", _values));
        public IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => new TestConfigurationSection($"{_key}:{key}", _values);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithTestMode_ShouldInitialize()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        _testConfiguration["GoogleWorkspace:ImpersonatedUser"] = "test@example.com";

        // Act
        var plugin = new Plugin(_testConfiguration, _logger);

        // Assert
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";

        // Act
        var plugin = new Plugin(_testConfiguration, null);

        // Assert
        plugin.Should().NotBeNull();
    }

    #endregion

    #region ListFiles Tests

    [Fact]
    public async Task ListFiles_InTestMode_ShouldReturnTestData()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListFiles("Work");

        // Assert
        result.Should().StartWith("Test mode: Found 2 files in folder 'Work':");
        result.Should().Contain("test-document.txt");
        result.Should().Contain("test-spreadsheet.xlsx");
    }

    [Fact]
    public async Task ListFiles_WithNullFolder_ShouldUseDefaultFolder()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListFiles(null);

        // Assert
        result.Should().StartWith("Test mode: Found 2 files in folder 'Work':");
    }

    #endregion

    #region UploadFile Tests

    [Fact]
    public async Task UploadFile_InTestMode_ShouldReturnSuccess()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.UploadFile("test.txt", "dGVzdA==", "Work");

        // Assert
        result.Should().StartWith("SUCCESS: Test mode - Fil 'test.txt' uppladdad till mappen 'Work'");
    }

    #endregion

    #region ReadFile Tests

    [Fact]
    public async Task ReadFile_InTestMode_ShouldReturnTestContent()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ReadFile("test.txt", "Work");

        // Assert
        result.Should().NotBeNull();
        // Should return base64 encoded test content
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result));
        decoded.Should().Contain("Test mode content for file 'test.txt'");
    }

    #endregion

    #region Calendar Tests

    [Fact]
    public async Task CreateCalendarEvent_InTestMode_ShouldReturnSuccess()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.CreateCalendarEvent("Test Event", "2024-01-01T10:00:00Z", "2024-01-01T11:00:00Z", "Test description");

        // Assert
        result.Should().StartWith("SUCCESS: Test mode - Kalenderhändelse 'Test Event' skapad.");
    }

    [Fact]
    public async Task ListCalendarEvents_InTestMode_ShouldReturnTestEvents()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListCalendarEvents(5);

        // Assert
        result.Should().StartWith("Test mode: Found 2 calendar events:");
        result.Should().Contain("Team Meeting");
        result.Should().Contain("Project Review");
    }

    #endregion

    #region Chat Tests

    [Fact]
    public async Task SendChatMessage_InTestMode_ShouldReturnSuccess()
    {
        // Arrange
        _testConfiguration["GoogleWorkspace:TestMode"] = "true";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.SendChatMessage("test@example.com", "Test message");

        // Assert
        result.Should().StartWith("SUCCESS: Test mode - Meddelande skickat till test@example.com.");
    }

    #endregion
}

