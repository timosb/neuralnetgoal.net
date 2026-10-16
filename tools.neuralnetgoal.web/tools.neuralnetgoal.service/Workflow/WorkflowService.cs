using System.Diagnostics;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Services;
using Tools.NeuralNetGoal.Service.Data;
using Tools.NeuralNetGoal.Service.Export;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Workflow;

/// <summary>The logic of the old console's App class, writing its progress to a <see cref="Job"/> instead of the console.</summary>
public sealed class WorkflowService(
    ParserHost parser,
    ILeagueRepository leagues,
    IResultsRepository results,
    IFixturesRepository fixtures,
    ITrainingRepository training,
    IErrorRepository errors,
    IConfiguration config)
{
    public async Task RunAsync(Job job, CancellationToken ct)
    {
        var r = job.Request;
        switch (job.Command)
        {
            case JobCommands.Results: await ResultsAsync(job, r, ct); break;
            case JobCommands.Tables: await TablesAsync(job, r, ct); break;
            case JobCommands.Fixtures: await FixturesAsync(job, r, ct); break;
            case JobCommands.All:
                await ResultsAsync(job, r, ct);
                await TablesAsync(job, r, ct);
                await FixturesAsync(job, r, ct);
                break;
            case JobCommands.Training: await TrainingAsync(job, ct); break;
            default: throw new ArgumentException($"Unknown command '{job.Command}'.");
        }
    }

    // ------------------------------------------------------------------ results
    private async Task ResultsAsync(Job job, StartJobRequest r, CancellationToken ct)
    {
        var selected = await SelectedLeaguesAsync(r, ct);
        job.Info($"Getting latest results for {selected.Count} leagues" + (r.Recent is { } n ? $" (most recent {n} matches each)" : "") + "...");
        var sw = Stopwatch.StartNew();
        int added = 0, failedLeagues = 0;
        var dbFailures = new List<string>();

        foreach (var league in selected)
        {
            ct.ThrowIfCancellationRequested();
            job.Header(league.ToString());

            var outcome = await parser.Results.GetResultsAsync(league.ToRef(), new ResultsOptions(MaxMatches: r.Recent), job, ct);
            if (outcome.Items.Count == 0 && outcome.Issues.Count > 0) failedLeagues++;

            // A database problem in one league must not throw away the other leagues: report it and carry on.
            try
            {
                if (outcome.Issues.Count > 0)
                {
                    await errors.SaveIssuesAsync(outcome.Issues, ct);
                    job.Warn($"{outcome.Issues.Count} issues saved to dbo.Errors.");
                }

                var newRows = await results.SaveAsync(outcome.Items.ToList(), ct);
                added += newRows;
                job.Info($"{league}: {outcome.Items.Count} read, {newRows} new in database.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                dbFailures.Add($"{league}: {ex.Message}");
                job.Fail($"{league}: could not save to the database: {ex.Message}");
            }
        }

        job.Info($"Results done in {sw.Elapsed:hh\\:mm\\:ss}: {added} new matches" + (failedLeagues > 0 ? $", {failedLeagues} leagues returned nothing (see issues above)." : "."));
        if (dbFailures.Count > 0)
        {
            job.Fail($"{dbFailures.Count} leagues could NOT be saved:");
            foreach (var f in dbFailures) job.Fail("  " + f);
        }
    }

    // ------------------------------------------------------------------ league tables
    private async Task TablesAsync(Job job, StartJobRequest r, CancellationToken ct)
    {
        var selected = await SelectedLeaguesAsync(r, ct);
        job.Info($"Building league tables for {selected.Count} leagues...");
        var failed = 0;
        foreach (var league in selected)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            try
            {
                await leagues.BuildTableAsync(league, ct);
                job.Info($"  {league}  ({sw.Elapsed.TotalSeconds:0.#}s)");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                job.Fail($"  {league}: FAILED: {ex.Message}");
            }
        }

        job.Info(failed == 0 ? "League tables done." : $"League tables done, {failed} of {selected.Count} FAILED (see above).");
    }

    // ------------------------------------------------------------------ fixtures + excel
    private async Task FixturesAsync(Job job, StartJobRequest r, CancellationToken ct)
    {
        var selected = await SelectedLeaguesAsync(r, ct);
        job.Info($"Getting fixtures for {selected.Count} leagues...");
        var all = new List<Fixture>();
        var issues = new List<ScrapeIssue>();

        foreach (var league in selected)
        {
            ct.ThrowIfCancellationRequested();
            job.Header(league.ToString());
            var outcome = await parser.Fixtures.GetFixturesAsync(league.ToRef(), null, job, ct);
            all.AddRange(outcome.Items);
            issues.AddRange(outcome.Issues);
        }

        if (issues.Count > 0)
        {
            await errors.SaveIssuesAsync(issues, ct);
            job.Warn($"{issues.Count} issues saved to dbo.Errors.");
        }

        if (all.Count == 0)
        {
            job.Warn("No fixtures were read - the SoccerFixtures table and the Excel file were NOT touched.");
            return;
        }

        await fixtures.ReplaceAsync(all, ct);
        job.Info($"{all.Count} fixtures stored. Building feature vectors...");

        var vectors = await fixtures.GetVectorsAsync(ct);
        job.FilePath = Exporter().ExportFixtures(vectors);
        job.Info($"Excel file ready ({vectors.Count} rows): {job.FileName}");
        WarnIfFreeLicense(job);
    }

    // ------------------------------------------------------------------ training export
    private async Task TrainingAsync(Job job, CancellationToken ct)
    {
        var running = await leagues.GetRunningAsync(ct);
        var vectors = await training.GetVectorsAsync(ct);
        job.FilePath = Exporter().ExportTraining(vectors, running);
        job.Info($"Training workbook ready ({vectors.Count} rows): {job.FileName}");
        WarnIfFreeLicense(job);
    }

    // ------------------------------------------------------------------ helpers
    private ExcelExporter Exporter() => new(ExportDirectory(config), config["Export:GemBoxLicense"] ?? "");

    /// <summary>Where the Excel files are written before the browser downloads them. Default: App_Data/exports next to the site.</summary>
    public static string ExportDirectory(IConfiguration config) =>
        string.IsNullOrWhiteSpace(config["Export:Directory"])
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "exports")
            : config["Export:Directory"]!;

    private void WarnIfFreeLicense(Job job)
    {
        if (string.IsNullOrWhiteSpace(config["Export:GemBoxLicense"]) || config["Export:GemBoxLicense"] == "FREE-LIMITED-KEY")
            job.Warn("GemBox is running with the FREE license (limited rows/sheets). Set Export:GemBoxLicense.");
    }

    private async Task<List<RunningLeague>> SelectedLeaguesAsync(StartJobRequest r, CancellationToken ct)
    {
        var list = await leagues.GetRunningAsync(ct);
        if (!string.IsNullOrWhiteSpace(r.Country)) list = list.Where(l => l.Country.Equals(r.Country, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(r.Division)) list = list.Where(l => l.Division.Equals(r.Division, StringComparison.OrdinalIgnoreCase)).ToList();
        if (list.Count == 0) throw new InvalidOperationException("No running leagues matched (check dbo.SoccerRunningLeagues and the country/division filter).");
        return list;
    }
}
