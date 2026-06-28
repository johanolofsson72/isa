using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Slack.Webhooks;

namespace Slack;

/// <summary>
/// Slack plugin för att skicka meddelanden via Slack API
/// </summary>
public class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly string _botToken;
    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a new instance of SlackPlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing Slack settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? NullLogger<Plugin>.Instance;

        _botToken =
            configuration["Slack:BotToken"]
            ?? throw new ArgumentNullException("Slack:BotToken configuration is missing");

        _webhookUrl =
            configuration["Slack:WebhookUrl"]
            ?? throw new ArgumentNullException("Slack:WebhookUrl configuration is missing");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_botToken}");

        _logger.LogInformation("Slack plugin initialized");
    }
    /// <summary>
    /// Skickar ett meddelande till en Slack-kanal
    /// </summary>
    /// <param name="channel">Kanalen att skicka till (t.ex. #general)</param>
    /// <param name="message">Meddelandet att skicka</param>
    /// <returns>Resultat av operationen</returns>
    [KernelFunction]
    [Description("Skickar ett meddelande till en Slack-kanal")]
    public async Task<string> SendMessage(
        [Description("Kanalen att skicka till (t.ex. #general)")] string channel,
        [Description("Meddelandet att skicka")] string message
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return "ERROR: Kanal saknas";
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return "ERROR: Meddelande saknas";
            }

            _logger.LogInformation("Skickar Slack-meddelande till kanal {Channel}", channel);

            var payload = new
            {
                channel = channel,
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Slack-meddelande skickat framgångsrikt");
                return "SUCCESS: Meddelandet skickades framgångsrikt till Slack";
            }
            else
            {
                _logger.LogError("Fel vid skickande av Slack-meddelande: {Response}", responseContent);
                return $"ERROR: Kunde inte skicka meddelandet: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid skickande av Slack-meddelande");
            return $"ERROR: Fel vid skickande av Slack-meddelande: {ex.Message}";
        }
    }
    /// <summary>
    /// Skickar ett direktmeddelande till en användare
    /// </summary>
    /// <param name="user">Användarnamn (t.ex. @demo.user eller demo.user)</param>
    /// <param name="message">Meddelandet att skicka</param>
    /// <returns>Resultat av operationen</returns>
    [KernelFunction]
    [Description("Skickar ett direktmeddelande till en Slack-användare")]
    public async Task<string> SendDirectMessage(
        [Description("Användarnamn (t.ex. @demo.user eller demo.user)")] string user,
        [Description("Meddelandet att skicka")] string message
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(user))
            {
                return "ERROR: Användarnamn saknas";
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return "ERROR: Meddelande saknas";
            }

            // Normalisera användarnamn för DM och visning
            var baseUser = user?.Trim() ?? string.Empty;
            var stripped = baseUser.TrimStart('@');
            string displayUser;
            if (baseUser.StartsWith("@@"))
            {
                displayUser = "@" + stripped; // collapse till en @ för visning
            }
            else if (baseUser.StartsWith("@"))
            {
                displayUser = stripped; // ta bort enkel @ för visning
            }
            else
            {
                displayUser = baseUser; // redan utan @
            }

            // Slack DM target börjar alltid med @ + användarnamn (ej med två @)
            string dmTarget = "@" + stripped;

            _logger.LogInformation("Skickar direktmeddelande till användare {User}", displayUser);

            var payload = new
            {
                channel = dmTarget,
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Direktmeddelande skickat framgångsrikt till {User}", displayUser);
                return $"SUCCESS: Direktmeddelande skickat till {displayUser}";
            }
            else
            {
                _logger.LogError("Fel vid skickande av direktmeddelande: {Response}", responseContent);
                return $"ERROR: Kunde inte skicka direktmeddelande till {displayUser}: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid skickande av direktmeddelande");
            return $"ERROR: Fel vid skickande av direktmeddelande: {ex.Message}";
        }
    }





    /// <summary>
    /// Skickar ett meddelande via webhook
    /// </summary>
    /// <param name="message">Meddelandet att skicka</param>
    /// <returns>Resultat av operationen</returns>
    [KernelFunction]
    [Description("Skickar ett meddelande via Slack webhook")]
    public async Task<string> SendWebhookMessage(
        [Description("Meddelandet att skicka")] string message
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "ERROR: Meddelande saknas";
            }

            _logger.LogInformation("Skickar Slack webhook-meddelande");

            var slackClient = new Slack.Webhooks.SlackClient(_webhookUrl);
            var slackMessage = new SlackMessage
            {
                Text = message,
                Username = "Agent",
                IconEmoji = ":robot_face:"
            };

            var result = await slackClient.PostAsync(slackMessage);

            if (result)
            {
                _logger.LogInformation("Slack webhook-meddelande skickat framgångsrikt");
                return "SUCCESS: Meddelandet skickades framgångsrikt via webhook";
            }
            else
            {
                _logger.LogError("Slack webhook misslyckades");
                return "ERROR: Fel vid skickande via webhook";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid skickande av Slack webhook-meddelande");
            return $"ERROR: Fel vid skickande av webhook-meddelande: {ex.Message}";
        }
    }
}
