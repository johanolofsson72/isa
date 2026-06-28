using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Agent.Models;
using Agent.Services;
using Agent.Services.Interfaces;

namespace Agent.Endpoints;

/// <summary>
/// REST API endpoints for chat functionality
/// </summary>
public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var chatGroup = app.MapGroup("/api/chat")
            .WithTags("Chat")
            .WithOpenApi();

        chatGroup.MapPost("/", SendMessage)
            .WithName("SendChatMessage")
            .WithSummary("Send a message to the chat agent")
            .Produces<ChatResponse>()
            .ProducesValidationProblem();

        chatGroup.MapDelete("/session/{sessionId}", DeleteSession)
            .WithName("DeleteChatSession")
            .WithSummary("Delete a chat session")
            .Produces(204)
            .Produces(404);

        chatGroup.MapGet("/health", HealthCheck)
            .WithName("ChatHealthCheck")
            .WithSummary("Check chat service health")
            .Produces<object>();
    }

    private static async Task<IResult> SendMessage(
        [FromBody] ChatRequest request,
        [FromServices] IChatSessionService sessionService,
        [FromServices] IChatExecutionService chatExecutionService,
        [FromServices] IAuditLogService auditLogService,
        [FromServices] ILogger<ChatRequest> logger)
    {
        var stopwatch = Stopwatch.StartNew();
        string? response = null;
        string? errorMessage = null;

        try
        {
            // Generate session ID if not provided
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            // Get or create chat session
            var chatHistory = await sessionService.GetOrCreateSessionAsync(
                sessionId, 
                request.DeviceFingerprint);

            logger.LogInformation(
                "Received chat message from session {SessionId} (device: {DeviceFingerprint}): {Message}",
                sessionId,
                request.DeviceFingerprint ?? "unknown",
                request.Message
            );

            // Execute chat using ChatExecutionService for memory integration
            response = await chatExecutionService.ExecuteChatAsync(
                chatHistory,
                request.Message,
                sessionId,
                request.DeviceFingerprint);

            // Trim chat history if needed to prevent token limit issues
            TrimChatHistoryIfNeeded(chatHistory, sessionId, logger);

            stopwatch.Stop();

            var chatResponse = new ChatResponse
            {
                Message = response ?? string.Empty,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                HasError = false,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };

            return Results.Ok(chatResponse);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            logger.LogError(
                ex,
                "Error processing chat message from session {SessionId}",
                request.SessionId
            );
            
            errorMessage = ex.Message;
            response = "I apologize, but I encountered an error processing your request. Please try again.";

            var errorResponse = new ChatResponse
            {
                Message = response,
                SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                HasError = true,
                ErrorMessage = errorMessage,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };

            return Results.Ok(errorResponse); // Return 200 with error info instead of 500
        }
        finally
        {
            // Log the interaction
            await auditLogService.LogChatInteractionAsync(
                request.SessionId ?? "unknown",
                request.Message,
                response,
                errorMessage,
                stopwatch.ElapsedMilliseconds
            );
        }
    }

    private static IResult DeleteSession(
        string sessionId,
        [FromServices] IChatSessionService sessionService,
        [FromServices] ILogger<string> logger)
    {
        try
        {
            sessionService.RemoveSession(sessionId);
            logger.LogInformation("Deleted chat session: {SessionId}", sessionId);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting chat session: {SessionId}", sessionId);
            return Results.NotFound();
        }
    }

    private static IResult HealthCheck()
    {
        return Results.Ok(new
        {
            Status = "Healthy",
            Service = "Chat API",
            Timestamp = DateTime.UtcNow
        });
    }

    private static void TrimChatHistoryIfNeeded(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory, 
        string sessionId, 
        ILogger logger)
    {
        const int maxMessages = 50; // Keep last 50 messages
        
        if (chatHistory.Count > maxMessages)
        {
            var messagesToRemove = chatHistory.Count - maxMessages;
            for (int i = 0; i < messagesToRemove; i++)
            {
                chatHistory.RemoveAt(0); // Remove oldest messages
            }
            
            logger.LogInformation(
                "Trimmed {Count} messages from chat history for session {SessionId}",
                messagesToRemove,
                sessionId
            );
        }
    }
}
