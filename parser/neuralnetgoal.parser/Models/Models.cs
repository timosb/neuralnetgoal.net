namespace NeuralNetGoal.Parser.Models;

/// <summary>
/// Identifies one league season to scrape. The caller owns the list of leagues (e.g. a SoccerRunningLeagues table);
/// the parser only ever receives one reference at a time. <see cref="UrlPath"/> is the site-relative path
/// of the league page, e.g. "/soccer/england/premier-league/".
/// </summary>
public sealed record LeagueRef(string Country, string Division, string Season, string UrlPath);

/// <summary>A country entry from the site catalog.</summary>
public sealed record CountryRef(string Name, string UrlPath);

/// <summary>One league + season entry from a country page.</summary>
public sealed record LeagueSeasonRef(string Country, string Division, string Season, string UrlPath);

/// <summary>A finished match. <see cref="MatchUrl"/> is the stable site path of the match page and is the key
/// callers should store so they can pass it back as a "known match" next time.</summary>
public sealed record MatchResult(
    string MatchUrl,
    string Country,
    string Division,
    string Season,
    string Round,
    DateTime MatchDate,
    string HomeTeam,
    string AwayTeam,
    int HomeScore,
    int AwayScore,
    int? HalfTimeHomeScore,
    int? HalfTimeAwayScore,
    decimal? AvgHomeOdds,
    decimal? AvgDrawOdds,
    decimal? AvgAwayOdds);

/// <summary>An upcoming match with its average 1/X/2 odds.</summary>
public sealed record Fixture(
    string MatchUrl,
    string Country,
    string Division,
    string Season,
    DateTime MatchDate,
    string HomeTeam,
    string AwayTeam,
    decimal? AvgHomeOdds,
    decimal? AvgDrawOdds,
    decimal? AvgAwayOdds);

/// <summary>Something that went wrong (replaces the legacy Errors table row).</summary>
public sealed record ScrapeIssue(string Code, string Message, string? Url, DateTime At);

public static class IssueCodes
{
    /// <summary>The site refused us, or redirected to another host (firewall, captcha, geo block).</summary>
    public const string Blocked = "Blocked";
    public const string HttpError = "HttpError";
    public const string Timeout = "Timeout";
    /// <summary>Expected HTML structure was not found. Usually means the site layout changed.</summary>
    public const string LayoutChanged = "LayoutChanged";
    public const string MatchNotPlayed = "MatchNotPlayed";
    public const string ParseError = "ParseError";
}

public sealed record ScrapeStats(
    int PagesFetched,
    int Retries,
    int MatchesFound,
    int MatchesParsed,
    int MatchesSkipped,
    int MatchesFailed,
    TimeSpan Duration);

public sealed record ScrapeOutcome<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<ScrapeIssue> Issues,
    ScrapeStats Stats)
{
    public bool HasIssues => Issues.Count > 0;
}
