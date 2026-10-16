using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Tools.NeuralNetGoal.Service.Auth;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Endpoints;

public static class AuthEndpoints
{
    private const int LockoutAfter = 5;
    private static readonly TimeSpan LockoutFor = TimeSpan.FromMinutes(15);
    private const string BadLogin = "Invalid user name or password.";

    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/auth");

        g.MapPost("/login", async (LoginRequest req, HttpContext http, IUserRepository users, CancellationToken ct) =>
        {
            var name = (req.UserName ?? "").Trim();
            var ip = http.Connection.RemoteIpAddress?.ToString();
            var agent = http.Request.Headers.UserAgent.ToString();

            var user = name.Length == 0 ? null : await users.FindByNameAsync(name, ct);
            if (user is null)
            {
                await users.AuditAsync(name, null, false, "UnknownUser", ip, agent, ct);
                return Results.Json(new { error = BadLogin }, statusCode: 401);
            }
            if (!user.IsActive)
            {
                await users.AuditAsync(name, user.UserId, false, "Inactive", ip, agent, ct);
                return Results.Json(new { error = BadLogin }, statusCode: 401);
            }
            if (user.LockoutEndUtc is { } until && until > DateTime.UtcNow)
            {
                await users.AuditAsync(name, user.UserId, false, "Locked", ip, agent, ct);
                return Results.Json(new { error = $"Too many failed attempts. Try again after {Math.Ceiling((until - DateTime.UtcNow).TotalMinutes)} minute(s)." }, statusCode: 429);
            }
            if (user.PasswordHash is null)
            {
                await users.AuditAsync(name, user.UserId, false, "NoPasswordSet", ip, agent, ct);
                return Results.Json(new { error = "This account has no password yet. Ask the administrator." }, statusCode: 401);
            }
            if (!PasswordHasher.Verify(req.Password ?? "", user.PasswordHash))
            {
                await users.RecordFailureAsync(user.UserId, LockoutAfter, LockoutFor, ct);
                await users.AuditAsync(name, user.UserId, false, "BadPassword", ip, agent, ct);
                return Results.Json(new { error = BadLogin }, statusCode: 401);
            }

            await users.RecordSuccessAsync(user.UserId, ct);
            await users.AuditAsync(name, user.UserId, true, null, ip, agent, ct);
            var info = await SignInAsync(http, users, user, ct);
            return Results.Ok(info);
        }).AllowAnonymous();

        // First run only: while Admin has no password, whoever opens the site first creates it (in the browser, never in a file or chat).
        g.MapGet("/setup-status", async (IUserRepository users, CancellationToken ct) =>
            new SetupStatus((await users.FindByNameAsync("Admin", ct)) is { PasswordHash: null })).AllowAnonymous();

        g.MapPost("/setup", async (SetupRequest req, HttpContext http, IUserRepository users, CancellationToken ct) =>
        {
            var admin = await users.FindByNameAsync("Admin", ct);
            if (admin is not { PasswordHash: null }) return Results.Conflict(new { error = "The Admin password is already set." });
            if (PasswordHasher.Validate(req.Password) is { } problem) return Results.BadRequest(new { error = problem });

            await users.SetPasswordAsync(admin.UserId, PasswordHasher.Hash(req.Password), mustChange: false, ct);
            await users.AuditAsync("Admin", admin.UserId, true, "PasswordCreated", http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString(), ct);
            admin = (await users.FindByIdAsync(admin.UserId, ct))!;
            return Results.Ok(await SignInAsync(http, users, admin, ct));
        }).AllowAnonymous();

        g.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        // Anonymous callers get 401; the client treats that as "not signed in".
        g.MapGet("/me", (ClaimsPrincipal p) => Results.Ok(ToInfo(p))).RequireAuthorization();

        g.MapPost("/change-password", async (ChangePasswordRequest req, HttpContext http, ClaimsPrincipal principal, IUserRepository users, CancellationToken ct) =>
        {
            if (!int.TryParse(principal.FindFirstValue(AuthClaims.UserId), out var uid)) return Results.Unauthorized();
            var user = await users.FindByIdAsync(uid, ct);
            if (user is null || !user.IsActive) return Results.Unauthorized();

            if (!PasswordHasher.Verify(req.CurrentPassword ?? "", user.PasswordHash))
                return Results.BadRequest(new { error = "The current password is not correct." });
            if (PasswordHasher.Validate(req.NewPassword) is { } problem)
                return Results.BadRequest(new { error = problem });
            if (req.NewPassword == req.CurrentPassword)
                return Results.BadRequest(new { error = "The new password must be different from the current one." });

            await users.SetPasswordAsync(uid, PasswordHasher.Hash(req.NewPassword), mustChange: false, ct);
            user = (await users.FindByIdAsync(uid, ct))!; // new SecurityStamp
            var info = await SignInAsync(http, users, user, ct);
            return Results.Ok(info);
        }).RequireAuthorization();
    }

    private static async Task<UserInfo> SignInAsync(HttpContext http, IUserRepository users, UserRecord user, CancellationToken ct)
    {
        var roles = await users.GetRolesAsync(user.UserId, ct);
        var claims = new List<Claim>
        {
            new(AuthClaims.UserId, user.UserId.ToString()),
            new(AuthClaims.Stamp, user.SecurityStamp.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display", user.DisplayName ?? user.UserName),
        };
        if (!user.MustChangePassword) claims.Add(new Claim(AuthClaims.PasswordOk, "1"));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return ToInfo(principal);
    }

    private static UserInfo ToInfo(ClaimsPrincipal p) => new(
        p.Identity?.Name ?? "",
        p.FindFirstValue("display"),
        p.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
        p.FindFirstValue(AuthClaims.PasswordOk) != "1");
}
