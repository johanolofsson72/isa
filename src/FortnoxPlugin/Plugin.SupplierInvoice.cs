using System.Text.Json;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Fortnox;

public partial class Plugin
{
    [KernelFunction]
    [Description("Hämtar en lista med leverantörsfakturor från Fortnox.")]
    public async Task<string> GetSupplierInvoicesAsync()
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/supplierinvoices";
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
                _logger.LogError("Failed to fetch supplier invoices: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching supplier invoices");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Hämtar en leverantörsfaktura från Fortnox baserat på faktura-ID.")]
    public async Task<string> GetSupplierInvoiceAsync(string invoiceId)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/supplierinvoices/{invoiceId}";
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
                _logger.LogError("Failed to fetch supplier invoice: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching supplier invoice");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Skapar en leverantörsfaktura i Fortnox.")]
    public async Task<string> CreateSupplierInvoiceAsync(SupplierInvoiceRequest invoice)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/supplierinvoices";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, //Don't include nulls.
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter( namingPolicy: null) } //Serialize enums as strings with exact casing.
        };
        var invoicePayload = JsonSerializer.Serialize(invoice, options);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(invoicePayload, Encoding.UTF8, "application/json")
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
                _logger.LogError("Failed to create supplier invoice: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when creating supplier invoice");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Uppdaterar en leverantörsfaktura i Fortnox baserat på faktura-ID.")]
    public async Task<string> UpdateSupplierInvoiceAsync(string invoiceId, SupplierInvoiceRequest invoice)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/supplierinvoices/{invoiceId}";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, //Don't include nulls.
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter( namingPolicy: null) } //Serialize enums as strings with exact casing.
        };
        var invoicePayload = JsonSerializer.Serialize(invoice, options);

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(invoicePayload, Encoding.UTF8, "application/json")
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
                _logger.LogError("Failed to update supplier invoice: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when updating supplier invoice");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Tar bort en leverantörsfaktura från Fortnox baserat på faktura-ID.")]
    public async Task<string> DeleteSupplierInvoiceAsync(string invoiceId)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/supplierinvoices/{invoiceId}";
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
                _logger.LogError("Failed to delete supplier invoice: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when deleting supplier invoice");
            return $"ERROR: {ex.Message}";
        }
    }
}