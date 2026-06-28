using System.Text.Json;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Fortnox;

public partial class Plugin
{
    [KernelFunction]
    [Description("Hämtar en lista med konton från Fortnox.")]
    public async Task<string> GetAccountsAsync()
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/accounts";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }
            else
            {
                _logger.LogError("Failed to fetch accounts: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching accounts");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Hämtar ett konto från Fortnox baserat på kontonummer.")]
    public async Task<string> GetAccountAsync(string accountNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/accounts/{accountNumber}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }
            else
            {
                _logger.LogError("Failed to fetch account: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching account");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Skapar ett konto i Fortnox.")]
    public async Task<string> CreateAccountAsync(AccountRequest account)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/accounts";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var accountPayload = JsonSerializer.Serialize(account, options);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(accountPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }
            else
            {
                _logger.LogError("Failed to create account: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when creating account");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Uppdaterar ett konto i Fortnox baserat på kontonummer.")]
    public async Task<string> UpdateAccountAsync(string accountNumber, AccountRequest account)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/accounts/{accountNumber}";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var accountPayload = JsonSerializer.Serialize(account, options);

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(accountPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }
            else
            {
                _logger.LogError("Failed to update account: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when updating account");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Tar bort ett konto från Fortnox baserat på kontonummer.")]
    public async Task<string> DeleteAccountAsync(string accountNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/accounts/{accountNumber}";
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }
            else
            {
                _logger.LogError("Failed to delete account: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when deleting account");
            return $"ERROR: {ex.Message}";
        }
    }
}