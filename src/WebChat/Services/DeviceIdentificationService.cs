using Shared.Services;
using WebChat.Services.Interfaces;

namespace WebChat.Services;

/// <summary>
/// Legacy wrapper for device identification service - delegates to shared implementation
/// </summary>
[Obsolete("Use Shared.Services.IDeviceFingerprintService directly")]
public class DeviceIdentificationService : IDeviceIdentificationService
{
    private readonly IDeviceFingerprintService _sharedService;

    public DeviceIdentificationService(IDeviceFingerprintService sharedService)
    {
        _sharedService = sharedService;
    }

    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        return await _sharedService.GetOrCreateDeviceFingerprintAsync();
    }

    public async Task<bool> IsFirstTimeSeenAsync()
    {
        return await _sharedService.IsFirstTimeSeenAsync();
    }

    public async Task MarkDeviceAsSeenAsync()
    {
        await _sharedService.MarkDeviceAsSeenAsync();
    }

    public async Task IncrementPageViewAsync()
    {
        await _sharedService.IncrementPageViewAsync();
    }

    public async Task<int> GetProfileVersionAsync()
    {
        return await _sharedService.GetProfileVersionAsync();
    }

    public async Task UpdateProfileVersionAsync(int version)
    {
        await _sharedService.UpdateProfileVersionAsync(version);
    }
}
