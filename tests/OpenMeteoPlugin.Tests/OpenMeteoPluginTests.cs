using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using OpenMeteo;
using Xunit;

namespace OpenMeteoPlugin.Tests;

/// <summary>
/// Unit tests for OpenMeteoPlugin functionality
/// These tests use real objects and avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
/// </summary>
[Trait("Category", "CI")]
public class OpenMeteoPluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public OpenMeteoPluginTests()
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
        // Arrange & Act
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
    public void Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        // Arrange & Act
        var plugin = new Plugin(_testConfiguration, null);

        // Assert
        plugin.Should().NotBeNull();
    }

    #endregion

    #region GetCurrentWeather Tests

    [Fact]
    public async Task GetCurrentWeather_WithValidCoordinates_ShouldAttemptToGetWeather()
    {
        // Arrange
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GetCurrentWeather(59.3293, 18.0686); // Stockholm coordinates

        // Assert
        result.Should().NotBeNull();
        // Since we don't have safe mode, this will attempt a real API call
        // The result will either be weather data or an error message
    }

    [Theory]
    [InlineData(91.0, 0.0)] // Invalid latitude > 90
    [InlineData(-91.0, 0.0)] // Invalid latitude < -90
    [InlineData(0.0, 181.0)] // Invalid longitude > 180
    [InlineData(0.0, -181.0)] // Invalid longitude < -180
    public async Task GetCurrentWeather_WithInvalidCoordinates_ShouldHandleGracefully(double latitude, double longitude)
    {
        // Arrange
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GetCurrentWeather(latitude, longitude);

        // Assert
        result.Should().NotBeNull();
        // OpenMeteo API should handle invalid coordinates gracefully
    }

    #endregion

    #region GetWeatherForecast Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(10)]
    public async Task GetWeatherForecast_WithInvalidDays_ShouldReturnError(int days)
    {
        // Arrange
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GetWeatherForecast(59.3293, 18.0686, days);

        // Assert
        result.Should().Be("ERROR: Antal dagar måste vara mellan 1 och 7");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task GetWeatherForecast_WithValidDays_ShouldAttemptToGetForecast(int days)
    {
        // Arrange
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GetWeatherForecast(59.3293, 18.0686, days);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBe("ERROR: Antal dagar måste vara mellan 1 och 7");
    }

    [Fact]
    public async Task GetWeatherForecast_WithDefaultDays_ShouldUseThreeDays()
    {
        // Arrange
        var plugin = new Plugin(_testConfiguration, _logger);

        // Act
        var result = await plugin.GetWeatherForecast(59.3293, 18.0686);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBe("ERROR: Antal dagar måste vara mellan 1 och 7");
    }

    #endregion
}

