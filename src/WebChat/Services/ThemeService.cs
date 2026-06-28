using Microsoft.JSInterop;

namespace WebChat.Services;

/// <summary>
/// Tillhandahåller funktionalitet för att hantera applikationens tema (light/dark mode)
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isDarkMode = false;
    
    // Event för att meddela komponenter om tema-ändringar
    public event Action<bool>? OnThemeChanged;
    
    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }
    
    /// <summary>
    /// Initierar temat baserat på användarens preferenser eller tidigare val
    /// </summary>
    public async Task InitializeAsync()
    {
        // Kontrollera om ett tema finns lagrat i localStorage
        var storedTheme = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "theme");
        
        // Kontrollera även om systemet föredrar dark mode
        var prefersDarkMode = await _jsRuntime.InvokeAsync<bool>("matchMedia", "(prefers-color-scheme: dark)");
        
        // Sätt tema baserat på lagrat värde eller systeminställning
        if (storedTheme == "dark" || (storedTheme == null && prefersDarkMode))
        {
            _isDarkMode = true;
            await SetDarkMode(true);
        }
        else
        {
            _isDarkMode = false;
            await SetDarkMode(false);
        }
    }
    
    /// <summary>
    /// Växlar mellan dark och light mode
    /// </summary>
    public async Task ToggleThemeAsync()
    {
        _isDarkMode = !_isDarkMode;
        await SetDarkMode(_isDarkMode);
        OnThemeChanged?.Invoke(_isDarkMode);
    }
    
    /// <summary>
    /// Returnerar true om dark mode är aktivt
    /// </summary>
    public bool IsDarkMode()
    {
        return _isDarkMode;
    }
    
    /// <summary>
    /// Sätter specifikt tema (dark/light)
    /// </summary>
    private async Task SetDarkMode(bool isDark)
    {
        if (isDark)
        {
            await _jsRuntime.InvokeVoidAsync("document.documentElement.classList.add", "dark-theme");
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", "dark");
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("document.documentElement.classList.remove", "dark-theme");
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", "light");
        }
        
        _isDarkMode = isDark;
    }
}
