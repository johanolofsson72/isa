using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Agent.Services;

/// <summary>
/// Service for rate limiting notifications to prevent spam
/// Tracks notification frequency and enforces rate limits per workflow and notification type
/// </summary>
public class NotificationRateLimitService
{
    private readonly ILogger<NotificationRateLimitService> _logger;
    private readonly ConcurrentDictionary<string, NotificationTracker> _notificationTrackers;
    private readonly object _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    public NotificationRateLimitService(ILogger<NotificationRateLimitService> logger)
    {
        _logger = logger;
        _notificationTrackers = new ConcurrentDictionary<string, NotificationTracker>();
    }

    /// <summary>
    /// Checks if a notification should be sent based on rate limiting rules
    /// </summary>
    public bool ShouldSendNotification(
        string workflowId, 
        NotificationType notificationType, 
        NotificationSeverity severity,
        int rateLimitMinutes,
        bool enableRateLimiting)
    {
        // Always allow critical notifications for safety
        if (severity == NotificationSeverity.Critical)
        {
            _logger.LogDebug("🚨 Critical notification allowed (bypasses rate limiting): {WorkflowId} - {Type}",
                workflowId, notificationType);
            return true;
        }

        // If rate limiting is disabled, allow all notifications
        if (!enableRateLimiting)
        {
            _logger.LogDebug("📧 Rate limiting disabled, allowing notification: {WorkflowId} - {Type}",
                workflowId, notificationType);
            return true;
        }

        // Cleanup old entries periodically
        PerformPeriodicCleanup();

        var key = GenerateTrackingKey(workflowId, notificationType);
        var tracker = _notificationTrackers.GetOrAdd(key, _ => new NotificationTracker());

        var now = DateTime.UtcNow;
        var rateLimitWindow = TimeSpan.FromMinutes(rateLimitMinutes);

        lock (tracker)
        {
            // Remove old entries outside the rate limit window
            tracker.NotificationTimes.RemoveAll(time => now - time > rateLimitWindow);

            // Check if we're within rate limit
            if (tracker.NotificationTimes.Count > 0)
            {
                var timeSinceLastNotification = now - tracker.NotificationTimes.Last();
                
                if (timeSinceLastNotification < rateLimitWindow)
                {
                    _logger.LogDebug("🚫 Notification rate limited: {WorkflowId} - {Type} (last sent {Minutes:F1} minutes ago)",
                        workflowId, notificationType, timeSinceLastNotification.TotalMinutes);
                    
                    tracker.RateLimitedCount++;
                    return false;
                }
            }

            // Allow the notification and track it
            tracker.NotificationTimes.Add(now);
            tracker.SentCount++;
            
            _logger.LogDebug("✅ Notification allowed: {WorkflowId} - {Type} (sent {Count} times, rate limited {RateLimited} times)",
                workflowId, notificationType, tracker.SentCount, tracker.RateLimitedCount);
            
            return true;
        }
    }

    /// <summary>
    /// Checks if current time is within quiet hours
    /// </summary>
    public bool IsQuietHours(List<string> quietHours)
    {
        if (!quietHours.Any()) return false;

        var now = DateTime.Now;
        var currentTime = now.TimeOfDay;

        foreach (var quietHour in quietHours)
        {
            if (TryParseQuietHour(quietHour, out var startTime, out var endTime))
            {
                // Handle overnight quiet hours (e.g., 22:00-06:00)
                if (startTime > endTime)
                {
                    if (currentTime >= startTime || currentTime <= endTime)
                    {
                        _logger.LogDebug("🔇 Current time {CurrentTime} is within quiet hours: {QuietHour}",
                            currentTime, quietHour);
                        return true;
                    }
                }
                // Handle same-day quiet hours (e.g., 12:00-13:00)
                else if (currentTime >= startTime && currentTime <= endTime)
                {
                    _logger.LogDebug("🔇 Current time {CurrentTime} is within quiet hours: {QuietHour}",
                        currentTime, quietHour);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets notification statistics for monitoring
    /// </summary>
    public NotificationStatistics GetStatistics()
    {
        var stats = new NotificationStatistics
        {
            TotalTrackers = _notificationTrackers.Count,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var kvp in _notificationTrackers)
        {
            var tracker = kvp.Value;
            lock (tracker)
            {
                stats.TotalNotificationsSent += tracker.SentCount;
                stats.TotalNotificationsRateLimited += tracker.RateLimitedCount;
            }
        }

        stats.RateLimitEffectiveness = stats.TotalNotificationsSent > 0 
            ? (double)stats.TotalNotificationsRateLimited / (stats.TotalNotificationsSent + stats.TotalNotificationsRateLimited) * 100
            : 0;

        return stats;
    }

    /// <summary>
    /// Clears all rate limiting data (useful for testing or reset)
    /// </summary>
    public void ClearAllTracking()
    {
        _notificationTrackers.Clear();
        _logger.LogInformation("🧹 Cleared all notification rate limiting data");
    }

    /// <summary>
    /// Generates a unique tracking key for workflow and notification type
    /// </summary>
    private string GenerateTrackingKey(string workflowId, NotificationType notificationType)
    {
        return $"{workflowId}:{notificationType}";
    }

    /// <summary>
    /// Parses quiet hour string (e.g., "22:00-06:00")
    /// </summary>
    private bool TryParseQuietHour(string quietHour, out TimeSpan startTime, out TimeSpan endTime)
    {
        startTime = TimeSpan.Zero;
        endTime = TimeSpan.Zero;

        try
        {
            var parts = quietHour.Split('-');
            if (parts.Length != 2) return false;

            if (TimeSpan.TryParse(parts[0].Trim(), out startTime) &&
                TimeSpan.TryParse(parts[1].Trim(), out endTime))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to parse quiet hour: {QuietHour}", quietHour);
        }

        return false;
    }

    /// <summary>
    /// Performs periodic cleanup of old tracking data
    /// </summary>
    private void PerformPeriodicCleanup()
    {
        var now = DateTime.UtcNow;
        
        // Only cleanup once per hour
        if (now - _lastCleanup < TimeSpan.FromHours(1)) return;

        lock (_cleanupLock)
        {
            // Double-check after acquiring lock
            if (now - _lastCleanup < TimeSpan.FromHours(1)) return;

            var keysToRemove = new List<string>();
            var cleanupThreshold = now.AddHours(-24); // Remove trackers older than 24 hours

            foreach (var kvp in _notificationTrackers)
            {
                var tracker = kvp.Value;
                lock (tracker)
                {
                    // Remove old notification times
                    tracker.NotificationTimes.RemoveAll(time => time < cleanupThreshold);
                    
                    // If no recent notifications, mark for removal
                    if (!tracker.NotificationTimes.Any())
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            // Remove empty trackers
            foreach (var key in keysToRemove)
            {
                _notificationTrackers.TryRemove(key, out _);
            }

            _lastCleanup = now;
            
            if (keysToRemove.Any())
            {
                _logger.LogDebug("🧹 Cleaned up {Count} old notification trackers", keysToRemove.Count);
            }
        }
    }
}

/// <summary>
/// Tracks notification history for rate limiting
/// </summary>
public class NotificationTracker
{
    public List<DateTime> NotificationTimes { get; set; } = new();
    public int SentCount { get; set; }
    public int RateLimitedCount { get; set; }
}

/// <summary>
/// Statistics about notification rate limiting
/// </summary>
public class NotificationStatistics
{
    public int TotalTrackers { get; set; }
    public int TotalNotificationsSent { get; set; }
    public int TotalNotificationsRateLimited { get; set; }
    public double RateLimitEffectiveness { get; set; }
    public DateTime GeneratedAt { get; set; }
}
