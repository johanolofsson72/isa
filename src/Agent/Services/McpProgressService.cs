using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Agent.Services
{
    public interface IMcpProgressService
    {
        Task<string> StartProgressTrackingAsync(string sessionId, string operation);
        Task UpdateProgressAsync(string progressId, string status, int percentage = 0);
        Task CompleteProgressAsync(string progressId, string result);
        Task<string?> GetProgressStatusAsync(string progressId);
    }

    public class McpProgressService : IMcpProgressService
    {
        private readonly ILogger<McpProgressService> _logger;
        private readonly ConcurrentDictionary<string, ProgressInfo> _progressTracking = new();

        public McpProgressService(ILogger<McpProgressService> logger)
        {
            _logger = logger;
        }

        public Task<string> StartProgressTrackingAsync(string sessionId, string operation)
        {
            var progressId = Guid.NewGuid().ToString();
            var progressInfo = new ProgressInfo
            {
                SessionId = sessionId,
                Operation = operation,
                Status = "Startar...",
                Percentage = 0,
                StartTime = DateTime.UtcNow,
                IsCompleted = false
            };

            _progressTracking[progressId] = progressInfo;
            _logger.LogInformation("🔄 MCP Progress: Started tracking {Operation} for session {SessionId}", 
                operation, sessionId);

            return Task.FromResult(progressId);
        }

        public Task UpdateProgressAsync(string progressId, string status, int percentage = 0)
        {
            if (_progressTracking.TryGetValue(progressId, out var progressInfo))
            {
                progressInfo.Status = status;
                progressInfo.Percentage = Math.Clamp(percentage, 0, 100);
                progressInfo.LastUpdated = DateTime.UtcNow;

                _logger.LogInformation("📊 MCP Progress: {ProgressId} - {Status} ({Percentage}%)",
                    progressId, status, percentage);
            }
            return Task.CompletedTask;
        }

        public Task CompleteProgressAsync(string progressId, string result)
        {
            if (_progressTracking.TryGetValue(progressId, out var progressInfo))
            {
                progressInfo.Status = "Slutförd";
                progressInfo.Percentage = 100;
                progressInfo.Result = result;
                progressInfo.IsCompleted = true;
                progressInfo.CompletedTime = DateTime.UtcNow;

                _logger.LogInformation("✅ MCP Progress: {ProgressId} completed in {Duration}ms", 
                    progressId, (progressInfo.CompletedTime - progressInfo.StartTime)?.TotalMilliseconds);

                // Clean up after 5 minutes
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                    _progressTracking.TryRemove(progressId, out var _));
            }
            return Task.CompletedTask;
        }

        public Task<string?> GetProgressStatusAsync(string progressId)
        {
            if (_progressTracking.TryGetValue(progressId, out var progressInfo))
            {
                if (progressInfo.IsCompleted)
                {
                    return Task.FromResult<string?>(progressInfo.Result);
                }
                else
                {
                    return Task.FromResult<string?>($"{progressInfo.Status} ({progressInfo.Percentage}%)");
                }
            }
            return Task.FromResult<string?>(null);
        }

        private class ProgressInfo
        {
            public string SessionId { get; set; } = string.Empty;
            public string Operation { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int Percentage { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? LastUpdated { get; set; }
            public DateTime? CompletedTime { get; set; }
            public bool IsCompleted { get; set; }
            public string? Result { get; set; }
        }
    }
}
