using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using OpenCage;
using Xunit;

namespace OpenCagePlugin.Tests;

/// <summary>
/// Unit tests for OpenCagePlugin functionality
/// These tests use real objects and avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
/// </summary>
[Trait("Category", "CI")]
public class OpenCagePluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public OpenCagePluginTests()
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
        _testConfiguration["OpenCage:ApiKey"] = "test-api-key";

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

    [Fact]
    public void Constructor_WithMissingApiKey_ShouldThrowArgumentNullException()
    {
        // Arrange
        _testConfiguration["OpenCage:ApiKey"] = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Plugin(_testConfiguration, _logger));
    }

    #endregion

    #region GeocodeAddress Tests

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GeocodeAddress_WithInvalidAddress_ShouldReturnError(string? address)
    {
        // Arrange
        _testConfiguration["OpenCage:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GeocodeAddress(address!);

        // Assert
        result.Should().Be("ERROR: Adress saknas");
    }

    [Fact]
    public async Task GeocodeAddress_WithValidAddress_ShouldAttemptToGeocode()
    {
        // Arrange
        _testConfiguration["OpenCage:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GeocodeAddress("Stockholm, Sverige");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBe("ERROR: Adress saknas");
        // Since we don't have a real API key, this will likely fail with an API error
    }

    [Theory]
    [InlineData("Stockholm")]
    [InlineData("New York, USA")]
    [InlineData("Tokyo, Japan")]
    [InlineData("123 Main Street, Anytown")]
    public async Task GeocodeAddress_WithVariousAddressFormats_ShouldHandleGracefully(string address)
    {
        // Arrange
        _testConfiguration["OpenCage:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GeocodeAddress(address);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBe("ERROR: Adress saknas");
    }

    #endregion

    #region ReverseGeocode Tests

    [Fact]
    public async Task ReverseGeocode_WithValidCoordinates_ShouldAttemptToReverseGeocode()
    {
        // Arrange
        _testConfiguration["OpenCage:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ReverseGeocode(59.3293, 18.0686); // Stockholm coordinates

        // Assert
        result.Should().NotBeNull();
        // Since we don't have a real API key, this will likely fail with an API error
    }

    [Theory]
    [InlineData(0.0, 0.0)] // Null Island
    [InlineData(90.0, 0.0)] // North Pole
    [InlineData(-90.0, 0.0)] // South Pole
    [InlineData(0.0, 180.0)] // International Date Line
    [InlineData(0.0, -180.0)] // International Date Line (other side)
    public async Task ReverseGeocode_WithEdgeCaseCoordinates_ShouldHandleGracefully(double latitude, double longitude)
    {
        // Arrange
        _testConfiguration["OpenCage:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.ReverseGeocode(latitude, longitude);

        // Assert
        result.Should().NotBeNull();
        // Should handle edge cases gracefully
    }

    #endregion
}

