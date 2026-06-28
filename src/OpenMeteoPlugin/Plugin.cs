using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace OpenMeteo;

/// <summary>
/// OpenMeteo plugin för väderdata
/// </summary>
public class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.open-meteo.com/v1/forecast";

    /// <summary>
    /// Creates a new instance of OpenMeteoPlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing OpenMeteo settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? NullLogger<Plugin>.Instance;
        _httpClient = new HttpClient();

        _logger.LogInformation("OpenMeteo plugin initialized");
    }

    /// <summary>
    /// Hämtar aktuellt väder för en plats
    /// </summary>
    /// <param name="latitude">Latitud</param>
    /// <param name="longitude">Longitud</param>
    /// <returns>Väderdata</returns>
    [KernelFunction]
    [Description("Hämtar aktuellt väder för en plats")]
    public async Task<string> GetCurrentWeather(
        [Description("Latitud (t.ex. 59.3293)")] double latitude,
        [Description("Longitud (t.ex. 18.0686)")] double longitude
    )
    {
        try
        {
            _logger.LogInformation(
                "Hämtar väder för koordinater: {Lat}, {Lng}",
                latitude,
                longitude
            );

            // Använd invariant kultur för att säkerställa punkt som decimalseparator
            var latStr = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lngStr = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Utökad URL med fler parametrar för detaljerad väderdata
            var url =
                $"{BaseUrl}?latitude={latStr}&longitude={lngStr}&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,rain,showers,snowfall,weather_code,cloud_cover,pressure_msl,surface_pressure,wind_speed_10m,wind_direction_10m,wind_gusts_10m&hourly=uv_index,uv_index_clear_sky,sunshine_duration&daily=uv_index_max,uv_index_clear_sky_max,sunshine_duration&timezone=auto&forecast_days=1";
            _logger.LogInformation("API URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<JsonElement>(content);

                // Aktuell data
                var current = json.GetProperty("current");
                var temperature = current.GetProperty("temperature_2m").GetDouble();
                var feelsLike = current.GetProperty("apparent_temperature").GetDouble();
                var humidity = current.GetProperty("relative_humidity_2m").GetInt32();
                var isDay = current.GetProperty("is_day").GetInt32() == 1;
                var precipitation = current.GetProperty("precipitation").GetDouble();
                var rain = current.GetProperty("rain").GetDouble();
                var cloudCover = current.GetProperty("cloud_cover").GetInt32();
                var pressure = current.GetProperty("pressure_msl").GetDouble();
                var windSpeed = current.GetProperty("wind_speed_10m").GetDouble();
                var windDirection = current.GetProperty("wind_direction_10m").GetDouble();
                var windGusts = current.GetProperty("wind_gusts_10m").GetDouble();
                var weatherCode = current.GetProperty("weather_code").GetInt32();
                var time = current.GetProperty("time").GetString();

                // UV-index och soldata från daily
                var daily = json.GetProperty("daily");
                var uvIndexMax = daily
                    .GetProperty("uv_index_max")
                    .EnumerateArray()
                    .First()
                    .GetDouble();
                var sunshineDuration = daily
                    .GetProperty("sunshine_duration")
                    .EnumerateArray()
                    .First()
                    .GetDouble();

                // Aktuell UV från hourly (hitta närmaste timme)
                var hourly = json.GetProperty("hourly");
                var uvIndexArray = hourly.GetProperty("uv_index").EnumerateArray().ToArray();
                var currentHour = DateTime.Now.Hour;
                var currentUvIndex =
                    uvIndexArray.Length > currentHour ? uvIndexArray[currentHour].GetDouble() : 0;

                var weatherDescription = GetWeatherDescription(weatherCode);
                var uvDescription = GetUvDescription(currentUvIndex, uvIndexMax);
                var sunDescription = GetSunDescription(sunshineDuration, isDay);

                _logger.LogInformation(
                    "Väderdata hämtad framgångsrikt för {Lat}, {Lng}",
                    latitude,
                    longitude
                );

                return $"SUCCESS: Detaljerat väder för ({latitude}, {longitude}):\n"
                    + $"🌡️ Temperatur: {temperature}°C (känns som {feelsLike}°C)\n"
                    + $"💨 Vind: {windSpeed} m/s från {windDirection}° (byar upp till {windGusts} m/s)\n"
                    + $"💧 Luftfuktighet: {humidity}%\n"
                    + $"🌧️ Nederbörd: {precipitation} mm (regn: {rain} mm)\n"
                    + $"☁️ Molntäcke: {cloudCover}%\n"
                    + $"📊 Lufttryck: {pressure} hPa\n"
                    + $"☀️ UV-index: {currentUvIndex:F1} (max idag: {uvIndexMax:F1}) - {uvDescription}\n"
                    + $"🌞 Solsken: {sunshineDuration / 3600:F1} timmar idag\n"
                    + $"🌤️ Väder: {weatherDescription} ({(isDay ? "Dag" : "Natt")})\n"
                    + $"⏰ Tidpunkt: {time}\n"
                    + $"📝 {sunDescription}";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenMeteo API-fel: {Error}", error);
                return $"ERROR: API-fel: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid hämtning av väderdata");
            return $"ERROR: Fel vid hämtning av väderdata: {ex.Message}";
        }
    }

    /// <summary>
    /// Hämtar väderprognos för en plats
    /// </summary>
    /// <param name="latitude">Latitud</param>
    /// <param name="longitude">Longitud</param>
    /// <param name="days">Antal dagar framåt (1-7)</param>
    /// <returns>Väderprognos</returns>
    [KernelFunction]
    [Description("Hämtar väderprognos för en plats")]
    public async Task<string> GetWeatherForecast(
        [Description("Latitud (t.ex. 59.3293)")] double latitude,
        [Description("Longitud (t.ex. 18.0686)")] double longitude,
        [Description("Antal dagar framåt (1-7)")] int days = 3
    )
    {
        try
        {
            if (days < 1 || days > 7)
            {
                return "ERROR: Antal dagar måste vara mellan 1 och 7";
            }

            _logger.LogInformation(
                "Hämtar väderprognos för {Days} dagar för koordinater: {Lat}, {Lng}",
                days,
                latitude,
                longitude
            );

            // Använd invariant kultur för att säkerställa punkt som decimalseparator
            var latStr = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lngStr = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Utökad URL med UV och soldata för prognos
            var url =
                $"{BaseUrl}?latitude={latStr}&longitude={lngStr}&daily=temperature_2m_max,temperature_2m_min,weather_code,uv_index_max,sunshine_duration,precipitation_sum,wind_speed_10m_max,wind_gusts_10m_max&forecast_days={days}&timezone=auto";
            _logger.LogInformation("API URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<JsonElement>(content);
                var daily = json.GetProperty("daily");

                var times = daily
                    .GetProperty("time")
                    .EnumerateArray()
                    .Select(t => t.GetString())
                    .ToArray();
                var maxTemps = daily
                    .GetProperty("temperature_2m_max")
                    .EnumerateArray()
                    .Select(t => t.GetDouble())
                    .ToArray();
                var minTemps = daily
                    .GetProperty("temperature_2m_min")
                    .EnumerateArray()
                    .Select(t => t.GetDouble())
                    .ToArray();
                var weatherCodes = daily
                    .GetProperty("weather_code")
                    .EnumerateArray()
                    .Select(w => w.GetInt32())
                    .ToArray();
                var uvMaxValues = daily
                    .GetProperty("uv_index_max")
                    .EnumerateArray()
                    .Select(uv => uv.GetDouble())
                    .ToArray();
                var sunshineDurations = daily
                    .GetProperty("sunshine_duration")
                    .EnumerateArray()
                    .Select(s => s.GetDouble())
                    .ToArray();
                var precipitations = daily
                    .GetProperty("precipitation_sum")
                    .EnumerateArray()
                    .Select(p => p.GetDouble())
                    .ToArray();
                var windSpeeds = daily
                    .GetProperty("wind_speed_10m_max")
                    .EnumerateArray()
                    .Select(w => w.GetDouble())
                    .ToArray();

                var forecast = new List<string>();
                for (int i = 0; i < times.Length; i++)
                {
                    var weatherDescription = GetWeatherDescription(weatherCodes[i]);
                    var uvDescription = GetUvDescription(uvMaxValues[i], uvMaxValues[i]);
                    var sunHours = sunshineDurations[i] / 3600;

                    forecast.Add($"📅 {times[i]}:");
                    forecast.Add($"   🌡️ {minTemps[i]:F1}°C - {maxTemps[i]:F1}°C");
                    forecast.Add($"   🌤️ {weatherDescription}");
                    forecast.Add(
                        $"   ☀️ UV-index: {uvMaxValues[i]:F1} ({uvDescription.Split('-')[0].Trim()})"
                    );
                    forecast.Add(
                        $"   🌞 Solsken: {sunHours:F1}h, 🌧️ Nederbörd: {precipitations[i]:F1}mm, 💨 Vind: {windSpeeds[i]:F1}m/s"
                    );
                    forecast.Add("");
                }

                _logger.LogInformation("Väderprognos hämtad framgångsrikt för {Days} dagar", days);

                return $"SUCCESS: Väderprognos för ({latitude}, {longitude}) - {days} dagar:\n"
                    + string.Join("\n", forecast);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenMeteo API-fel: {Error}", error);
                return $"ERROR: API-fel: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid hämtning av väderprognos");
            return $"ERROR: Fel vid hämtning av väderprognos: {ex.Message}";
        }
    }

    private static string GetWeatherDescription(int weatherCode)
    {
        return weatherCode switch
        {
            0 => "Klar himmel",
            1 => "Mestadels klart",
            2 => "Delvis molnigt",
            3 => "Molnigt",
            45 => "Dimma",
            48 => "Rimfrost dimma",
            51 => "Lätt duggregn",
            53 => "Måttligt duggregn",
            55 => "Kraftigt duggregn",
            61 => "Lätt regn",
            63 => "Måttligt regn",
            65 => "Kraftigt regn",
            71 => "Lätt snöfall",
            73 => "Måttligt snöfall",
            75 => "Kraftigt snöfall",
            80 => "Lätta regnskurar",
            81 => "Måttliga regnskurar",
            82 => "Kraftiga regnskurar",
            95 => "Åska",
            96 => "Åska med lätt hagel",
            99 => "Åska med kraftigt hagel",
            _ => $"Okänt väder (kod: {weatherCode})"
        };
    }

    private static string GetUvDescription(double uvIndex, double maxUvIndex)
    {
        return uvIndex switch
        {
            >= 0 and < 3 => "Låg UV-strålning - minimal solskyddsrisk",
            >= 3 and < 6 => "Måttlig UV-strålning - använd solskydd",
            >= 6 and < 8 => "Hög UV-strålning - solskydd nödvändigt",
            >= 8 and < 11 => "Mycket hög UV-strålning - extra skydd krävs",
            >= 11 => "Extrem UV-strålning - undvik sol",
            _ => $"UV-index: {uvIndex:F1}"
        };
    }

    private static string GetSunDescription(double sunshineDuration, bool isDay)
    {
        var hours = sunshineDuration / 3600;
        var sunInfo = hours switch
        {
            >= 8 => "Mycket soligt - stark solstrålning hela dagen",
            >= 6 => "Soligt - bra solförhållanden",
            >= 4 => "Delvis soligt - måttlig solstrålning",
            >= 2 => "Lite sol - begränsad solstrålning",
            _ => "Molnigt - minimal solstrålning"
        };

        var dayNight = isDay ? "Dagsljus" : "Mörkt";
        return $"{sunInfo}. {dayNight} just nu.";
    }
}
