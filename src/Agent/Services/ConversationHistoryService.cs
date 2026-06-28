using Agent.Data;
using Agent.Models;
using Agent.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agent.Services;

/// <summary>
/// Service for managing persistent conversation history
/// </summary>
public interface IConversationHistoryService
{
    Task StoreMessageAsync(string sessionId, string content, string role, string? deviceFingerprint = null, long? processingTimeMs = null, bool hasError = false, string? errorMessage = null);
    Task<List<ConversationHistory>> GetSessionHistoryAsync(string sessionId, int? maxMessages = null);
    Task<ChatHistory> LoadChatHistoryAsync(string sessionId, string? deviceFingerprint = null);
    Task CleanupExpiredHistoryAsync();
    Task DeleteSessionHistoryAsync(string sessionId);
    Task<int> GetSessionMessageCountAsync(string sessionId);
}

/// <summary>
/// Service for managing persistent conversation history
/// </summary>
public class ConversationHistoryService : IConversationHistoryService
{
    private readonly MemoryDbContext _context;
    private readonly IChatHistoryFactory _chatHistoryFactory;
    private readonly ILogger<ConversationHistoryService> _logger;
    private const int DEFAULT_MAX_MESSAGES = 50; // Prevent token limit issues
    private const int MAX_CONTENT_LENGTH = 10000; // Prevent excessive storage

    public ConversationHistoryService(
        MemoryDbContext context,
        IChatHistoryFactory chatHistoryFactory,
        ILogger<ConversationHistoryService> logger)
    {
        _context = context;
        _chatHistoryFactory = chatHistoryFactory;
        _logger = logger;
    }

    public async Task StoreMessageAsync(
        string sessionId, 
        string content, 
        string role, 
        string? deviceFingerprint = null, 
        long? processingTimeMs = null, 
        bool hasError = false, 
        string? errorMessage = null)
    {
        try
        {
            // Truncate content if too long
            var truncatedContent = content.Length > MAX_CONTENT_LENGTH 
                ? content[..MAX_CONTENT_LENGTH] + "... [truncated]"
                : content;

            // Get next message order for this session
            var messageOrder = await GetNextMessageOrderAsync(sessionId);

            var historyEntry = new ConversationHistory
            {
                SessionId = sessionId,
                Content = truncatedContent,
                Role = role,
                DeviceFingerprint = deviceFingerprint,
                MessageOrder = messageOrder,
                ProcessingTimeMs = processingTimeMs,
                HasError = hasError,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(365) // 365-day retention
            };

            _context.ConversationHistory.Add(historyEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Stored conversation message: Session={SessionId}, Role={Role}, Order={Order}, Device={DeviceFingerprint}",
                sessionId, role, messageOrder, deviceFingerprint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to store conversation message: Session={SessionId}, Role={Role}", 
                sessionId, role);
            // Don't throw - conversation should continue even if storage fails
        }
    }

    public async Task<List<ConversationHistory>> GetSessionHistoryAsync(string sessionId, int? maxMessages = null)
    {
        try
        {
            var limit = maxMessages ?? DEFAULT_MAX_MESSAGES;
            
            var history = await _context.ConversationHistory
                .Where(h => h.SessionId == sessionId && h.ExpiresAt > DateTime.UtcNow)
                .OrderBy(h => h.MessageOrder)
                .Take(limit)
                .ToListAsync();

            _logger.LogInformation(
                "Retrieved {Count} messages for session {SessionId}",
                history.Count, sessionId);

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to retrieve session history: Session={SessionId}", 
                sessionId);
            return new List<ConversationHistory>();
        }
    }

    public async Task<ChatHistory> LoadChatHistoryAsync(string sessionId, string? deviceFingerprint = null)
    {
        try
        {
            // Create fresh ChatHistory with system prompt
            var chatHistory = await _chatHistoryFactory.CreateWebChatHistoryAsync(deviceFingerprint);

            // Load persisted conversation messages
            var persistedMessages = await GetSessionHistoryAsync(sessionId);

            // Add persisted messages to ChatHistory (skip system messages as they're already added)
            foreach (var message in persistedMessages.Where(m => m.Role != "System"))
            {
                switch (message.Role.ToLowerInvariant())
                {
                    case "user":
                        await _chatHistoryFactory.AddUserMessageAsync(chatHistory, message.Content);
                        break;
                    case "assistant":
                        await _chatHistoryFactory.AddAssistantMessageAsync(chatHistory, message.Content);
                        break;
                }
            }

            _logger.LogInformation(
                "Loaded ChatHistory for session {SessionId} with {Count} persisted messages",
                sessionId, persistedMessages.Count);

            return chatHistory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to load chat history for session {SessionId}, creating fresh history", 
                sessionId);
            
            // Fallback to fresh history if loading fails
            return await _chatHistoryFactory.CreateWebChatHistoryAsync(deviceFingerprint);
        }
    }

    public async Task CleanupExpiredHistoryAsync()
    {
        try
        {
            var expiredCount = await _context.ConversationHistory
                .Where(h => h.ExpiresAt <= DateTime.UtcNow)
                .ExecuteDeleteAsync();

            if (expiredCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired conversation history entries", expiredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired conversation history");
        }
    }

    public async Task DeleteSessionHistoryAsync(string sessionId)
    {
        try
        {
            var deletedCount = await _context.ConversationHistory
                .Where(h => h.SessionId == sessionId)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Deleted {Count} messages for session {SessionId}", deletedCount, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session history: Session={SessionId}", sessionId);
        }
    }

    public async Task<int> GetSessionMessageCountAsync(string sessionId)
    {
        try
        {
            return await _context.ConversationHistory
                .Where(h => h.SessionId == sessionId && h.ExpiresAt > DateTime.UtcNow)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get message count for session {SessionId}", sessionId);
            return 0;
        }
    }

    private async Task<int> GetNextMessageOrderAsync(string sessionId)
    {
        try
        {
            var maxOrder = await _context.ConversationHistory
                .Where(h => h.SessionId == sessionId)
                .MaxAsync(h => (int?)h.MessageOrder);

            return (maxOrder ?? 0) + 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next message order for session {SessionId}", sessionId);
            return 1; // Fallback to first message
        }
    }
}
