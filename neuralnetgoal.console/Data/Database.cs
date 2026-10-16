using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NeuralNetGoal.Parser.Models;

namespace NeuralNetGoal.ConsoleApp.Data;

/// <summary>
/// All SQL of the console in one place. It talks to the EXISTING SQL Server schema (SoccerRunningLeagues, SoccerResults,
/// SoccerFixtures, Errors, the Build_Soccer_league_Tables procedure, fn_Soccer_fixtures(), DataVectors) so the console can
/// replace the legacy scraper without touching the database.
/// </summary>
public sealed class Database(string connectionString)
{
    private const int CommandTimeoutSeconds = 200;

    private SqlConnection Open()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "No database connection string. Set it with:  dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\" --project neuralnetgoal.console");
        var c = new SqlConnection(connectionString);
        c.Open();
        return c;
    }

    // ------------------------------------------------------------------ running leagues

    public async Task<List<RunningLeague>> GetRunningLeaguesAsync()
    {
        await using var c = Open();
        var rows = await c.QueryAsync<RunningLeague>(
            "SELECT Id, Country, Division, Season, URL FROM dbo.SoccerRunningLeagues ORDER BY Country, Division",
            commandTimeout: CommandTimeoutSeconds);
        return rows.ToList();
    }

    // ------------------------------------------------------------------ results

    /// <summary>
    /// Inserts the scraped matches that are not stored yet (same natural key as the legacy scraper:
    /// date + teams + country + division + season) and returns how many were new.
    /// The legacy table columns are NOT NULL, so missing half-time scores and odds are stored as 0, exactly as before.
    /// </summary>
    public async Task<int> SaveResultsAsync(IReadOnlyCollection<MatchResult> results)
    {
        if (results.Count == 0) return 0;

        var table = new DataTable();
        table.Columns.Add("Country", typeof(string));
        table.Columns.Add("Division", typeof(string));
        table.Columns.Add("Season", typeof(string));
        table.Columns.Add("MatchDate", typeof(DateTime));
        table.Columns.Add("HomeTeam", typeof(string));
        table.Columns.Add("AwayTeam", typeof(string));
        table.Columns.Add("HomeScore", typeof(int));
        table.Columns.Add("AwayScore", typeof(int));
        table.Columns.Add("HalfTimeHomeScore", typeof(int));
        table.Columns.Add("HalfTimeAwayScore", typeof(int));
        table.Columns.Add("AvgHomeOdds", typeof(decimal));
        table.Columns.Add("AvgDrawOdds", typeof(decimal));
        table.Columns.Add("AvgAwayOdds", typeof(decimal));
        table.Columns.Add("Round", typeof(string));

        foreach (var r in results)
        {
            table.Rows.Add(r.Country, r.Division, r.Season, r.MatchDate, r.HomeTeam, r.AwayTeam,
                r.HomeScore, r.AwayScore, r.HalfTimeHomeScore ?? 0, r.HalfTimeAwayScore ?? 0,
                r.AvgHomeOdds ?? 0m, r.AvgDrawOdds ?? 0m, r.AvgAwayOdds ?? 0m, r.Round);
        }

        await using var c = Open();

        // A temp table lives in tempdb and would get the SERVER's default collation. Your database uses another one
        // (Greek_CI_AS), and comparing text between the two fails with "Cannot resolve the collation conflict".
        // COLLATE DATABASE_DEFAULT gives every text column the collation of the database we are connected to.
        await c.ExecuteAsync(@"
            CREATE TABLE #SoccerResultsTmp(
                [Country] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [Division] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [Season] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [MatchDate] datetime2(7) NOT NULL,
                [HomeTeam] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [AwayTeam] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [HomeScore] int NOT NULL, [AwayScore] int NOT NULL, [HalfTimeHomeScore] int NOT NULL, [HalfTimeAwayScore] int NOT NULL,
                [AvgHomeOdds] decimal(18,2) NOT NULL, [AvgDrawOdds] decimal(18,2) NOT NULL, [AvgAwayOdds] decimal(18,2) NOT NULL,
                [Round] nvarchar(max) COLLATE DATABASE_DEFAULT NULL)", commandTimeout: CommandTimeoutSeconds);

        using (var bulk = new SqlBulkCopy(c) { DestinationTableName = "#SoccerResultsTmp", BatchSize = 500, BulkCopyTimeout = CommandTimeoutSeconds })
        {
            foreach (DataColumn col in table.Columns) bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            await bulk.WriteToServerAsync(table);
        }

        return await c.ExecuteAsync(@"
            INSERT INTO dbo.SoccerResults
                ([Country],[Division],[Season],[MatchDate],[HomeTeam],[AwayTeam],[HomeScore],[AwayScore],
                 [HalfTimeHomeScore],[HalfTimeAwayScore],[AvgHomeOdds],[AvgDrawOdds],[AvgAwayOdds],[Round])
            SELECT [Country],[Division],[Season],[MatchDate],[HomeTeam],[AwayTeam],[HomeScore],[AwayScore],
                   [HalfTimeHomeScore],[HalfTimeAwayScore],[AvgHomeOdds],[AvgDrawOdds],[AvgAwayOdds],[Round]
            FROM #SoccerResultsTmp tmp
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.SoccerResults res
                WHERE res.MatchDate = tmp.MatchDate AND res.HomeTeam = tmp.HomeTeam AND res.AwayTeam = tmp.AwayTeam
                  AND res.Country = tmp.Country AND res.Division = tmp.Division AND res.Season = tmp.Season)",
            commandTimeout: CommandTimeoutSeconds);
    }

    // ------------------------------------------------------------------ errors

    public async Task SaveIssuesAsync(IEnumerable<ScrapeIssue> issues)
    {
        var rows = issues.Select(i => new
        {
            RunTime = i.At,
            Message = Truncate($"[{i.Code}] {i.Message}", 4000),
            ULR = Truncate(i.Url, 4000), // (sic) the legacy column is spelled ULR
        }).ToList();
        if (rows.Count == 0) return;

        await using var c = Open();
        await c.ExecuteAsync("INSERT INTO dbo.Errors (RunTime, Message, ULR) VALUES (@RunTime, @Message, @ULR)", rows);
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];

    // ------------------------------------------------------------------ league tables

    /// <summary>Runs the existing stored procedure. The calculation itself stays in SQL for now.</summary>
    public async Task BuildLeagueTableAsync(RunningLeague league)
    {
        await using var c = Open();
        // positional EXEC, parameterised (the legacy code built this statement by string concatenation)
        await c.ExecuteAsync("EXEC [dbo].[Build_Soccer_league_Tables] @country, @season, @division",
            new { country = league.Country, season = league.Season, division = league.Division },
            commandTimeout: CommandTimeoutSeconds);
    }

    // ------------------------------------------------------------------ fixtures

    /// <summary>Replaces the content of SoccerFixtures in ONE transaction, so a failure never leaves the table empty
    /// (the legacy code truncated first and scraped afterwards).</summary>
    public async Task ReplaceFixturesAsync(IReadOnlyCollection<Fixture> fixtures)
    {
        var table = new DataTable();
        table.Columns.Add("Country", typeof(string));
        table.Columns.Add("Division", typeof(string));
        table.Columns.Add("Seazon", typeof(string));
        table.Columns.Add("MatchDate", typeof(DateTime));
        table.Columns.Add("HomeTeam", typeof(string));
        table.Columns.Add("AwayTeam", typeof(string));
        table.Columns.Add("AvgHomeOdds", typeof(decimal));
        table.Columns.Add("AvgDrawOdds", typeof(decimal));
        table.Columns.Add("AvgAwayOdds", typeof(decimal));

        foreach (var f in fixtures)
        {
            table.Rows.Add(f.Country, f.Division, f.Season, f.MatchDate, f.HomeTeam, f.AwayTeam,
                (object?)f.AvgHomeOdds ?? DBNull.Value, (object?)f.AvgDrawOdds ?? DBNull.Value, (object?)f.AvgAwayOdds ?? DBNull.Value);
        }

        await using var c = Open();
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync();

        await c.ExecuteAsync("DELETE FROM dbo.SoccerFixtures", transaction: tx, commandTimeout: CommandTimeoutSeconds);

        using (var bulk = new SqlBulkCopy(c, SqlBulkCopyOptions.Default, tx)
               { DestinationTableName = "dbo.SoccerFixtures", BatchSize = 500, BulkCopyTimeout = CommandTimeoutSeconds })
        {
            foreach (DataColumn col in table.Columns) bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            await bulk.WriteToServerAsync(table);
        }

        await tx.CommitAsync();
    }

    /// <summary>The feature vectors of the stored fixtures, computed by the existing SQL function.</summary>
    public async Task<List<SoccerDataVector>> GetFixtureVectorsAsync()
    {
        await using var c = Open();
        return (await c.QueryAsync<SoccerDataVector>("SELECT * FROM dbo.fn_Soccer_fixtures()", commandTimeout: CommandTimeoutSeconds)).ToList();
    }

    // ------------------------------------------------------------------ training export

    public async Task<List<SoccerDataVector>> GetTrainingVectorsAsync()
    {
        await using var c = Open();
        return (await c.QueryAsync<SoccerDataVector>(
            "SELECT * FROM dbo.DataVectors ORDER BY Country, Division, Seazon, MatchDate", commandTimeout: CommandTimeoutSeconds)).ToList();
    }
}
