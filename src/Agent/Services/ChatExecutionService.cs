using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Agent.Models;
using Agent.Services;
using Agent.Services.Interfaces;
using Agent.Data;
using Shared.Models;
using Shared.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Agent.Services;

/// <summary>
/// Shared service for executing chat completions with plugins
/// Used by both WebChat and Workflows to ensure consistent behavior
/// </summary>
public class ChatExecutionService : IChatExecutionService
{
    private readonly Kernel _kernel;
    private readonly IOpenAICacheService _cacheService;
    private readonly IAuditLogService _auditLogService;
    private readonly IProductionMemoryService _memoryService;
    private readonly IIntelligentMemoryExtractor _memoryExtractor;
    private readonly IOnlineSearchService _onlineSearchService;
    private readonly IConversationHistoryService _conversationHistoryService;
    private readonly IChatHistoryFactory _chatHistoryFactory;
    private readonly SmartUserIdentificationService _smartUserIdentificationService;
    private readonly ILogger<ChatExecutionService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly FilesDbContext _filesDbContext;
    private readonly ITempFilesService _tempFilesService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FileProcessingService _fileProcessingService;

    public ChatExecutionService(
        Kernel kernel,
        IOpenAICacheService cacheService,
        IAuditLogService auditLogService,
        IProductionMemoryService memoryService,
        IIntelligentMemoryExtractor memoryExtractor,
        IOnlineSearchService onlineSearchService,
        IConversationHistoryService conversationHistoryService,
        IChatHistoryFactory chatHistoryFactory,
        SmartUserIdentificationService smartUserIdentificationService,
        ILogger<ChatExecutionService> logger,
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor,
        FilesDbContext filesDbContext,
        ITempFilesService tempFilesService,
        IHttpClientFactory httpClientFactory,
        FileProcessingService fileProcessingService)
    {
        _kernel = kernel;
        _cacheService = cacheService;
        _auditLogService = auditLogService;
        _memoryService = memoryService;
        _memoryExtractor = memoryExtractor;
        _conversationHistoryService = conversationHistoryService;
        _chatHistoryFactory = chatHistoryFactory;
        _smartUserIdentificationService = smartUserIdentificationService;
        _onlineSearchService = onlineSearchService;
        _logger = logger;
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
        _filesDbContext = filesDbContext;
        _tempFilesService = tempFilesService;
        _httpClientFactory = httpClientFactory;
        _fileProcessingService = fileProcessingService;
    }

    /// <summary>
    /// Execute a chat with user message, memory integration, and learning
    /// Used by ChatHub for WebChat conversations
    /// </summary>
    /// <param name="chatHistory">The chat history</param>
    /// <param name="userMessage">The user message</param>
    /// <param name="sessionId">Session ID for logging</param>
    /// <param name="deviceFingerprint">Device fingerprint for memory context</param>
    /// <returns>The chat response</returns>
    public async Task<string> ExecuteChatAsync(
        ChatHistory chatHistory,
        string userMessage,
        string sessionId,
        string? deviceFingerprint = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string? response = null;
        bool hasError = false;
        string? errorMessage = null;

        try
        {
            // Store user message immediately
            await _conversationHistoryService.StoreMessageAsync(
                sessionId, userMessage, "User", deviceFingerprint);

            // Smart user identification - check if user is identifying themselves
            if (!string.IsNullOrEmpty(deviceFingerprint))
            {
                var identifiedUserId = await _smartUserIdentificationService.AnalyzeUserIdentificationAsync(userMessage, deviceFingerprint);
                if (!string.IsNullOrEmpty(identifiedUserId))
                {
                    _logger.LogInformation("🎯 SMART_ID: User identified as {UserId} from message analysis", identifiedUserId);
                }
            }

            // Check for file IDs in message and process them using FileProcessingService
            _logger.LogInformation("🔍 CHAT: About to process file references in message: {Message}", userMessage);
            var processedMessage = await _fileProcessingService.ProcessFileReferencesInMessageAsync(userMessage);
            _logger.LogInformation("🔍 CHAT: Processed message result length: {OriginalLength} -> {ProcessedLength}",
                userMessage.Length, processedMessage.Length);

            // Add processed message to history
            chatHistory.AddUserMessage(processedMessage);

            // Execute chat completion with memory integration
            response = await ExecuteChatCompletionAsync(chatHistory, sessionId, true, null, deviceFingerprint);

            // Add assistant response to history
            chatHistory.AddAssistantMessage(response);

            stopwatch.Stop();

            // Store assistant response
            await _conversationHistoryService.StoreMessageAsync(
                sessionId, response, "Assistant", deviceFingerprint, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            hasError = true;
            errorMessage = ex.Message;

            _logger.LogError(ex, "Error executing chat for session {SessionId}", sessionId);

            response = "I apologize, but I encountered an error processing your request. Please try again.";

            // Store error response
            await _conversationHistoryService.StoreMessageAsync(
                sessionId, response, "Assistant", deviceFingerprint, stopwatch.ElapsedMilliseconds, hasError, errorMessage);

            return response;
        }
    }

    /// <summary>
    /// Execute a chat completion with plugins - THE PERFECT IMPLEMENTATION
    /// This is the proven method that works 100% in WebChat.
    /// All other services (Workflows, MCP) should use this exact same approach.
    /// </summary>
    /// <param name="chatHistory">The chat history</param>
    /// <param name="sessionId">Session ID for logging</param>
    /// <param name="useCache">Whether to use caching</param>
    /// <param name="executionSettings">Optional execution settings</param>
    /// <param name="deviceFingerprint">Device fingerprint for memory context</param>
    /// <returns>The chat response</returns>
    public async Task<string> ExecuteChatCompletionAsync(
        ChatHistory chatHistory,
        string sessionId,
        bool useCache = true,
        OpenAIPromptExecutionSettings? executionSettings = null,
        string? deviceFingerprint = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Use the PERFECT settings that work 100% in WebChat if none provided
        executionSettings ??= CreatePerfectExecutionSettings();

        try
        {
            // Enhance chat with memory context
            await EnhanceChatWithMemoryAsync(chatHistory, deviceFingerprint);

            // Get user message for memory extraction
            var userMessage = chatHistory.LastOrDefault(m => m.Role == AuthorRole.User)?.Content ?? "";

            // Check cache first if enabled (disabled in test environment for deterministic behavior)
            string? cachedResponse = null;
            var isTestEnvironment = _environment.EnvironmentName == "Test" || _environment.EnvironmentName == "Development";
            if (useCache && !isTestEnvironment)
            {
                cachedResponse = await _cacheService.GetCachedResponseAsync(chatHistory, executionSettings);
                if (cachedResponse != null)
                {
                    _logger.LogInformation("💾 Using cached response for session {SessionId}", sessionId);
                    return cachedResponse;
                }
            }
            else if (isTestEnvironment)
            {
                _logger.LogInformation("🧪 TEST MODE: Caching disabled for deterministic behavior in session {SessionId}", sessionId);
            }

            // Execute with plugins
            _logger.LogInformation("🤖 Executing chat completion with plugins for session {SessionId}", sessionId);

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: executionSettings,
                kernel: _kernel
            );

            var response = result.Content ?? "";

            // Log any function calls that were made
            if (result.Items != null && result.Items.Any())
            {
                foreach (var item in result.Items)
                {
                    if (item is Microsoft.SemanticKernel.FunctionCallContent functionCall)
                    {
                        _logger.LogInformation("🔧 AI called function: {FunctionName} with arguments: {Arguments}",
                            functionCall.FunctionName, functionCall.Arguments);
                    }
                    else if (item is Microsoft.SemanticKernel.FunctionResultContent functionResult)
                    {
                        _logger.LogInformation("✅ Function result: {Result}", functionResult.Result);
                    }
                }
            }
            else
            {
                _logger.LogInformation("ℹ️ No function calls were made by AI for session {SessionId}", sessionId);
            }

            // Cache the response if enabled
            if (useCache)
            {
                await _cacheService.CacheResponseAsync(chatHistory, response, executionSettings);
            }

            // Extract and store memories from the conversation
            await LearnFromConversationAsync(userMessage, response, sessionId, deviceFingerprint);

            _logger.LogInformation("✅ Chat completion successful for session {SessionId} in {ElapsedMs}ms", 
                sessionId, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Chat completion failed for session {SessionId} after {ElapsedMs}ms", 
                sessionId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Execute a chat completion with a specific tool/plugin hint
    /// </summary>
    /// <param name="chatHistory">The chat history</param>
    /// <param name="toolName">Name of the preferred tool/plugin</param>
    /// <param name="sessionId">Session ID for logging</param>
    /// <param name="useCache">Whether to use caching</param>
    /// <returns>The chat response</returns>
    public async Task<string> ExecuteChatCompletionWithToolAsync(
        ChatHistory chatHistory,
        string toolName,
        string sessionId,
        bool useCache = true)
    {
        _logger.LogInformation("🔧 Executing chat completion with tool hint: {ToolName} for session {SessionId}", 
            toolName, sessionId);

        // Create execution settings with tool preference
        // Simplified settings for Docker compatibility
        var executionSettings = new OpenAIPromptExecutionSettings();

        return await ExecuteChatCompletionAsync(chatHistory, sessionId, useCache, executionSettings);
    }

    /// <summary>
    /// Creates the PERFECT execution settings that work 100% in WebChat.
    /// These exact settings are proven to work with plugin function calling.
    /// DO NOT CHANGE THESE VALUES - they are the golden standard.
    /// </summary>
    /// <returns>Perfect execution settings for plugin function calling</returns>
    public static OpenAIPromptExecutionSettings CreatePerfectExecutionSettings()
    {
        return new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4000,
            Temperature = 0.1,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };
    }

    /// <summary>
    /// Enhance chat history with memory context
    /// </summary>
    private async Task EnhanceChatWithMemoryAsync(ChatHistory chatHistory, string? deviceFingerprint = null)
    {
        try
        {
            // Get the current system message to use as base
            var currentSystemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
            var baseSystemPrompt = currentSystemMessage?.Content ?? "";

            // Generate memory-enhanced system prompt using current system prompt as base
            var enhancedSystemPrompt = await _memoryService.GenerateSystemPromptAsync(deviceFingerprint, baseSystemPrompt);

            // Remove existing system messages to replace with memory-enhanced prompt
            var nonSystemMessages = chatHistory.Where(m => m.Role != AuthorRole.System).ToList();
            chatHistory.Clear();

            // Add the memory-enhanced system prompt
            chatHistory.AddSystemMessage(enhancedSystemPrompt);

            // Re-add non-system messages
            foreach (var message in nonSystemMessages)
            {
                chatHistory.Add(message);
            }

            _logger.LogInformation("Enhanced chat with memory context for device {DeviceFingerprint}", deviceFingerprint ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enhance chat with memory context, continuing without enhancement");
        }
    }

    /// <summary>
    /// Learn from conversation by extracting and storing memories
    /// </summary>
    private async Task LearnFromConversationAsync(string userMessage, string assistantResponse, string sessionId, string? deviceFingerprint = null)
    {
        try
        {
            // Extract memories from the conversation
            var extractedMemories = await _memoryExtractor.ExtractMemoriesAsync(
                userMessage, assistantResponse, sessionId, deviceFingerprint);

            // Store each extracted memory
            foreach (var memory in extractedMemories)
            {
                // Set the owner user ID based on device fingerprint or name-based identification
                if (!string.IsNullOrEmpty(deviceFingerprint))
                {
                    var user = await GetOrIdentifyUserAsync(deviceFingerprint, userMessage);
                    memory.OwnerUserId = user.UserId;
                    memory.SubjectUserId = user.UserId;
                }

                await _memoryService.StoreMemoryAsync(memory);
            }

            if (extractedMemories.Any())
            {
                _logger.LogInformation("Learned {Count} memories from conversation in session {SessionId}",
                    extractedMemories.Count, sessionId);
            }

            // Handle online search consent requests
            await HandleOnlineSearchRequestsAsync(userMessage, assistantResponse, deviceFingerprint);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to learn from conversation in session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Handle online search requests and consent management
    /// </summary>
    private async Task HandleOnlineSearchRequestsAsync(string userMessage, string assistantResponse, string? deviceFingerprint = null)
    {
        try
        {
            if (string.IsNullOrEmpty(deviceFingerprint)) return;

            var user = await GetOrIdentifyUserAsync(deviceFingerprint, userMessage);
            var userMessageLower = userMessage.ToLowerInvariant();

            // Check for consent requests
            if (userMessageLower.Contains("online") && userMessageLower.Contains("sökning") ||
                userMessageLower.Contains("sök") && userMessageLower.Contains("internet") ||
                userMessageLower.Contains("google") && userMessageLower.Contains("sök"))
            {
                var consentMessage = await _onlineSearchService.RequestOnlineSearchConsentAsync(user.UserId);
                _logger.LogInformation("Requested online search consent for user {UserId}", user.UserId);
            }

            // Check for consent responses
            if (userMessageLower == "ja" || userMessageLower == "nej" ||
                userMessageLower == "yes" || userMessageLower == "no" ||
                userMessageLower == "godkänn" || userMessageLower == "avböj")
            {
                var responseMessage = await _onlineSearchService.ProcessConsentResponseAsync(user.UserId, userMessage);
                _logger.LogInformation("Processed consent response for user {UserId}: {Response}", user.UserId, userMessage);
            }

            // Check for search result confirmations
            if (userMessageLower.Contains("bekräfta") && userMessageLower.Contains("sökresultat") ||
                userMessageLower.Contains("confirm") && userMessageLower.Contains("search"))
            {
                var confirmMessage = await _onlineSearchService.ConfirmSearchResultsAsync(user.UserId, true);
                _logger.LogInformation("User {UserId} confirmed search results", user.UserId);
            }
            else if (userMessageLower.Contains("avvisa") && userMessageLower.Contains("sökresultat") ||
                     userMessageLower.Contains("reject") && userMessageLower.Contains("search"))
            {
                var rejectMessage = await _onlineSearchService.ConfirmSearchResultsAsync(user.UserId, false);
                _logger.LogInformation("User {UserId} rejected search results", user.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle online search requests");
        }
    }

    /// <summary>
    /// Get or identify user based on device fingerprint and message content
    /// Implements name-based identification as fallback according to memory.txt
    /// In test environment, uses explicit test user ID from headers
    /// </summary>
    private async Task<User> GetOrIdentifyUserAsync(string deviceFingerprint, string userMessage)
    {
        // TEST MODE: Check for explicit test user ID in headers
        if (_environment.EnvironmentName == "Test" || _environment.EnvironmentName == "Development")
        {
            var testUserId = _httpContextAccessor.HttpContext?.Request.Headers["X-Test-User-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(testUserId))
            {
                _logger.LogInformation("TEST MODE: Using explicit test user ID: {TestUserId}", testUserId);
                var testUser = await _memoryService.GetOrCreateUserAsync(testUserId);

                // Extract name from message for test user if not set
                if (string.IsNullOrEmpty(testUser.DisplayName))
                {
                    var extractedName = ExtractNameFromMessage(userMessage);
                    if (!string.IsNullOrEmpty(extractedName))
                    {
                        testUser.DisplayName = extractedName;
                        await _memoryService.UpdateUserAsync(testUser);
                        _logger.LogInformation("TEST MODE: Set display name '{Name}' for test user {UserId}",
                            extractedName, testUser.UserId);
                    }
                }

                return testUser;
            }
        }

        // PRODUCTION MODE: Standard device fingerprint flow
        _logger.LogInformation("🔍 DEVICE DEBUG: Agent ChatExecutionService received device fingerprint: {DeviceFingerprint}", deviceFingerprint);
        var user = await _memoryService.GetOrCreateUserAsync(deviceFingerprint);

        _logger.LogInformation("IDENTITY: Retrieved user {UserId} (Name: '{DisplayName}') for device {DeviceFingerprint}",
            user.UserId, user.DisplayName ?? "null", deviceFingerprint);

        // DEBUG: Check if user has any memories
        var memoryCount = await _memoryService.GetMemoryCountForUserAsync(user.UserId);
        _logger.LogInformation("🧠 MEMORY DEBUG: User {UserId} has {MemoryCount} memories", user.UserId, memoryCount);

        // Enhanced identity matching and device fingerprint consolidation
        user = await HandleIdentityMatchingAndConsolidationAsync(user, userMessage, deviceFingerprint);

        return user;
    }

    /// <summary>
    /// Extract name from message using identity patterns
    /// SECURITY: Only extracts names when user is introducing THEMSELVES, never when asking about others
    /// </summary>
    private string? ExtractNameFromMessage(string userMessage)
    {
        // SECURITY: Prevent privacy leaks by ensuring we only extract self-identification
        // Patterns that could indicate asking about someone else (security check)
        var askingAboutOthersPatterns = new[]
        {
            @"vad heter\s+([A-ZÅÄÖ][a-zåäö]+)",  // "vad heter Anna"
            @"vet du\s+.*([A-ZÅÄÖ][a-zåäö]+)",   // "vet du var Anna"
            @"var.*([A-ZÅÄÖ][a-zåäö]+).*bor",    // "var bor Anna"
            @"var.*([A-ZÅÄÖ][a-zåäö]+).*bodde",  // "var bodde Anna"
        };

        // SECURITY: Don't extract names when it's about middle names - that should be handled by memory extraction
        var middleNamePatterns = new[]
        {
            @"jag heter\s+[A-ZÅÄÖ][a-zåäö]+\s+i\s+mellannamn",  // "jag heter Patrick i mellannamn"
            @"mitt mellannamn är\s+[A-ZÅÄÖ][a-zåäö]+",          // "mitt mellannamn är Patrick"
        };

        foreach (var pattern in askingAboutOthersPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(userMessage, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("SECURITY: Blocked name extraction from message asking about others: {Message}",
                    userMessage[..Math.Min(50, userMessage.Length)]);
                return null; // Don't extract names when asking about other people
            }
        }

        // Check if it's about middle names (should be handled by memory extraction, not name extraction)
        foreach (var pattern in middleNamePatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(userMessage, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                _logger.LogInformation("Skipping name extraction for middle name message: {Message}",
                    userMessage[..Math.Min(50, userMessage.Length)]);
                return null; // Don't extract names when it's about middle names
            }
        }

        // Only extract names for SELF-identification patterns
        var identityPatterns = new[]
        {
            @"jag heter\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*?)(?:\s+i\s+mellannamn|$)",  // Stop before "i mellannamn"
            @"det är\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)\s+igen",
            @"mitt namn är\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
            @"jag är\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)"
        };

        foreach (var pattern in identityPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(userMessage, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var extractedName = match.Groups[1].Value.Trim();
                _logger.LogInformation("SECURITY: Extracted self-identification name: {Name} from message: {Message}", 
                    extractedName, userMessage[..Math.Min(50, userMessage.Length)]);
                return extractedName;
            }
        }

        return null;
    }

    /// <summary>
    /// Enhanced identity matching and device fingerprint consolidation
    /// Supports multi-factor identification and automatic device consolidation
    /// </summary>
    private async Task<User> HandleIdentityMatchingAndConsolidationAsync(User currentUser, string userMessage, string deviceFingerprint)
    {
        // Extract identifying information from the message
        var identityFactors = ExtractIdentityFactors(userMessage);

        _logger.LogInformation("CONSOLIDATION: Extracted {Count} identity factors from message", identityFactors.Count);

        // If we have sufficient identity factors (at least 2), attempt consolidation
        if (identityFactors.Count >= 2)
        {
            var matchedUser = await FindUserByMultipleFactorsAsync(identityFactors);

            if (matchedUser != null && matchedUser.UserId != currentUser.UserId)
            {
                _logger.LogInformation("CONSOLIDATION: Found existing user {ExistingUserId} matching identity factors, consolidating with current user {CurrentUserId}",
                    matchedUser.UserId, currentUser.UserId);

                // Check if we should consolidate device fingerprints
                var recommendedFingerprint = await _memoryService.ConsolidateDeviceFingerprintAsync(deviceFingerprint, matchedUser.UserId);

                if (!string.IsNullOrEmpty(recommendedFingerprint))
                {
                    _logger.LogInformation("CONSOLIDATION: Device fingerprint consolidation recommended: {CurrentDevice} → {RecommendedDevice}",
                        deviceFingerprint, recommendedFingerprint);

                    // Note: In a real implementation, you would need to update the client's localStorage
                    // For now, we'll link the current device to the existing user
                }

                // Link current device to the existing user
                await _memoryService.LinkDeviceToUserAsync(deviceFingerprint, matchedUser.UserId);

                return matchedUser;
            }
        }

        // Fallback to single-factor name-based identification for new users
        if (string.IsNullOrEmpty(currentUser.DisplayName))
        {
            var extractedName = ExtractNameFromMessage(userMessage);
            if (!string.IsNullOrEmpty(extractedName))
            {
                _logger.LogInformation("IDENTITY: Extracted name '{Name}' from message: {Message}",
                    extractedName, userMessage[..Math.Min(50, userMessage.Length)]);

                // Try to find existing user with this name
                var existingUser = await _memoryService.FindUserByNameAsync(extractedName);

                if (existingUser != null)
                {
                    _logger.LogInformation("IDENTITY: Found existing user {UserId} with name '{Name}' for device {DeviceFingerprint}",
                        existingUser.UserId, extractedName, deviceFingerprint);

                    // Link this device to the existing user
                    await _memoryService.LinkDeviceToUserAsync(deviceFingerprint, existingUser.UserId);

                    return existingUser;
                }

                // If no existing user found, update the new user's display name
                currentUser.DisplayName = extractedName;
                await _memoryService.UpdateUserAsync(currentUser);

                _logger.LogInformation("IDENTITY: Set display name '{Name}' for new user {UserId} on device {DeviceFingerprint}",
                    extractedName, currentUser.UserId, deviceFingerprint);
            }
            else
            {
                _logger.LogInformation("IDENTITY: No name extracted from message for device {DeviceFingerprint}", deviceFingerprint);
            }
        }

        return currentUser;
    }

    /// <summary>
    /// Extract multiple identity factors from a message for enhanced matching
    /// </summary>
    private List<IdentityFactor> ExtractIdentityFactors(string message)
    {
        var factors = new List<IdentityFactor>();

        // Extract full name (first + last name)
        var fullNamePatterns = new[]
        {
            @"jag heter\s+([A-ZÅÄÖ][a-zåäö]+\s+[A-ZÅÄÖ][a-zåäö]+)",
            @"det är\s+([A-ZÅÄÖ][a-zåäö]+\s+[A-ZÅÄÖ][a-zåäö]+)\s+igen",
            @"mitt namn är\s+([A-ZÅÄÖ][a-zåäö]+\s+[A-ZÅÄÖ][a-zåäö]+)",
            @"jag är\s+([A-ZÅÄÖ][a-zåäö]+\s+[A-ZÅÄÖ][a-zåäö]+)"
        };

        foreach (var pattern in fullNamePatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var fullName = match.Groups[1].Value.Trim();
                var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (nameParts.Length >= 2)
                {
                    factors.Add(new IdentityFactor { Type = "FirstName", Value = nameParts[0] });
                    factors.Add(new IdentityFactor { Type = "LastName", Value = nameParts[1] });
                    factors.Add(new IdentityFactor { Type = "FullName", Value = fullName });
                }
                break;
            }
        }

        // Extract location information
        var locationPatterns = new[]
        {
            @"jag bor i\s+([A-ZÅÄÖ][a-zåäö]+)",
            @"från\s+([A-ZÅÄÖ][a-zåäö]+)",
            @"i\s+([A-ZÅÄÖ][a-zåäö]+)\s+igen"
        };

        foreach (var pattern in locationPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                factors.Add(new IdentityFactor { Type = "Location", Value = match.Groups[1].Value.Trim() });
                break;
            }
        }

        return factors;
    }

    /// <summary>
    /// Find user by multiple identity factors for enhanced matching
    /// </summary>
    private async Task<User?> FindUserByMultipleFactorsAsync(List<IdentityFactor> factors)
    {
        if (factors.Count < 2)
            return null;

        try
        {
            // Build search query from identity factors
            var searchTerms = factors.Select(f => f.Value).ToList();
            var searchQuery = string.Join(" ", searchTerms);

            // Search for users with matching identity memories
            var identityMemories = await _memoryService.RetrieveMemoriesAsync(
                searchQuery,
                MemoryType.Identity,
                0.85, // High threshold for identity matching
                10);

            // Group memories by owner and count matches
            var userMatches = identityMemories
                .Where(m => !string.IsNullOrEmpty(m.OwnerUserId))
                .GroupBy(m => m.OwnerUserId)
                .Select(g => new { UserId = g.Key, MatchCount = g.Count() })
                .OrderByDescending(x => x.MatchCount)
                .ToList();

            // Find the user with the most matching factors
            foreach (var match in userMatches)
            {
                if (string.IsNullOrEmpty(match.UserId))
                    continue;

                var user = await _memoryService.GetUserByIdAsync(match.UserId);

                if (user != null)
                {
                    // Verify that at least 2 factors match
                    var matchingFactors = 0;
                    foreach (var factor in factors)
                    {
                        var hasMatchingMemory = identityMemories.Any(m =>
                            m.OwnerUserId == user.UserId &&
                            m.Content.Contains(factor.Value, StringComparison.OrdinalIgnoreCase));

                        if (hasMatchingMemory)
                            matchingFactors++;
                    }

                    if (matchingFactors >= 2)
                    {
                        _logger.LogInformation("CONSOLIDATION: Found user {UserId} with {MatchCount} matching identity factors",
                            user.UserId, matchingFactors);
                        return user;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find user by multiple factors");
            return null;
        }
    }







    /// <summary>
    /// Clean up processed files from database and disk
    /// </summary>
    private async Task CleanupProcessedFilesAsync(List<Guid> fileIds)
    {
        try
        {
            foreach (var fileId in fileIds)
            {
                var file = await _filesDbContext.Files.FindAsync(fileId);
                if (file != null)
                {
                    // Delete physical file
                    if (File.Exists(file.FilePath))
                    {
                        try
                        {
                            File.Delete(file.FilePath);
                            _logger.LogInformation("Deleted physical file: {FilePath}", file.FilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete physical file: {FilePath}", file.FilePath);
                        }
                    }

                    // Remove from database
                    _filesDbContext.Files.Remove(file);

                    // Also remove any processing results
                    var processingResults = _filesDbContext.ProcessingResults.Where(pr => pr.FileId == fileId);
                    _filesDbContext.ProcessingResults.RemoveRange(processingResults);
                }
            }

            await _filesDbContext.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {FileCount} processed files from database", fileIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup processed files");
        }
    }



    /// <summary>
    /// Store processing result in database for future use
    /// </summary>
    private async Task StoreProcessingResultAsync(Guid fileId, string result)
    {
        try
        {
            var processingResult = new ProcessingResult
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                Result = result,
                Status = ProcessingStatus.Completed,
                ProcessedAt = DateTime.UtcNow
            };

            _filesDbContext.ProcessingResults.Add(processingResult);
            await _filesDbContext.SaveChangesAsync();

            _logger.LogInformation("Stored processing result for file ID: {FileId}", fileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store processing result for file ID: {FileId}", fileId);
        }
    }

    /// <summary>
    /// Identity factor for multi-factor user matching
    /// </summary>
    private class IdentityFactor
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
