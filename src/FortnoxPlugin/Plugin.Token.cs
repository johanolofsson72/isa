using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Fortnox;

public partial class Plugin
{
/// <summary>
    /// Returns a valid access token from disk, or an error if expired or missing.
    /// </summary>
    /// <returns>Access token or error message</returns>
    [Description("Hämtar en giltig access token från disk eller returnerar fel om den är ogiltig eller saknas")]
    public async Task<string> GetValidAccessTokenFromFileAsync()
    {
        try
        {
            if (File.Exists(_accessTokenFilePath))
            {
                var json = await File.ReadAllTextAsync(_accessTokenFilePath);
                var token = JsonSerializer.Deserialize<TokenFileModel>(json);

                // Add a 10 minute buffer to the expiration check
                var buffer = TimeSpan.FromMinutes(10);
                if (token != null && token.ExpiresAt > DateTime.UtcNow.Subtract(buffer) && !string.IsNullOrWhiteSpace(token.AccessToken))
                {
                    _logger.LogInformation("Access token loaded from disk and is still valid (with buffer).");
                    return token.AccessToken;
                }
                else
                {
                    _logger.LogWarning("Access token on disk is expired or invalid (including buffer).");
                    return "ERROR: Access token on disk is expired or invalid.";
                }
            }
            else
            {
                _logger.LogWarning("No access token file found.");
                return "ERROR: No access token file found.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting valid access token.");
            return $"ERROR: {ex.Message}";
        }
    }

    /// <summary>
    /// Refreshes the Fortnox access token using the stored refresh token.
    /// </summary>
    /// <returns>New access token or error message</returns>
    [Description("Förnyar access token från Fortnox med refresh token och sparar till fil på disk")]
    public async Task<string> RefreshAccessTokenAsync()
    {
        try
        {
            // Read the current token file
            if (!File.Exists(_accessTokenFilePath))
                return "ERROR: No token file found to refresh.";

            var json = await File.ReadAllTextAsync(_accessTokenFilePath);
            var token = JsonSerializer.Deserialize<TokenFileModel>(json);

            if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
                return "ERROR: No refresh token found in token file.";

            var tokenUrl = "https://apps.fortnox.se/oauth-v1/token";
            var payload = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", token.RefreshToken }
            };
            var content = new FormUrlEncodedContent(payload);

            // Add Basic Auth header
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

            _logger.LogInformation("Förnyar access token från Fortnox med refresh token");
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                if (responseJson.TryGetProperty("access_token", out var tokenElement))
                {
                    var accessToken = tokenElement.GetString() ?? "";
                    var refreshToken = responseJson.TryGetProperty("refresh_token", out var refreshElement) ? refreshElement.GetString() ?? token.RefreshToken : token.RefreshToken;
                    var expiresIn = responseJson.TryGetProperty("expires_in", out var expiresElement) ? expiresElement.GetInt32() : 3600;
                    var tokenType = responseJson.TryGetProperty("token_type", out var typeElement) ? typeElement.GetString() ?? "" : "";
                    var scope = responseJson.TryGetProperty("scope", out var scopeElement) ? scopeElement.GetString() ?? "" : "";

                    var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

                    var newTokenObject = new TokenFileModel
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        TokenType = tokenType,
                        Scope = scope,
                        CreatedAt = token.CreatedAt,
                        LastModified = DateTime.UtcNow
                    };

                    var newJson = JsonSerializer.Serialize(newTokenObject, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_accessTokenFilePath, newJson);

                    _logger.LogInformation("Access token förnyad och sparad till fil: {FilePath}", _accessTokenFilePath);
                    return accessToken;
                }
                else
                {
                    _logger.LogError("access_token saknas i svaret: {Response}", responseContent);
                    return $"ERROR: access_token saknas i svaret: {responseContent}";
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Fortnox API-fel vid förnyelse: {Error}", error);
                return $"ERROR: API-fel vid förnyelse: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid förnyelse av access token");
            return $"ERROR: Fel vid förnyelse av access token: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets a valid access token from disk, or refreshes it if expired or missing.
    /// </summary>
    /// <returns>Access token or error message</returns>
    public async Task<string> GetOrRefreshAccessTokenAsync()
    {
        var token = await GetValidAccessTokenFromFileAsync();
        if (token.StartsWith("ERROR"))
        {
            // Try to refresh if token is expired or missing
            var refreshedToken = await RefreshAccessTokenAsync();
            if (!refreshedToken.StartsWith("ERROR")) {
                return refreshedToken;
            }

            _logger.LogError("Failed to refresh access token: {Error}", refreshedToken);
            return refreshedToken;
        }

        return token;
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