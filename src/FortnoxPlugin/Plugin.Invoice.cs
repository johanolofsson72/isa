using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fortnox;

public partial class Plugin
{
    [KernelFunction]
    [Description("Bokför en faktura i Fortnox. Returnerar den bokförda fakturan.")]
    public async Task<InvoiceResponse> BookkeepInvoiceAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/bookkeep";
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when bookkeeping invoice.");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };
        }
    }

    [KernelFunction]
    [Description("Makulerar en faktura i Fortnox. Returnerar den makulerade fakturan.")]
    public async Task<InvoiceResponse> CancelInvoiceAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/cancel";
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when canceling invoice.");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };
        }
    }

    [KernelFunction]
    [Description("Skapar en faktura i Fortnox. Returnerar den skapade fakturan.")]
    public async Task<InvoiceResponse> CreateInvoiceAsync(
    [Description("Faktura")] InvoiceRequest invoiceRequest)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new NullableStringEnumConverterFactory(),
                new NullableDateTimeConverter()
            }
        };

        var invoicePayload = JsonSerializer.Serialize(invoiceRequest, options);
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
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize created invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize created invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse created invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse created invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to create invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when creating invoice");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };

        }
    }

    [KernelFunction]
    [Description("Krediterar en faktura i Fortnox. Returnerar information om fakturan, fältet CreditInvoiceReference innehåller fakturanumret på den skapade kreditfakturan.")]
    public async Task<InvoiceResponse> CreditInvoiceAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/credit";
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when crediting invoice.");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };
        }
    }

    [KernelFunction]
    [Description("Hämtar en faktura från Fortnox baserat på fakturanummer.")]
    public async Task<InvoiceResponse> GetInvoiceAsync(
        [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(), 
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching invoice.");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };
        }
    }

    [KernelFunction]
    [Description("Förhandsgranskning. Visar fakturan som PDF, fakturan markeras inte som skickad.")]
    public async Task<string> PreviewInvoiceAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return "Errror: " + accessToken;
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/preview";
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
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when setting invoice as sent.");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Hämtar fakturan som PDF, fakturan markeras som skickad.")]
    public async Task<string> PrintInvoiceAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return "Errror: " + accessToken;
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/print";
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
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when setting invoice as sent.");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Påminnelsefaktura, hämtar fakturans PDF som en påminnelsefaktura.")]
    public async Task<string> PrintReminderInvoiceAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return "Errror: " + accessToken;
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/printreminder";
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
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return $"ERROR: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when setting invoice as sent.");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Söker efter fakturor i Fortnox")]
    public async Task<InvoiceListResponse> SearchInvoicesAsync(
        [Description("Filter, val: ")] InvoiceFilter? filter,
        [Description("Fakturanr")] string? documentNumber,
        [Description("Kundnr")] string? customerNumber,
        [Description("Kundens namn")] string? customerName,
        [Description("Från datum")]  DateTime? fromDate,
        [Description("Till datum")] DateTime? toDate)
    {

        Dictionary<string, string> queryParams = new Dictionary<string, string>();
 
        if(filter.HasValue)
        {
            switch (filter)
            {
                case InvoiceFilter.All:
                    break;
                case InvoiceFilter.Cancelled:
                    queryParams["filter"] = "cancelled";
                    break;
                case InvoiceFilter.Fullypaid:
                    queryParams["filter"] = "fullypaid";    
                    break;
                case InvoiceFilter.Unpaid:
                    queryParams["filter"] = "unpaid";
                    break;
                case InvoiceFilter.Unpaidoverdue:
                    queryParams["filter"] = "unpaidoverdue";
                    break;
                default:
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(documentNumber))
            queryParams["documentnumber"] = documentNumber;
        if (!string.IsNullOrWhiteSpace(customerNumber))
            queryParams["customernumber"] = customerNumber;
        if (!string.IsNullOrWhiteSpace(customerName))
            queryParams["customername"] = customerName;
        if (fromDate.HasValue && fromDate != DateTime.MinValue)
            queryParams["fromdate"] = fromDate.Value.ToString("yyyy-MM-dd");
        if (toDate.HasValue && fromDate != DateTime.MinValue)
            queryParams["todate"] = toDate.Value.ToString("yyyy-MM-dd");

        string queryString = string.Join("&", queryParams
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceListResponse
            {
                InvoiceListItems = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        string url = $"{_apiUrl}/invoices?{queryString}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoicesNode = rootNode?["Invoices"];

                if (invoicesNode is not null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    IReadOnlyList<InvoiceListItem>? invoiceListItems = invoicesNode.Deserialize<IReadOnlyList<InvoiceListItem>>(options);
                    if (invoiceListItems != null)
                    {
                        return new InvoiceListResponse
                        {
                            InvoiceListItems = invoiceListItems,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize invoice list: {Response}", responseContent);
                        return new InvoiceListResponse
                        {
                            InvoiceListItems = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize invoice list.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse invoicelist json: {Response}", responseContent);
                    return new InvoiceListResponse
                    {
                        InvoiceListItems = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse invoicelist json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to search invoices: {Response}", responseContent);
                return new InvoiceListResponse
                {
                    InvoiceListItems = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when searching invoices");
            return new InvoiceListResponse
            {
                InvoiceListItems = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };
        }
    }

    [KernelFunction]
    [Description("Skickar en faktura via email. Uppgifterna på fältet EmailInformation i fakturan används för att skapa emailet, som t.ex. mottagare, avsändare, ämne och text")]
    public async Task<InvoiceResponse> SendInvoiceAsEmailAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/email";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when sending invoice by email.");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };
        }
    }

    [KernelFunction]
    [Description("Markera en faktura som skickad i Fortnox. Returnerar den uppdaterade fakturan.")]
    public async Task<InvoiceResponse> SetInvoiceAsSentAsync(
    [Description("Fakturanummer")] string documentNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}/externalprint";
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to fetch invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when setting invoice as sent.");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };
        }
    }

    [KernelFunction]
    [Description("Uppdaterar en faktura i Fortnox. Returnerar den uppdaterade fakturan.")]
    public async Task<InvoiceResponse> UpdateInvoiceAsync(
    [Description("Fakturanummer")] string documentNumber,
    [Description("Faktura")] InvoiceRequest invoiceRequest)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
        {
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = accessToken,
            };
        }

        var url = $"{_apiUrl}/invoices/{documentNumber}";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new NullableStringEnumConverterFactory(),
                new NullableDateTimeConverter()
            }
        };

        var invoicePayload = JsonSerializer.Serialize(invoiceRequest, options);
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
                JsonNode? rootNode = JsonNode.Parse(responseContent);
                JsonNode? invoiceNode = rootNode?["Invoice"];

                if (invoiceNode is not null)
                {
                    options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
                            new NullableStringEnumConverterFactory(),
                            new NullableDateTimeConverter()
                        }
                    };

                    Invoice? invoice = invoiceNode.Deserialize<Invoice>(options);

                    if (invoice != null)
                    {
                        return new InvoiceResponse
                        {
                            Invoice = invoice,
                            Success = true,
                            ErrorMessage = string.Empty,
                        };
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize updated invoice: {Response}", responseContent);
                        return new InvoiceResponse
                        {
                            Invoice = null,
                            Success = false,
                            ErrorMessage = "ERROR: Failed to deserialize updated invoice.",
                        };
                    }
                }
                else
                {
                    _logger.LogError("Failed to parse updated invoice json: {Response}", responseContent);
                    return new InvoiceResponse
                    {
                        Invoice = null,
                        Success = false,
                        ErrorMessage = "ERROR: Failed to parse updated invoice json.",
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to update invoice: {Response}", responseContent);
                return new InvoiceResponse
                {
                    Invoice = null,
                    Success = false,
                    ErrorMessage = $"ERROR: {responseContent}",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when updating invoice");
            return new InvoiceResponse
            {
                Invoice = null,
                Success = false,
                ErrorMessage = $"ERROR: {ex.Message}",
            };

        }
    }
}
