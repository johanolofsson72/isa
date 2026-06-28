using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace OpenCage;

/// <summary>
/// OpenCage plugin för geokodning och adresssökning
/// </summary>
public class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.opencagedata.com/geocode/v1/json";

    /// <summary>
    /// Creates a new instance of OpenCagePlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing OpenCage settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? NullLogger<Plugin>.Instance;

        _apiKey = configuration["OpenCage:ApiKey"] 
            ?? throw new ArgumentNullException("OpenCage:ApiKey configuration is missing");

        _httpClient = new HttpClient();

        _logger.LogInformation("OpenCage plugin initialized");
    }

    /// <summary>
    /// Geokodning: Konverterar adress till koordinater
    /// </summary>
    /// <param name="address">Adressen att geokoda</param>
    /// <returns>Koordinater och adressinformation</returns>
    [KernelFunction]
    [Description("Konverterar en adress till koordinater (geokodning)")]
    public async Task<string> GeocodeAddress(
        [Description("Adressen att geokoda (t.ex. 'Stockholm, Sverige')")] string address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return "ERROR: Adress saknas";
            }

            _logger.LogInformation("Geokodning av adress: {Address}", address);

            var url = $"{BaseUrl}?q={Uri.EscapeDataString(address)}&key={_apiKey}&limit=1";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<JsonElement>(content);
                var results = json.GetProperty("results").EnumerateArray();

                if (results.Any())
                {
                    var result = results.First();
                    var geometry = result.GetProperty("geometry");
                    var lat = geometry.GetProperty("lat").GetDouble();
                    var lng = geometry.GetProperty("lng").GetDouble();
                    var formatted = result.GetProperty("formatted").GetString();

                    _logger.LogInformation("Geokodning framgångsrik för {Address}", address);
                    return $"SUCCESS: Adress '{address}' geokodad till:\nLatitud: {lat}\nLongitud: {lng}\nFormaterad adress: {formatted}";
                }
                else
                {
                    _logger.LogWarning("Ingen adress hittades för {Address}", address);
                    return $"WARNING: Ingen adress hittades för '{address}'";
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenCage API-fel: {Error}", error);
                return $"ERROR: API-fel: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid geokodning");
            return $"ERROR: Fel vid geokodning: {ex.Message}";
        }
    }

    /// <summary>
    /// Omvänd geokodning: Konverterar koordinater till adress
    /// </summary>
    /// <param name="latitude">Latitud</param>
    /// <param name="longitude">Longitud</param>
    /// <returns>Adressinformation</returns>
    [KernelFunction]
    [Description("Konverterar koordinater till adress (omvänd geokodning)")]
    public async Task<string> ReverseGeocode(
        [Description("Latitud (t.ex. 59.3293)")] double latitude,
        [Description("Longitud (t.ex. 18.0686)")] double longitude)
    {
        try
        {
            _logger.LogInformation("Omvänd geokodning av koordinater: {Lat}, {Lng}", latitude, longitude);

            var url = $"{BaseUrl}?q={latitude},{longitude}&key={_apiKey}&limit=1";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<JsonElement>(content);
                var results = json.GetProperty("results").EnumerateArray();

                if (results.Any())
                {
                    var result = results.First();
                    var formatted = result.GetProperty("formatted").GetString();
                    var components = result.GetProperty("components");
                    
                    var city = components.TryGetProperty("city", out var cityElement) ? cityElement.GetString() : "";
                    var country = components.TryGetProperty("country", out var countryElement) ? countryElement.GetString() : "";

                    _logger.LogInformation("Omvänd geokodning framgångsrik för {Lat}, {Lng}", latitude, longitude);
                    return $"SUCCESS: Koordinater ({latitude}, {longitude}) motsvarar:\nAdress: {formatted}\nStad: {city}\nLand: {country}";
                }
                else
                {
                    _logger.LogWarning("Ingen adress hittades för koordinaterna {Lat}, {Lng}", latitude, longitude);
                    return $"WARNING: Ingen adress hittades för koordinaterna ({latitude}, {longitude})";
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenCage API-fel: {Error}", error);
                return $"ERROR: API-fel: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid omvänd geokodning");
            return $"ERROR: Fel vid omvänd geokodning: {ex.Message}";
        }
    }
}
