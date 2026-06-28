using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Agent.Data;
using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Service for logging and auditing system activities
/// </summary>
public interface IAuditService
{
    Task LogAsync(string activityType, string source, string action, string description, 
        string? userId = null, string? sessionId = null, object? data = null, 
        string severity = "Info", bool success = true, string? errorMessage = null, 
        int? durationMs = null, string? ipAddress = null, string? userAgent = null);
    
    Task LogChatActivityAsync(string sessionId, string action, string message, string? userId = null, 
        object? metadata = null, bool success = true, string? errorMessage = null);
    
    Task LogWorkflowActivityAsync(string workflowName, string action, string description, 
        object? data = null, bool success = true, string? errorMessage = null, int? durationMs = null);
    
    Task LogApiActivityAsync(string apiName, string action, string description, 
        object? requestData = null, object? responseData = null, bool success = true, 
        string? errorMessage = null, int? durationMs = null);
    
    Task<List<AuditLog>> GetRecentLogsAsync(int count = 100, string? activityType = null, string? source = null);
    Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime fromDate, DateTime toDate, string? activityType = null);
    Task<int> GetLogCountAsync(string? activityType = null, DateTime? fromDate = null);
    Task CleanupOldLogsAsync(TimeSpan maxAge);
    Task ClearAllLogsAsync();
}

/// <summary>
/// Implementation of audit service using SQLite
/// </summary>
public class AuditService : IAuditService
{
    private readonly AgentDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AgentDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(string activityType, string source, string action, string description, 
        string? userId = null, string? sessionId = null, object? data = null, 
        string severity = "Info", bool success = true, string? errorMessage = null, 
        int? durationMs = null, string? ipAddress = null, string? userAgent = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = activityType,
                SessionId = sessionId,
                UserInput = action,
                AIResponse = description,
                Status = success ? "Success" : "Error",
                ErrorMessage = errorMessage,
                DurationMs = durationMs,
                Metadata = JsonSerializer.Serialize(new { 
                    Source = source, 
                    UserId = userId, 
                    Severity = severity, 
                    Data = data,
                    IpAddress = ipAddress,
                    UserAgent = userAgent 
                })
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Audit log created: {ActivityType} - {Action} - {Description}", 
                activityType, action, description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit log");
        }
    }

    public async Task LogChatActivityAsync(string sessionId, string action, string message, string? userId = null, 
        object? metadata = null, bool success = true, string? errorMessage = null)
    {
        var data = new
        {
            Message = message,
            Metadata = metadata
        };

        await LogAsync("Chat", "WebChat", action, $"Chat {action.ToLower()}: {message[..Math.Min(message.Length, 100)]}", 
            userId, sessionId, data, success ? "Info" : "Error", success, errorMessage);
    }

    public async Task LogWorkflowActivityAsync(string workflowName, string action, string description, 
        object? data = null, bool success = true, string? errorMessage = null, int? durationMs = null)
    {
        await LogAsync("Workflow", "WorkflowService", action, description, 
            null, null, data, success ? "Info" : "Error", success, errorMessage, durationMs);
    }

    public async Task LogApiActivityAsync(string apiName, string action, string description, 
        object? requestData = null, object? responseData = null, bool success = true, 
        string? errorMessage = null, int? durationMs = null)
    {
        var data = new
        {
            API = apiName,
            Request = requestData,
            Response = responseData
        };

        await LogAsync("API", apiName, action, description, 
            null, null, data, success ? "Info" : "Error", success, errorMessage, durationMs);
    }

    public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 100, string? activityType = null, string? source = null)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(activityType))
                query = query.Where(log => log.EventType == activityType);

            if (!string.IsNullOrEmpty(source))
                query = query.Where(log => log.Metadata != null && log.Metadata.Contains(source));

            return await query
                .OrderByDescending(log => log.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent logs");
            return new List<AuditLog>();
        }
    }

    public async Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime fromDate, DateTime toDate, string? activityType = null)
    {
        try
        {
            var query = _context.AuditLogs
                .Where(log => log.Timestamp >= fromDate && log.Timestamp <= toDate);

            if (!string.IsNullOrEmpty(activityType))
                query = query.Where(log => log.EventType == activityType);

            return await query
                .OrderByDescending(log => log.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs by date range");
            return new List<AuditLog>();
        }
    }

    public async Task<int> GetLogCountAsync(string? activityType = null, DateTime? fromDate = null)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(activityType))
                query = query.Where(log => log.EventType == activityType);

            if (fromDate.HasValue)
                query = query.Where(log => log.Timestamp >= fromDate.Value);

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting log count");
            return 0;
        }
    }

    public async Task CleanupOldLogsAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            
            var oldLogs = await _context.AuditLogs
                .Where(log => log.Timestamp < cutoffDate)
                .ToListAsync();

            if (oldLogs.Any())
            {
                _context.AuditLogs.RemoveRange(oldLogs);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} old audit logs older than {MaxAge}", 
                    oldLogs.Count, maxAge);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old audit logs");
        }
    }

    public async Task ClearAllLogsAsync()
    {
        try
        {
            var allLogs = await _context.AuditLogs.ToListAsync();
            
            if (allLogs.Any())
            {
                _context.AuditLogs.RemoveRange(allLogs);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleared all {Count} audit logs", allLogs.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all audit logs");
        }
    }
}
