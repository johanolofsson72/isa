using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WebChat.Services
{
    public class FortnoxService
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly string _redirectUri;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _accessTokenFilePath;

        /// <summary>
        /// Creates a new instance of FortnoxPlugin from configuration
        /// </summary>
        /// <param name="configuration">Configuration containing Fortnox settings</param>
        /// <param name="logger">Optional logger for the plugin</param>
        public FortnoxService(IConfiguration configuration, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            _logger = logger ?? new NullLogger<FortnoxService>();

            _redirectUri =
                configuration["Fortnox:RedirectUri"]
                ?? throw new ArgumentNullException("Fortnox:RedirectUri configuration is missing");

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

            _logger.LogInformation("=== FORTNOX SERVICE INITIALIZED ===");
            _logger.LogInformation("ClientId: {ClientId}", _clientId);
            _logger.LogInformation("RedirectUri: {RedirectUri}", _redirectUri);
            _logger.LogInformation("AccessTokenFilePath: {FilePath}", _accessTokenFilePath);
            _logger.LogInformation("ClientSecret configured: {HasSecret}", !string.IsNullOrEmpty(_clientSecret));
            _logger.LogInformation("=== FORTNOX SERVICE READY ===");
        }

        /// <summary>
        /// Fetch a new access token from Fortnox and save it to disk.
        /// </summary>
        /// <returns>Access token or error message</returns>
        [Description("Hämtar en ny access token från Fortnox och sparar till fil på disk")]
        public async Task<string> FetchAccessTokenFromFortnoxAsync(string authCode)
        {
            _logger.LogInformation("=== FORTNOX OAUTH FLOW START ===");
            _logger.LogInformation("AuthCode mottagen: {AuthCode}", authCode?.Substring(0, Math.Min(10, authCode?.Length ?? 0)) + "...");
            _logger.LogInformation("RedirectUri: {RedirectUri}", _redirectUri);
            _logger.LogInformation("ClientId: {ClientId}", _clientId);
            _logger.LogInformation("AccessTokenFilePath: {FilePath}", _accessTokenFilePath);

            try
            {
                var tokenUrl = "https://apps.fortnox.se/oauth-v1/token";
                _logger.LogInformation("Token URL: {TokenUrl}", tokenUrl);

                var payload = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", authCode! },
                    { "redirect_uri", _redirectUri } // Same url as used when generating the auth code.
                };
                _logger.LogInformation("Payload skapad - grant_type: {GrantType}, redirect_uri: {RedirectUri}",
                    payload["grant_type"], payload["redirect_uri"]);

                var content = new FormUrlEncodedContent(payload);

                // Add Basic Auth header
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
                _logger.LogInformation("Basic Auth header skapad för ClientId: {ClientId}", _clientId);

                var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
                {
                    Content = content
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                _logger.LogInformation("HTTP request skapad med Basic Auth");

                _logger.LogInformation("Skickar token request till Fortnox...");
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Fortnox svar mottaget - StatusCode: {StatusCode}", response.StatusCode);
                _logger.LogInformation("Response Content Length: {Length}", responseContent?.Length ?? 0);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Fortnox svar SUCCESS - börjar parsa JSON");
                    var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent!);
                    _logger.LogInformation("JSON deserialiserat framgångsrikt");

                    if (responseJson.TryGetProperty("access_token", out var tokenElement))
                    {
                        _logger.LogInformation("✅ access_token hittad i svaret");
                        var accessToken = tokenElement.GetString() ?? "";
                        var refreshToken = responseJson.TryGetProperty("refresh_token", out var refreshElement) ? refreshElement.GetString() ?? "" : "";
                        var expiresIn = responseJson.TryGetProperty("expires_in", out var expiresElement) ? expiresElement.GetInt32() : 3600;
                        var tokenType = responseJson.TryGetProperty("token_type", out var typeElement) ? typeElement.GetString() ?? "" : "";
                        var scope = responseJson.TryGetProperty("scope", out var scopeElement) ? scopeElement.GetString() ?? "" : "";

                        _logger.LogInformation("Token data extraherad - TokenType: {TokenType}, ExpiresIn: {ExpiresIn}s, Scope: {Scope}",
                            tokenType, expiresIn, scope);
                        _logger.LogInformation("AccessToken längd: {Length}, RefreshToken längd: {RefreshLength}",
                            accessToken?.Length ?? 0, refreshToken?.Length ?? 0);

                        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                        _logger.LogInformation("Token expires at: {ExpiresAt} UTC", expiresAt);

                        var tokenObject = new TokenFileModel
                        {
                            AccessToken = accessToken!,
                            RefreshToken = refreshToken!,
                            ExpiresAt = expiresAt,
                            TokenType = tokenType,
                            Scope = scope,
                            CreatedAt = DateTime.UtcNow,
                            LastModified = DateTime.UtcNow
                        };

                        var json = JsonSerializer.Serialize(tokenObject, new JsonSerializerOptions { WriteIndented = true });
                        _logger.LogInformation("Token serialiserat till JSON, längd: {Length}", json?.Length ?? 0);

                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(_accessTokenFilePath);
                        _logger.LogInformation("Kontrollerar katalog: {Directory}", directory);

                        if (!string.IsNullOrEmpty(directory))
                        {
                            if (!Directory.Exists(directory))
                            {
                                _logger.LogWarning("Katalog existerar inte, skapar: {Directory}", directory);
                                Directory.CreateDirectory(directory);
                                _logger.LogInformation("✅ Katalog skapad: {Directory}", directory);
                            }
                            else
                            {
                                _logger.LogInformation("✅ Katalog existerar redan: {Directory}", directory);
                            }
                        }

                        _logger.LogInformation("Skriver token till fil: {FilePath}", _accessTokenFilePath);
                        await File.WriteAllTextAsync(_accessTokenFilePath, json);
                        _logger.LogInformation("✅ Token fil skriven framgångsrikt!");

                        // Verifiera att filen skapades
                        if (File.Exists(_accessTokenFilePath))
                        {
                            var fileInfo = new FileInfo(_accessTokenFilePath);
                            _logger.LogInformation("✅ Fil verifierad - Storlek: {Size} bytes, Skapad: {Created}",
                                fileInfo.Length, fileInfo.CreationTime);
                        }
                        else
                        {
                            _logger.LogError("❌ Fil kunde inte verifieras efter skrivning!");
                        }

                        _logger.LogInformation("=== FORTNOX OAUTH FLOW SUCCESS ===");
                        return accessToken!;
                    }
                    else
                    {
                        _logger.LogError("❌ access_token saknas i svaret från Fortnox");
                        _logger.LogError("Fullständigt svar: {Response}", responseContent);
                        _logger.LogInformation("=== FORTNOX OAUTH FLOW FAILED - NO ACCESS TOKEN ===");
                        return $"ERROR: access_token saknas i svaret: {responseContent}";
                    }
                }
                else
                {
                    _logger.LogError("❌ Fortnox API returnerade fel - StatusCode: {StatusCode}", response.StatusCode);
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Fel från Fortnox API: {Error}", error);
                    _logger.LogInformation("=== FORTNOX OAUTH FLOW FAILED - API ERROR ===");
                    return $"ERROR: API-fel: {error}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception vid hämtning av access token");
                _logger.LogError("Exception typ: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception meddelande: {Message}", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerException}", ex.InnerException.Message);
                }
                _logger.LogInformation("=== FORTNOX OAUTH FLOW FAILED - EXCEPTION ===");
                return $"ERROR: Fel vid hämtning av access token: {ex.Message}";
            }
        }

        private class TokenFileModel
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public string TokenType { get; set; } = string.Empty;
            public string Scope { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime LastModified { get; set; }
        }
    }
}
