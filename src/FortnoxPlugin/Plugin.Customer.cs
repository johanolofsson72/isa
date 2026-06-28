using System.Text.Json;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Fortnox;

public partial class Plugin
{
    [KernelFunction]
    [Description("Skapar en kund i Fortnox.")]
    public async Task<string> CreateCustomerAsync(CustomerRequest customer)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/customers";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var customerPayload = JsonSerializer.Serialize(customer, options);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(customerPayload, Encoding.UTF8, "application/json")
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
                _logger.LogError("Failed to create customer: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when creating customer");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Hämtar en kund från Fortnox baserat på kundnummer.")]
    public async Task<string> GetCustomerAsync(string customerNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/customers/{customerNumber}";
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
                _logger.LogError("Failed to fetch customer: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching customer");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Hämtar en lista med kunder från Fortnox.")]
    public async Task<string> GetCustomersAsync()
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/customers";
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
                _logger.LogError("Failed to fetch customers: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching customers");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Tar bort en kund från Fortnox baserat på kundnummer.")]
    public async Task<string> DeleteCustomerAsync(string customerNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/customers/{customerNumber}";
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
                _logger.LogError("Failed to delete customer: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when deleting customer");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Uppdaterar en kund i Fortnox baserat på kundnummer.")]
    public async Task<string> UpdateCustomerAsync(string customerNumber, CustomerRequest customer)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/customers/{customerNumber}";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var customerPayload = JsonSerializer.Serialize(customer, options);

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(customerPayload, Encoding.UTF8, "application/json")
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
                _logger.LogError("Failed to update customer: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when updating customer");
            return $"ERROR: {ex.Message}";
        }
    }
}