using System.Diagnostics;
using System.Text.Json;
using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Agent.Services;

/// <summary>
/// Service for audit logging of chat interactions and workflow executions
/// </summary>
public interface IAuditLogService
{
    Task LogChatInteractionAsync(string sessionId, string userInput, string? aiResponse = null, string? errorMessage = null, long? durationMs = null);
    Task LogWorkflowExecutionAsync(string sessionId, string workflowId, string status, string? errorMessage = null, long? durationMs = null);
    Task LogPluginExecutionAsync(string sessionId, string pluginName, string functionName, string status, string? errorMessage = null, long? durationMs = null, object? metadata = null);
    Task<List<AuditLog>> GetRecentLogsAsync(int count = 100, string? eventType = null);
    Task CleanupOldLogsAsync(int daysToKeep = 30);
    IDisposable StartTimer(string sessionId, string eventType, string? identifier = null);
}

public class AuditLogService : IAuditLogService
{
    private readonly AgentDbContext _dbContext;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AgentDbContext dbContext, ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogChatInteractionAsync(string sessionId, string userInput, string? aiResponse = null, string? errorMessage = null, long? durationMs = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = "ChatInteraction",
                SessionId = sessionId,
                UserInput = userInput,
                AIResponse = aiResponse,
                Status = errorMessage != null ? "Error" : "Success",
                ErrorMessage = errorMessage,
                DurationMs = durationMs
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogDebug("Logged chat interaction for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging chat interaction for session {SessionId}", sessionId);
        }
    }

    public async Task LogWorkflowExecutionAsync(string sessionId, string workflowId, string status, string? errorMessage = null, long? durationMs = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = "WorkflowExecution",
                SessionId = sessionId,
                WorkflowId = workflowId,
                Status = status,
                ErrorMessage = errorMessage,
                DurationMs = durationMs
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogDebug("Logged workflow execution {WorkflowId} for session {SessionId}", workflowId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging workflow execution {WorkflowId} for session {SessionId}", workflowId, sessionId);
        }
    }

    public async Task LogPluginExecutionAsync(string sessionId, string pluginName, string functionName, string status, string? errorMessage = null, long? durationMs = null, object? metadata = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = "PluginExecution",
                SessionId = sessionId,
                PluginName = pluginName,
                FunctionName = functionName,
                Status = status,
                ErrorMessage = errorMessage,
                DurationMs = durationMs,
                Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogDebug("Logged plugin execution {PluginName}.{FunctionName} for session {SessionId}", pluginName, functionName, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging plugin execution {PluginName}.{FunctionName} for session {SessionId}", pluginName, functionName, sessionId);
        }
    }

    public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 100, string? eventType = null)
    {
        try
        {
            var query = _dbContext.AuditLogs.AsQueryable();
            
            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(log => log.EventType == eventType);
            }
            
            return await query
                .OrderByDescending(log => log.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent audit logs");
            return new List<AuditLog>();
        }
    }

    public async Task CleanupOldLogsAsync(int daysToKeep = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldLogs = await _dbContext.AuditLogs
                .Where(log => log.Timestamp < cutoffDate)
                .ToListAsync();

            if (oldLogs.Any())
            {
                _dbContext.AuditLogs.RemoveRange(oldLogs);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Cleaned up {Count} old audit log entries", oldLogs.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old audit logs");
        }
    }

    public IDisposable StartTimer(string sessionId, string eventType, string? identifier = null)
    {
        return new AuditTimer(this, sessionId, eventType, identifier);
    }
}

/// <summary>
/// Helper class for timing operations and automatically logging them
/// </summary>
public class AuditTimer : IDisposable
{
    private readonly IAuditLogService _auditLogService;
    private readonly string _sessionId;
    private readonly string _eventType;
    private readonly string? _identifier;
    private readonly Stopwatch _stopwatch;
    private bool _disposed = false;

    public AuditTimer(IAuditLogService auditLogService, string sessionId, string eventType, string? identifier = null)
    {
        _auditLogService = auditLogService;
        _sessionId = sessionId;
        _eventType = eventType;
        _identifier = identifier;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            
            // Log based on event type
            var durationMs = _stopwatch.ElapsedMilliseconds;
            
            if (_eventType == "ChatInteraction")
            {
                // This will be handled by the ChatHub itself
            }
            else if (_eventType == "WorkflowExecution" && !string.IsNullOrEmpty(_identifier))
            {
                _ = Task.Run(async () => await _auditLogService.LogWorkflowExecutionAsync(_sessionId, _identifier, "Completed", null, durationMs));
            }
            else if (_eventType == "PluginExecution" && !string.IsNullOrEmpty(_identifier))
            {
                var parts = _identifier.Split('.');
                if (parts.Length >= 2)
                {
                    _ = Task.Run(async () => await _auditLogService.LogPluginExecutionAsync(_sessionId, parts[0], parts[1], "Completed", null, durationMs));
                }
            }
            
            _disposed = true;
        }
    }
}
