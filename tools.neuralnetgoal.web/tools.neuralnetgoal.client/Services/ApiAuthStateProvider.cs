using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Client.Services;

/// <summary>Asks the service who is signed in (the cookie is HttpOnly, so the browser code cannot read it, only the server can).</summary>
public sealed class ApiAuthStateProvider(ApiClient api) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private AuthenticationState? _state;

    public UserInfo? Current { get; private set; }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_state is not null) return _state;
        var me = await api.MeAsync();
        return _state = Build(me.Ok ? me.Value : null);
    }

    public void SetUser(UserInfo? user)
    {
        _state = Build(user);
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
    }

    private AuthenticationState Build(UserInfo? user)
    {
        Current = user;
        if (user is null) return Anonymous;
        var claims = new List<Claim> { new(ClaimTypes.Name, user.UserName) };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie")));
    }
}
