using Agent.Services.Interfaces;
using Agent.Models;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Extensions;

/// <summary>
/// Extension methods for memory system API endpoints
/// </summary>
public static class MemoryEndpointExtensions
{
    /// <summary>
    /// Maps memory system endpoints
    /// </summary>
    public static WebApplication MapMemoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/memory")
            .WithTags("Memory System")
            .WithOpenApi();

        // GET: Generate system prompt for device
        group.MapGet("/system-prompt", async (
            [FromQuery] string? deviceFingerprint,
            IProductionMemoryService memoryService) =>
        {
            try
            {
                var systemPrompt = await memoryService.GenerateSystemPromptAsync(deviceFingerprint);
                return Results.Ok(new { systemPrompt });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to generate system prompt: {ex.Message}");
            }
        })
        .WithName("GenerateSystemPrompt")
        .WithSummary("Generate memory-enhanced system prompt")
        .WithDescription("Generates a system prompt with user's memory context based on device fingerprint");

        // POST: Store memory entry
        group.MapPost("/store", async (
            [FromBody] MemoryEntry entry,
            IProductionMemoryService memoryService) =>
        {
            try
            {
                await memoryService.StoreMemoryAsync(entry);
                return Results.Ok(new { message = "Memory stored successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to store memory: {ex.Message}");
            }
        })
        .WithName("StoreMemory")
        .WithSummary("Store a memory entry")
        .WithDescription("Stores a memory entry with automatic embedding generation");

        // GET: Retrieve memories by query
        group.MapGet("/search", async (
            [FromQuery] string query,
            [FromQuery] MemoryType? type,
            IProductionMemoryService memoryService,
            [FromQuery] double threshold = 0.80,
            [FromQuery] int topK = 5,
            [FromQuery] string? userId = null) =>
        {
            try
            {
                if (type == null)
                {
                    return Results.BadRequest("Memory type is required");
                }

                var memories = await memoryService.RetrieveMemoriesAsync(query, type.Value, threshold, topK, userId);
                return Results.Ok(memories);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to retrieve memories: {ex.Message}");
            }
        })
        .WithName("SearchMemories")
        .WithSummary("Search memories by query")
        .WithDescription("Retrieves memories using semantic search with similarity thresholds");

        // POST: Trigger memory cleanup
        group.MapPost("/cleanup", async (IProductionMemoryService memoryService) =>
        {
            try
            {
                await memoryService.ConsolidateAndCleanupAsync();
                return Results.Ok(new { message = "Memory cleanup completed" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to cleanup memories: {ex.Message}");
            }
        })
        .WithName("CleanupMemories")
        .WithSummary("Trigger memory cleanup")
        .WithDescription("Consolidates and cleans up expired memories");

        // User management endpoints
        group.MapGet("/user", async (
            [FromQuery] string deviceFingerprint,
            IProductionMemoryService memoryService) =>
        {
            try
            {
                var user = await memoryService.GetOrCreateUserAsync(deviceFingerprint);

                // Create a DTO to avoid circular references
                var userDto = new
                {
                    UserId = user.UserId,
                    DisplayName = user.DisplayName,
                    DeviceId = deviceFingerprint,
                    CreatedAt = user.CreatedAt,
                    LastSeenAt = user.LastSeenAt
                };

                return Results.Ok(userDto);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get user: {ex.Message}");
            }
        })
        .WithName("GetOrCreateUser")
        .WithSummary("Get or create user by device fingerprint")
        .WithDescription("Gets an existing user or creates a new one based on device fingerprint");

        // Consent management endpoints
        group.MapGet("/consent/{userId}/{consentKind}", async (
            string userId,
            string consentKind,
            IProductionMemoryService memoryService) =>
        {
            try
            {
                var hasConsent = await memoryService.HasConsentAsync(userId, consentKind);
                return Results.Ok(new { hasConsent });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to check consent: {ex.Message}");
            }
        })
        .WithName("CheckConsent")
        .WithSummary("Check user consent")
        .WithDescription("Checks if user has granted specific consent");

        group.MapPost("/consent", async (
            [FromBody] ConsentRequest request,
            IProductionMemoryService memoryService) =>
        {
            try
            {
                await memoryService.UpdateConsentAsync(request.UserId, request.ConsentKind, request.Granted);
                return Results.Ok(new { message = "Consent updated successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update consent: {ex.Message}");
            }
        })
        .WithName("UpdateConsent")
        .WithSummary("Update user consent")
        .WithDescription("Updates user consent for specific operations");

        // Runtime state endpoints
        group.MapGet("/runtime-state/{userId}", async (
            string userId,
            IProductionMemoryService memoryService) =>
        {
            try
            {
                var state = await memoryService.GetOrCreateUserRuntimeStateAsync(userId);
                return Results.Ok(state);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get runtime state: {ex.Message}");
            }
        })
        .WithName("GetUserRuntimeState")
        .WithSummary("Get user runtime state")
        .WithDescription("Gets or creates user runtime state");

        group.MapPut("/runtime-state", async (
            [FromBody] UserRuntimeState state,
            IProductionMemoryService memoryService) =>
        {
            try
            {
                await memoryService.UpdateUserRuntimeStateAsync(state);
                return Results.Ok(new { message = "Runtime state updated successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update runtime state: {ex.Message}");
            }
        })
        .WithName("UpdateUserRuntimeState")
        .WithSummary("Update user runtime state")
        .WithDescription("Updates user runtime state");

        // Extract memories from conversation
        group.MapPost("/extract", async (
            [FromBody] MemoryExtractionRequest request,
            IIntelligentMemoryExtractor memoryExtractor) =>
        {
            try
            {
                var memories = await memoryExtractor.ExtractMemoriesAsync(
                    request.UserMessage, 
                    request.AssistantResponse, 
                    request.SessionId, 
                    request.DeviceFingerprint);
                return Results.Ok(memories);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to extract memories: {ex.Message}");
            }
        })
        .WithName("ExtractMemories")
        .WithSummary("Extract memories from conversation")
        .WithDescription("Extracts memory entries from user message and assistant response");

        return app;
    }
}

/// <summary>
/// Request model for consent updates
/// </summary>
public record ConsentRequest(string UserId, string ConsentKind, bool Granted);

/// <summary>
/// Request model for memory extraction
/// </summary>
public record MemoryExtractionRequest(
    string UserMessage, 
    string AssistantResponse, 
    string SessionId, 
    string? DeviceFingerprint = null);
