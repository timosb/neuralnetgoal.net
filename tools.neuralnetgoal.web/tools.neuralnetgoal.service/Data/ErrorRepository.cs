using Dapper;
using NeuralNetGoal.Parser.Models;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Data;

public interface IErrorRepository
{
    Task SaveIssuesAsync(IEnumerable<ScrapeIssue> issues, CancellationToken ct = default);
    Task<List<ErrorRowDto>> GetLatestAsync(int top, CancellationToken ct = default);
}

public sealed class ErrorRepository(Db db) : IErrorRepository
{
    public async Task SaveIssuesAsync(IEnumerable<ScrapeIssue> issues, CancellationToken ct = default)
    {
        var rows = issues.Select(i => new
        {
            RunTime = i.At,
            Message = Truncate($"[{i.Code}] {i.Message}", 4000),
            ULR = Truncate(i.Url, 4000), // (sic) the legacy column is spelled ULR
        }).ToList();
        if (rows.Count == 0) return;

        await using var c = await db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.Errors (RunTime, Message, ULR) VALUES (@RunTime, @Message, @ULR)",
            rows, cancellationToken: ct));
    }

    public async Task<List<ErrorRowDto>> GetLatestAsync(int top, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        var rows = await c.QueryAsync<ErrorRowDto>(new CommandDefinition(
            "SELECT TOP (@top) RunTime, Message, ULR AS Url FROM dbo.Errors ORDER BY RunTime DESC",
            new { top = Math.Clamp(top, 1, 1000) }, commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct));
        return rows.ToList();
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
