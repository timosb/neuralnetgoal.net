using Dapper;

namespace Tools.NeuralNetGoal.Service.Data;

public interface ILeagueRepository
{
    Task<List<RunningLeague>> GetRunningAsync(CancellationToken ct = default);
    Task BuildTableAsync(RunningLeague league, CancellationToken ct = default);
}

public sealed class LeagueRepository(Db db) : ILeagueRepository
{
    public async Task<List<RunningLeague>> GetRunningAsync(CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        var rows = await c.QueryAsync<RunningLeague>(new CommandDefinition(
            "SELECT Id, Country, Division, Season, URL FROM dbo.SoccerRunningLeagues ORDER BY Country, Division",
            commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct));
        return rows.ToList();
    }

    /// <summary>Runs the existing stored procedure; the league table calculation stays in SQL for now.</summary>
    public async Task BuildTableAsync(RunningLeague league, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("EXEC [dbo].[Build_Soccer_league_Tables] @country, @season, @division",
            new { country = league.Country, season = league.Season, division = league.Division },
            commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct));
    }
}
