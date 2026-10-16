using Dapper;

namespace Tools.NeuralNetGoal.Service.Data;

public interface ITrainingRepository
{
    Task<List<SoccerDataVector>> GetVectorsAsync(CancellationToken ct = default);
}

public sealed class TrainingRepository(Db db) : ITrainingRepository
{
    public async Task<List<SoccerDataVector>> GetVectorsAsync(CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        return (await c.QueryAsync<SoccerDataVector>(new CommandDefinition(
            "SELECT * FROM dbo.DataVectors ORDER BY Country, Division, Seazon, MatchDate",
            commandTimeout: Db.CommandTimeoutSeconds, cancellationToken: ct))).ToList();
    }
}
