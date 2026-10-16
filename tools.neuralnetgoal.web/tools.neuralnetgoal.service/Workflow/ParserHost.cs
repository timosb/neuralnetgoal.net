using NeuralNetGoal.Parser;
using NeuralNetGoal.Parser.Client;
using NeuralNetGoal.Parser.Services;

namespace Tools.NeuralNetGoal.Service.Workflow;

/// <summary>The parser, wherever it runs: behind the Web API on the VPS (Remote, the normal case) or inside this process (Local,
/// for development on a PC that can reach betexplorer). Both expose the same interfaces.</summary>
public sealed class ParserHost : IDisposable
{
    private readonly IDisposable _owner;

    private ParserHost(IResultsScraper r, IFixturesScraper f, ILeagueCatalogScraper c, string description, Func<CancellationToken, Task<(bool Ok, string Message)>> health, IDisposable owner)
    {
        Results = r; Fixtures = f; Catalog = c; Description = description; Health = health; _owner = owner;
    }

    public IResultsScraper Results { get; }
    public IFixturesScraper Fixtures { get; }
    public ILeagueCatalogScraper Catalog { get; }
    public string Description { get; }
    public Func<CancellationToken, Task<(bool Ok, string Message)>> Health { get; }

    public static ParserHost Create(IConfiguration config)
    {
        var mode = config["Parser:Mode"] ?? "Remote";

        if (mode.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            var bx = new BetexplorerOptions();
            config.GetSection("Betexplorer").Bind(bx);
            var local = ParserFactory.Create(bx);
            return new ParserHost(local.Results, local.Fixtures, local.Catalog, "Local (in-process)", async ct =>
            {
                var r = await local.Html.GetAsync(bx.BaseUrl, null, ct);
                return (r.Ok, r.Ok ? $"betexplorer reachable (HTTP {r.StatusCode})" : $"betexplorer NOT reachable: {r.ErrorCode} - {r.ErrorMessage}");
            }, local);
        }

        var url = config["Parser:ApiUrl"];
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("Parser:ApiUrl is not set.");

        var api = ParserApiClient.Create(url, config["Parser:ApiKey"] ?? "");
        return new ParserHost(api.Results, api.Fixtures, api.Catalog, $"Remote ({url})", async ct =>
        {
            var h = await api.GetHealthAsync(ct);
            return (h.Site.Reachable, $"API {h.Status} v{h.Version}; betexplorer reachable: {h.Site.Reachable}" + (h.Site.Reachable ? "" : $" ({h.Site.Error})"));
        }, api);
    }

    public void Dispose() => _owner.Dispose();
}
