using Agent.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Endpoints;

/// <summary>
/// Endpoints for user identification and device synchronization
/// </summary>
public static class UserIdentificationEndpoints
{
    public static void MapUserIdentificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/user-identification")
            .WithTags("User Identification")
            .WithOpenApi();

        // Get user devices for synchronization
        group.MapGet("/devices/{userId}", GetUserDevices)
            .WithName("GetUserDevices")
            .WithSummary("Get all devices for a user")
            .WithDescription("Returns all device fingerprints associated with a user for cross-device synchronization");

        // Check if device should prompt for identification
        group.MapGet("/should-prompt/{deviceFingerprint}", ShouldPromptForIdentification)
            .WithName("ShouldPromptForIdentification")
            .WithSummary("Check if device should prompt for user identification")
            .WithDescription("Returns true if the device should prompt the user to identify themselves");

        // Manual user identification endpoint
        group.MapPost("/identify", IdentifyUser)
            .WithName("IdentifyUser")
            .WithSummary("Manually identify a user for a device")
            .WithDescription("Links a device fingerprint to a specific user");
    }

    /// <summary>
    /// Get all devices for a user
    /// </summary>
    private static async Task<IResult> GetUserDevices(
        string userId,
        SmartUserIdentificationService smartUserIdentificationService)
    {
        try
        {
            var devices = await smartUserIdentificationService.GetUserDevicesAsync(userId);
            return Results.Ok(new { UserId = userId, Devices = devices });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to get user devices: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if device should prompt for identification
    /// </summary>
    private static async Task<IResult> ShouldPromptForIdentification(
        string deviceFingerprint,
        SmartUserIdentificationService smartUserIdentificationService)
    {
        try
        {
            var shouldPrompt = await smartUserIdentificationService.ShouldPromptForIdentificationAsync(deviceFingerprint);
            return Results.Ok(new { DeviceFingerprint = deviceFingerprint, ShouldPrompt = shouldPrompt });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to check prompt status: {ex.Message}");
        }
    }

    /// <summary>
    /// Manually identify a user for a device
    /// </summary>
    private static async Task<IResult> IdentifyUser(
        [FromBody] UserIdentificationRequest request,
        SmartUserIdentificationService smartUserIdentificationService)
    {
        try
        {
            if (string.IsNullOrEmpty(request.DeviceFingerprint) || string.IsNullOrEmpty(request.UserName))
            {
                return Results.BadRequest("DeviceFingerprint and UserName are required");
            }

            var userId = await smartUserIdentificationService.AnalyzeUserIdentificationAsync(
                $"jag är {request.UserName}", request.DeviceFingerprint);

            if (!string.IsNullOrEmpty(userId))
            {
                return Results.Ok(new { 
                    Success = true, 
                    UserId = userId, 
                    Message = $"Successfully identified user {request.UserName}" 
                });
            }
            else
            {
                return Results.NotFound(new { 
                    Success = false, 
                    Message = $"User {request.UserName} not found in users.json" 
                });
            }
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to identify user: {ex.Message}");
        }
    }
}

/// <summary>
/// Request model for user identification
/// </summary>
public class UserIdentificationRequest
{
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
