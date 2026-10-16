using Tools.NeuralNetGoal.Service.Data;
using Tools.NeuralNetGoal.Service.Workflow;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Endpoints;

public static class DataEndpoints
{
    public static void MapDataEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("").RequireAuthorization(Policies.Read);

        g.MapGet("/leagues", async (ILeagueRepository leagues, CancellationToken ct) =>
            (await leagues.GetRunningAsync(ct)).Select(l => new LeagueDto(l.Id, l.Country, l.Division, l.Season, l.URL)));

        g.MapGet("/errors", async (IErrorRepository errors, CancellationToken ct, int top = 100) => await errors.GetLatestAsync(top, ct));

        // Is the database reachable, and can the parser reach betexplorer? Each part reports its own failure.
        g.MapGet("/health", async (Db db, ParserHost parser, CancellationToken ct) =>
        {
            bool dbOk; string? dbError = null;
            try { await using var c = await db.OpenAsync(ct); dbOk = true; }
            catch (Exception ex) { dbOk = false; dbError = ex.Message; }

            bool parserOk; string? message;
            try { (parserOk, message) = await parser.Health(ct); }
            catch (Exception ex) { parserOk = false; message = ex.Message; }

            return new HealthDto("tools.neuralnetgoal.service", dbOk, dbError, parser.Description, parserOk, message);
        });
    }
}
