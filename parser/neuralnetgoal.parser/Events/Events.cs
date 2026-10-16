using Microsoft.Extensions.Logging;
using NeuralNetGoal.Parser.Models;

namespace NeuralNetGoal.Parser.Events;

public enum ScrapeLevel { Debug, Info, Warn, Error }

public enum ScrapeEventKind
{
    // job lifecycle (emitted by hosts such as the Web API)
    JobStarted,
    JobCompleted,
    JobFailed,
    JobCancelled,
    /// <summary>Emitted by the API stream when a slow/late client missed events that were already trimmed.</summary>
    Gap,

    // league level
    LeagueStarted,
    /// <summary>The list page was parsed: Counters.Total rows found, Counters.Skipped already known.</summary>
    LeagueListParsed,
    LeagueFinished,

    // http level
    PageFetched,
    PageRetry,

    // match level: one MatchStarted per match the parser tries to read, followed by exactly one outcome
    MatchStarted,
    MatchParsed,
    /// <summary>The match was deliberately not read (already known to the caller, or a fixture without odds yet).</summary>
    MatchSkipped,
    MatchFailed,

    // catalog
    CatalogParsed,
}

public sealed record ScrapeCounters(int Total, int Done, int Skipped, int Failed);

/// <summary>One thing that happened while scraping. <see cref="Seq"/> is assigned by the host that fans events out
/// (the library emits 0) so that consumers can resume with "give me everything after Seq N".</summary>
public sealed record ScrapeEvent
{
    public long Seq { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public ScrapeLevel Level { get; init; }
    public ScrapeEventKind Kind { get; init; }
    public string? Country { get; init; }
    public string? Division { get; init; }
    public string? Season { get; init; }
    public string? MatchUrl { get; init; }
    public string? Home { get; init; }
    public string? Away { get; init; }
    public string Message { get; init; } = "";
    public ScrapeCounters? Counters { get; init; }
    public int? HttpStatus { get; init; }
    public int? Attempt { get; init; }
    public int? DurationMs { get; init; }

    public override string ToString()
    {
        var who = Home is null ? "" : $" {Home} - {Away}";
        var where = Division is null ? "" : $" [{Country}/{Division}]";
        return $"{At.ToLocalTime():HH:mm:ss} {Level,-5} {Kind}{where}{who} {Message}".TrimEnd();
    }
}

/// <summary>Receives events. Implementations must be thread-safe and must not throw
/// (the emitter swallows sink exceptions so a broken listener can never stop a scrape).</summary>
public interface IScrapeEventSink
{
    void Emit(ScrapeEvent e);
}

public sealed class NullScrapeEventSink : IScrapeEventSink
{
    public static readonly NullScrapeEventSink Instance = new();
    public void Emit(ScrapeEvent e) { }
}

/// <summary>Convenience sink that forwards to a delegate (console, UI, test collector...).</summary>
public sealed class DelegateScrapeEventSink(Action<ScrapeEvent> handler) : IScrapeEventSink
{
    public void Emit(ScrapeEvent e) => handler(e);
}

/// <summary>Helper the scrapers use to emit events, stamped with the league being processed,
/// and mirror them to an optional <see cref="ILogger"/>.</summary>
public sealed class ScrapeEmitter
{
    private readonly IScrapeEventSink? _sink;
    private readonly ILogger? _log;
    private readonly LeagueRef? _league;

    public ScrapeEmitter(IScrapeEventSink? sink, ILogger? log = null, LeagueRef? league = null)
    {
        _sink = sink;
        _log = log;
        _league = league;
    }

    public ScrapeEmitter ForLeague(LeagueRef league) => new(_sink, _log, league);

    public void Emit(
        ScrapeEventKind kind,
        ScrapeLevel level,
        string message,
        string? matchUrl = null,
        string? home = null,
        string? away = null,
        ScrapeCounters? counters = null,
        int? httpStatus = null,
        int? attempt = null,
        int? durationMs = null)
    {
        var e = new ScrapeEvent
        {
            Level = level,
            Kind = kind,
            Country = _league?.Country,
            Division = _league?.Division,
            Season = _league?.Season,
            MatchUrl = matchUrl,
            Home = home,
            Away = away,
            Message = message,
            Counters = counters,
            HttpStatus = httpStatus,
            Attempt = attempt,
            DurationMs = durationMs,
        };

        if (_log is not null)
        {
            var lvl = level switch
            {
                ScrapeLevel.Debug => LogLevel.Debug,
                ScrapeLevel.Info => LogLevel.Information,
                ScrapeLevel.Warn => LogLevel.Warning,
                _ => LogLevel.Error,
            };
            _log.Log(lvl, "{Kind} {Country}/{Division} {Home}-{Away} {Message}", kind, e.Country, e.Division, home, away, message);
        }

        try { _sink?.Emit(e); }
        catch { /* a broken listener must never stop the scrape */ }
    }
}
