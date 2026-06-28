using Agent.Models;

namespace Agent.Services.Interfaces;

/// <summary>
/// Service for extracting memories from conversations using regex patterns
/// </summary>
public interface IIntelligentMemoryExtractor
{
    /// <summary>
    /// Extract memory entries from user message and assistant response
    /// </summary>
    /// <param name="userMessage">User's message</param>
    /// <param name="assistantResponse">Assistant's response</param>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="deviceFingerprint">Device identifier</param>
    /// <returns>List of extracted memory entries</returns>
    Task<List<MemoryEntry>> ExtractMemoriesAsync(
        string userMessage, 
        string assistantResponse, 
        string sessionId, 
        string? deviceFingerprint = null);
}