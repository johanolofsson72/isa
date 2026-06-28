using System.Collections.Concurrent;
using Microsoft.SemanticKernel.ChatCompletion;
using Agent.Services.Interfaces;

namespace Agent.Services;

/// <summary>
/// Service for managing chat sessions without SignalR
/// </summary>
public interface IChatSessionService
{
    Task<ChatHistory> GetOrCreateSessionAsync(string sessionId, string? deviceFingerprint = null);
    void RemoveSession(string sessionId);
    void CleanupExpiredSessions();
}

public class ChatSessionService : IChatSessionService
{
    private readonly ConcurrentDictionary<string, ChatSessionData> _sessions = new();
    private readonly IChatHistoryFactory _chatHistoryFactory;
    private readonly IConversationHistoryService _conversationHistoryService;
    private readonly ILogger<ChatSessionService> _logger;
    private readonly Timer _cleanupTimer;

    public ChatSessionService(
        IChatHistoryFactory chatHistoryFactory,
        IConversationHistoryService conversationHistoryService,
        ILogger<ChatSessionService> logger)
    {
        _chatHistoryFactory = chatHistoryFactory;
        _conversationHistoryService = conversationHistoryService;
        _logger = logger;

        // Cleanup expired sessions every 30 minutes
        _cleanupTimer = new Timer(
            _ => CleanupExpiredSessions(),
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(30));
    }

    public async Task<ChatHistory> GetOrCreateSessionAsync(string sessionId, string? deviceFingerprint = null)
    {
        if (_sessions.TryGetValue(sessionId, out var existingSession))
        {
            existingSession.LastAccessed = DateTime.UtcNow;
            return existingSession.ChatHistory;
        }

        // Load conversation history from database and create ChatHistory with persisted messages
        var chatHistory = await _conversationHistoryService.LoadChatHistoryAsync(sessionId, deviceFingerprint);

        var sessionData = new ChatSessionData
        {
            ChatHistory = chatHistory,
            CreatedAt = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow,
            DeviceFingerprint = deviceFingerprint
        };

        _sessions[sessionId] = sessionData;

        var messageCount = await _conversationHistoryService.GetSessionMessageCountAsync(sessionId);

        _logger.LogInformation(
            "Created/loaded chat session: {SessionId} with device {DeviceFingerprint}, loaded {MessageCount} persisted messages",
            sessionId, deviceFingerprint, messageCount);

        return chatHistory;
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out _))
        {
            _logger.LogInformation("Removed chat session: {SessionId}", sessionId);
        }
    }

    public void CleanupExpiredSessions()
    {
        var expiredSessions = _sessions
            .Where(kvp => DateTime.UtcNow - kvp.Value.LastAccessed > TimeSpan.FromHours(2))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            RemoveSession(sessionId);
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired chat sessions", expiredSessions.Count);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// Data for a chat session
/// </summary>
internal class ChatSessionData
{
    public ChatHistory ChatHistory { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessed { get; set; }
    public string? DeviceFingerprint { get; set; }
}
