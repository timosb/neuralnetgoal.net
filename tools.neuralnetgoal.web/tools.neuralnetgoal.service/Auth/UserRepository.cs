using Dapper;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Auth;

/// <summary>A row of auth.Users (the tables are created by database/001_auth_schema.sql, not by migrations).</summary>
public sealed class UserRecord
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PasswordHash { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public Guid SecurityStamp { get; set; }
}

public interface IUserRepository
{
    Task<UserRecord?> FindByNameAsync(string userName, CancellationToken ct = default);
    Task<UserRecord?> FindByIdAsync(int userId, CancellationToken ct = default);
    Task<string[]> GetRolesAsync(int userId, CancellationToken ct = default);
    Task RecordFailureAsync(int userId, int lockoutAfter, TimeSpan lockoutFor, CancellationToken ct = default);
    Task RecordSuccessAsync(int userId, CancellationToken ct = default);
    Task SetPasswordAsync(int userId, string passwordHash, bool mustChange, CancellationToken ct = default);
    Task AuditAsync(string userName, int? userId, bool succeeded, string? reason, string? ip, string? userAgent, CancellationToken ct = default);
    Task<List<UserDto>> ListAsync(CancellationToken ct = default);
    Task<int> CreateAsync(CreateUserRequest request, string passwordHash, CancellationToken ct = default);
    Task SetActiveAsync(int userId, bool active, CancellationToken ct = default);
}

public sealed class UserRepository(Data.Db db) : IUserRepository
{
    private const string UserColumns = "UserId, UserName, Email, DisplayName, PasswordHash, MustChangePassword, IsActive, FailedLoginCount, LockoutEndUtc, LastLoginUtc, SecurityStamp";

    public async Task<UserRecord?> FindByNameAsync(string userName, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        return await c.QuerySingleOrDefaultAsync<UserRecord>(new CommandDefinition(
            $"SELECT {UserColumns} FROM auth.Users WHERE UserName = @userName", new { userName }, cancellationToken: ct));
    }

    public async Task<UserRecord?> FindByIdAsync(int userId, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        return await c.QuerySingleOrDefaultAsync<UserRecord>(new CommandDefinition(
            $"SELECT {UserColumns} FROM auth.Users WHERE UserId = @userId", new { userId }, cancellationToken: ct));
    }

    public async Task<string[]> GetRolesAsync(int userId, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        var roles = await c.QueryAsync<string>(new CommandDefinition(
            "SELECT r.Name FROM auth.UserRoles ur JOIN auth.Roles r ON r.RoleId = ur.RoleId WHERE ur.UserId = @userId",
            new { userId }, cancellationToken: ct));
        return roles.ToArray();
    }

    public async Task RecordFailureAsync(int userId, int lockoutAfter, TimeSpan lockoutFor, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        // the counter restarts after a lockout, so the next series of mistakes gets its own full set of attempts
        await c.ExecuteAsync(new CommandDefinition(@"
            UPDATE auth.Users SET
                FailedLoginCount = CASE WHEN FailedLoginCount + 1 >= @lockoutAfter THEN 0 ELSE FailedLoginCount + 1 END,
                LockoutEndUtc    = CASE WHEN FailedLoginCount + 1 >= @lockoutAfter THEN DATEADD(SECOND, @seconds, SYSUTCDATETIME()) ELSE LockoutEndUtc END
            WHERE UserId = @userId", new { userId, lockoutAfter, seconds = (int)lockoutFor.TotalSeconds }, cancellationToken: ct));
    }

    public async Task RecordSuccessAsync(int userId, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE auth.Users SET FailedLoginCount = 0, LockoutEndUtc = NULL, LastLoginUtc = SYSUTCDATETIME() WHERE UserId = @userId",
            new { userId }, cancellationToken: ct));
    }

    public async Task SetPasswordAsync(int userId, string passwordHash, bool mustChange, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        // a new SecurityStamp signs the user out everywhere else
        await c.ExecuteAsync(new CommandDefinition(@"
            UPDATE auth.Users SET PasswordHash = @passwordHash, MustChangePassword = @mustChange,
                   PasswordUpdatedUtc = SYSUTCDATETIME(), FailedLoginCount = 0, LockoutEndUtc = NULL, SecurityStamp = NEWID()
            WHERE UserId = @userId", new { userId, passwordHash, mustChange }, cancellationToken: ct));
    }

    public async Task AuditAsync(string userName, int? userId, bool succeeded, string? reason, string? ip, string? userAgent, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO auth.LoginAudit (UserName, UserId, Succeeded, Reason, IpAddress, UserAgent)
            VALUES (@userName, @userId, @succeeded, @reason, @ip, @userAgent)",
            new
            {
                userName = Cut(userName, 100)!, userId, succeeded, reason = Cut(reason, 100), ip = Cut(ip, 64), userAgent = Cut(userAgent, 300)
            }, cancellationToken: ct));
    }

    public async Task<List<UserDto>> ListAsync(CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        var rows = (await c.QueryAsync<(int UserId, string UserName, string? Email, string? DisplayName, bool IsActive, bool HasPassword, DateTime? LastLoginUtc, string? Roles)>(
            new CommandDefinition(@"
                SELECT u.UserId, u.UserName, u.Email, u.DisplayName, u.IsActive,
                       HasPassword = CAST(CASE WHEN u.PasswordHash IS NULL THEN 0 ELSE 1 END AS bit),
                       u.LastLoginUtc, Roles = STRING_AGG(r.Name, ',')
                FROM auth.Users u
                LEFT JOIN auth.UserRoles ur ON ur.UserId = u.UserId
                LEFT JOIN auth.Roles r ON r.RoleId = ur.RoleId
                GROUP BY u.UserId, u.UserName, u.Email, u.DisplayName, u.IsActive, u.PasswordHash, u.LastLoginUtc
                ORDER BY u.UserName", cancellationToken: ct))).ToList();
        return rows.Select(r => new UserDto(r.UserId, r.UserName, r.Email, r.DisplayName, r.IsActive, r.HasPassword, r.LastLoginUtc,
            (r.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))).ToList();
    }

    public async Task<int> CreateAsync(CreateUserRequest request, string passwordHash, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO auth.Users (UserName, Email, DisplayName, PasswordHash, PasswordUpdatedUtc, MustChangePassword, IsActive)
            OUTPUT INSERTED.UserId
            VALUES (@UserName, @Email, @DisplayName, @passwordHash, SYSUTCDATETIME(), 1, 1)",
            new { request.UserName, request.Email, request.DisplayName, passwordHash }, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO auth.UserRoles (UserId, RoleId) SELECT @id, RoleId FROM auth.Roles WHERE Name = @Role",
            new { id, request.Role }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task SetActiveAsync(int userId, bool active, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE auth.Users SET IsActive = @active, SecurityStamp = NEWID() WHERE UserId = @userId", new { userId, active }, cancellationToken: ct));
    }

    private static string? Cut(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
