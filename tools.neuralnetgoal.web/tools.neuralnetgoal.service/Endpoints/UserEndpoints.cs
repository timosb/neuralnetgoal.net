using Tools.NeuralNetGoal.Service.Auth;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/users").RequireAuthorization(Policies.Admin);

        g.MapGet("", async (IUserRepository users, CancellationToken ct) => await users.ListAsync(ct));

        g.MapPost("", async (CreateUserRequest req, IUserRepository users, CancellationToken ct) =>
        {
            var name = (req.UserName ?? "").Trim();
            if (name.Length is < 3 or > 100 || name.Any(char.IsWhiteSpace)) return Results.BadRequest(new { error = "User name: 3-100 characters, no spaces." });
            if (!Roles.All.Contains(req.Role)) return Results.BadRequest(new { error = "Unknown role." });
            if (PasswordHasher.Validate(req.Password) is { } problem) return Results.BadRequest(new { error = problem });
            if (await users.FindByNameAsync(name, ct) is not null) return Results.Conflict(new { error = "That user name already exists." });

            // the new user must choose their own password at the first login (MustChangePassword = 1 in CreateAsync)
            await users.CreateAsync(req with { UserName = name }, PasswordHasher.Hash(req.Password), ct);
            return Results.Ok(await users.ListAsync(ct));
        });

        g.MapPost("/{id:int}/active", async (int id, SetActiveRequest req, System.Security.Claims.ClaimsPrincipal me, IUserRepository users, CancellationToken ct) =>
        {
            if (id.ToString() == me.FindFirst(AuthClaims.UserId)?.Value && !req.IsActive)
                return Results.BadRequest(new { error = "You cannot deactivate your own account." });
            await users.SetActiveAsync(id, req.IsActive, ct);
            return Results.Ok(await users.ListAsync(ct));
        });
    }
}
