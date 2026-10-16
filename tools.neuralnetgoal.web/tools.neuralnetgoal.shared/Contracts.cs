namespace Tools.NeuralNetGoal.Shared;

// ---- authentication -------------------------------------------------------------------------------------------
public sealed record LoginRequest(string UserName, string Password);
/// <summary>True while the Admin account has no password: the login page then offers to create it.</summary>
public sealed record SetupStatus(bool NeedsSetup);
public sealed record SetupRequest(string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>The signed-in user as the browser needs it. <see cref="MustChangePassword"/> forces the change-password page.</summary>
public sealed record UserInfo(string UserName, string? DisplayName, string[] Roles, bool MustChangePassword);

public sealed record UserDto(int UserId, string UserName, string? Email, string? DisplayName, bool IsActive,
    bool HasPassword, DateTime? LastLoginUtc, string[] Roles);
public sealed record CreateUserRequest(string UserName, string? Email, string? DisplayName, string Role, string Password);
public sealed record SetActiveRequest(bool IsActive);

public static class Roles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    public static readonly string[] All = [Admin, Operator, Viewer];
}

// ---- data -----------------------------------------------------------------------------------------------------
public sealed record LeagueDto(int Id, string Country, string Division, string Season, string Url);
public sealed record ErrorRowDto(DateTime RunTime, string? Message, string? Url);

// ---- jobs (results / tables / fixtures / all / training) ---------------------------------------------------------
public static class JobCommands
{
    public const string Results = "results";
    public const string Tables = "tables";
    public const string Fixtures = "fixtures";
    public const string All = "all";
    public const string Training = "training";
}

/// <summary>Start a job. Recent = only the newest N unknown matches per league. Country/Division narrow the leagues.</summary>
public sealed record StartJobRequest(string Command, string? Country, string? Division, int? Recent);

public sealed record JobSummaryDto(string Id, string Command, string State, DateTime StartedUtc, DateTime? FinishedUtc, string? Description);

public sealed record JobLogLine(long Seq, DateTime AtUtc, string Level, string Text);

/// <summary>State of a job plus the log lines after the sequence number the browser already has.</summary>
public sealed record JobStatusDto(string Id, string Command, string State, string? Description, DateTime StartedUtc, DateTime? FinishedUtc,
    string? Error, string? FileName, long NextSeq, JobLogLine[] Lines);

public static class JobStates
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed record HealthDto(string Service, bool DatabaseOk, string? DatabaseError, string Parser, bool ParserOk, string? ParserMessage);
