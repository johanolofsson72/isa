using Shared.Services;

namespace Agent.Services;

/// <summary>
/// Legacy wrapper for device fingerprint service - delegates to shared implementation
/// </summary>
[Obsolete("Use Shared.Services.IDeviceFingerprintService directly")]
public class DeviceFingerprintService
{
    private readonly IDeviceFingerprintService _sharedService;

    public DeviceFingerprintService(IDeviceFingerprintService sharedService)
    {
        _sharedService = sharedService;
    }

    /// <summary>
    /// Extract device fingerprint from HTTP context
    /// </summary>
    public string? GetDeviceFingerprint()
    {
        return _sharedService.GetDeviceFingerprint();
    }

    /// <summary>
    /// Create or update device fingerprint cookie
    /// </summary>
    public string CreateOrUpdateDeviceFingerprint()
    {
        return _sharedService.CreateOrUpdateDeviceFingerprint();
    }
}
