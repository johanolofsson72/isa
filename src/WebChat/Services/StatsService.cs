using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WebChat.Services;

public class StatsService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StatsService> _logger;

    public StatsService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<StatsService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public class RealtimeStats
    {
        public int CurrentMessageChars { get; set; }
        public int CurrentMessageBytes { get; set; }
        public int TokenUsageApprox { get; set; }
        public int SystemPromptChars { get; set; }
        public int PluginFunctions { get; set; }
        public int PluginCount { get; set; }
        public int UsersJsonBytes { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public async Task<(int systemPromptChars, int pluginCount, int functionCount)> GetSystemPromptStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("agent");
            var resp = await client.GetAsync("/api/settings/systemprompt/stats", ct);
            if (!resp.IsSuccessStatusCode)
                return (0, 0, 0);

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            int coreLen = doc.RootElement.GetProperty("coreInstructionsLength").GetInt32();
            int pluginCount = doc.RootElement.GetProperty("pluginCount").GetInt32();
            int functionCount = doc.RootElement.GetProperty("functionCount").GetInt32();
            return (coreLen, pluginCount, functionCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get system prompt stats");
            return (0, 0, 0);
        }
    }

    public async Task<int> GetUsersJsonSizeBytesAsync(CancellationToken ct = default)
    {
        try
        {
            // Agent exposes users via /api/settings/users
            var client = _httpClientFactory.CreateClient("agent");
            var resp = await client.GetAsync("/api/settings/users", ct);
            if (!resp.IsSuccessStatusCode)
                return 0;
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            return bytes.Length;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "users.json size not available; returning 0");
            return 0;
        }
    }

    public async Task<(int descChars, int descBytes)> GetPluginDescriptionsSizeAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("agent");
            var resp = await client.GetAsync("/api/settings/plugins", ct);
            if (!resp.IsSuccessStatusCode)
                return (0, 0);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            int charSum = 0;
            void AddStringLen(JsonElement e)
            {
                if (e.ValueKind == JsonValueKind.String)
                {
                    var s = e.GetString() ?? string.Empty;
                    charSum += s.Length;
                }
            }
            if (doc.RootElement.TryGetProperty("plugins", out var pluginsEl) && pluginsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pluginsEl.EnumerateArray())
                {
                    if (p.TryGetProperty("description", out var d)) AddStringLen(d);
                    if (p.TryGetProperty("functions", out var funcs) && funcs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var f in funcs.EnumerateArray())
                        {
                            if (f.TryGetProperty("description", out var fd)) AddStringLen(fd);
                        }
                    }
                }
            }
            var bytes = System.Text.Encoding.UTF8.GetByteCount(new string('a', Math.Max(charSum, 0)));
            // The above approximates bytes ~ chars for ASCII; to be more accurate we'd need the actual concatenated string.
            return (charSum, bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute plugin descriptions size");
            return (0, 0);
        }
    }

    public static (int chars, int bytes) MeasureString(string s)
    {
        if (s == null) return (0, 0);
        var chars = s.Length;
        var bytes = System.Text.Encoding.UTF8.GetByteCount(s);
        return (chars, bytes);
    }

    public static int ApproxTokensFromChars(int chars)
    {
        // Updated heuristic: 1 token ≈ 4 chars (as requested by user)
        if (chars <= 0) return 0;
        return (int)Math.Round(chars / 4.0);
    }

    /// <summary>
    /// Get plugin usage statistics for the current session
    /// </summary>
    public async Task<List<PluginUsageStats>> GetPluginUsageStatsAsync(string? sessionId = null, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("agent");
            var url = "/api/stats/plugin-usage";
            if (!string.IsNullOrEmpty(sessionId))
            {
                url += $"?sessionId={Uri.EscapeDataString(sessionId)}";
            }

            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                return new List<PluginUsageStats>();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var stats = JsonSerializer.Deserialize<List<PluginUsageStats>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return stats ?? new List<PluginUsageStats>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get plugin usage stats");
            return new List<PluginUsageStats>();
        }
    }

    /// <summary>
    /// Get chat history size statistics
    /// </summary>
    public async Task<ChatHistoryStats> GetChatHistoryStatsAsync(string? sessionId = null, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("agent");
            var url = "/api/stats/chat-history";
            if (!string.IsNullOrEmpty(sessionId))
            {
                url += $"?sessionId={Uri.EscapeDataString(sessionId)}";
            }

            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                return new ChatHistoryStats();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var stats = JsonSerializer.Deserialize<ChatHistoryStats>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return stats ?? new ChatHistoryStats();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get chat history stats");
            return new ChatHistoryStats();
        }
    }
}

/// <summary>
/// Plugin usage statistics model
/// </summary>
public class PluginUsageStats
{
    public string PluginName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public int CallCount { get; set; }
    public DateTime LastUsed { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Chat history size statistics model
/// </summary>
public class ChatHistoryStats
{
    public int MessageCount { get; set; }
    public int TotalCharacters { get; set; }
    public int EstimatedTokens { get; set; }
    public DateTime LastMessageTime { get; set; }
}

