using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Fetching;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Parsing;

namespace NeuralNetGoal.Parser.Services;

public sealed class ResultsScraper(
    IHtmlSource html,
    IOptions<BetexplorerOptions> options,
    ILogger<ResultsScraper>? log = null) : IResultsScraper
{
    private readonly BetexplorerOptions _cfg = options.Value;

    public async Task<ScrapeOutcome<MatchResult>> GetResultsAsync(
        LeagueRef league,
        ResultsOptions? options = null,
        IScrapeEventSink? events = null,
        CancellationToken ct = default)
    {
        var o = options ?? new ResultsOptions();
        var session = new ScrapeSession(html, _cfg, new ScrapeEmitter(events, log, league));
        var em = session.Emit;

        em.Emit(ScrapeEventKind.LeagueStarted, ScrapeLevel.Info, $"Results {league.Country} / {league.Division} / {league.Season}");

        var resultsUrl = UrlUtil.Combine(league.UrlPath, _cfg.ResultsSuffix);
        var first = await session.FetchAsync(resultsUrl, ct).ConfigureAwait(false);
        if (!first.Ok)
        {
            session.AddIssue(first.ErrorCode!, first.ErrorMessage!, first.Url);
            em.Emit(ScrapeEventKind.LeagueFinished, ScrapeLevel.Error, $"Could not load the results page: {first.ErrorMessage}", httpStatus: first.StatusCode == 0 ? null : first.StatusCode);
            return session.Complete(Array.Empty<MatchResult>());
        }

        // ---- collect the rows of every stage tab ----
        var page = ResultsListParser.Parse(first.Html!);
        if (!page.TableFound)
        {
            session.AddIssue(IssueCodes.LayoutChanged, "Results table not found on the league page.", first.Url);
            em.Emit(ScrapeEventKind.LeagueFinished, ScrapeLevel.Error, "Results table not found (site layout changed, or the league has no results).");
            return session.Complete(Array.Empty<MatchResult>());
        }

        var all = new List<(ResultRow Row, string Round)>();
        all.AddRange(page.Rows.Select(r => (r, page.Round)));

        if (o.IncludeAllStages)
        {
            foreach (var stage in page.Stages.Where(s => !s.IsCurrent && !string.IsNullOrWhiteSpace(s.Href)))
            {
                var stageFetch = await session.FetchAsync(UrlUtil.ResolveStage(resultsUrl, stage.Href), ct).ConfigureAwait(false);
                if (!stageFetch.Ok)
                {
                    session.AddIssue(stageFetch.ErrorCode!, $"Stage '{stage.Text}': {stageFetch.ErrorMessage}", stageFetch.Url);
                    continue;
                }

                var stagePage = ResultsListParser.Parse(stageFetch.Html!);
                all.AddRange(stagePage.Rows.Select(r => (r, stage.Text)));
            }
        }

        var distinct = all.GroupBy(x => x.Row.MatchUrl).Select(g => g.First()).ToList();
        session.SetFound(distinct.Count);

        // ---- decide what to read ----
        var toRead = new List<(ResultRow Row, string Round)>();
        foreach (var item in distinct)
        {
            if (o.KnownMatchUrls?.Contains(item.Row.MatchUrl) == true)
            {
                session.MarkSkipped();
                var (h, a) = SplitTeams(item.Row.TeamsText);
                em.Emit(ScrapeEventKind.MatchSkipped, ScrapeLevel.Debug, "Already known", item.Row.MatchUrl, h, a);
            }
            else
            {
                toRead.Add(item);
            }
        }

        if (o.MaxMatches is { } max && toRead.Count > max) toRead = toRead.Take(max).ToList();

        em.Emit(ScrapeEventKind.LeagueListParsed, ScrapeLevel.Info,
            $"{distinct.Count} matches listed, {session.Skipped} already known, {toRead.Count} to read.", counters: session.Counters);

        // ---- read every match page ----
        var results = new ConcurrentBag<MatchResult>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await Parallel.ForEachAsync(
                toRead,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _cfg.MaxConcurrentRequests), CancellationToken = stop.Token },
                async (item, token) =>
                {
                    var (row, round) = item;
                    var (homeHint, awayHint) = SplitTeams(row.TeamsText);
                    em.Emit(ScrapeEventKind.MatchStarted, ScrapeLevel.Info, "Reading match", row.MatchUrl, homeHint, awayHint, session.Counters);

                    var sw = Stopwatch.StartNew();
                    var (parse, fetch) = await session.FetchMatchAsync(row.MatchUrl, MatchDetailParser.ParseResult, token).ConfigureAwait(false);

                    void Fail(string code, string message, string? home, string? away)
                    {
                        session.AddIssue(code, $"{home ?? homeHint} - {away ?? awayHint}: {message}", fetch.Url);
                        session.MarkFailed();
                        var level = code == IssueCodes.MatchNotPlayed ? ScrapeLevel.Warn : ScrapeLevel.Error;
                        em.Emit(ScrapeEventKind.MatchFailed, level, $"{code}: {message}", row.MatchUrl, home ?? homeHint, away ?? awayHint,
                            session.Counters, fetch.StatusCode == 0 ? null : fetch.StatusCode, durationMs: (int)sw.ElapsedMilliseconds);
                    }

                    if (!fetch.Ok)
                    {
                        Fail(fetch.ErrorCode!, fetch.ErrorMessage!, null, null);
                        if (fetch.ErrorCode == IssueCodes.Blocked) stop.Cancel(); // no point hammering a site that blocks us
                        return;
                    }

                    if (parse is null || !parse.Ok || parse.Detail is not { } d)
                    {
                        Fail(parse?.ProblemCode ?? IssueCodes.ParseError, parse?.Problem ?? "Unknown parse problem.", parse?.Detail?.HomeTeam, parse?.Detail?.AwayTeam);
                        return;
                    }

                    results.Add(new MatchResult(
                        row.MatchUrl, league.Country, league.Division, league.Season, round,
                        d.MatchDate!.Value, d.HomeTeam, d.AwayTeam, d.HomeScore!.Value, d.AwayScore!.Value,
                        d.HalfTimeHomeScore, d.HalfTimeAwayScore, row.HomeOdds, row.DrawOdds, row.AwayOdds));

                    session.MarkParsed();
                    em.Emit(ScrapeEventKind.MatchParsed, ScrapeLevel.Info,
                        $"{d.HomeScore}-{d.AwayScore}  {d.MatchDate:yyyy-MM-dd HH:mm}", row.MatchUrl, d.HomeTeam, d.AwayTeam,
                        session.Counters, fetch.StatusCode, durationMs: (int)sw.ElapsedMilliseconds);
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            em.Emit(ScrapeEventKind.LeagueFinished, ScrapeLevel.Error, "Aborted: the site is blocking requests.", counters: session.Counters);
            return session.Complete(results.OrderBy(r => r.MatchDate).ThenBy(r => r.MatchUrl));
        }

        em.Emit(ScrapeEventKind.LeagueFinished,
            session.Failed > 0 ? ScrapeLevel.Warn : ScrapeLevel.Info,
            $"Finished: {session.Parsed} parsed, {session.Skipped} skipped, {session.Failed} failed.", counters: session.Counters);

        return session.Complete(results.OrderBy(r => r.MatchDate).ThenBy(r => r.MatchUrl));
    }

    /// <summary>The list shows "Home - Away"; used only to label events before the match page is read.</summary>
    internal static (string? home, string? away) SplitTeams(string text)
    {
        var i = text.IndexOf(" - ", StringComparison.Ordinal);
        return i < 0 ? (string.IsNullOrWhiteSpace(text) ? null : text, null) : (text[..i].Trim(), text[(i + 3)..].Trim());
    }
}

internal static class UrlUtil
{
    public static string Combine(string path, string suffix) => path.TrimEnd('/') + "/" + suffix.TrimStart('/');

    /// <summary>Stage tab links are usually relative to the results page ("?stage=xyz").</summary>
    public static string ResolveStage(string resultsUrl, string href) =>
        href.StartsWith('/') || href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? href : resultsUrl + href;
}
