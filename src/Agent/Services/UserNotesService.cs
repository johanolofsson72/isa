using System.Text.Json;
using Agent.Data;
using Microsoft.EntityFrameworkCore;

namespace Agent.Services;

/// <summary>
/// User information from users.json
/// </summary>
public class UserInfo
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, object>? Preferences { get; set; }
    public object? Address { get; set; }
    public Dictionary<string, object>? Contacts { get; set; }
    public string? PriorityChannel { get; set; }
}

/// <summary>
/// Root object for users.json
/// </summary>
public class UsersJsonRoot
{
    public List<UserInfo> Users { get; set; } = new();
}

/// <summary>
/// Service for accessing user notes and information from users.json
/// </summary>
public interface IUserNotesService
{
    Task<string?> GetUserNotesAsync(string? deviceFingerprint);
    Task<List<UserInfo>> GetAllUsersAsync();
    Task<UserInfo?> GetUserByDeviceFingerprintAsync(string deviceFingerprint);
    Task<UserInfo?> GetUserByNameAsync(string name);
}

/// <summary>
/// Service for accessing user notes and information from users.json
/// </summary>
public class UserNotesService : IUserNotesService
{
    private readonly MemoryDbContext _context;
    private readonly ILogger<UserNotesService> _logger;
    private readonly string _usersJsonPath;
    private UsersJsonRoot? _cachedUsers;
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public UserNotesService(
        MemoryDbContext context,
        ILogger<UserNotesService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _usersJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users.json");
    }

    public async Task<string?> GetUserNotesAsync(string? deviceFingerprint)
    {
        if (string.IsNullOrEmpty(deviceFingerprint))
            return null;

        try
        {
            var userInfo = await GetUserByDeviceFingerprintAsync(deviceFingerprint);
            return userInfo?.Notes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user notes for device {DeviceFingerprint}", deviceFingerprint);
            return null;
        }
    }

    public async Task<List<UserInfo>> GetAllUsersAsync()
    {
        try
        {
            var usersData = await LoadUsersJsonAsync();
            return usersData?.Users ?? new List<UserInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load all users from users.json");
            return new List<UserInfo>();
        }
    }

    public async Task<UserInfo?> GetUserByDeviceFingerprintAsync(string deviceFingerprint)
    {
        if (string.IsNullOrEmpty(deviceFingerprint))
            return null;

        try
        {
            // First, try to find the user through the device fingerprint in the database
            var deviceUser = await _context.DeviceUsers
                .Include(du => du.User)
                .FirstOrDefaultAsync(du => du.DeviceId == deviceFingerprint);

            if (deviceUser?.User?.DisplayName != null)
            {
                // Found user in database, now get their info from users.json
                var userInfo = await GetUserByNameAsync(deviceUser.User.DisplayName);
                if (userInfo != null)
                {
                    _logger.LogInformation("Found user {UserName} for device {DeviceFingerprint}",
                        userInfo.Name, deviceFingerprint);
                    return userInfo;
                }
            }

            // Fallback: try to match device fingerprint patterns or other logic
            _logger.LogWarning("No user found for device fingerprint {DeviceFingerprint}", deviceFingerprint);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user by device fingerprint {DeviceFingerprint}", deviceFingerprint);
            return null;
        }
    }

    public async Task<UserInfo?> GetUserByNameAsync(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        try
        {
            var users = await GetAllUsersAsync();
            return users.FirstOrDefault(u => 
                string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user by name {Name}", name);
            return null;
        }
    }

    private async Task<UsersJsonRoot?> LoadUsersJsonAsync()
    {
        try
        {
            // Check cache first
            if (_cachedUsers != null && DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry)
            {
                return _cachedUsers;
            }

            if (!File.Exists(_usersJsonPath))
            {
                _logger.LogWarning("users.json file not found at {Path}", _usersJsonPath);
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(_usersJsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            _cachedUsers = JsonSerializer.Deserialize<UsersJsonRoot>(jsonContent, options);
            _lastCacheUpdate = DateTime.UtcNow;

            _logger.LogInformation("Loaded {Count} users from users.json", _cachedUsers?.Users?.Count ?? 0);
            return _cachedUsers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users.json from {Path}", _usersJsonPath);
            return null;
        }
    }
}
