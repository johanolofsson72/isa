using Agent.Models;
using Agent.Services.Interfaces;
using System.Text.RegularExpressions;

namespace Agent.Services;

/// <summary>
/// Service for extracting memories from conversations using regex patterns
/// </summary>
public class IntelligentMemoryExtractor : IIntelligentMemoryExtractor
{
    private readonly ILogger<IntelligentMemoryExtractor> _logger;

    // Question Patterns - these should NOT trigger memory extraction
    private static readonly Regex[] QuestionPatterns = {
        new(@"vad heter\s+(jag|du|han|hon|den|det)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"vem är\s+(jag|du|han|hon|den|det)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"vad är\s+mitt\s+namn", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"what is\s+my\s+name", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"who am\s+i", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"vad\s+heter\s+jag", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"vem\s+är\s+jag", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Identity Patterns (MemoryType.Identity, threshold: 0.90)
    private static readonly Regex[] IdentityPatterns = {
        new(@"jag heter\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"jag arbetar som\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"mitt namn är\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"det är\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)\s+igen", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"i am\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"my name is\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Middle Name Patterns - specific patterns for middle names
    private static readonly Regex[] MiddleNamePatterns = {
        new(@"jag heter\s+([A-ZÅÄÖ][a-zåäö]+)\s+i mellannamn", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"mitt mellannamn är\s+([A-ZÅÄÖ][a-zåäö]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"my middle name is\s+([A-Z][a-z]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Facts Patterns (MemoryType.Facts, threshold: 0.85)
    private static readonly Regex[] FactsPatterns = {
        // Mathematical equations and assignments
        new(@"(\w+)\s*[=:]\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(\w+)\s+är\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"svaret är\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(\w+)\s+equals\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"the answer is\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Complex mathematical expressions
        new(@"(\w+)\s*=\s*(\w+)\s*[-+*/]\s*(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(\w+)\s+och\s+(\w+)\s+och\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled), // "x = 77 and z = 34 and y = x - z"
        // General factual statements - only match if no private keywords
        new(@"^(?!.*(?:hemligt|secret|privat|private))([A-ZÅÄÖ][a-zåäö]+(?:\s+[a-zåäö]+)*)\s+är\s+([^.!?]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?!.*(?:hemligt|secret|privat|private))([A-Z][a-z]+(?:\s+[a-z]+)*)\s+is\s+([^.!?]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Context Patterns (MemoryType.EpisodicContext, threshold: 0.80)
    private static readonly Regex[] ContextPatterns = {
        new(@"jag gillar\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"alltid\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"kom ihåg att\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"i like\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"remember that\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"always\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Location and childhood patterns - specific patterns for places
        new(@"jag bodde i\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"jag växte upp i\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"jag kommer från\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"jag bor i\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"jag flyttade till\s+([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // General episodic contexts (past events, activities) - only match if no private keywords
        new(@"^(?!.*(?:hemligt|secret|privat|private))(igår\s+.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?!.*(?:hemligt|secret|privat|private))(yesterday\s+.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?!.*(?:hemligt|secret|privat|private))(idag\s+.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(?!.*(?:hemligt|secret|privat|private))(today\s+.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Secret/Private Patterns
    private static readonly Regex[] PrivatePatterns = {
        // Pattern for "kom ihåg denna hemlighet: mitt mellannamn är Erik och det är hemligt"
        new(@"kom ihåg denna hemlighet:\s*([^.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Pattern for "mitt mellannamn är Erik och det är hemligt"
        new(@"mitt mellannamn är\s+([A-ZÅÄÖ][a-zåäö]+)\s+och det är hemligt", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Pattern for "jag heter Erik i mellannamn och det är hemligt"
        new(@"jag heter\s+([^.!?]+)\s+i mellannamn\s+och det är hemligt", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Pattern for "min kod är 1234 och det är privat" - captures "kod är 1234"
        new(@"min\s+([^.!?]+)\s+och det är\s+(?:hemligt|secret|privat|private)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Simple pattern for just the secret keywords (fallback)
        new(@"(hemligt|secret|privat|private)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    public IntelligentMemoryExtractor(ILogger<IntelligentMemoryExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extract memory entries from user message and assistant response
    /// </summary>
    public Task<List<MemoryEntry>> ExtractMemoriesAsync(
        string userMessage,
        string assistantResponse,
        string sessionId,
        string? deviceFingerprint = null)
    {
        var memories = new List<MemoryEntry>();

        try
        {
            // Extract from user message
            memories.AddRange(ExtractFromText(userMessage, sessionId, deviceFingerprint, "user"));

            // Extract from assistant response (only confirmed facts)
            memories.AddRange(ExtractFromText(assistantResponse, sessionId, deviceFingerprint, "agent"));

            _logger.LogInformation("Extracted {Count} memories from conversation in session {SessionId}",
                memories.Count, sessionId);

            return Task.FromResult(memories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract memories from conversation in session {SessionId}", sessionId);
            return Task.FromResult(new List<MemoryEntry>());
        }
    }

    private List<MemoryEntry> ExtractFromText(string text, string sessionId, string? deviceFingerprint, string source)
    {
        var memories = new List<MemoryEntry>();

        if (string.IsNullOrWhiteSpace(text))
            return memories;

        // Check if this contains private information
        bool isPrivate = PrivatePatterns.Any(pattern => pattern.IsMatch(text));
        var scope = isPrivate ? MemoryScope.UserPrivate : MemoryScope.Public;
        var confidentiality = isPrivate ? Confidentiality.Private : Confidentiality.Public;

        // Use a simple approach: try patterns in priority order and return the first match
        // This prevents multiple extractions from the same text

        // Priority 0: Check if this is a question - if so, don't extract memories
        if (QuestionPatterns.Any(pattern => pattern.IsMatch(text)))
        {
            _logger.LogDebug("Skipping memory extraction from question: {Text}", text[..Math.Min(50, text.Length)]);
            return memories; // Return empty list for questions
        }

        // Priority 1: Check for specific secret patterns first
        var secretMatch = PrivatePatterns
            .Select(pattern => pattern.Match(text))
            .FirstOrDefault(match => match.Success && match.Groups.Count >= 2 && !string.IsNullOrEmpty(match.Groups[1].Value.Trim()));

        if (secretMatch != null)
        {
            var extractedValue = secretMatch.Groups[1].Value.Trim();

            // Determine the context based on which pattern matched
            var content = extractedValue;
            var matchedPattern = secretMatch.Value;

            if (matchedPattern.Contains("mellannamn"))
            {
                content = $"mellannamn är {extractedValue}";
            }
            else if (matchedPattern.Contains("efternamn"))
            {
                content = $"efternamn är {extractedValue}";
            }
            else if (matchedPattern.Contains("förnamn"))
            {
                content = $"förnamn är {extractedValue}";
            }
            else
            {
                // For other secret patterns, keep the original extracted value
                content = extractedValue;
            }

            memories.Add(CreateMemoryEntry(content, MemoryType.Identity, MemoryScope.UserPrivate,
                Confidentiality.Private, source, deviceFingerprint, 0.95, true, ConfirmationState.Confirmed));
            return memories; // Return early to avoid multiple extractions
        }

        // Priority 2: Check for middle name patterns first (more specific than general identity)
        var middleNameMatch = MiddleNamePatterns
            .Select(pattern => pattern.Match(text))
            .FirstOrDefault(match => match.Success && match.Groups.Count >= 2 && !string.IsNullOrEmpty(match.Groups[1].Value.Trim()));

        if (middleNameMatch != null)
        {
            var middleName = middleNameMatch.Groups[1].Value.Trim();
            memories.Add(CreateMemoryEntry($"mellannamn är {middleName}", MemoryType.Identity,
                MemoryScope.UserPrivate, Confidentiality.Private, source, deviceFingerprint, 0.95,
                source == "user", source == "user" ? ConfirmationState.Confirmed : ConfirmationState.Pending));
            return memories; // Return early to avoid multiple extractions
        }

        // Priority 3: Check for general identity information
        var identityMatch = IdentityPatterns
            .Select(pattern => pattern.Match(text))
            .FirstOrDefault(match => match.Success && match.Groups.Count >= 2 && !string.IsNullOrEmpty(match.Groups[1].Value.Trim()));

        if (identityMatch != null)
        {
            var content = identityMatch.Groups[1].Value.Trim();
            memories.Add(CreateMemoryEntry($"Name: {content}", MemoryType.Identity,
                MemoryScope.UserPrivate, Confidentiality.Private, source, deviceFingerprint, 0.9,
                source == "user", source == "user" ? ConfirmationState.Confirmed : ConfirmationState.Pending));
            return memories; // Return early to avoid multiple extractions
        }

        // Priority 4: Check for factual information
        foreach (var pattern in FactsPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                string content;
                if (match.Groups.Count >= 3)
                {
                    var first = match.Groups[1].Value.Trim();
                    var second = match.Groups[2].Value.Trim();

                    // Check if it's a simple assignment pattern (contains = or :)
                    if (text.Contains("=") || text.Contains(":"))
                    {
                        content = $"{first} = {second}";
                    }
                    else
                    {
                        // General factual statement - use the full text
                        content = text.Trim();
                    }
                }
                else
                {
                    // Simple answer pattern
                    content = match.Groups[1].Value.Trim();
                }

                if (!string.IsNullOrEmpty(content))
                {
                    // Log memory classification for debugging
                    _logger.LogInformation("Classified as FACTS: {Content} (pattern: {Pattern})",
                        content[..Math.Min(50, content.Length)], pattern.ToString()[..Math.Min(30, pattern.ToString().Length)]);

                    memories.Add(CreateMemoryEntry(content, MemoryType.Facts, scope, confidentiality,
                        source, deviceFingerprint, 0.85, source == "user",
                        source == "user" ? ConfirmationState.Confirmed : ConfirmationState.Pending));
                    return memories; // Return early to avoid multiple extractions
                }
            }
        }

        // Priority 5: Check for contextual information (lowest priority)
        foreach (var pattern in ContextPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                string content;
                if (match.Groups.Count >= 2)
                {
                    // Use the full text for episodic contexts
                    content = text.Trim();
                }
                else
                {
                    content = match.Groups[1].Value.Trim();
                }

                if (!string.IsNullOrEmpty(content))
                {
                    memories.Add(CreateMemoryEntry(content, MemoryType.EpisodicContext, scope, confidentiality,
                        source, deviceFingerprint, 0.8, source == "user",
                        source == "user" ? ConfirmationState.Confirmed : ConfirmationState.Pending));
                    return memories; // Return early to avoid multiple extractions
                }
            }
        }

        return memories;
    }

    /// <summary>
    /// Create a memory entry with common properties
    /// </summary>
    private MemoryEntry CreateMemoryEntry(
        string content,
        MemoryType type,
        MemoryScope scope,
        Confidentiality confidentiality,
        string source,
        string? deviceFingerprint,
        double confidence,
        bool isAuthoritative,
        ConfirmationState confirmation)
    {
        return new MemoryEntry
        {
            Content = content,
            Type = type,
            Scope = scope,
            Confidentiality = confidentiality,
            Source = source,
            Provenance = source == "user" ? Provenance.DirectUser : Provenance.Inferred,
            Confidence = confidence,
            IsAuthoritative = isAuthoritative,
            DeviceFingerprint = deviceFingerprint,
            Confirmation = confirmation
        };
    }
}