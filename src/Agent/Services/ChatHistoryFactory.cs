using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Agent.Services;

/// <summary>
/// Factory service responsible for creating isolated ChatHistory instances for conversations and workflows.
/// Ensures proper isolation between different sessions and loads fresh system prompts.
/// </summary>
public interface IChatHistoryFactory
{
    /// <summary>
    /// Creates a new ChatHistory instance for a webchat conversation with system prompt
    /// </summary>
    Task<ChatHistory> CreateWebChatHistoryAsync(string? deviceFingerprint = null);

    /// <summary>
    /// Creates a new ChatHistory instance for a workflow execution with system prompt
    /// </summary>
    Task<ChatHistory> CreateWorkflowHistoryAsync(string? deviceFingerprint = null);

    /// <summary>
    /// Creates a new ChatHistory instance with custom system prompt
    /// </summary>
    Task<ChatHistory> CreateCustomHistoryAsync(string systemPrompt);
    
    /// <summary>
    /// Adds a sanitized user message to the history
    /// </summary>
    Task AddUserMessageAsync(ChatHistory history, string message);
    
    /// <summary>
    /// Adds a sanitized system message to the history
    /// </summary>
    Task AddSystemMessageAsync(ChatHistory history, string message);
    
    /// <summary>
    /// Adds an assistant message to the history (no sanitization needed for AI responses)
    /// </summary>
    Task AddAssistantMessageAsync(ChatHistory history, string message);
}

public class ChatHistoryFactory : IChatHistoryFactory
{
    private readonly IConfiguration _configuration;
    private readonly IMessageSanitizationService _sanitizationService;
    private readonly ILogger<ChatHistoryFactory> _logger;
    private readonly IDynamicSystemPromptGenerator _systemPromptGenerator;
    private readonly IUserNotesService _userNotesService;

    public ChatHistoryFactory(
        IConfiguration configuration,
        IMessageSanitizationService sanitizationService,
        ILogger<ChatHistoryFactory> logger,
        IDynamicSystemPromptGenerator systemPromptGenerator,
        IUserNotesService userNotesService)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _sanitizationService = sanitizationService ?? throw new ArgumentNullException(nameof(sanitizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemPromptGenerator = systemPromptGenerator ?? throw new ArgumentNullException(nameof(systemPromptGenerator));
        _userNotesService = userNotesService ?? throw new ArgumentNullException(nameof(userNotesService));
    }

    public async Task<ChatHistory> CreateWebChatHistoryAsync(string? deviceFingerprint = null)
    {
        var systemPrompt = await LoadSystemPromptAsync(deviceFingerprint);
        var history = new ChatHistory();

        // Add sanitized system prompt
        var sanitizedPrompt = await _sanitizationService.SanitizeSystemMessageAsync(systemPrompt);
        history.AddSystemMessage(sanitizedPrompt);

        _logger.LogInformation("Created new WebChat ChatHistory with system prompt ({Length} chars) for device {DeviceFingerprint}",
            sanitizedPrompt.Length, deviceFingerprint ?? "unknown");

        return history;
    }

    public async Task<ChatHistory> CreateWorkflowHistoryAsync(string? deviceFingerprint = null)
    {
        // 🎯 CRITICAL FIX: Use same system prompt loading as WebChat to get users.json data
        var systemPrompt = await LoadSystemPromptAsync(deviceFingerprint);
        var history = new ChatHistory();

        // Add sanitized system prompt
        var sanitizedPrompt = await _sanitizationService.SanitizeSystemMessageAsync(systemPrompt);
        history.AddSystemMessage(sanitizedPrompt);

        _logger.LogInformation("Created new Workflow ChatHistory with system prompt ({Length} chars) for device {DeviceFingerprint}",
            sanitizedPrompt.Length, deviceFingerprint ?? "unknown");

        return history;
    }

    public async Task<ChatHistory> CreateCustomHistoryAsync(string systemPrompt)
    {
        var history = new ChatHistory();
        
        // Add sanitized system prompt
        var sanitizedPrompt = await _sanitizationService.SanitizeSystemMessageAsync(systemPrompt);
        history.AddSystemMessage(sanitizedPrompt);
        
        _logger.LogInformation("Created new Custom ChatHistory with system prompt ({Length} chars)", 
            sanitizedPrompt.Length);
        
        return history;
    }

    public async Task AddUserMessageAsync(ChatHistory history, string message)
    {
        if (history == null) throw new ArgumentNullException(nameof(history));
        
        var sanitizedMessage = await _sanitizationService.SanitizeUserMessageAsync(message);
        history.AddUserMessage(sanitizedMessage);
        
        _logger.LogDebug("Added sanitized user message to ChatHistory ({Length} chars)", 
            sanitizedMessage.Length);
    }

    public async Task AddSystemMessageAsync(ChatHistory history, string message)
    {
        if (history == null) throw new ArgumentNullException(nameof(history));
        
        var sanitizedMessage = await _sanitizationService.SanitizeSystemMessageAsync(message);
        history.AddSystemMessage(sanitizedMessage);
        
        _logger.LogDebug("Added sanitized system message to ChatHistory ({Length} chars)", 
            sanitizedMessage.Length);
    }

    public async Task AddAssistantMessageAsync(ChatHistory history, string message)
    {
        if (history == null) throw new ArgumentNullException(nameof(history));
        
        // Assistant messages don't need sanitization as they come from the AI
        history.AddAssistantMessage(message ?? string.Empty);
        
        _logger.LogDebug("Added assistant message to ChatHistory ({Length} chars)", 
            (message ?? string.Empty).Length);
        
        await Task.CompletedTask; // For async consistency
    }

    private async Task<string> LoadSystemPromptAsync(string? deviceFingerprint = null)
    {
        try
        {
            // Use dynamic system prompt generator to combine static content with plugin sections
            var systemPrompt = await _systemPromptGenerator.GenerateSystemPromptAsync();

            // Add user notes if available
            if (!string.IsNullOrEmpty(deviceFingerprint))
            {
                var userNotes = await _userNotesService.GetUserNotesAsync(deviceFingerprint);
                if (!string.IsNullOrEmpty(userNotes))
                {
                    systemPrompt += $"\n\n# User Information\nHär är viktig information om användaren som du bör komma ihåg och använda för att personalisera konversationen:\n{userNotes}";

                    _logger.LogInformation("Added user notes to system prompt for device {DeviceFingerprint}", deviceFingerprint);
                }
            }

            _logger.LogInformation("Generated dynamic system prompt ({Length} chars) for device {DeviceFingerprint}",
                systemPrompt.Length, deviceFingerprint ?? "unknown");

            return systemPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dynamic system prompt, using fallback");
            return "You are a helpful AI assistant.";
        }
    }
}
