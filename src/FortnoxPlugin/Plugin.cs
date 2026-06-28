using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fortnox;

public partial class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _accessTokenFilePath;

    /// <summary>
    /// Static service provider for accessing DI services (set by Agent during startup)
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Creates a new instance of FortnoxPlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing Fortnox settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? new NullLogger<Plugin>();

        _apiUrl =
            configuration["Fortnox:ApiUrl"]
            ?? throw new ArgumentNullException("Fortnox:ApiUrl configuration is missing");

        _clientId =
            configuration["Fortnox:ClientId"]
            ?? throw new ArgumentNullException("Fortnox:ClientId configuration is missing");

        _clientSecret =
            configuration["Fortnox:ClientSecret"]
            ?? throw new ArgumentNullException("Fortnox:ClientSecret configuration is missing");

        _accessTokenFilePath =
            configuration["Fortnox:AccessTokenFilePath"]
            ?? throw new ArgumentNullException("Fortnox:AccessTokenFilePath configuration is missing");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Client-Secret", _clientSecret);

        _logger.LogInformation("Fortnox plugin initialized");
    }
}
