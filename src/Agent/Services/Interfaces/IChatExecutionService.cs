using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Agent.Services.Interfaces;

/// <summary>
/// Interface for chat execution service
/// </summary>
public interface IChatExecutionService
{
    /// <summary>
    /// Execute a chat with user message, memory integration, and learning
    /// Used by ChatHub for WebChat conversations
    /// </summary>
    /// <param name="chatHistory">The chat history</param>
    /// <param name="userMessage">The user message</param>
    /// <param name="sessionId">Session ID for logging</param>
    /// <param name="deviceFingerprint">Device fingerprint for memory context</param>
    /// <returns>The chat response</returns>
    Task<string> ExecuteChatAsync(
        ChatHistory chatHistory,
        string userMessage,
        string sessionId,
        string? deviceFingerprint = null);

    /// <summary>
    /// Execute a chat completion with plugins
    /// </summary>
    /// <param name="chatHistory">The chat history</param>
    /// <param name="sessionId">Session ID for logging</param>
    /// <param name="useCache">Whether to use caching</param>
    /// <param name="executionSettings">Optional execution settings</param>
    /// <param name="deviceFingerprint">Device fingerprint for memory context</param>
    /// <returns>The chat response</returns>
    Task<string> ExecuteChatCompletionAsync(
        ChatHistory chatHistory,
        string sessionId,
        bool useCache = true,
        OpenAIPromptExecutionSettings? executionSettings = null,
        string? deviceFingerprint = null);

    /// <summary>
    /// Execute a chat completion with a specific tool/plugin hint
    /// </summary>
    /// <param name="chatHistory">The chat history</param>
    /// <param name="toolName">Name of the preferred tool/plugin</param>
    /// <param name="sessionId">Session ID for logging</param>
    /// <param name="useCache">Whether to use caching</param>
    /// <returns>The chat response</returns>
    Task<string> ExecuteChatCompletionWithToolAsync(
        ChatHistory chatHistory,
        string toolName,
        string sessionId,
        bool useCache = true);
}
