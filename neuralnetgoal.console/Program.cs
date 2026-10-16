using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using NeuralNetGoal.ConsoleApp;
using NeuralNetGoal.ConsoleApp.Data;
using NeuralNetGoal.ConsoleApp.Export;
using NeuralNetGoal.Parser;
using NeuralNetGoal.Parser.Client;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Services;

// ---------------------------------------------------------------------------------------------------------------
// neuralnetgoal.console - TEMPORARY desktop client of the parser.
//   results   scrape the latest results of the running leagues and store the new ones
//   tables    build the league tables (existing stored procedure) for the running leagues
//   fixtures  scrape upcoming matches, store them, and export the final Excel file
//   all       results -> tables -> fixtures + Excel
//   training  export the training workbook from dbo.DataVectors
//   health    check the parser (API or in-process) and its access to betexplorer
//   catalog   list the countries the parser can see (quick parser check, touches no database)
// Options: --recent=N  --country=X  --division=Y  --local | --remote  --dump=DIR  --verbose
//          --out=FOLDER (where files are created; default: your Downloads folder)  --no-open (do not open the Excel file)
// Runs on Windows and macOS (and Linux). Files go to the Downloads folder and open automatically.
// ---------------------------------------------------------------------------------------------------------------

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.local.json", optional: true) // git-ignored, for machine-specific values
    .AddUserSecrets<Settings>(optional: true)               // dotnet user-secrets: connection string, API key, GemBox license
    .AddEnvironmentVariables("NNG_")
    .Build();

var settings = Settings.From(config, args);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); Console.WriteLine("\nCancelling..."); };

var command = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant();

try
{
    using var parser = ParserHost.Create(settings);
    var app = new App(settings, parser);

    if (command is not null)
    {
        await app.RunAsync(command, cts.Token);
        return 0;
    }

    // interactive menu
    while (!cts.IsCancellationRequested)
    {
        Console.WriteLine();
        Console.WriteLine($"neuralnetgoal console   parser: {parser.Description}");
        Console.WriteLine("  1) results   2) tables   3) fixtures + Excel   4) all   5) training export   6) health   7) catalog   0) exit");
        Console.Write("> ");
        var choice = Console.ReadLine()?.Trim();
        var cmd = choice switch
        {
            "1" => "results", "2" => "tables", "3" => "fixtures", "4" => "all",
            "5" => "training", "6" => "health", "7" => "catalog", _ => null,
        };
        if (choice is "0" or null) break;
        if (cmd is null) continue;
        try { await app.RunAsync(cmd, cts.Token); }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"ERROR: {ex.Message}"); Console.ResetColor(); }
    }
    return 0;
}
catch (OperationCanceledException)
{
    return 130;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.ResetColor();
    return 1;
}

// =====================================================================================================================

namespace NeuralNetGoal.ConsoleApp
{
    public sealed class Settings
    {
        public string ConnectionString { get; init; } = "";
        public string Mode { get; init; } = "Remote";
        public string ApiUrl { get; init; } = "";
        public string ApiKey { get; init; } = "";
        public BetexplorerOptions Betexplorer { get; init; } = new();
        /// <summary>Where every created file goes. Resolved: Export:Directory if set, otherwise the user's Downloads folder.</summary>
        public string ExportDirectory { get; init; } = "";
        public bool OpenAfterExport { get; init; } = true;
        public string GemBoxLicense { get; init; } = "";
        public int? Recent { get; init; }
        public string? Country { get; init; }
        public string? Division { get; init; }
        public bool Verbose { get; init; }

        public static Settings From(IConfiguration c, string[] args)
        {
            string? Opt(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.OrdinalIgnoreCase))?[(name.Length + 3)..];
            bool Flag(string name) => args.Any(a => a.Equals($"--{name}", StringComparison.OrdinalIgnoreCase));

            var bx = new BetexplorerOptions();
            c.GetSection("Betexplorer").Bind(bx);
            if (Opt("dump") is { } dump) bx.DumpDirectory = dump;

            var mode = Flag("local") ? "Local" : Flag("remote") ? "Remote" : c["Parser:Mode"] ?? "Remote";

            return new Settings
            {
                ConnectionString = c.GetConnectionString("Default") ?? "",
                Mode = mode,
                ApiUrl = c["Parser:ApiUrl"] ?? "",
                ApiKey = c["Parser:ApiKey"] ?? "",
                Betexplorer = bx,
                ExportDirectory = Platform.ResolveExportDirectory(Opt("out") ?? c["Export:Directory"]),
                // files open automatically; --no-open (or Export:OpenAfterExport=false) turns that off
                OpenAfterExport = !Flag("no-open") && (Flag("open") || c.GetValue("Export:OpenAfterExport", true)),
                GemBoxLicense = c["Export:GemBoxLicense"] ?? "",
                Recent = int.TryParse(Opt("recent"), out var n) && n > 0 ? n : null,
                Country = Opt("country"),
                Division = Opt("division"),
                Verbose = Flag("verbose"),
            };
        }
    }

    /// <summary>The parser, wherever it runs: in this process (Local) or behind the Web API on the VPS (Remote).
    /// Both expose the same interfaces, so the rest of the console does not care.</summary>
    public sealed class ParserHost : IDisposable
    {
        private readonly IDisposable _owner;

        private ParserHost(IResultsScraper r, IFixturesScraper f, ILeagueCatalogScraper c, string description, Func<CancellationToken, Task<string>> health, IDisposable owner)
        {
            Results = r; Fixtures = f; Catalog = c; Description = description; Health = health; _owner = owner;
        }

        public IResultsScraper Results { get; }
        public IFixturesScraper Fixtures { get; }
        public ILeagueCatalogScraper Catalog { get; }
        public string Description { get; }
        public Func<CancellationToken, Task<string>> Health { get; }

        public static ParserHost Create(Settings s)
        {
            if (s.Mode.Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                var local = ParserFactory.Create(s.Betexplorer);
                return new ParserHost(local.Results, local.Fixtures, local.Catalog, "Local (in-process)", async ct =>
                {
                    var r = await local.Html.GetAsync(s.Betexplorer.BaseUrl, null, ct);
                    return r.Ok ? $"betexplorer reachable (HTTP {r.StatusCode})" : $"betexplorer NOT reachable: {r.ErrorCode} - {r.ErrorMessage}";
                }, local);
            }

            if (string.IsNullOrWhiteSpace(s.ApiUrl))
                throw new InvalidOperationException("Parser:ApiUrl is not set (appsettings.json), or use --local.");

            var api = ParserApiClient.Create(s.ApiUrl, s.ApiKey);
            return new ParserHost(api.Results, api.Fixtures, api.Catalog, $"Remote ({s.ApiUrl})", async ct =>
            {
                var h = await api.GetHealthAsync(ct);
                return $"API {h.Status} v{h.Version}; betexplorer reachable: {h.Site.Reachable}" +
                       (h.Site.Reachable ? "" : $" ({h.Site.Error})");
            }, api);
        }

        public void Dispose() => _owner.Dispose();
    }

    public sealed class App(Settings s, ParserHost parser)
    {
        private readonly Database _db = new(s.ConnectionString);
        private readonly ConsoleEventSink _sink = new(s.Verbose);

        public async Task RunAsync(string command, CancellationToken ct)
        {
            switch (command)
            {
                case "results": await ResultsAsync(ct); break;
                case "tables": await TablesAsync(ct); break;
                case "fixtures": await FixturesAsync(ct); break;
                case "all":
                    await ResultsAsync(ct);
                    await TablesAsync(ct);
                    await FixturesAsync(ct);
                    break;
                case "training": await TrainingAsync(); break;
                case "health": Info(await parser.Health(ct)); break;
                case "catalog": await CatalogAsync(ct); break;
                default:
                    throw new ArgumentException($"Unknown command '{command}'. Use: results, tables, fixtures, all, training, health, catalog.");
            }
        }

        // ------------------------------------------------------------------ results
        private async Task ResultsAsync(CancellationToken ct)
        {
            var leagues = await SelectedLeaguesAsync();
            Info($"Getting latest results for {leagues.Count} leagues" + (s.Recent is { } n ? $" (most recent {n} matches each)" : "") + "...");
            var sw = Stopwatch.StartNew();
            int added = 0, failedLeagues = 0;
            var dbFailures = new List<string>();

            foreach (var league in leagues)
            {
                ct.ThrowIfCancellationRequested();
                Header(league.ToString());

                var outcome = await parser.Results.GetResultsAsync(league.ToRef(), new ResultsOptions(MaxMatches: s.Recent), _sink, ct);
                if (outcome.Items.Count == 0 && outcome.Issues.Count > 0) failedLeagues++;

                // A database problem in one league must not throw away the other leagues: report it and carry on.
                try
                {
                    if (outcome.Issues.Count > 0)
                    {
                        await _db.SaveIssuesAsync(outcome.Issues);
                        Warn($"{outcome.Issues.Count} issues saved to dbo.Errors.");
                    }

                    var newRows = await _db.SaveResultsAsync(outcome.Items.ToList());
                    added += newRows;
                    Info($"{league}: {outcome.Items.Count} read, {newRows} new in database.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    dbFailures.Add($"{league}: {ex.Message}");
                    Error($"{league}: could not save to the database: {ex.Message}");
                }
            }

            Info($"Results done in {sw.Elapsed:hh\\:mm\\:ss}: {added} new matches" + (failedLeagues > 0 ? $", {failedLeagues} leagues returned nothing (see issues above)." : "."));
            if (dbFailures.Count > 0)
            {
                Error($"{dbFailures.Count} leagues could NOT be saved:");
                foreach (var f in dbFailures) Error("  " + f);
            }
        }

        // ------------------------------------------------------------------ league tables
        private async Task TablesAsync(CancellationToken ct)
        {
            var leagues = await SelectedLeaguesAsync();
            Info($"Building league tables for {leagues.Count} leagues...");
            var failed = new List<string>();
            foreach (var league in leagues)
            {
                ct.ThrowIfCancellationRequested();
                var sw = Stopwatch.StartNew();
                try
                {
                    await _db.BuildLeagueTableAsync(league);
                    Info($"  {league}  ({sw.Elapsed.TotalSeconds:0.#}s)");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed.Add($"{league}: {ex.Message}");
                    Error($"  {league}: FAILED: {ex.Message}");
                }
            }

            Info(failed.Count == 0 ? "League tables done." : $"League tables done, {failed.Count} of {leagues.Count} FAILED (see above).");
        }

        // ------------------------------------------------------------------ fixtures + excel
        private async Task FixturesAsync(CancellationToken ct)
        {
            var leagues = await SelectedLeaguesAsync();
            Info($"Getting fixtures for {leagues.Count} leagues...");
            var all = new List<Fixture>();
            var issues = new List<ScrapeIssue>();

            foreach (var league in leagues)
            {
                ct.ThrowIfCancellationRequested();
                Header(league.ToString());
                var outcome = await parser.Fixtures.GetFixturesAsync(league.ToRef(), null, _sink, ct);
                all.AddRange(outcome.Items);
                issues.AddRange(outcome.Issues);
            }

            if (issues.Count > 0)
            {
                await _db.SaveIssuesAsync(issues);
                Warn($"{issues.Count} issues saved to dbo.Errors.");
            }

            if (all.Count == 0)
            {
                Warn("No fixtures were read - the SoccerFixtures table and the Excel file were NOT touched.");
                return;
            }

            await _db.ReplaceFixturesAsync(all);
            Info($"{all.Count} fixtures stored. Building feature vectors...");

            var vectors = await _db.GetFixtureVectorsAsync();
            var path = new ExcelExporter(s.ExportDirectory, s.GemBoxLicense).ExportFixtures(vectors);
            Info($"Excel file ready ({vectors.Count} rows): {path}");
            if (s.GemBoxLicense is "" or "FREE-LIMITED-KEY")
                Warn("GemBox is running with the FREE license (limited rows/sheets). Set Export:GemBoxLicense in user-secrets.");

            OpenExport(path);
        }

        // ------------------------------------------------------------------ training export
        private async Task TrainingAsync()
        {
            var leagues = await _db.GetRunningLeaguesAsync();
            var vectors = await _db.GetTrainingVectorsAsync();
            var path = new ExcelExporter(s.ExportDirectory, s.GemBoxLicense).ExportTraining(vectors, leagues);
            Info($"Training workbook ready ({vectors.Count} rows): {path}");
            OpenExport(path);
        }

        /// <summary>Opens the created file in the program the system associates with it (Excel), unless --no-open was given.</summary>
        private void OpenExport(string path)
        {
            if (!s.OpenAfterExport) return;
            if (Platform.OpenFile(path, out var error)) Info("Opening it in Excel...");
            else Warn($"Could not open the file automatically ({error}). It is here: {path}");
        }

        // ------------------------------------------------------------------ catalog (no database)
        private async Task CatalogAsync(CancellationToken ct)
        {
            var countries = await parser.Catalog.GetCountriesAsync(_sink, ct);
            foreach (var i in countries.Issues) Warn($"{i.Code}: {i.Message}");
            Info($"{countries.Items.Count} countries" + (countries.Items.Count > 0 ? $", e.g. {string.Join(", ", countries.Items.Take(5).Select(c => c.Name))}" : ""));
        }

        // ------------------------------------------------------------------ helpers
        private async Task<List<RunningLeague>> SelectedLeaguesAsync()
        {
            var leagues = await _db.GetRunningLeaguesAsync();
            if (s.Country is not null) leagues = leagues.Where(l => l.Country.Equals(s.Country, StringComparison.OrdinalIgnoreCase)).ToList();
            if (s.Division is not null) leagues = leagues.Where(l => l.Division.Equals(s.Division, StringComparison.OrdinalIgnoreCase)).ToList();
            if (leagues.Count == 0) throw new InvalidOperationException("No running leagues matched (check dbo.SoccerRunningLeagues and --country/--division).");
            return leagues;
        }

        private static void Info(string m) => Console.WriteLine(m);
        private static void Header(string m) { Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine($"\n=== {m} ==="); Console.ResetColor(); }
        private static void Warn(string m) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine(m); Console.ResetColor(); }
        private static void Error(string m) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine(m); Console.ResetColor(); }
    }
}
