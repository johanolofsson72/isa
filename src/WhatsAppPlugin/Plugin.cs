using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace WhatsApp;

public class Plugin
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;
    private readonly ILogger<Plugin> _logger;
    private readonly IConfiguration? _configuration;

    /// <summary>
    /// Creates a new instance of WhatsAppPlugin
    /// </summary>
    /// <param name="accountSid">The Twilio Account SID</param>
    /// <param name="authToken">The Twilio Auth Token</param>
    /// <param name="fromNumber">The WhatsApp sender number (in E.164 format)</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(
        string accountSid,
        string authToken,
        string fromNumber,
        ILogger<Plugin>? logger = null
    )
    {
        _accountSid = accountSid ?? throw new ArgumentNullException(nameof(accountSid));
        _authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
        _fromNumber = fromNumber ?? throw new ArgumentNullException(nameof(fromNumber));
        _logger = logger ?? new NullLogger<Plugin>();

        // Initialize Twilio client
        TwilioClient.Init(_accountSid, _authToken);

        _logger.LogInformation(
            "WhatsApp plugin initialized with sender number: {FromNumber}",
            _fromNumber
        );
    }

    /// <summary>
    /// Creates a new instance of WhatsAppPlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing WhatsApp settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration;
        _accountSid =
            configuration["WhatsApp:AccountSid"]
            ?? throw new ArgumentNullException("WhatsApp:AccountSid configuration is missing");

        _authToken =
            configuration["WhatsApp:AuthToken"]
            ?? throw new ArgumentNullException("WhatsApp:AuthToken configuration is missing");

        _fromNumber =
            configuration["WhatsApp:FromNumber"]
            ?? throw new ArgumentNullException("WhatsApp:FromNumber configuration is missing");

        _logger = logger ?? new NullLogger<Plugin>();

        // Initialize Twilio client
        TwilioClient.Init(_accountSid, _authToken);

        _logger.LogInformation(
            "WhatsApp plugin initialized with sender number: {FromNumber}",
            _fromNumber
        );
    }

    /// <summary>
    /// Sends a WhatsApp message using the Twilio API
    /// </summary>
    /// <param name="to">Recipient phone number in E.164 format (e.g., +46701234567)</param>
    /// <param name="message">Message content to send</param>
    /// <returns>A response message indicating success or failure</returns>
    [KernelFunction]
    [Description("Send a WhatsApp message using Twilio with reliable delivery")]
    public async Task<string> SendMessageAsync(
        [Description("Recipient phone number in E.164 format (e.g., +46701234567)")] string to,
        [Description("Message content to send")] string message
    )
    {
        try
        {
            // Parameter validation
            if (string.IsNullOrWhiteSpace(to))
                return "ERROR: Recipient phone number is required";
            if (string.IsNullOrWhiteSpace(message))
                return "ERROR: Message content is required";

            _logger.LogInformation("Preparing to send WhatsApp message to {Recipient}", to);

            // Format phone numbers for WhatsApp
            string formattedTo = FormatWhatsAppNumber(to);
            string formattedFrom = FormatWhatsAppNumber(_fromNumber);

            _logger.LogInformation(
                "Sending WhatsApp message\nFrom: {From}\nTo: {To}\nMessage: {Message}",
                formattedFrom,
                formattedTo,
                message.Length > 100 ? message.Substring(0, 100) + "..." : message
            );

            // Send the message via Twilio WhatsApp API
            var twilioMessage = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber($"whatsapp:{formattedFrom}"),
                to: new PhoneNumber($"whatsapp:{formattedTo}")
            );

            _logger.LogInformation(
                "WhatsApp message sent successfully. SID: {MessageSid}, Status: {Status}",
                twilioMessage.Sid,
                twilioMessage.Status
            );

            return $"Successfully sent WhatsApp message to {to}. Message ID: {twilioMessage.Sid}";
        }
        catch (ApiException ex) when (ex.Message.Contains("Channel"))
        {
            var errorGuidance = GetWhatsAppConfigurationGuidance();
            _logger.LogError(ex, "WhatsApp channel configuration error: {Message}", ex.Message);
            return $"WhatsApp number configuration error: {ex.Message}\n\n{errorGuidance}";
        }
        catch (ApiException ex) when (ex.Code == 21211)
        {
            _logger.LogError(ex, "Invalid phone number format: {Message}", ex.Message);
            return $"Invalid phone number format: {ex.Message}\nPlease use E.164 format (e.g., +46701234567)";
        }
        catch (ApiException ex)
        {
            _logger.LogError(
                ex,
                "Twilio API error when sending WhatsApp message: {Message}",
                ex.Message
            );
            return $"Twilio API error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp message to {Recipient}", to);
            var error = $"Failed to send WhatsApp message: {ex.Message}";
            if (ex.InnerException != null)
            {
                error += $"\nInner error: {ex.InnerException.Message}";
            }
            return error;
        }
    }

    /// <summary>
    /// Formats a phone number for WhatsApp usage (validates E.164 format)
    /// </summary>
    /// <param name="phoneNumber">Phone number to format</param>
    /// <returns>Formatted phone number</returns>
    private string FormatWhatsAppNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number cannot be empty");

        // Remove any whitespace and common separators
        var cleaned = phoneNumber
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "");

        // Ensure the number starts with +
        if (!cleaned.StartsWith("+"))
        {
            throw new ArgumentException("Phone number must be in E.164 format (start with +)");
        }

        // Basic validation for E.164 format (+ followed by 1-15 digits)
        if (cleaned.Length < 2 || cleaned.Length > 16)
        {
            throw new ArgumentException("Phone number must be in valid E.164 format");
        }

        // Check that everything after + is digits
        for (int i = 1; i < cleaned.Length; i++)
        {
            if (!char.IsDigit(cleaned[i]))
            {
                throw new ArgumentException(
                    "Phone number must contain only digits after the + sign"
                );
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Provides detailed guidance for resolving WhatsApp configuration issues
    /// </summary>
    /// <returns>Configuration guidance text</returns>
    private string GetWhatsAppConfigurationGuidance()
    {
        return @"WhatsApp Configuration Help:

1. For Development/Testing:
   - Use Twilio WhatsApp Sandbox: https://www.twilio.com/console/sms/whatsapp/sandbox
   - Set FromNumber to sandbox number (e.g., +14155238886)
   - Recipients must join sandbox by sending the join code to the sandbox number

2. For Production:
   - You need an approved WhatsApp Business Profile through Twilio
   - Verify that FromNumber is authorized for WhatsApp messaging
   - Ensure number is in E.164 format: +[country code][number]
   - Check Twilio Console that number is WhatsApp-enabled

3. Common Issues:
   - Error 63007: Sender not authorized for WhatsApp
   - Error 21211: Invalid phone number format
   - Error 21614: Message body is required

For more help: https://www.twilio.com/docs/whatsapp/tutorial";
    }

    /// <summary>
    /// Gets the current configuration status for debugging
    /// </summary>
    /// <returns>Configuration status information</returns>
    [KernelFunction]
    [Description("Get WhatsApp plugin configuration status for debugging")]
    public Task<string> GetConfigurationStatusAsync()
    {
        var status =
            $@"WhatsApp Plugin Configuration Status:
- Account SID: {(_accountSid?.Length > 10 ? _accountSid.Substring(0, 10) + "..." : "Not set")}
- Auth Token: {(!string.IsNullOrEmpty(_authToken) ? "Set (hidden)" : "Not set")}
- From Number: {_fromNumber ?? "Not set"}
- Twilio Client Initialized: {(TwilioClient.GetRestClient() != null ? "Yes" : "No")}";

        return Task.FromResult(status);
    }
}
