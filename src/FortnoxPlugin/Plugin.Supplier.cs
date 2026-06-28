using System.Text.Json;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Fortnox;

public partial class Plugin
{
    [KernelFunction]
    [Description("Hämtar en lista med leverantörer från Fortnox.")]
    public async Task<string> GetSuppliersAsync()
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/suppliers";
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
                _logger.LogError("Failed to fetch suppliers: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching suppliers");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Hämtar en leverantör från Fortnox baserat på leverantörsnummer.")]
    public async Task<string> GetSupplierAsync(string supplierNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/suppliers/{supplierNumber}";
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
                _logger.LogError("Failed to fetch supplier: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching supplier");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Skapar en leverantör i Fortnox.")]
    public async Task<string> CreateSupplierAsync(SupplierRequest supplier)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/suppliers";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var supplierPayload = JsonSerializer.Serialize(supplier, options);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(supplierPayload, Encoding.UTF8, "application/json")
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
                _logger.LogError("Failed to create supplier: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when creating supplier");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Uppdaterar en leverantör i Fortnox baserat på leverantörsnummer.")]
    public async Task<string> UpdateSupplierAsync(string supplierNumber, SupplierRequest supplier)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/suppliers/{supplierNumber}";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var supplierPayload = JsonSerializer.Serialize(supplier, options);

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(supplierPayload, Encoding.UTF8, "application/json")
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
                _logger.LogError("Failed to update supplier: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when updating supplier");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Tar bort en leverantör från Fortnox baserat på leverantörsnummer.")]
    public async Task<string> DeleteSupplierAsync(string supplierNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/suppliers/{supplierNumber}";
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
                _logger.LogError("Failed to delete supplier: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when deleting supplier");
            return $"ERROR: {ex.Message}";
        }
    }
}