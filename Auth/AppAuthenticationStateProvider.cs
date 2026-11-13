using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using panialera.Models;

namespace panialera.Auth;

public class AppAuthenticationStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal _current = new(new ClaimsIdentity());

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_current));

    public void SignIn(User user)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("UserId", user.Id.ToString())
        }, authenticationType: "app");

        _current = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void SignOut()
    {
        _current = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public int? CurrentUserId =>
        int.TryParse(_current.FindFirst("UserId")?.Value, out var id) ? id : null;

    public string? CurrentUserName =>
        _current.Identity?.IsAuthenticated == true ? _current.Identity.Name : null;
}
