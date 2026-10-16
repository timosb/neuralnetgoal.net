using HtmlAgilityPack;
using NeuralNetGoal.Parser.Models;

namespace NeuralNetGoal.Parser.Parsing;

/// <summary>One row of a results list. The list already shows teams, final score and average odds, but only a relative date
/// ("Yesterday", "10.10."), so the match page is still read for the exact date/time, half-time score and the team names
/// the database historically stored.</summary>
public sealed record ResultRow(string MatchUrl, string TeamsText, string ScoreText, string DateText, decimal? HomeOdds, decimal? DrawOdds, decimal? AwayOdds);

/// <summary>A tab (stage / round) of a results page, e.g. "Main", "Relegation round".</summary>
public sealed record StageLink(string Href, string Text, bool IsCurrent);

public sealed record ResultsListPage(IReadOnlyList<ResultRow> Rows, IReadOnlyList<StageLink> Stages, string Round, bool TableFound);

public static class ResultsListParser
{
    public static ResultsListPage Parse(string html)
    {
        var doc = ParserHelpers.Load(html);

        // stage tabs
        var stages = new List<StageLink>();
        var tabList = doc.DocumentNode.SelectNodes(Selectors.StageTabList)?.FirstOrDefault();
        if (tabList is not null)
        {
            foreach (var li in tabList.ChildNodes.Where(n => n.Name == "li"))
            foreach (var a in li.ChildNodes.Where(n => n.Name == "a"))
            {
                var href = a.GetAttributeValue("href", "");
                stages.Add(new StageLink(href, ParserHelpers.Text(a), a.HasClass(Selectors.CurrentTabClass)));
            }
        }

        // The page shows the tab marked "current". (The legacy code used the 2nd tab's text, which is only right when the
        // current stage happens to be the second one; for the Champions League sample both give "League phase".)
        var currentTab = stages.FirstOrDefault(s => s.IsCurrent && !string.IsNullOrWhiteSpace(s.Text));
        var round = currentTab?.Text ?? Selectors.DefaultRound;

        // rows
        var tables = doc.DocumentNode.SelectNodes(Selectors.ResultsTables);
        if (tables is null) return new ResultsListPage([], stages, round, TableFound: false);

        var rows = new List<ResultRow>();
        foreach (var table in tables)
        {
            var trs = table.SelectNodes(".//tr");
            if (trs is null) continue;

            foreach (var tr in trs)
            {
                var tds = ParserHelpers.Cells(tr); // empty for header rows ("6. Round")
                if (tds.Count == 0) continue;

                var link = ParserHelpers.MatchLink(tr);
                if (link is null) continue;

                var (home, draw, away) = ParserHelpers.RowOdds(tr);
                var score = tds.Count > 1 ? ParserHelpers.Text(tds[1]) : "";
                var date = ParserHelpers.Text(tds[^1]);
                rows.Add(new ResultRow(link.Value.url, link.Value.teams, score, date, home, draw, away));
            }
        }

        return new ResultsListPage(rows, stages, round, TableFound: true);
    }
}

/// <summary>One row of a fixtures list. <see cref="DateText"/> is what the list shows ("Today 20:00", "17.10. 12:30",
/// or "&amp;nbsp;" when the time is not set yet): no year, and in the time zone the site chose for the visitor.</summary>
public sealed record FixtureRow(string MatchUrl, string TeamsText, string DateText, decimal? HomeOdds, decimal? DrawOdds, decimal? AwayOdds);

public sealed record FixturesListPage(IReadOnlyList<FixtureRow> Rows, bool TableFound);

public static class FixturesListParser
{
    public static FixturesListPage Parse(string html)
    {
        var doc = ParserHelpers.Load(html);
        var tables = doc.DocumentNode.SelectNodes(Selectors.FixturesTables);
        if (tables is null) return new FixturesListPage([], TableFound: false);

        var rows = new List<FixtureRow>();
        foreach (var table in tables)
        {
            var trs = table.SelectNodes(".//tr");
            if (trs is null) continue;

            foreach (var tr in trs)
            {
                var tds = ParserHelpers.Cells(tr); // empty for header rows ("7. Round")
                if (tds.Count == 0) continue;

                var link = ParserHelpers.MatchLink(tr);
                if (link is null) continue;

                var (home, draw, away) = ParserHelpers.RowOdds(tr);
                var dateCell = tds.FirstOrDefault(c => c.HasClass(Selectors.FixtureDateCellClass));
                rows.Add(new FixtureRow(link.Value.url, link.Value.teams, ParserHelpers.Text(dateCell), home, draw, away));
            }
        }

        return new FixturesListPage(rows, TableFound: true);
    }
}

public static class CatalogParser
{
    public static IReadOnlyList<CountryRef> ParseCountries(string html)
    {
        var doc = ParserHelpers.Load(html);
        var result = new List<CountryRef>();
        var items = doc.DocumentNode.SelectNodes(Selectors.CountryItems);
        if (items is null) return result;

        foreach (var li in items)
        {
            var a = li.SelectNodes($".//a[contains(concat(' ', normalize-space(@class), ' '), ' {Selectors.CountryTitleClass} ')]")?.FirstOrDefault();
            var href = a?.GetAttributeValue("href", null);
            if (a is null || string.IsNullOrWhiteSpace(href)) continue;
            result.Add(new CountryRef(ParserHelpers.Text(a), href));
        }

        return result;
    }

    /// <summary>A country page has one table body per season; the first row of each holds the season label.</summary>
    public static IReadOnlyList<LeagueSeasonRef> ParseLeagueSeasons(string html, string country)
    {
        var doc = ParserHelpers.Load(html);
        var result = new List<LeagueSeasonRef>();
        var bodies = doc.DocumentNode.SelectNodes(Selectors.LeagueSeasonBodies);
        if (bodies is null) return result;

        foreach (var body in bodies)
        {
            var trs = body.ChildNodes.Where(n => n.Name == "tr").ToList();
            if (trs.Count == 0) continue;

            var season = ParserHelpers.Text(trs[0].ChildNodes.FirstOrDefault(n => n.NodeType == HtmlNodeType.Element));

            foreach (var tr in trs)
            {
                var tds = ParserHelpers.Cells(tr);
                if (tds.Count == 0) continue;

                var href = ParserHelpers.Href(tds[0]);
                if (string.IsNullOrWhiteSpace(href)) continue;

                result.Add(new LeagueSeasonRef(country, ParserHelpers.Text(tds[0]), season, href));
            }
        }

        return result;
    }
}
