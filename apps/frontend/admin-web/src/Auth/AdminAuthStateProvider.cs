using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AdminWeb.Auth;

public sealed class AdminAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly AuthTokenStore _tokenStore;

    public AdminAuthStateProvider(AuthTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        _tokenStore.Changed += NotifyChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_tokenStore.IsAuthenticated)
        {
            return Task.FromResult(AnonymousState);
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, _tokenStore.Username ?? string.Empty) },
            authenticationType: "admin-api");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
