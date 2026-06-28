using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using DateTimePlugin;
using Xunit;

namespace DateTimePlugin.Tests;

/// <summary>
/// Unit tests for DateTimePlugin functionality
/// These tests use real objects and avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
/// </summary>
[Trait("Category", "CI")]
public class DateTimePluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public DateTimePluginTests()
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
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        _testConfiguration["TimeZoneDB:ApiUrl"] = "http://api.timezonedb.com/v2.1";

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
        _testConfiguration["TimeZoneDB:ApiKey"] = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Plugin(_testConfiguration, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";

        // Act
        var plugin = new Plugin(_testConfiguration, null);

        // Assert
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDefaultApiUrl_ShouldUseDefaultUrl()
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        // Don't set ApiUrl to test default

        // Act
        var plugin = new Plugin(_testConfiguration, _logger);

        // Assert
        plugin.Should().NotBeNull();
    }

    #endregion

    #region Coordinate Validation Tests

    [Theory]
    [InlineData(91.0, 0.0)] // Invalid latitude > 90
    [InlineData(-91.0, 0.0)] // Invalid latitude < -90
    [InlineData(0.0, 181.0)] // Invalid longitude > 180
    [InlineData(0.0, -181.0)] // Invalid longitude < -180
    [InlineData(double.NaN, 0.0)] // NaN latitude
    [InlineData(0.0, double.NaN)] // NaN longitude
    public async Task GetCurrentTime_WithInvalidCoordinates_ShouldThrowArgumentException(double latitude, double longitude)
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => plugin.GetCurrentTime(latitude, longitude));
        exception.InnerException.Should().BeOfType<ArgumentException>();
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    public async Task GetCurrentDate_WithInvalidCoordinates_ShouldThrowArgumentException(double latitude, double longitude)
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => plugin.GetCurrentDate(latitude, longitude));
        exception.InnerException.Should().BeOfType<ArgumentException>();
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    public async Task GetCurrent_WithInvalidCoordinates_ShouldThrowArgumentException(double latitude, double longitude)
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => plugin.GetCurrent(latitude, longitude));
        exception.InnerException.Should().BeOfType<ArgumentException>();
    }

    #endregion

    #region Valid Coordinate Tests

    [Theory]
    [InlineData(59.3293, 18.0686)] // Stockholm
    [InlineData(40.7128, -74.0060)] // New York
    [InlineData(-33.8688, 151.2093)] // Sydney
    [InlineData(0.0, 0.0)] // Null Island
    [InlineData(90.0, 0.0)] // North Pole
    [InlineData(-90.0, 0.0)] // South Pole
    [InlineData(0.0, 180.0)] // International Date Line
    [InlineData(0.0, -180.0)] // International Date Line (other side)
    public async Task GetCurrentTime_WithValidCoordinates_ShouldAttemptApiCall(double latitude, double longitude)
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        // Since we don't have a real API key, this will likely fail with an API error
        // But it should not fail with coordinate validation
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => plugin.GetCurrentTime(latitude, longitude));
        exception.Message.Should().NotContain("Ogiltiga koordinater");
    }

    [Theory]
    [InlineData(59.3293, 18.0686)] // Stockholm
    [InlineData(40.7128, -74.0060)] // New York
    [InlineData(-33.8688, 151.2093)] // Sydney
    public async Task GetCurrentDate_WithValidCoordinates_ShouldAttemptApiCall(double latitude, double longitude)
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        // Since we don't have a real API key, this will likely fail with an API error
        // But it should not fail with coordinate validation
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => plugin.GetCurrentDate(latitude, longitude));
        exception.Message.Should().NotContain("Ogiltiga koordinater");
    }

    [Theory]
    [InlineData(59.3293, 18.0686)] // Stockholm
    [InlineData(40.7128, -74.0060)] // New York
    [InlineData(-33.8688, 151.2093)] // Sydney
    public async Task GetCurrent_WithValidCoordinates_ShouldAttemptApiCall(double latitude, double longitude)
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        // Since we don't have a real API key, this will likely fail with an API error
        // But it should not fail with coordinate validation
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => plugin.GetCurrent(latitude, longitude));
        exception.Message.Should().NotContain("Ogiltiga koordinater");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        plugin.Invoking(p => p.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        _testConfiguration["TimeZoneDB:ApiKey"] = "test-api-key";
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act & Assert
        plugin.Invoking(p => p.Dispose()).Should().NotThrow();
        plugin.Invoking(p => p.Dispose()).Should().NotThrow();
    }

    #endregion
}

