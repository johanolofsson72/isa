using System.Text.Json;
using Microsoft.Extensions.Logging;
using WebChat.Services.Interfaces;
using Shared.Models;

namespace WebChat.Services;

/// <summary>
/// Service for querying Agent's memory system for personal information
/// </summary>
public class MemoryQueryService : IMemoryQueryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MemoryQueryService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MemoryQueryService(IHttpClientFactory httpClientFactory, ILogger<MemoryQueryService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("agent");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Get user information and personal details for device fingerprint
    /// </summary>
    public async Task<PersonalInfo?> GetPersonalInfoAsync(string deviceFingerprint)
    {
        try
        {
            _logger.LogInformation("DEBUG: Querying personal info for device fingerprint: {DeviceFingerprint}", deviceFingerprint);

            // First, get the user information
            var userResponse = await _httpClient.GetAsync($"/api/memory/user?deviceFingerprint={Uri.EscapeDataString(deviceFingerprint)}");

            if (!userResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user info for device {DeviceFingerprint}: {StatusCode}",
                    deviceFingerprint, userResponse.StatusCode);
                return null;
            }

            var userJson = await userResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("DEBUG: User API response: {UserJson}", userJson);

            var userInfo = JsonSerializer.Deserialize<UserInfoDto>(userJson, _jsonOptions);

            if (userInfo == null)
            {
                _logger.LogWarning("Failed to deserialize user info for device {DeviceFingerprint}", deviceFingerprint);
                return null;
            }

            _logger.LogInformation("DEBUG: Deserialized user - UserId: {UserId}, DisplayName: '{DisplayName}'",
                userInfo.UserId, userInfo.DisplayName ?? "null");

            // Create personal info object
            var personalInfo = new PersonalInfo
            {
                UserId = userInfo.UserId,
                DisplayName = userInfo.DisplayName,
                FirstName = ExtractFirstName(userInfo.DisplayName)
            };

            // If we have a display name, we're done
            if (!string.IsNullOrEmpty(personalInfo.DisplayName))
            {
                _logger.LogInformation("Found personal info for device {DeviceFingerprint}: {DisplayName}",
                    deviceFingerprint, personalInfo.DisplayName);
                return personalInfo;
            }

            _logger.LogInformation("DEBUG: No DisplayName found, searching identity memories for user {UserId}", userInfo.UserId);

            // If no display name, try to get identity memories
            var identityMemories = await GetIdentityMemoriesAsync(userInfo.UserId);
            if (identityMemories?.Any() == true)
            {
                _logger.LogInformation("DEBUG: Found {Count} identity memories", identityMemories.Count);
                foreach (var memory in identityMemories)
                {
                    _logger.LogInformation("DEBUG: Memory content: '{Content}', Score: {Score}", memory.Content, memory.Score);
                }

                // Extract name from identity memories
                var nameFromMemory = ExtractNameFromMemories(identityMemories);
                if (!string.IsNullOrEmpty(nameFromMemory))
                {
                    personalInfo.DisplayName = nameFromMemory;
                    personalInfo.FirstName = ExtractFirstName(nameFromMemory);

                    _logger.LogInformation("Found name from memories for device {DeviceFingerprint}: {Name}",
                        deviceFingerprint, nameFromMemory);
                    return personalInfo;
                }
                else
                {
                    _logger.LogWarning("DEBUG: Could not extract name from {Count} identity memories", identityMemories.Count);
                }
            }
            else
            {
                _logger.LogInformation("DEBUG: No identity memories found for user {UserId}", userInfo.UserId);
            }

            _logger.LogInformation("DEBUG: No personal information found for device {DeviceFingerprint}", deviceFingerprint);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personal info for device {DeviceFingerprint}", deviceFingerprint);
            return null;
        }
    }

    /// <summary>
    /// Get identity memories for a user
    /// </summary>
    private async Task<List<MemoryEntryDto>?> GetIdentityMemoriesAsync(string userId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/memory/search?query=name&type=Identity&userId={Uri.EscapeDataString(userId)}&topK=5&threshold=0.7");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get identity memories for user {UserId}: {StatusCode}", 
                    userId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var memories = JsonSerializer.Deserialize<List<MemoryEntryDto>>(json, _jsonOptions);
            
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting identity memories for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Extract name from memory entries
    /// </summary>
    private string? ExtractNameFromMemories(List<MemoryEntryDto> memories)
    {
        foreach (var memory in memories)
        {
            var content = memory.Content?.ToLower() ?? "";
            
            // Look for patterns like "my name is X", "I am X", "jag heter X", etc.
            var namePatterns = new[]
            {
                @"(?:my name is|i am|jag heter|mitt namn är|i'm called|kallas)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"(?:name|namn)[:=]\s*([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)\s+(?:is my name|är mitt namn)"
            };

            foreach (var pattern in namePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(memory.Content ?? "", pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (match.Success && match.Groups.Count > 1)
                {
                    var name = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(name) && name.Length > 1)
                    {
                        return name;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extract first name from full name
    /// </summary>
    private string? ExtractFirstName(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return null;

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }
}



// Using shared DTOs from Shared.Models:
// - UserInfoDto
// - MemoryEntryDto
