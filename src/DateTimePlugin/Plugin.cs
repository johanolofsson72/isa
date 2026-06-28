using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace DateTimePlugin;

/// <summary>
/// DateTime plugin för att hämta tid och datum baserat på koordinater
/// </summary>
public class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiUrl;

    /// <summary>
    /// Creates a new instance of DateTimePlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing TimeZoneDB settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? NullLogger<Plugin>.Instance;

        _apiKey =
            configuration["TimeZoneDB:ApiKey"]
            ?? throw new ArgumentNullException("TimeZoneDB:ApiKey configuration is missing");

        _apiUrl = configuration["TimeZoneDB:ApiUrl"] ?? "http://api.timezonedb.com/v2.1";

        _httpClient = new HttpClient();

        _logger.LogInformation("DateTime plugin initialized");
    }

    /// <summary>
    /// Hämtar aktuell tid baserat på koordinater
    /// </summary>
    /// <param name="latitude">Latitud (t.ex. 59.3293)</param>
    /// <param name="longitude">Longitud (t.ex. 18.0686)</param>
    /// <returns>Aktuell tid för angiven plats</returns>
    [KernelFunction]
    [Description("Hämtar aktuell tid baserat på koordinater")]
    public async Task<string> GetCurrentTime(
        [Description("Latitud (breddgrad)")] double latitude,
        [Description("Longitud (längdgrad)")] double longitude
    )
    {
        try
        {
            // Validera koordinater
            if (
                double.IsNaN(latitude)
                || double.IsNaN(longitude)
                || latitude < -90
                || latitude > 90
                || longitude < -180
                || longitude > 180
            )
            {
                throw new ArgumentException(
                    $"Ogiltiga koordinater: lat={latitude}, lng={longitude}"
                );
            }

            var url =
                $"{_apiUrl}/get-time-zone?key={_apiKey}&format=json&by=position&lat={latitude.ToString("F6", CultureInfo.InvariantCulture)}&lng={longitude.ToString("F6", CultureInfo.InvariantCulture)}";

            _logger.LogDebug("Calling TimeZoneDB API: {Url}", url);

            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);

            if (data.TryGetProperty("status", out var status) && status.GetString() != "OK")
            {
                var message = data.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Unknown error";
                throw new InvalidOperationException($"TimeZoneDB API error: {message}");
            }

            var timestamp = data.GetProperty("timestamp").GetInt64();
            var timeZoneName = data.GetProperty("zoneName").GetString() ?? "Unknown";
            var formatted =
                data.GetProperty("formatted").GetString()
                ?? throw new InvalidOperationException("No formatted time received");

            var result = new
            {
                CurrentTime = formatted,
                TimeZone = timeZoneName,
                Timestamp = timestamp,
                Coordinates = new { Latitude = latitude, Longitude = longitude }
            };

            _logger.LogInformation(
                "Retrieved time for coordinates {Lat}, {Lng}: {Time}",
                latitude,
                longitude,
                formatted
            );

            return JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting current time for coordinates {Lat}, {Lng}",
                latitude,
                longitude
            );
            throw new InvalidOperationException($"Failed to get current time: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Hämtar aktuellt datum baserat på koordinater
    /// </summary>
    /// <param name="latitude">Latitud (t.ex. 59.3293)</param>
    /// <param name="longitude">Longitud (t.ex. 18.0686)</param>
    /// <returns>Aktuellt datum för angiven plats</returns>
    [KernelFunction]
    [Description("Hämtar aktuellt datum baserat på koordinater")]
    public async Task<string> GetCurrentDate(
        [Description("Latitud (breddgrad)")] double latitude,
        [Description("Longitud (längdgrad)")] double longitude
    )
    {
        try
        {
            // Validera koordinater
            if (
                double.IsNaN(latitude)
                || double.IsNaN(longitude)
                || latitude < -90
                || latitude > 90
                || longitude < -180
                || longitude > 180
            )
            {
                throw new ArgumentException(
                    $"Ogiltiga koordinater: lat={latitude}, lng={longitude}"
                );
            }

            var url =
                $"{_apiUrl}/get-time-zone?key={_apiKey}&format=json&by=position&lat={latitude.ToString("F6", CultureInfo.InvariantCulture)}&lng={longitude.ToString("F6", CultureInfo.InvariantCulture)}";

            _logger.LogDebug("Calling TimeZoneDB API: {Url}", url);

            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);

            if (data.TryGetProperty("status", out var status) && status.GetString() != "OK")
            {
                var message = data.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Unknown error";
                throw new InvalidOperationException($"TimeZoneDB API error: {message}");
            }

            var timestamp = data.GetProperty("timestamp").GetInt64();
            var timeZoneName = data.GetProperty("zoneName").GetString() ?? "Unknown";
            var formatted =
                data.GetProperty("formatted").GetString()
                ?? throw new InvalidOperationException("No formatted time received");

            // Extrahera endast datum från den formaterade strängen
            var dateTime = System.DateTime.Parse(formatted);
            var dateOnly = dateTime.ToString("yyyy-MM-dd");

            var result = new
            {
                CurrentDate = dateOnly,
                FormattedDate = dateTime.ToString("dddd, dd MMMM yyyy", new CultureInfo("sv-SE")),
                TimeZone = timeZoneName,
                Timestamp = timestamp,
                Coordinates = new { Latitude = latitude, Longitude = longitude }
            };

            _logger.LogInformation(
                "Retrieved date for coordinates {Lat}, {Lng}: {Date}",
                latitude,
                longitude,
                dateOnly
            );

            return JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting current date for coordinates {Lat}, {Lng}",
                latitude,
                longitude
            );
            throw new InvalidOperationException($"Failed to get current date: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Hämtar både aktuell tid och datum baserat på koordinater
    /// </summary>
    /// <param name="latitude">Latitud (t.ex. 59.3293)</param>
    /// <param name="longitude">Longitud (t.ex. 18.0686)</param>
    /// <returns>Aktuell tid och datum för angiven plats</returns>
    [KernelFunction]
    [Description("Hämtar både aktuell tid och datum baserat på koordinater")]
    public async Task<string> GetCurrent(
        [Description("Latitud (breddgrad)")] double latitude,
        [Description("Longitud (längdgrad)")] double longitude
    )
    {
        try
        {
            // Validera koordinater
            if (
                double.IsNaN(latitude)
                || double.IsNaN(longitude)
                || latitude < -90
                || latitude > 90
                || longitude < -180
                || longitude > 180
            )
            {
                throw new ArgumentException(
                    $"Ogiltiga koordinater: lat={latitude}, lng={longitude}"
                );
            }

            var url =
                $"{_apiUrl}/get-time-zone?key={_apiKey}&format=json&by=position&lat={latitude.ToString("F6", CultureInfo.InvariantCulture)}&lng={longitude.ToString("F6", CultureInfo.InvariantCulture)}";

            _logger.LogDebug("Calling TimeZoneDB API: {Url}", url);

            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);

            if (data.TryGetProperty("status", out var status) && status.GetString() != "OK")
            {
                var message = data.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Unknown error";
                throw new InvalidOperationException($"TimeZoneDB API error: {message}");
            }

            var timestamp = data.GetProperty("timestamp").GetInt64();
            var timeZoneName = data.GetProperty("zoneName").GetString() ?? "Unknown";
            var formatted =
                data.GetProperty("formatted").GetString()
                ?? throw new InvalidOperationException("No formatted time received");

            var dateTime = System.DateTime.Parse(formatted);
            var dateOnly = dateTime.ToString("yyyy-MM-dd");
            var timeOnly = dateTime.ToString("HH:mm:ss");

            var result = new
            {
                DateTime = formatted,
                Date = dateOnly,
                Time = timeOnly,
                FormattedDate = dateTime.ToString("dddd, dd MMMM yyyy", new CultureInfo("sv-SE")),
                FormattedTime = dateTime.ToString("HH:mm", new CultureInfo("sv-SE")),
                TimeZone = timeZoneName,
                Timestamp = timestamp,
                Coordinates = new { Latitude = latitude, Longitude = longitude },
                Summary = $"Datum: {dateTime.ToString("dddd, dd MMMM yyyy", new CultureInfo("sv-SE"))}, Tid: {dateTime.ToString("HH:mm", new CultureInfo("sv-SE"))} ({timeZoneName})"
            };

            _logger.LogInformation(
                "Retrieved date and time for coordinates {Lat}, {Lng}: {DateTime}",
                latitude,
                longitude,
                formatted
            );

            return JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting current date and time for coordinates {Lat}, {Lng}",
                latitude,
                longitude
            );
            throw new InvalidOperationException(
                $"Failed to get current date and time: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
