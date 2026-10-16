using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Fetching;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Parsing;

namespace NeuralNetGoal.Parser.Services;

public sealed class FixturesScraper(
    IHtmlSource html,
    IOptions<BetexplorerOptions> options,
    ILogger<FixturesScraper>? log = null) : IFixturesScraper
{
    private readonly BetexplorerOptions _cfg = options.Value;

    public async Task<ScrapeOutcome<Fixture>> GetFixturesAsync(
        LeagueRef league,
        FixturesOptions? options = null,
        IScrapeEventSink? events = null,
        CancellationToken ct = default)
    {
        var o = options ?? new FixturesOptions();
        var session = new ScrapeSession(html, _cfg, new ScrapeEmitter(events, log, league));
        var em = session.Emit;

        em.Emit(ScrapeEventKind.LeagueStarted, ScrapeLevel.Info, $"Fixtures {league.Country} / {league.Division} / {league.Season}");

        var url = UrlUtil.Combine(league.UrlPath, _cfg.FixturesSuffix);
        var fetch = await session.FetchAsync(url, ct).ConfigureAwait(false);
        if (!fetch.Ok)
        {
            session.AddIssue(fetch.ErrorCode!, fetch.ErrorMessage!, fetch.Url);
            em.Emit(ScrapeEventKind.LeagueFinished, ScrapeLevel.Error, $"Could not load the fixtures page: {fetch.ErrorMessage}", httpStatus: fetch.StatusCode == 0 ? null : fetch.StatusCode);
            return session.Complete(Array.Empty<Fixture>());
        }

        var page = FixturesListParser.Parse(fetch.Html!);
        if (!page.TableFound)
        {
            // A league with no upcoming matches (season over) legitimately has no table: warn, do not fail.
            em.Emit(ScrapeEventKind.LeagueFinished, ScrapeLevel.Warn, "No fixtures table found (season finished, or the site layout changed).");
            session.AddIssue(IssueCodes.LayoutChanged, "Fixtures table not found.", fetch.Url);
            return session.Complete(Array.Empty<Fixture>());
        }

        session.SetFound(page.Rows.Count);
        var toRead = new List<FixtureRow>();
        foreach (var row in page.Rows)
        {
            if (o.RequireOdds && (row.HomeOdds is null || row.DrawOdds is null || row.AwayOdds is null))
            {
                session.MarkSkipped();
                var (h, a) = ResultsScraper.SplitTeams(row.TeamsText);
                em.Emit(ScrapeEventKind.MatchSkipped, ScrapeLevel.Debug, "No odds yet", row.MatchUrl, h, a);
                continue;
            }
            toRead.Add(row);
        }

        if (o.MaxMatches is { } max && toRead.Count > max) toRead = toRead.Take(max).ToList();

        em.Emit(ScrapeEventKind.LeagueListParsed, ScrapeLevel.Info,
            $"{page.Rows.Count} fixtures listed, {session.Skipped} without odds, {toRead.Count} to read.", counters: session.Counters);

        var fixtures = new ConcurrentBag<Fixture>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await Parallel.ForEachAsync(
                toRead,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _cfg.MaxConcurrentRequests), CancellationToken = stop.Token },
                async (row, token) =>
                {
                    var (homeHint, awayHint) = ResultsScraper.SplitTeams(row.TeamsText);
                    em.Emit(ScrapeEventKind.MatchStarted, ScrapeLevel.Info, "Reading fixture", row.MatchUrl, homeHint, awayHint, session.Counters);

                    var sw = Stopwatch.StartNew();
                    var (parse, f) = await session.FetchMatchAsync(row.MatchUrl, MatchDetailParser.ParseFixture, token).ConfigureAwait(false);

                    void Fail(string code, string message)
                    {
                        session.AddIssue(code, $"{homeHint} - {awayHint}: {message}", f.Url);
                        session.MarkFailed();
                        em.Emit(ScrapeEventKind.MatchFailed, ScrapeLevel.Error, $"{code}: {message}", row.MatchUrl, homeHint, awayHint,
                            session.Counters, f.StatusCode == 0 ? null : f.StatusCode, durationMs: (int)sw.ElapsedMilliseconds);
                    }

                    if (!f.Ok)
                    {
                        Fail(f.ErrorCode!, f.ErrorMessage!);
                        if (f.ErrorCode == IssueCodes.Blocked) stop.Cancel();
                        return;
                    }

                    if (parse is null || !parse.Ok || parse.Detail is not { } d)
                    {
                        Fail(parse?.ProblemCode ?? IssueCodes.ParseError, parse?.Problem ?? "Unknown parse problem.");
                        return;
                    }

                    if (d.MatchDate is null)
                    {
                        Fail(IssueCodes.ParseError, "Match date not found.");
                        return;
                    }

                    fixtures.Add(new Fixture(row.MatchUrl, league.Country, league.Division, league.Season,
                        d.MatchDate.Value, d.HomeTeam, d.AwayTeam, row.HomeOdds, row.DrawOdds, row.AwayOdds));

                    session.MarkParsed();
                    em.Emit(ScrapeEventKind.MatchParsed, ScrapeLevel.Info, $"{d.MatchDate:yyyy-MM-dd HH:mm}  odds {row.HomeOdds}/{row.DrawOdds}/{row.AwayOdds}",
                        row.MatchUrl, d.HomeTeam, d.AwayTeam, session.Counters, f.StatusCode, durationMs: (int)sw.ElapsedMilliseconds);
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            em.Emit(ScrapeEventKind.LeagueFinished, ScrapeLevel.Error, "Aborted: the site is blocking requests.", counters: session.Counters);
            return session.Complete(fixtures.OrderBy(x => x.MatchDate).ThenBy(x => x.MatchUrl));
        }

        em.Emit(ScrapeEventKind.LeagueFinished, session.Failed > 0 ? ScrapeLevel.Warn : ScrapeLevel.Info,
            $"Finished: {session.Parsed} parsed, {session.Skipped} skipped, {session.Failed} failed.", counters: session.Counters);

        return session.Complete(fixtures.OrderBy(x => x.MatchDate).ThenBy(x => x.MatchUrl));
    }
}

public sealed class LeagueCatalogScraper(
    IHtmlSource html,
    IOptions<BetexplorerOptions> options,
    ILogger<LeagueCatalogScraper>? log = null) : ILeagueCatalogScraper
{
    private readonly BetexplorerOptions _cfg = options.Value;

    public async Task<ScrapeOutcome<CountryRef>> GetCountriesAsync(IScrapeEventSink? events = null, CancellationToken ct = default)
    {
        var session = new ScrapeSession(html, _cfg, new ScrapeEmitter(events, log));
        var fetch = await session.FetchAsync(_cfg.CatalogPath, ct).ConfigureAwait(false);
        if (!fetch.Ok)
        {
            session.AddIssue(fetch.ErrorCode!, fetch.ErrorMessage!, fetch.Url);
            return session.Complete(Array.Empty<CountryRef>());
        }

        var countries = CatalogParser.ParseCountries(fetch.Html!);
        if (countries.Count == 0)
            session.AddIssue(IssueCodes.LayoutChanged, "No countries found on the catalog page.", fetch.Url);

        session.Emit.Emit(ScrapeEventKind.CatalogParsed, countries.Count == 0 ? ScrapeLevel.Error : ScrapeLevel.Info, $"{countries.Count} countries found.");
        return session.Complete(countries);
    }

    public async Task<ScrapeOutcome<LeagueSeasonRef>> GetLeagueSeasonsAsync(CountryRef country, IScrapeEventSink? events = null, CancellationToken ct = default)
    {
        var session = new ScrapeSession(html, _cfg, new ScrapeEmitter(events, log));
        var fetch = await session.FetchAsync(country.UrlPath, ct).ConfigureAwait(false);
        if (!fetch.Ok)
        {
            session.AddIssue(fetch.ErrorCode!, fetch.ErrorMessage!, fetch.Url);
            return session.Complete(Array.Empty<LeagueSeasonRef>());
        }

        var leagues = CatalogParser.ParseLeagueSeasons(fetch.Html!, country.Name);
        if (leagues.Count == 0)
            session.AddIssue(IssueCodes.LayoutChanged, $"No leagues found for {country.Name}.", fetch.Url);

        session.Emit.Emit(ScrapeEventKind.CatalogParsed, leagues.Count == 0 ? ScrapeLevel.Warn : ScrapeLevel.Info, $"{country.Name}: {leagues.Count} league seasons found.");
        return session.Complete(leagues);
    }
}
