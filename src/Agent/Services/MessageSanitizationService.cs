using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Agent.Services;

/// <summary>
/// Service responsible for sanitizing and validating messages before they enter ChatHistory.
/// Prevents prompt injection attacks and ensures message safety.
/// </summary>
public interface IMessageSanitizationService
{
    /// <summary>
    /// Sanitizes a user message before adding it to ChatHistory
    /// </summary>
    Task<string> SanitizeUserMessageAsync(string message);
    
    /// <summary>
    /// Sanitizes a system message before adding it to ChatHistory
    /// </summary>
    Task<string> SanitizeSystemMessageAsync(string message);
    
    /// <summary>
    /// Validates if a message contains potential security threats
    /// </summary>
    Task<bool> IsMessageSafeAsync(string message);
}

public class MessageSanitizationService : IMessageSanitizationService
{
    private readonly ILogger<MessageSanitizationService> _logger;
    
    // Common prompt injection patterns
    private static readonly Regex[] PromptInjectionPatterns = new[]
    {
        new Regex(@"ignore\s+previous\s+instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"forget\s+everything", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"system\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"assistant\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"user\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"<\s*system\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"<\s*/\s*system\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"pretend\s+you\s+are", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"act\s+as\s+if", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"roleplay\s+as", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"simulate\s+being", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"override\s+your", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"disregard\s+your", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"new\s+instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"updated\s+instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\\n\\n", RegexOptions.Compiled), // Multiple newlines that could break context
        new Regex(@"```\s*system", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"```\s*assistant", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };
    
    // Suspicious patterns that should be logged but not necessarily blocked
    private static readonly Regex[] SuspiciousPatterns = new[]
    {
        new Regex(@"jailbreak", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"prompt\s+injection", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"bypass\s+safety", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"harmful\s+content", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    public MessageSanitizationService(ILogger<MessageSanitizationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> SanitizeUserMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var sanitized = message;
        
        // Basic sanitization
        sanitized = await SanitizeBasicAsync(sanitized);
        
        // Check for prompt injection
        if (!await IsMessageSafeAsync(sanitized))
        {
            _logger.LogWarning("Potential prompt injection detected in user message. Message sanitized.");
            sanitized = SanitizePromptInjection(sanitized);
        }
        
        // Log suspicious patterns
        await LogSuspiciousPatternsAsync(sanitized, "user");
        
        return sanitized;
    }

    public async Task<string> SanitizeSystemMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // System messages are trusted but still apply basic sanitization
        var sanitized = await SanitizeBasicAsync(message);
        
        return sanitized;
    }

    public async Task<bool> IsMessageSafeAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        await Task.CompletedTask; // For async consistency
        
        // Check for prompt injection patterns
        foreach (var pattern in PromptInjectionPatterns)
        {
            if (pattern.IsMatch(message))
            {
                _logger.LogWarning("Prompt injection pattern detected: {Pattern}", pattern.ToString());
                return false;
            }
        }
        
        return true;
    }

    private async Task<string> SanitizeBasicAsync(string message)
    {
        await Task.CompletedTask; // For async consistency
        
        // Remove null characters
        message = message.Replace('\0', ' ');
        
        // Normalize whitespace but preserve intentional formatting
        message = Regex.Replace(message, @"\r\n|\r|\n", "\n");
        
        // Remove excessive whitespace (more than 3 consecutive spaces)
        message = Regex.Replace(message, @" {4,}", "   ");
        
        // Remove excessive newlines (more than 3 consecutive)
        message = Regex.Replace(message, @"\n{4,}", "\n\n\n");
        
        // Trim but preserve intentional leading/trailing whitespace within reason
        message = message.Trim();
        
        return message;
    }

    private string SanitizePromptInjection(string message)
    {
        var sanitized = message;
        
        // Replace dangerous patterns with safe alternatives
        foreach (var pattern in PromptInjectionPatterns)
        {
            sanitized = pattern.Replace(sanitized, "[SANITIZED]");
        }
        
        return sanitized;
    }

    private async Task LogSuspiciousPatternsAsync(string message, string messageType)
    {
        await Task.CompletedTask; // For async consistency
        
        foreach (var pattern in SuspiciousPatterns)
        {
            if (pattern.IsMatch(message))
            {
                _logger.LogInformation("Suspicious pattern detected in {MessageType} message: {Pattern}", 
                    messageType, pattern.ToString());
            }
        }
    }
}
