using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Models;

namespace NeuralNetGoal.Parser.Services;

/// <param name="KnownMatchUrls">Match URLs the caller already stored. Those matches are skipped without opening their page.</param>
/// <param name="MaxMatches">Only read the first N not-yet-known rows of the list (the site lists newest first).
/// Handy for "just the recent ones" while the caller does not yet store match URLs.</param>
/// <param name="IncludeAllStages">Also read the other tabs (playoff / relegation rounds) of the results page.</param>
public sealed record ResultsOptions(
    ISet<string>? KnownMatchUrls = null,
    int? MaxMatches = null,
    bool IncludeAllStages = true);

/// <param name="RequireOdds">Skip fixtures that do not (yet) have all three average odds. This is the legacy behaviour.</param>
public sealed record FixturesOptions(bool RequireOdds = true, int? MaxMatches = null);

public interface IResultsScraper
{
    /// <summary>Reads the finished matches of one league season. Every match attempt is reported to <paramref name="events"/>.</summary>
    Task<ScrapeOutcome<MatchResult>> GetResultsAsync(
        LeagueRef league,
        ResultsOptions? options = null,
        IScrapeEventSink? events = null,
        CancellationToken ct = default);
}

public interface IFixturesScraper
{
    /// <summary>Reads the upcoming matches (with average odds) of one league.</summary>
    Task<ScrapeOutcome<Fixture>> GetFixturesAsync(
        LeagueRef league,
        FixturesOptions? options = null,
        IScrapeEventSink? events = null,
        CancellationToken ct = default);
}

public interface ILeagueCatalogScraper
{
    Task<ScrapeOutcome<CountryRef>> GetCountriesAsync(
        IScrapeEventSink? events = null,
        CancellationToken ct = default);

    Task<ScrapeOutcome<LeagueSeasonRef>> GetLeagueSeasonsAsync(
        CountryRef country,
        IScrapeEventSink? events = null,
        CancellationToken ct = default);
}
