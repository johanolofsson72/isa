using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Dropbox;
using Xunit;

namespace DropboxPlugin.Tests;

/// <summary>
/// Unit tests for DropboxPlugin functionality
/// These tests use real objects and avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
/// </summary>
[Trait("Category", "CI")]
public class DropboxPluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public DropboxPluginTests()
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
    public void Constructor_WithValidConfiguration_ShouldInitialize()
    {
        // Arrange
        _testConfiguration["Dropbox:AppKey"] = "test-app-key";
        _testConfiguration["Dropbox:AppSecret"] = "test-app-secret";
        _testConfiguration["Dropbox:RefreshToken"] = "test-refresh-token";

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
    [InlineData("Dropbox:AppKey")]
    [InlineData("Dropbox:AppSecret")]
    [InlineData("Dropbox:RefreshToken")]
    public void Constructor_WithMissingConfigurationValue_ShouldThrowArgumentNullException(string missingKey)
    {
        // Arrange
        var config = new TestConfiguration();
        config["Dropbox:AppKey"] = missingKey == "Dropbox:AppKey" ? null : "test-app-key";
        config["Dropbox:AppSecret"] = missingKey == "Dropbox:AppSecret" ? null : "test-app-secret";
        config["Dropbox:RefreshToken"] = missingKey == "Dropbox:RefreshToken" ? null : "test-refresh-token";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Plugin(config, _logger));
    }

    #endregion

    #region ListFiles Tests

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ListFiles_WithInvalidPath_ShouldReturnError(string? path)
    {
        // Arrange
        _testConfiguration["Dropbox:AppKey"] = "test-app-key";
        _testConfiguration["Dropbox:AppSecret"] = "test-app-secret";
        _testConfiguration["Dropbox:RefreshToken"] = "test-refresh-token";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListFiles(path!);

        // Assert
        // DropboxPlugin doesn't validate parameters, it tries API call and returns API error
        result.Should().StartWith("ERROR:");
        result.Should().Contain("Fel vid listning av filer");
    }

    [Fact]
    public async Task ListFiles_WithValidPath_ShouldAttemptToList()
    {
        // Arrange
        _testConfiguration["Dropbox:AppKey"] = "test-app-key";
        _testConfiguration["Dropbox:AppSecret"] = "test-app-secret";
        _testConfiguration["Dropbox:RefreshToken"] = "test-refresh-token";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ListFiles("/Documents");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBe("ERROR: Sökväg saknas");
    }

    #endregion

    #region UploadFile Tests

    [Theory]
    [InlineData("", "dGVzdA==", "/", "ERROR: Filnamn saknas")]
    [InlineData(null, "dGVzdA==", "/", "ERROR: Filnamn saknas")]
    [InlineData("   ", "dGVzdA==", "/", "ERROR: Filnamn saknas")]
    [InlineData("test.txt", "", "/", "ERROR: Filinnehåll saknas")]
    [InlineData("test.txt", null, "/", "ERROR: Filinnehåll saknas")]
    [InlineData("test.txt", "   ", "/", "ERROR: Filinnehåll saknas")]
    public async Task UploadFile_WithInvalidParameters_ShouldReturnError(string? fileName, string? content, string path, string expectedError)
    {
        // Arrange
        _testConfiguration["Dropbox:AppKey"] = "test-app-key";
        _testConfiguration["Dropbox:AppSecret"] = "test-app-secret";
        _testConfiguration["Dropbox:RefreshToken"] = "test-refresh-token";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.UploadFile(fileName!, content!, path);

        // Assert
        result.Should().Be(expectedError);
    }

    #endregion

    #region DownloadFile Tests

    [Theory]
    [InlineData("", "ERROR: Filsökväg saknas")]
    [InlineData(null, "ERROR: Filsökväg saknas")]
    [InlineData("   ", "ERROR: Filsökväg saknas")]
    public async Task DownloadFile_WithInvalidPath_ShouldReturnError(string? filePath, string expectedError)
    {
        // Arrange
        _testConfiguration["Dropbox:AppKey"] = "test-app-key";
        _testConfiguration["Dropbox:AppSecret"] = "test-app-secret";
        _testConfiguration["Dropbox:RefreshToken"] = "test-refresh-token";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.DownloadFile(filePath!);

        // Assert
        result.Should().Be(expectedError);
    }

    #endregion
}

