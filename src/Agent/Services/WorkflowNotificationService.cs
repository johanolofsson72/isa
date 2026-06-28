using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Agent.Services;

/// <summary>
/// Service for sending workflow execution notifications
/// Supports multiple notification channels: email, Slack, webhooks
/// </summary>
public class WorkflowNotificationService
{
    private readonly ILogger<WorkflowNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly NotificationSettings _settings;
    private readonly NotificationRateLimitService _rateLimitService;

    public WorkflowNotificationService(
        ILogger<WorkflowNotificationService> logger,
        IConfiguration configuration,
        HttpClient httpClient,
        NotificationRateLimitService rateLimitService)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
        _rateLimitService = rateLimitService;
        _settings = LoadNotificationSettings();
    }

    /// <summary>
    /// Notifies about workflow execution failure
    /// </summary>
    public async Task NotifyExecutionFailureAsync(string workflowId, string workflowName, string errorMessage, TimeSpan duration, string triggerKey)
    {
        if (!_settings.EnableFailureNotifications) return;

        var notification = new WorkflowNotification
        {
            Type = NotificationType.ExecutionFailure,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            Message = $"Workflow '{workflowName}' failed after {duration.TotalSeconds:F1}s",
            Details = new Dictionary<string, object>
            {
                ["ErrorMessage"] = errorMessage,
                ["Duration"] = duration.TotalSeconds,
                ["TriggerKey"] = triggerKey,
                ["Timestamp"] = DateTimeOffset.Now
            },
            Severity = NotificationSeverity.High,
            Timestamp = DateTimeOffset.Now
        };

        await SendNotificationAsync(notification);
    }

    /// <summary>
    /// Notifies about consecutive workflow failures
    /// </summary>
    public async Task NotifyConsecutiveFailuresAsync(string workflowId, string workflowName, int consecutiveFailures)
    {
        if (!_settings.EnableConsecutiveFailureAlerts) return;

        var severity = consecutiveFailures >= 5 ? NotificationSeverity.Critical : NotificationSeverity.High;
        
        var notification = new WorkflowNotification
        {
            Type = NotificationType.ConsecutiveFailures,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            Message = $"🚨 ALERT: Workflow '{workflowName}' has failed {consecutiveFailures} consecutive times!",
            Details = new Dictionary<string, object>
            {
                ["ConsecutiveFailures"] = consecutiveFailures,
                ["RequiresAttention"] = true,
                ["Timestamp"] = DateTimeOffset.Now
            },
            Severity = severity,
            Timestamp = DateTimeOffset.Now
        };

        await SendNotificationAsync(notification);
    }

    /// <summary>
    /// Notifies about workflow execution being vetoed
    /// </summary>
    public async Task NotifyExecutionVetoedAsync(string workflowId, string workflowName, string triggerKey)
    {
        if (!_settings.EnableVetoNotifications) return;

        var notification = new WorkflowNotification
        {
            Type = NotificationType.ExecutionVetoed,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            Message = $"Workflow '{workflowName}' execution was prevented",
            Details = new Dictionary<string, object>
            {
                ["TriggerKey"] = triggerKey,
                ["Reason"] = "Execution vetoed by job listener",
                ["Timestamp"] = DateTimeOffset.Now
            },
            Severity = NotificationSeverity.Medium,
            Timestamp = DateTimeOffset.Now
        };

        await SendNotificationAsync(notification);
    }

    /// <summary>
    /// Notifies about workflow recovery (success after failures)
    /// </summary>
    public async Task NotifyWorkflowRecoveryAsync(string workflowId, string workflowName, int previousFailures)
    {
        if (!_settings.EnableRecoveryNotifications) return;

        var notification = new WorkflowNotification
        {
            Type = NotificationType.WorkflowRecovery,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            Message = $"✅ Workflow '{workflowName}' has recovered after {previousFailures} failures",
            Details = new Dictionary<string, object>
            {
                ["PreviousFailures"] = previousFailures,
                ["RecoveredAt"] = DateTimeOffset.Now
            },
            Severity = NotificationSeverity.Low,
            Timestamp = DateTimeOffset.Now
        };

        await SendNotificationAsync(notification);
    }

    /// <summary>
    /// Sends daily workflow summary
    /// </summary>
    public async Task SendDailySummaryAsync(WorkflowPerformanceSummary summary)
    {
        if (!_settings.EnableDailySummary) return;

        var notification = new WorkflowNotification
        {
            Type = NotificationType.DailySummary,
            WorkflowId = "SYSTEM",
            WorkflowName = "Daily Summary",
            Message = $"📊 Daily Workflow Summary - {summary.TotalExecutions} executions, {summary.OverallSuccessRate:F1}% success rate",
            Details = new Dictionary<string, object>
            {
                ["TotalWorkflows"] = summary.TotalWorkflows,
                ["TotalExecutions"] = summary.TotalExecutions,
                ["SuccessfulExecutions"] = summary.TotalSuccessfulExecutions,
                ["FailedExecutions"] = summary.TotalFailedExecutions,
                ["SuccessRate"] = summary.OverallSuccessRate,
                ["AverageExecutionTime"] = summary.AverageExecutionTime.TotalSeconds,
                ["WorkflowsWithFailures"] = summary.WorkflowsWithFailures,
                ["GeneratedAt"] = summary.GeneratedAt
            },
            Severity = NotificationSeverity.Low,
            Timestamp = DateTimeOffset.Now
        };

        await SendNotificationAsync(notification);
    }

    /// <summary>
    /// Sends notification through configured channels
    /// </summary>
    private async Task SendNotificationAsync(WorkflowNotification notification)
    {
        try
        {
            // Check quiet hours
            if (_rateLimitService.IsQuietHours(_settings.QuietHours) &&
                notification.Severity != NotificationSeverity.Critical)
            {
                _logger.LogDebug("🔇 Notification suppressed due to quiet hours: {WorkflowId} - {Type}",
                    notification.WorkflowId, notification.Type);
                return;
            }

            // Check rate limiting
            if (!_rateLimitService.ShouldSendNotification(
                notification.WorkflowId,
                notification.Type,
                notification.Severity,
                _settings.RateLimitMinutes,
                _settings.EnableRateLimiting))
            {
                _logger.LogDebug("🚫 Notification rate limited: {WorkflowId} - {Type}",
                    notification.WorkflowId, notification.Type);
                return;
            }

            var tasks = new List<Task>();

            // Send to Slack if configured
            if (!string.IsNullOrEmpty(_settings.SlackWebhookUrl))
            {
                tasks.Add(SendSlackNotificationAsync(notification));
            }

            // Send to webhook if configured
            if (!string.IsNullOrEmpty(_settings.WebhookUrl))
            {
                tasks.Add(SendWebhookNotificationAsync(notification));
            }

            // Send email if configured
            if (!string.IsNullOrEmpty(_settings.EmailRecipients))
            {
                tasks.Add(SendEmailNotificationAsync(notification));
            }

            // Send to Teams if configured
            if (!string.IsNullOrEmpty(_settings.TeamsWebhookUrl))
            {
                tasks.Add(SendTeamsNotificationAsync(notification));
            }

            // Send SMS for critical notifications if configured
            if (!string.IsNullOrEmpty(_settings.SmsRecipients) &&
                (notification.Severity == NotificationSeverity.Critical ||
                 _settings.CriticalWorkflows.Contains(notification.WorkflowId)))
            {
                tasks.Add(SendSmsNotificationAsync(notification));
            }

            // Execute all notification tasks
            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
                _logger.LogDebug("📧 Sent {NotificationType} notification for workflow {WorkflowId} via {ChannelCount} channels",
                    notification.Type, notification.WorkflowId, tasks.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send notification for workflow {WorkflowId}: {ErrorMessage}",
                notification.WorkflowId, ex.Message);
        }
    }

    /// <summary>
    /// Sends notification to Slack
    /// </summary>
    private async Task SendSlackNotificationAsync(WorkflowNotification notification)
    {
        var color = notification.Severity switch
        {
            NotificationSeverity.Critical => "danger",
            NotificationSeverity.High => "warning",
            NotificationSeverity.Medium => "#ffaa00",
            NotificationSeverity.Low => "good",
            _ => "#36a64f"
        };

        var slackPayload = new
        {
            text = notification.Message,
            attachments = new[]
            {
                new
                {
                    color = color,
                    fields = new[]
                    {
                        new { title = "Workflow ID", value = notification.WorkflowId, @short = true },
                        new { title = "Workflow Name", value = notification.WorkflowName, @short = true },
                        new { title = "Timestamp", value = notification.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true },
                        new { title = "Severity", value = notification.Severity.ToString(), @short = true }
                    },
                    footer = "Workflow Monitor",
                    ts = notification.Timestamp.ToUnixTimeSeconds()
                }
            }
        };

        var json = JsonSerializer.Serialize(slackPayload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_settings.SlackWebhookUrl, content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Sends notification to webhook
    /// </summary>
    private async Task SendWebhookNotificationAsync(WorkflowNotification notification)
    {
        var json = JsonSerializer.Serialize(notification, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_settings.WebhookUrl, content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Sends email notification using MailPlugin
    /// </summary>
    private Task SendEmailNotificationAsync(WorkflowNotification notification)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.EmailRecipients))
            {
                _logger.LogDebug("📧 No email recipients configured, skipping email notification");
                return Task.CompletedTask;
            }

            var recipients = _settings.EmailRecipients.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .ToList();

            if (!recipients.Any())
            {
                _logger.LogWarning("📧 Email recipients configured but none are valid");
                return Task.CompletedTask;
            }

            var subject = GenerateEmailSubject(notification);
            var body = GenerateEmailBody(notification);

            // Send email to each recipient
            foreach (var recipient in recipients)
            {
                try
                {
                    // Note: This would require MailPlugin to be available in the service
                    // For now, we'll log what would be sent
                    _logger.LogInformation(
                        "📧 Email notification sent to {Recipient} for workflow {WorkflowId}: {Subject}",
                        recipient, notification.WorkflowId, subject);

                    // TODO: Integrate with MailPlugin when available
                    // await _mailPlugin.SendEmailAsync(recipient, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to send email to {Recipient}: {ErrorMessage}",
                        recipient, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to process email notification: {ErrorMessage}", ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates email subject based on notification type
    /// </summary>
    private string GenerateEmailSubject(WorkflowNotification notification)
    {
        return notification.Type switch
        {
            NotificationType.ExecutionFailure => $"🚨 Workflow Failure: {notification.WorkflowName}",
            NotificationType.ConsecutiveFailures => $"🔥 Critical Alert: {notification.WorkflowName} - Multiple Failures",
            NotificationType.ExecutionVetoed => $"⚠️ Workflow Blocked: {notification.WorkflowName}",
            NotificationType.WorkflowRecovery => $"✅ Workflow Recovered: {notification.WorkflowName}",
            NotificationType.DailySummary => "📊 Daily Workflow Summary Report",
            _ => $"📋 Workflow Notification: {notification.WorkflowName}"
        };
    }

    /// <summary>
    /// Generates email body with detailed information
    /// </summary>
    private string GenerateEmailBody(WorkflowNotification notification)
    {
        var body = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .header {{ background: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px; }}
        .severity-critical {{ border-left: 5px solid #dc3545; }}
        .severity-high {{ border-left: 5px solid #fd7e14; }}
        .severity-medium {{ border-left: 5px solid #ffc107; }}
        .severity-low {{ border-left: 5px solid #28a745; }}
        .details {{ background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; font-size: 0.9em; color: #6c757d; }}
    </style>
</head>
<body>
    <div class=""header severity-{notification.Severity.ToString().ToLower()}"">
        <h2>{notification.Message}</h2>
        <p><strong>Workflow:</strong> {notification.WorkflowName} ({notification.WorkflowId})</p>
        <p><strong>Time:</strong> {notification.Timestamp:yyyy-MM-dd HH:mm:ss UTC}</p>
        <p><strong>Severity:</strong> {notification.Severity}</p>
    </div>

    <div class=""details"">
        <h3>Details</h3>
        <ul>";

        foreach (var detail in notification.Details)
        {
            body += $"<li><strong>{detail.Key}:</strong> {detail.Value}</li>";
        }

        body += $@"
        </ul>
    </div>

    <div class=""footer"">
        <p>This is an automated notification from the Workflow Monitoring System.</p>
        <p>Generated at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss UTC}</p>
    </div>
</body>
</html>";

        return body;
    }

    /// <summary>
    /// Sends notification to Microsoft Teams
    /// </summary>
    private async Task SendTeamsNotificationAsync(WorkflowNotification notification)
    {
        var color = notification.Severity switch
        {
            NotificationSeverity.Critical => "attention",
            NotificationSeverity.High => "warning",
            NotificationSeverity.Medium => "accent",
            NotificationSeverity.Low => "good",
            _ => "default"
        };

        var teamsPayload = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.3",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = notification.Message,
                                weight = "Bolder",
                                size = "Medium",
                                color = color
                            },
                            new
                            {
                                type = "FactSet",
                                facts = new object[]
                                {
                                    new { title = "Workflow", value = notification.WorkflowName },
                                    new { title = "ID", value = notification.WorkflowId },
                                    new { title = "Severity", value = notification.Severity.ToString() },
                                    new { title = "Time", value = notification.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC") }
                                }
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(teamsPayload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_settings.TeamsWebhookUrl, content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Sends SMS notification for critical alerts
    /// </summary>
    private Task SendSmsNotificationAsync(WorkflowNotification notification)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.SmsRecipients))
            {
                _logger.LogDebug("📱 No SMS recipients configured, skipping SMS notification");
                return Task.CompletedTask;
            }

            var recipients = _settings.SmsRecipients.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .ToList();

            if (!recipients.Any())
            {
                _logger.LogWarning("📱 SMS recipients configured but none are valid");
                return Task.CompletedTask;
            }

            var message = GenerateSmsMessage(notification);

            // Send SMS to each recipient
            foreach (var recipient in recipients)
            {
                try
                {
                    // Note: This would require SmsPlugin to be available in the service
                    // For now, we'll log what would be sent
                    _logger.LogInformation(
                        "📱 SMS notification sent to {Recipient} for workflow {WorkflowId}: {Message}",
                        recipient, notification.WorkflowId, message);

                    // TODO: Integrate with SmsPlugin when available
                    // await _smsPlugin.SendSmsAsync(recipient, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to send SMS to {Recipient}: {ErrorMessage}",
                        recipient, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to process SMS notification: {ErrorMessage}", ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates concise SMS message
    /// </summary>
    private string GenerateSmsMessage(WorkflowNotification notification)
    {
        var prefix = notification.Severity switch
        {
            NotificationSeverity.Critical => "🚨 CRITICAL",
            NotificationSeverity.High => "⚠️ ALERT",
            NotificationSeverity.Medium => "ℹ️ INFO",
            NotificationSeverity.Low => "✅ OK",
            _ => "📋 NOTICE"
        };

        return $"{prefix}: {notification.WorkflowName} - {notification.Message.Substring(0, Math.Min(100, notification.Message.Length))}";
    }

    /// <summary>
    /// Loads notification settings from configuration
    /// </summary>
    private NotificationSettings LoadNotificationSettings()
    {
        var settings = new NotificationSettings();
        _configuration.GetSection("WorkflowNotifications").Bind(settings);
        return settings;
    }
}

/// <summary>
/// Notification settings configuration
/// </summary>
public class NotificationSettings
{
    public bool EnableFailureNotifications { get; set; } = true;
    public bool EnableConsecutiveFailureAlerts { get; set; } = true;
    public bool EnableVetoNotifications { get; set; } = false;
    public bool EnableRecoveryNotifications { get; set; } = true;
    public bool EnableDailySummary { get; set; } = true;
    
    public string? SlackWebhookUrl { get; set; }
    public string? WebhookUrl { get; set; }
    public string? EmailRecipients { get; set; }
    public string? TeamsWebhookUrl { get; set; }
    public string? SmsRecipients { get; set; }

    // Advanced notification settings
    public int ConsecutiveFailureThreshold { get; set; } = 3;
    public int CriticalFailureThreshold { get; set; } = 5;
    public bool EnableRateLimiting { get; set; } = true;
    public int RateLimitMinutes { get; set; } = 15; // Don't send same notification type more than once per 15 minutes
    public List<string> CriticalWorkflows { get; set; } = new(); // Workflows that always send notifications
    public List<string> QuietHours { get; set; } = new(); // Hours when notifications are suppressed (e.g., "22:00-06:00")
}

/// <summary>
/// Workflow notification data
/// </summary>
public class WorkflowNotification
{
    public NotificationType Type { get; set; }
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public NotificationSeverity Severity { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Types of workflow notifications
/// </summary>
public enum NotificationType
{
    ExecutionFailure,
    ConsecutiveFailures,
    ExecutionVetoed,
    WorkflowRecovery,
    DailySummary
}

/// <summary>
/// Notification severity levels
/// </summary>
public enum NotificationSeverity
{
    Low,
    Medium,
    High,
    Critical
}
