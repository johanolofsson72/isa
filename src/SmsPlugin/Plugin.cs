using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace Sms;

/// <summary>
/// /// Configuration for the 46elks SMS service
/// </summary>
public class SmsConfiguration
{
    /// <summary>
    /// API key for 46elks
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API secret for 46elks
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// API URL for 46elks
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.46elks.com/a1/";

    /// <summary>
    /// Default sender for SMS
    /// </summary>
    public string From { get; set; } = string.Empty;
}

/// <summary>
/// Plugin for sending SMS messages using 46elks
/// </summary>
public class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly HttpClient _httpClient;
    private readonly SmsConfiguration _smsConfig;

    /// <summary>
    /// Creates a new instance of the SMS plugin
    /// </summary>
    public Plugin(string apiKey, string apiSecret, string from, ILogger<Plugin>? logger = null)
    {
        _logger = logger ?? NullLogger<Plugin>.Instance;
        _httpClient = new HttpClient();

        _smsConfig = new SmsConfiguration
        {
            ApiKey = apiKey,
            ApiSecret = apiSecret,
            From = from
        };

        // Configure HTTP client with API key and secret for Basic authentication
        var authValue = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_smsConfig.ApiKey}:{_smsConfig.ApiSecret}")
        );
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        _httpClient.BaseAddress = new Uri(_smsConfig.ApiUrl);

        _logger.LogInformation(
            "sms configuration loaded: API URL={ApiUrl}, From={From}",
            _smsConfig.ApiUrl,
            _smsConfig.From
        );
    }

    /// <summary>
    /// Creates a new instance of the SMS plugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing ElksConfiguration settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? NullLogger<Plugin>.Instance;
        _httpClient = new HttpClient();

        _smsConfig = new SmsConfiguration();
        configuration.GetSection("ElksConfiguration").Bind(_smsConfig);

        // Validate required configuration
        if (string.IsNullOrEmpty(_smsConfig.ApiKey))
            throw new ArgumentNullException("ElksConfiguration:ApiKey configuration is missing");
        if (string.IsNullOrEmpty(_smsConfig.ApiSecret))
            throw new ArgumentNullException("ElksConfiguration:ApiSecret configuration is missing");
        if (string.IsNullOrEmpty(_smsConfig.From))
            throw new ArgumentNullException("ElksConfiguration:From configuration is missing");

        // Configure HTTP client with API key and secret for Basic authentication
        var authValue = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_smsConfig.ApiKey}:{_smsConfig.ApiSecret}")
        );
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        _httpClient.BaseAddress = new Uri(_smsConfig.ApiUrl);

        _logger.LogInformation(
            "SMS configuration loaded: API URL={ApiUrl}, From={From}",
            _smsConfig.ApiUrl,
            _smsConfig.From
        );
    }

    /// <summary>
    /// Send an SMS message
    /// </summary>
    /// <param name="to">Recipient phone number in E.164 format (e.g. +46701234567)</param>
    /// <param name="message">The message text to send</param>
    /// <param name="from">Optional sender ID (will use configured default if not specified)</param>
    /// <returns>Status of the SMS send operation</returns>
    [KernelFunction, Description("Send an SMS message to a phone number")]
    public async Task<string> SendSmsAsync(
        [Description("Recipient phone number in E.164 format (e.g. +46701234567)")] string to,
        [Description("The message text to send")] string message,
        [Description("Optional sender ID")] string? from = null
    )
    {
        try
        {
            _logger.LogInformation("=== Starting SMS send operation ===");
            _logger.LogInformation("Validating input parameters...");

            // Input validation with detailed logging
            if (string.IsNullOrEmpty(to))
            {
                _logger.LogError("Recipient phone number is missing");
                return "ERROR: Recipient phone number is required";
            }
            if (!to.StartsWith("+"))
            {
                _logger.LogWarning(
                    "Phone number does not start with '+', might not be E.164 format: {To}",
                    to
                );
            }
            if (string.IsNullOrEmpty(message))
            {
                _logger.LogError("Message text is empty");
                return "ERROR: Message text is required";
            }

            // Use configured sender name if none provided
            var sender = from ?? _smsConfig.From;
            if (string.IsNullOrEmpty(sender))
            {
                _logger.LogError("No sender ID available in configuration or parameter");
                throw new ArgumentException(
                    "Sender (From) is missing in both the call and configuration"
                );
            }

            _logger.LogInformation("SMS Parameters:");
            _logger.LogInformation("To: {To}", to);
            _logger.LogInformation("From: {From}", sender);
            _logger.LogInformation("Message Length: {Length} chars", message.Length);
            _logger.LogInformation("API Base URL: {BaseUrl}", _httpClient.BaseAddress);
            _logger.LogInformation(
                "Using API Key: {KeyPrefix}...",
                _smsConfig.ApiKey.Substring(0, Math.Min(4, _smsConfig.ApiKey.Length))
            );

            // Create and log payload for 46elks API
            _logger.LogInformation("Creating API request payload...");
            var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    { "from", sender },
                    { "to", to },
                    { "message", message }
                }
            );

            var startTime = DateTime.Now;
            _logger.LogInformation("Sending request to 46elks API at {Time}...", startTime);

            // Send request to 46elks API with timeout handling
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _httpClient.PostAsync("sms", content, cts.Token);

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Read and log detailed response
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Response received in {Duration}ms", duration.TotalMilliseconds);
            _logger.LogInformation(
                "Response Status: {Status} ({StatusCode})",
                response.ReasonPhrase,
                (int)response.StatusCode
            );
            _logger.LogInformation(
                "Response Headers: {Headers}",
                string.Join(
                    ", ",
                    response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")
                )
            );
            _logger.LogInformation("Response Content: {Content}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Request successful, parsing response...");
                // Try to parse response as JSON to get message ID
                try
                {
                    var jsonResponse = System.Text.Json.JsonDocument.Parse(responseContent);
                    _logger.LogInformation(
                        "Raw JSON response: {Json}",
                        jsonResponse.RootElement.ToString()
                    );

                    var messageId = jsonResponse.RootElement.GetProperty("id").GetString();
                    var status = jsonResponse.RootElement.TryGetProperty(
                        "status",
                        out var statusElement
                    )
                        ? statusElement.GetString()
                        : "unknown";

                    _logger.LogInformation("=== SMS sent successfully ===");
                    _logger.LogInformation("Message ID: {MessageId}", messageId);
                    _logger.LogInformation("Status: {Status}", status);
                    _logger.LogInformation("Recipient: {To}", to);
                    _logger.LogInformation("Time: {Time}", DateTime.Now);
                    _logger.LogInformation(
                        "Request Duration: {Duration}ms",
                        duration.TotalMilliseconds
                    );

                    return $"SMS sent successfully (ID: {messageId}, Status: {status})";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not parse response from 46elks API. Raw response: {Response}",
                        responseContent
                    );
                    return "SMS sent successfully (no confirmation ID available)";
                }
            }
            else
            {
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("46elks API Error Details:");
                errorBuilder.AppendLine(
                    $"Status Code: {(int)response.StatusCode} ({response.StatusCode})"
                );
                errorBuilder.AppendLine($"Response Content: {responseContent}");
                errorBuilder.AppendLine($"Request Duration: {duration.TotalMilliseconds}ms");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    errorBuilder.AppendLine(
                        "Authentication failed - please verify API credentials"
                    );
                }

                var errorDetails = errorBuilder.ToString();
                _logger.LogError("SMS send failed:\n{ErrorDetails}", errorDetails);
                return $"Error sending SMS: {response.StatusCode} - {responseContent}";
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("SMS send operation timed out after 30 seconds");
            return "Error sending SMS: Operation timed out";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP request failed while sending SMS to {To}. Status: {Status}",
                to,
                ex.StatusCode
            );
            return $"Error sending SMS: Network error - {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error sending SMS to {To}. Error type: {Type}",
                to,
                ex.GetType().Name
            );
            return $"Error sending SMS: {ex.Message}";
        }
    }
}
