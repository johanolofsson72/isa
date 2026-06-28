using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Data;
using Agent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agent.Services;

/// <summary>
/// Smart user identification service that handles multi-device scenarios and name-based recognition
/// </summary>
public class SmartUserIdentificationService
{
    private readonly FilesDbContext _context;
    private readonly ILogger<SmartUserIdentificationService> _logger;
    private readonly string _usersJsonPath;

    public SmartUserIdentificationService(
        FilesDbContext context,
        ILogger<SmartUserIdentificationService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _usersJsonPath = Path.Combine(configuration["DataDirectory"] ?? "src/Agent", "users.json");
    }

    /// <summary>
    /// Analyze message for user identification patterns like "det är jag Anna Andersson"
    /// </summary>
    public async Task<string?> AnalyzeUserIdentificationAsync(string message, string deviceFingerprint)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(deviceFingerprint))
            return null;

        try
        {
            _logger.LogInformation("🔍 SMART_ID: Analyzing message for user identification: '{Message}'", message);

            // Comprehensive patterns for user identification in Swedish and English
            var namePatterns = new[]
            {
                // Swedish direct identification patterns
                @"(?:det är jag|jag är|mitt namn är|jag heter)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"(?:hej|hallå|tjena|tja).*(?:det är jag|jag är|jag heter)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"(?:det är|this is)\s+jag\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"jag\s+heter\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)\s+(?:är|heter)\s+(?:mitt\s+namn|jag)",

                // Swedish memory/recognition patterns
                @"kom\s+ihåg\s+(?:mig|mej)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"kom\s+ihåg\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"minns\s+(?:du\s+)?(?:mig|mej)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"känner\s+(?:du\s+)?(?:igen\s+)?(?:mig|mej)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",

                // Swedish casual patterns
                @"(?:kalla|säg)\s+(?:mig|mej)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"(?:du\s+kan\s+)?kalla\s+(?:mig|mej)\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"jag\s+går\s+under\s+namnet\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",

                // English direct identification patterns
                @"(?:i am|my name is|this is)\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
                @"(?:hello|hi|hey).*(?:i am|my name is|this is)\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
                @"(?:you can\s+)?call\s+me\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
                @"i\s+go\s+by\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
                @"(?:the\s+)?name\s+is\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",

                // English memory/recognition patterns
                @"remember\s+me\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
                @"(?:do\s+you\s+)?remember\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
                @"(?:do\s+you\s+)?know\s+me\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",

                // Simple patterns (just the name after common words)
                @"jag\s+([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)",
                @"(?:^|\s)([A-ZÅÄÖ][a-zåäö]+\s+[A-ZÅÄÖ][a-zåäö]+)(?:\s|$)" // Two capitalized words
            };

            for (int i = 0; i < namePatterns.Length; i++)
            {
                var pattern = namePatterns[i];
                _logger.LogDebug("🔍 SMART_ID: Testing pattern {PatternIndex}: {Pattern}", i + 1, pattern);

                var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var extractedName = match.Groups[1].Value.Trim();
                    _logger.LogInformation("🎯 SMART_ID: Extracted name '{ExtractedName}' from message using pattern {PatternIndex}", extractedName, i + 1);

                    // Try to find this user in users.json
                    var userId = await FindUserInJsonByNameAsync(extractedName);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        // Link this device to the found user
                        await LinkDeviceToUserAsync(deviceFingerprint, userId, extractedName);
                        return userId;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze user identification in message");
            return null;
        }
    }

    /// <summary>
    /// Find user in users.json by name (fuzzy matching)
    /// </summary>
    private async Task<string?> FindUserInJsonByNameAsync(string name)
    {
        try
        {
            if (!File.Exists(_usersJsonPath))
            {
                _logger.LogWarning("users.json file not found at {Path}", _usersJsonPath);
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(_usersJsonPath);
            var jsonDocument = JsonDocument.Parse(jsonContent);

            if (!jsonDocument.RootElement.TryGetProperty("users", out var usersArray))
            {
                _logger.LogError("No 'users' array found in users.json");
                return null;
            }

            foreach (var userElement in usersArray.EnumerateArray())
            {
                if (userElement.TryGetProperty("name", out var nameProperty))
                {
                    var userName = nameProperty.GetString();
                    if (!string.IsNullOrEmpty(userName))
                    {
                        // Exact match
                        if (string.Equals(userName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("🎯 SMART_ID: Found exact match for '{Name}' in users.json", name);
                            return GenerateUserIdFromName(userName);
                        }

                        // Fuzzy match (first name or last name)
                        var nameParts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var userNameParts = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var namePart in nameParts)
                        {
                            foreach (var userNamePart in userNameParts)
                            {
                                if (string.Equals(namePart, userNamePart, StringComparison.OrdinalIgnoreCase) && namePart.Length > 2)
                                {
                                    _logger.LogInformation("🎯 SMART_ID: Found fuzzy match for '{Name}' → '{UserName}' in users.json", name, userName);
                                    return GenerateUserIdFromName(userName);
                                }
                            }
                        }
                    }
                }
            }

            _logger.LogWarning("No match found for name '{Name}' in users.json", name);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for user '{Name}' in users.json", name);
            return null;
        }
    }

    /// <summary>
    /// Link device to existing user and update database
    /// </summary>
    private async Task LinkDeviceToUserAsync(string deviceFingerprint, string userId, string displayName)
    {
        try
        {
            // Get or create user in database
            var user = await _context.Set<User>().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                user = new User
                {
                    UserId = userId,
                    DisplayName = displayName,
                    LastSeenAt = DateTime.UtcNow
                };
                _context.Set<User>().Add(user);
                _logger.LogInformation("🆕 SMART_ID: Created new user {UserId} with name '{DisplayName}'", userId, displayName);
            }
            else
            {
                // Update display name if it was empty
                if (string.IsNullOrEmpty(user.DisplayName))
                {
                    user.DisplayName = displayName;
                    _logger.LogInformation("📝 SMART_ID: Updated user {UserId} display name to '{DisplayName}'", userId, displayName);
                }
                user.LastSeenAt = DateTime.UtcNow;
            }

            // Get or create device
            var device = await _context.Set<Device>().FirstOrDefaultAsync(d => d.DeviceId == deviceFingerprint);
            if (device == null)
            {
                device = new Device
                {
                    DeviceId = deviceFingerprint,
                    PrimaryUserId = userId,
                    LastSeen = DateTime.UtcNow
                };
                _context.Set<Device>().Add(device);
                _logger.LogInformation("🆕 SMART_ID: Created new device {DeviceId} for user {UserId}", deviceFingerprint, userId);
            }
            else
            {
                device.PrimaryUserId = userId;
                device.LastSeen = DateTime.UtcNow;
                _logger.LogInformation("🔗 SMART_ID: Linked existing device {DeviceId} to user {UserId}", deviceFingerprint, userId);
            }

            // Create or update DeviceUser relationship
            var deviceUser = await _context.Set<DeviceUser>()
                .FirstOrDefaultAsync(du => du.DeviceId == deviceFingerprint && du.UserId == userId);

            if (deviceUser == null)
            {
                deviceUser = new DeviceUser
                {
                    DeviceId = deviceFingerprint,
                    UserId = userId,
                    Confidence = 0.95, // High confidence for name-based identification
                    LastSeen = DateTime.UtcNow
                };
                _context.Set<DeviceUser>().Add(deviceUser);
                _logger.LogInformation("🆕 SMART_ID: Created DeviceUser relationship: {DeviceId} → {UserId}", deviceFingerprint, userId);
            }
            else
            {
                deviceUser.Confidence = Math.Max(deviceUser.Confidence, 0.95);
                deviceUser.LastSeen = DateTime.UtcNow;
                _logger.LogInformation("📝 SMART_ID: Updated DeviceUser relationship: {DeviceId} → {UserId}", deviceFingerprint, userId);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("✅ SMART_ID: Successfully linked device {DeviceId} to user {UserId} ({DisplayName})", 
                deviceFingerprint, userId, displayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to link device {DeviceId} to user {UserId}", deviceFingerprint, userId);
            throw;
        }
    }

    /// <summary>
    /// Generate consistent user ID from name
    /// </summary>
    private string GenerateUserIdFromName(string name)
    {
        // Create a deterministic GUID based on the name
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name.ToLowerInvariant());
        var hash = System.Security.Cryptography.MD5.HashData(nameBytes);
        var guid = new Guid(hash);
        return guid.ToString();
    }

    /// <summary>
    /// Check if user should be prompted for identification
    /// </summary>
    public async Task<bool> ShouldPromptForIdentificationAsync(string deviceFingerprint)
    {
        try
        {
            // Check if device is already linked to a user
            var deviceUser = await _context.Set<DeviceUser>()
                .Include(du => du.User)
                .FirstOrDefaultAsync(du => du.DeviceId == deviceFingerprint);

            if (deviceUser?.User?.DisplayName != null)
            {
                return false; // Already identified
            }

            // Check if we've already prompted recently
            var device = await _context.Set<Device>().FirstOrDefaultAsync(d => d.DeviceId == deviceFingerprint);
            if (device != null && device.LastSeen > DateTime.UtcNow.AddHours(-1))
            {
                return false; // Don't prompt too frequently
            }

            return true; // Should prompt for identification
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if should prompt for identification");
            return false;
        }
    }

    /// <summary>
    /// Get all devices for a user (for cross-device synchronization)
    /// </summary>
    public async Task<List<string>> GetUserDevicesAsync(string userId)
    {
        try
        {
            var devices = await _context.Set<DeviceUser>()
                .Where(du => du.UserId == userId)
                .Select(du => du.DeviceId)
                .ToListAsync();

            return devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices for user {UserId}", userId);
            return new List<string>();
        }
    }
}
