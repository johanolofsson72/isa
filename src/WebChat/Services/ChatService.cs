using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Models;
using WebChat.Services.Interfaces;
using Shared.Services;

namespace WebChat.Services;

/// <summary>
/// Service for chat functionality using REST API
/// </summary>
public class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IDeviceFingerprintService _deviceService;
    private string? _sessionId;

    // Events for message updates
    public event Action<ChatMessageModel>? OnMessageReceived;
    public event Action<string>? OnError;
    public event Action? OnConnected;

    public bool IsConnected { get; private set; } = true; // Always connected for REST API

    public ChatService(IHttpClientFactory httpClientFactory, ILogger<ChatService> logger, IDeviceFingerprintService deviceService)
    {
        _httpClient = httpClientFactory.CreateClient("agent");
        _logger = logger;
        _deviceService = deviceService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Initialize the chat service
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Test connection to chat API
            var response = await _httpClient.GetAsync("/api/chat/health");
            if (response.IsSuccessStatusCode)
            {
                IsConnected = true;
                OnConnected?.Invoke();
                _logger.LogInformation("Chat service connected successfully");
            }
            else
            {
                IsConnected = false;
                OnError?.Invoke("Could not connect to chat service");
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            _logger.LogError(ex, "Error initializing chat connection");
            OnError?.Invoke("Could not initialize chat connection");
        }
    }

    /// <summary>
    /// Connect to the chat service
    /// </summary>
    public async Task ConnectAsync()
    {
        await InitializeAsync();
    }

    /// <summary>
    /// Send a message to the chat
    /// </summary>
    public async Task<bool> SendMessageAsync(string message)
    {
        _logger.LogInformation("🔍 CHAT SERVICE DEBUG: SendMessageAsync called with message: '{Message}'", message);
        _logger.LogInformation("🔍 CHAT SERVICE DEBUG: IsConnected = {IsConnected}", IsConnected);

        if (!IsConnected)
        {
            _logger.LogWarning("Attempted to send message while disconnected");
            await ConnectAsync();

            if (!IsConnected)
            {
                OnError?.Invoke("Cannot send message - not connected to chat server");
                return false;
            }
        }

        try
        {
            var deviceFingerprint = await GetDeviceFingerprintAsync();
            var sessionId = await GetOrCreateSessionIdAsync();

            _logger.LogInformation("🔍 DEVICE DEBUG: ChatService sending message with device fingerprint: {DeviceFingerprint}, session: {SessionId}",
                deviceFingerprint, sessionId);

            var request = new ChatRequest
            {
                Message = message,
                SessionId = sessionId,
                DeviceFingerprint = deviceFingerprint
            };

            _logger.LogInformation("🔍 CHAT SERVICE DEBUG: About to POST to /api/chat with request: Message='{Message}', SessionId='{SessionId}', DeviceFingerprint='{DeviceFingerprint}'",
                request.Message, request.SessionId, request.DeviceFingerprint);

            var response = await _httpClient.PostAsJsonAsync("/api/chat", request, _jsonOptions);

            _logger.LogInformation("🔍 CHAT SERVICE DEBUG: POST response received - StatusCode: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(_jsonOptions);
                if (chatResponse != null)
                {
                    _logger.LogInformation("🔍 CHAT SERVICE DEBUG: Successfully received chat response: '{Response}'", chatResponse.Message);

                    // Note: We don't update _sessionId here anymore since we use deterministic session IDs
                    // based on device fingerprint for persistent sessions across WebChat restarts

                    // Create ChatMessageModel for compatibility
                    var messageModel = new ChatMessageModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = chatResponse.Message,
                        IsUser = false,
                        Timestamp = chatResponse.Timestamp
                    };

                    // Trigger the message received event
                    OnMessageReceived?.Invoke(messageModel);
                    return true;
                }
                else
                {
                    _logger.LogError("🔍 CHAT SERVICE DEBUG: chatResponse was null");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Chat API returned error: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
                OnError?.Invoke("Failed to send message - server error");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to chat API");
            OnError?.Invoke("Failed to send message");
            return false;
        }

        return false;
    }

    private async Task<string> GetDeviceFingerprintAsync()
    {
        // Use shared DeviceFingerprintService for proper cookie-based device fingerprinting
        return await _deviceService.GetOrCreateDeviceFingerprintAsync();
    }

    /// <summary>
    /// Get or create session ID based on device fingerprint for persistent sessions
    /// </summary>
    private async Task<string> GetOrCreateSessionIdAsync()
    {
        if (!string.IsNullOrEmpty(_sessionId))
        {
            return _sessionId;
        }

        try
        {
            var deviceFingerprint = await GetDeviceFingerprintAsync();

            // Create deterministic session ID based on device fingerprint
            // This ensures same device always gets same session ID
            var sessionKey = $"session_{deviceFingerprint}";
            _sessionId = sessionKey;

            _logger.LogInformation("🔍 SESSION DEBUG: Created/retrieved session ID: {SessionId} for device: {DeviceFingerprint}",
                _sessionId, deviceFingerprint);

            return _sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create session ID, using fallback");
            _sessionId = $"session_{Guid.NewGuid()}";
            return _sessionId;
        }
    }
}

/// <summary>
/// Request model for chat API
/// </summary>
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? DeviceFingerprint { get; set; }
}

/// <summary>
/// Response model from chat API
/// </summary>
public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public long ProcessingTimeMs { get; set; }
}
