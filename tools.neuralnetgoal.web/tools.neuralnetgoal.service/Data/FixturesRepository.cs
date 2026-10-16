using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NeuralNetGoal.Parser.Models;

namespace Tools.NeuralNetGoal.Service.Data;

public interface IFixturesRepository
{
    /// <summary>Replaces the content of dbo.SoccerFixtures in ONE transaction (a failure never leaves the table empty).</summary>
    Task ReplaceAsync(IReadOnlyCollection<Fixture> fixtures, CancellationToken ct = default);
    /// <summary>Feature vectors of the stored fixtures, computed by the existing SQL function.</summary>
    Task<List<SoccerDataVector>> GetVectorsAsync(CancellationToken ct = default);
}

public sealed class FixturesRepository(Db db) : IFixturesRepository
{
    public async Task ReplaceAsync(IReadOnlyCollection<Fixture> fixtures, CancellationToken ct = default)
    {
        var table = new DataTable();
        table.Columns.Add("Country", typeof(string));
        table.Columns.Add("Division", typeof(string));
        table.Columns.Add("Seazon", typeof(string)); // (sic) legacy column spelling
        table.Columns.Add("MatchDate", typeof(DateTime));
        table.Columns.Add("HomeTeam", typeof(string));
        table.Columns.Add("AwayTeam", typeof(string));
        table.Columns.Add("AvgHomeOdds", typeof(decimal));
        table.Columns.Add("AvgDrawOdds", typeof(decimal));
        table.Columns.Add("AvgAwayOdds", typeof(decimal));

        foreach (var f in fixtures)
            table.Rows.Add(f.Country, f.Division, f.Season, f.MatchDate, f.HomeTeam, f.AwayTeam,
                (object?)f.AvgHomeOdds ?? DBNull.Value, (object?)f.AvgDrawOdds ?? DBNull.Value, (object?)f.AvgAwayOdds ?? DBNull.Value);

        await using var c = await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);

        await c.ExecuteAsync(new CommandDefinition("DELETE FROM dbo.SoccerFixtures", transaction: tx,
            commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct));

        using (var bulk = new SqlBulkCopy(c, SqlBulkCopyOptions.Default, tx)
               { DestinationTableName = "dbo.SoccerFixtures", BatchSize = 500, BulkCopyTimeout = Db.CommandTimeoutSeconds })
        {
            foreach (DataColumn col in table.Columns) bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            await bulk.WriteToServerAsync(table, ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<List<SoccerDataVector>> GetVectorsAsync(CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        return (await c.QueryAsync<SoccerDataVector>(new CommandDefinition("SELECT * FROM dbo.fn_Soccer_fixtures()",
            commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct))).ToList();
    }
}
