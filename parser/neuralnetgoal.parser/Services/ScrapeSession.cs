using System.Collections.Concurrent;
using System.Diagnostics;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Fetching;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Parsing;

namespace NeuralNetGoal.Parser.Services;

/// <summary>Shared bookkeeping for one scrape call: issues, counters, page statistics, and the "fetch a match page
/// until its teams parse" loop used by both results and fixtures.</summary>
internal sealed class ScrapeSession
{
    private readonly IHtmlSource _html;
    private readonly BetexplorerOptions _o;
    private readonly ConcurrentQueue<ScrapeIssue> _issues = new();
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    private int _pages, _retries, _found, _parsed, _skipped, _failed;

    public ScrapeEmitter Emit { get; }

    public ScrapeSession(IHtmlSource html, BetexplorerOptions options, ScrapeEmitter emitter)
    {
        _html = html;
        _o = options;
        Emit = emitter;
    }

    public int Total => Volatile.Read(ref _found);
    public int Parsed => Volatile.Read(ref _parsed);
    public int Skipped => Volatile.Read(ref _skipped);
    public int Failed => Volatile.Read(ref _failed);

    public ScrapeCounters Counters => new(Total, Parsed, Skipped, Failed);

    public void SetFound(int n) => Interlocked.Exchange(ref _found, n);
    public void MarkParsed() => Interlocked.Increment(ref _parsed);
    public void MarkSkipped() => Interlocked.Increment(ref _skipped);
    public void MarkFailed() => Interlocked.Increment(ref _failed);

    public void AddIssue(string code, string message, string? url) =>
        _issues.Enqueue(new ScrapeIssue(code, message, url, DateTime.Now));

    public async Task<HtmlFetchResult> FetchAsync(string pathOrUrl, CancellationToken ct)
    {
        var r = await _html.GetAsync(pathOrUrl, Emit, ct).ConfigureAwait(false);
        Interlocked.Increment(ref _pages);
        return r;
    }

    /// <summary>Fetches a match page and parses it. If the teams cannot be found the page is fetched again
    /// (the site sometimes serves a partial page), up to DetailPageAttempts times.</summary>
    public async Task<(MatchDetailParse? Parse, HtmlFetchResult Fetch)> FetchMatchAsync(
        string matchUrl, Func<string, MatchDetailParse> parse, CancellationToken ct)
    {
        HtmlFetchResult fetch = new(matchUrl, 0, null, IssueCodes.HttpError, "not fetched");
        MatchDetailParse? parsed = null;
        var attempts = Math.Max(1, _o.DetailPageAttempts);

        for (var i = 1; i <= attempts; i++)
        {
            fetch = await FetchAsync(matchUrl, ct).ConfigureAwait(false);
            if (!fetch.Ok) return (null, fetch);

            parsed = parse(fetch.Html!);
            if (parsed.Detail is not null) return (parsed, fetch); // teams found; any other problem is not transient

            if (i < attempts)
            {
                Interlocked.Increment(ref _retries);
                Emit.Emit(ScrapeEventKind.PageRetry, ScrapeLevel.Warn,
                    $"Match page incomplete ({parsed.Problem}), fetching again ({i}/{attempts - 1}).", matchUrl: matchUrl, attempt: i);
            }
        }

        return (parsed, fetch);
    }

    public ScrapeOutcome<T> Complete<T>(IEnumerable<T> items) =>
        new(items.ToList(), _issues.ToList(),
            new ScrapeStats(_pages, _retries, Total, Parsed, Skipped, Failed, _sw.Elapsed));
}
