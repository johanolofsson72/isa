using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Blazored.LocalStorage;

namespace Shared.Services;

/// <summary>
/// Unified device fingerprint service for both Agent and WebChat with robust persistence strategy
/// </summary>
public class DeviceFingerprintService : IDeviceFingerprintService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalStorageService? _localStorage;
    private readonly ILogger<DeviceFingerprintService> _logger;

    private const string ISA_DID_COOKIE = "isa_did";
    private const string ISA_SEEN_COOKIE = "isa_seen";
    private const string ISA_PV_COOKIE = "isa_pv";

    private string? _cachedDeviceId;
    private readonly AsyncLock _asyncLock = new();

    public DeviceFingerprintService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<DeviceFingerprintService> logger,
        ILocalStorageService? localStorage = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _localStorage = localStorage;
        _logger = logger;
    }

    /// <summary>
    /// Get existing device fingerprint from HTTP context (cookies only)
    /// </summary>
    public string? GetDeviceFingerprint()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null, cannot extract device fingerprint");
                return null;
            }

            var deviceId = httpContext.Request.Cookies[ISA_DID_COOKIE];
            
            if (!string.IsNullOrEmpty(deviceId) && Guid.TryParse(deviceId, out _))
            {
                _logger.LogDebug("Extracted device fingerprint: {DeviceId}", deviceId);
                return deviceId;
            }

            _logger.LogDebug("No valid device fingerprint found in cookies");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract device fingerprint");
            return null;
        }
    }

    /// <summary>
    /// Get or create device fingerprint with robust persistence strategy for E2E test stability
    /// </summary>
    public async Task<string> GetOrCreateDeviceFingerprintAsync()
    {
        using (await _asyncLock.LockAsync())
        {
            try
            {
                // Return cached device ID if available
                if (!string.IsNullOrEmpty(_cachedDeviceId))
                {
                    _logger.LogDebug("Using cached device fingerprint: {DeviceId}", _cachedDeviceId);
                    return _cachedDeviceId;
                }

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext == null)
                {
                    _logger.LogWarning("HttpContext is null, generating temporary device fingerprint");
                    _cachedDeviceId = Guid.NewGuid().ToString("D");
                    return _cachedDeviceId;
                }

                // STRATEGY: Try multiple sources in order of reliability for E2E tests
                string? deviceId = null;

                // 1. Try cookie first (most reliable for Playwright E2E tests)
                deviceId = await TryGetFromCookieAsync(httpContext);
                if (deviceId != null)
                {
                    _cachedDeviceId = deviceId;
                    await EnsureStoredEverywhereAsync(deviceId);
                    return _cachedDeviceId;
                }

                // 2. Try localStorage as fallback (if available)
                if (_localStorage != null)
                {
                    deviceId = await TryGetFromLocalStorageAsync();
                    if (deviceId != null)
                    {
                        _cachedDeviceId = deviceId;
                        await EnsureStoredEverywhereAsync(deviceId);
                        return _cachedDeviceId;
                    }
                }

                // 3. Create new device ID and store everywhere
                deviceId = Guid.NewGuid().ToString("D");
                _cachedDeviceId = deviceId;
                _logger.LogInformation("Created new device fingerprint: {DeviceId}", deviceId);

                await EnsureStoredEverywhereAsync(deviceId);
                return _cachedDeviceId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get or create device fingerprint");
                _cachedDeviceId = Guid.NewGuid().ToString("D");
                return _cachedDeviceId;
            }
        }
    }

    /// <summary>
    /// Create or update device fingerprint cookie (synchronous version for SignalR compatibility)
    /// </summary>
    public string CreateOrUpdateDeviceFingerprint()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null, generating temporary device fingerprint");
                return Guid.NewGuid().ToString("D");
            }

            // Try to get existing device ID
            var existingDeviceId = httpContext.Request.Cookies[ISA_DID_COOKIE];
            
            if (!string.IsNullOrEmpty(existingDeviceId) && Guid.TryParse(existingDeviceId, out _))
            {
                return existingDeviceId;
            }

            // Create new device ID
            var newDeviceId = Guid.NewGuid().ToString("D");

            // Only try to set cookie if response hasn't started (avoid SignalR conflicts)
            if (!httpContext.Response.HasStarted)
            {
                try
                {
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = false, // Allow JavaScript access for localStorage sync
                        Secure = false, // Allow HTTP for development
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddYears(2),
                        Path = "/"
                    };

                    httpContext.Response.Cookies.Append(ISA_DID_COOKIE, newDeviceId, cookieOptions);
                    _logger.LogInformation("Created new device fingerprint: {DeviceId}", newDeviceId);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Cannot set cookie, response already started. Using temporary device fingerprint: {DeviceId}", newDeviceId);
                }
            }
            else
            {
                _logger.LogDebug("Response already started, cannot set cookie. Using temporary device fingerprint: {DeviceId}", newDeviceId);
            }

            return newDeviceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or update device fingerprint");
            return Guid.NewGuid().ToString("D");
        }
    }

    private Task<string?> TryGetFromCookieAsync(HttpContext httpContext)
    {
        try
        {
            var cookieValue = httpContext.Request.Cookies[ISA_DID_COOKIE];
            _logger.LogDebug("Cookie check result: '{DeviceId}'", cookieValue ?? "null");

            if (!string.IsNullOrEmpty(cookieValue) && Guid.TryParse(cookieValue, out _))
            {
                _logger.LogDebug("Found valid device ID in cookie: {DeviceId}", cookieValue);
                return Task.FromResult<string?>(cookieValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error reading cookie: {Error}", ex.Message);
        }
        return Task.FromResult<string?>(null);
    }

    private async Task<string?> TryGetFromLocalStorageAsync()
    {
        if (_localStorage == null) return null;

        try
        {
            var localStorageValue = await _localStorage.GetItemAsync<string>("isa_did");
            _logger.LogDebug("localStorage check result: '{DeviceId}'", localStorageValue ?? "null");

            if (!string.IsNullOrEmpty(localStorageValue) && Guid.TryParse(localStorageValue, out _))
            {
                _logger.LogDebug("Found valid device ID in localStorage: {DeviceId}", localStorageValue);
                return localStorageValue;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error reading localStorage: {Error}", ex.Message);
        }
        return null;
    }

    private async Task EnsureStoredEverywhereAsync(string deviceId)
    {
        // Store in localStorage (if available)
        if (_localStorage != null)
        {
            try
            {
                await _localStorage.SetItemAsync("isa_did", deviceId);
                _logger.LogDebug("Stored device ID in localStorage: {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to store in localStorage: {Error}", ex.Message);
            }
        }

        // Store in cookie
        try
        {
            SetCookie(ISA_DID_COOKIE, deviceId, DateTimeOffset.UtcNow.AddYears(2));
            _logger.LogDebug("Stored device ID in cookie: {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to store in cookie: {Error}", ex.Message);
        }
    }

    public async Task<bool> IsFirstTimeSeenAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return true;

            var seenCookie = httpContext.Request.Cookies[ISA_SEEN_COOKIE];
            if (!string.IsNullOrEmpty(seenCookie))
            {
                _logger.LogDebug("User has visited before (seen cookie exists)");
                return false;
            }

            // Check localStorage as fallback (if available)
            if (_localStorage != null)
            {
                try
                {
                    var seenInStorage = await _localStorage.GetItemAsync<string>("isa_seen");
                    if (!string.IsNullOrEmpty(seenInStorage))
                    {
                        _logger.LogDebug("User has visited before (localStorage exists)");
                        // Sync back to cookie
                        SetCookie(ISA_SEEN_COOKIE, "true", DateTimeOffset.UtcNow.AddYears(2));
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error checking localStorage for seen flag: {Error}", ex.Message);
                }
            }

            _logger.LogDebug("First visit detected");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if first visit");
            return true; // Default to first visit on error
        }
    }

    public async Task MarkDeviceAsSeenAsync()
    {
        try
        {
            // Store in localStorage (if available)
            if (_localStorage != null)
            {
                try
                {
                    await _localStorage.SetItemAsync("isa_seen", "true");
                    _logger.LogDebug("Marked as seen in localStorage");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to mark as seen in localStorage: {Error}", ex.Message);
                }
            }

            // Store in cookie
            SetCookie(ISA_SEEN_COOKIE, "true", DateTimeOffset.UtcNow.AddYears(2));
            _logger.LogDebug("Marked device as seen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking device as seen");
        }
    }

    public async Task IncrementPageViewAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            // Get current page view count
            int currentCount = 0;
            var pvCookie = httpContext.Request.Cookies[ISA_PV_COOKIE];
            if (!string.IsNullOrEmpty(pvCookie) && int.TryParse(pvCookie, out var cookieCount))
            {
                currentCount = cookieCount;
            }
            else if (_localStorage != null)
            {
                // Check localStorage as fallback
                try
                {
                    var pvStorage = await _localStorage.GetItemAsync<string>("isa_pv");
                    if (!string.IsNullOrEmpty(pvStorage) && int.TryParse(pvStorage, out var storageCount))
                    {
                        currentCount = storageCount;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error reading page views from localStorage: {Error}", ex.Message);
                }
            }

            // Increment and store
            currentCount++;
            var newCountStr = currentCount.ToString();

            // Store in localStorage (if available)
            if (_localStorage != null)
            {
                try
                {
                    await _localStorage.SetItemAsync("isa_pv", newCountStr);
                    _logger.LogDebug("Updated page views in localStorage: {Count}", currentCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to update page views in localStorage: {Error}", ex.Message);
                }
            }

            // Store in cookie
            SetCookie(ISA_PV_COOKIE, newCountStr, DateTimeOffset.UtcNow.AddYears(2));
            _logger.LogDebug("Page view count: {Count}", currentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing page view");
        }
    }

    public async Task<int> GetProfileVersionAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return 1;

            // Try cookie first
            var pvCookie = httpContext.Request.Cookies[ISA_PV_COOKIE];
            if (!string.IsNullOrEmpty(pvCookie) && int.TryParse(pvCookie, out var cookieVersion))
            {
                _logger.LogDebug("Profile version from cookie: {Version}", cookieVersion);
                return cookieVersion;
            }

            // Check localStorage as fallback (if available)
            if (_localStorage != null)
            {
                try
                {
                    var pvStorage = await _localStorage.GetItemAsync<string>("isa_pv");
                    if (!string.IsNullOrEmpty(pvStorage) && int.TryParse(pvStorage, out var storageVersion))
                    {
                        _logger.LogDebug("Profile version from localStorage: {Version}", storageVersion);
                        // Sync back to cookie
                        SetCookie(ISA_PV_COOKIE, storageVersion.ToString(), DateTimeOffset.UtcNow.AddYears(2));
                        return storageVersion;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error reading profile version from localStorage: {Error}", ex.Message);
                }
            }

            _logger.LogDebug("No profile version found, returning default: 1");
            return 1; // Default version
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile version");
            return 1; // Default to version 1 on error
        }
    }

    public async Task UpdateProfileVersionAsync(int version)
    {
        try
        {
            var versionStr = version.ToString();

            // Store in localStorage (if available)
            if (_localStorage != null)
            {
                try
                {
                    await _localStorage.SetItemAsync("isa_pv", versionStr);
                    _logger.LogDebug("Updated profile version in localStorage: {Version}", version);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to update profile version in localStorage: {Error}", ex.Message);
                }
            }

            // Store in cookie
            SetCookie(ISA_PV_COOKIE, versionStr, DateTimeOffset.UtcNow.AddYears(2));
            _logger.LogDebug("Updated profile version: {Version}", version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile version");
        }
    }

    private void SetCookie(string name, string value, DateTimeOffset expires)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("Cannot set cookie - HttpContext is null");
                return;
            }

            if (httpContext.Response.HasStarted)
            {
                _logger.LogWarning("Cannot set cookie - Response has already started");
                return;
            }

            var cookieOptions = new CookieOptions
            {
                Expires = expires,
                HttpOnly = false, // Allow JavaScript access for localStorage sync
                Secure = false, // Allow HTTP for development
                SameSite = SameSiteMode.Lax,
                Path = "/"
            };

            httpContext.Response.Cookies.Append(name, value, cookieOptions);
            _logger.LogDebug("Successfully set cookie: {CookieName} = {Value}", name, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to set cookie {CookieName}: {Error}", name, ex.Message);
        }
    }
}
