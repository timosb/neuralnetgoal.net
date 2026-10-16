namespace NeuralNetGoal.Parser.Parsing;

/// <summary>
/// EVERY XPath / class name / attribute the parsers depend on lives here.
/// When betexplorer changes its HTML, this is the file to edit.
/// These were ported unchanged from the legacy Parser.cs and have NOT yet been re-verified against live pages
/// (use BetexplorerOptions.DumpDirectory to capture real pages).
/// </summary>
internal static class Selectors
{
    // ---- country / league catalog ----
    public const string CountryItems = "//ul[@id='countries-select']/li";
    public const string CountryTitleClass = "list-events__item__title";
    public const string LeagueSeasonBodies = "//table/tbody";

    // ---- results list page ----
    public const string ResultsTables = "//*[@id='js-leagueresults-all']/div/div/table";
    /// <summary>The stage tabs (Qualification / League phase / Playoffs...). Present only for leagues with stages;
    /// verified on the Champions League page, absent on the Premier League page.</summary>
    public const string StageTabList = "//*[@id='sm-0-0']/div/ul";
    public const string DefaultRound = "Regular Season";
    public const string CurrentTabClass = "current";

    // ---- fixtures list page ----
    public const string FixturesTables = "//*[@id='js-leaguefixtures-all']/div/div/table";
    public const string OddsCellClass = "table-main__odds";
    public const string FixtureDateCellClass = "table-main__datetime";

    // ---- list rows (results and fixtures) ----
    /// <summary>The link of a row that points at the match page. Its child spans hold the home and away team names.
    /// Verified against real pages (October 2026); the legacy code located it by cell index, which no longer works.</summary>
    public const string MatchLinkClass = "in-match";

    // ---- match detail page ----
    public const string TeamNodes = "//div[@class='list-details__item__title teamsLink']";
    public const string MatchDate = "//*[@id='match-date']";
    public const string MatchDateAttribute = "data-dt";
    public const string EventStage = "//*[@id='js-eventstage']";
    public const string Score = "//*[@id='js-score']";
    public const string PartialScores = "//*[@id='js-partial']";

    // ---- odds cells ----
    public const string OddAttribute = "data-odd";
    public const string ColoredCellClass = "colored";
}
