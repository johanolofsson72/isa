using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;

namespace Agent.Services
{
    public interface IMcpStreamingService
    {
        Task<string> ProcessStreamingChatAsync(
            ChatHistory history, 
            Kernel kernel, 
            string sessionId,
            CancellationToken cancellationToken = default);
    }

    public class McpStreamingService : IMcpStreamingService
    {
        private readonly ILogger<McpStreamingService> _logger;
        private readonly IMcpProgressService _progressService;

        public McpStreamingService(
            ILogger<McpStreamingService> logger,
            IMcpProgressService progressService)
        {
            _logger = logger;
            _progressService = progressService;
        }

        public async Task<string> ProcessStreamingChatAsync(
            ChatHistory history, 
            Kernel kernel, 
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var progressId = await _progressService.StartProgressTrackingAsync(sessionId, "Chat Processing");
            
            try
            {
                await _progressService.UpdateProgressAsync(progressId, "Förbereder AI-modell...", 10);

                var chatService = kernel.GetRequiredService<IChatCompletionService>();
                
                // Configure execution settings for better plugin orchestration
                var executionSettings = new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 4000,
                    Temperature = 0.1,
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                await _progressService.UpdateProgressAsync(progressId, "Bearbetar förfrågan...", 30);

                // Get streaming response
                var responseBuilder = new List<string>();
                var streamingResult = chatService.GetStreamingChatMessageContentsAsync(
                    history, 
                    executionSettings, 
                    kernel, 
                    cancellationToken);

                await _progressService.UpdateProgressAsync(progressId, "Genererar svar...", 50);

                await foreach (var content in streamingResult.WithCancellation(cancellationToken))
                {
                    if (!string.IsNullOrEmpty(content.Content))
                    {
                        responseBuilder.Add(content.Content);
                    }
                }

                await _progressService.UpdateProgressAsync(progressId, "Slutför bearbetning...", 90);

                var fullResponse = string.Join("", responseBuilder);
                
                // If streaming didn't work well, fall back to regular completion
                if (string.IsNullOrWhiteSpace(fullResponse))
                {
                    _logger.LogInformation("🔄 MCP Streaming: Falling back to regular completion");
                    var result = await chatService.GetChatMessageContentAsync(
                        history, 
                        executionSettings, 
                        kernel, 
                        cancellationToken);
                    fullResponse = result.Content ?? "";
                }

                await _progressService.CompleteProgressAsync(progressId, fullResponse);
                
                _logger.LogInformation("✅ MCP Streaming: Completed chat processing for session {SessionId}", sessionId);
                return fullResponse;
            }
            catch (OperationCanceledException)
            {
                await _progressService.UpdateProgressAsync(progressId, "Avbruten på grund av timeout", 0);
                _logger.LogWarning("🕐 MCP Streaming: Operation cancelled for session {SessionId}", sessionId);
                throw;
            }
            catch (Exception ex)
            {
                await _progressService.UpdateProgressAsync(progressId, $"Fel: {ex.Message}", 0);
                _logger.LogError(ex, "❌ MCP Streaming: Error processing chat for session {SessionId}", sessionId);
                throw;
            }
        }
    }
}
