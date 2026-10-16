using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NeuralNetGoal.Parser.Models;

namespace Tools.NeuralNetGoal.Service.Data;

public interface IResultsRepository
{
    /// <summary>Inserts the matches not stored yet and returns how many were new.</summary>
    Task<int> SaveAsync(IReadOnlyCollection<MatchResult> results, CancellationToken ct = default);
}

public sealed class ResultsRepository(Db db) : IResultsRepository
{
    // Same natural key as the legacy scraper: date + teams + country + division + season. The legacy columns are NOT NULL,
    // so missing half-time scores and odds are stored as 0, exactly as before.
    public async Task<int> SaveAsync(IReadOnlyCollection<MatchResult> results, CancellationToken ct = default)
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
            table.Rows.Add(r.Country, r.Division, r.Season, r.MatchDate, r.HomeTeam, r.AwayTeam,
                r.HomeScore, r.AwayScore, r.HalfTimeHomeScore ?? 0, r.HalfTimeAwayScore ?? 0,
                r.AvgHomeOdds ?? 0m, r.AvgDrawOdds ?? 0m, r.AvgAwayOdds ?? 0m, r.Round);

        await using var c = await db.OpenAsync(ct);

        // COLLATE DATABASE_DEFAULT: the temp table would otherwise get the server's collation and comparing it with the
        // Greek_CI_AS database fails with "Cannot resolve the collation conflict".
        await c.ExecuteAsync(new CommandDefinition(@"
            CREATE TABLE #SoccerResultsTmp(
                [Country] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [Division] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [Season] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [MatchDate] datetime2(7) NOT NULL,
                [HomeTeam] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [AwayTeam] nvarchar(250) COLLATE DATABASE_DEFAULT NULL,
                [HomeScore] int NOT NULL, [AwayScore] int NOT NULL, [HalfTimeHomeScore] int NOT NULL, [HalfTimeAwayScore] int NOT NULL,
                [AvgHomeOdds] decimal(18,2) NOT NULL, [AvgDrawOdds] decimal(18,2) NOT NULL, [AvgAwayOdds] decimal(18,2) NOT NULL,
                [Round] nvarchar(max) COLLATE DATABASE_DEFAULT NULL)", commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct));

        using (var bulk = new SqlBulkCopy(c) { DestinationTableName = "#SoccerResultsTmp", BatchSize = 500, BulkCopyTimeout = Db.CommandTimeoutSeconds })
        {
            foreach (DataColumn col in table.Columns) bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            await bulk.WriteToServerAsync(table, ct);
        }

        return await c.ExecuteAsync(new CommandDefinition(@"
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
            commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct));
    }
}
