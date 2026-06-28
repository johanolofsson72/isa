using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace WebChat.Services;

public class SessionAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public SessionAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var isAuthenticated = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "isAuthenticated");
            
            if (isAuthenticated == "true")
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "WebChat User"),
                    new Claim(ClaimTypes.Role, "User")
                };
                
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "Session"));
            }
            else
            {
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }
        catch (Exception)
        {
            // If we can't access session storage (during prerendering), assume not authenticated
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new AuthenticationState(_currentUser);
    }

    public async Task MarkUserAsAuthenticated()
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "isAuthenticated", "true");
        
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "WebChat User"),
            new Claim(ClaimTypes.Role, "User")
        };
        
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "Session"));
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "isAuthenticated");
        
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }
}
