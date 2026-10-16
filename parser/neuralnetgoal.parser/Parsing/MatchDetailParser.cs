using NeuralNetGoal.Parser.Models;

namespace NeuralNetGoal.Parser.Parsing;

/// <summary>What a match page tells us. Score fields are null for fixtures.</summary>
public sealed record MatchDetail(
    string HomeTeam,
    string AwayTeam,
    DateTime? MatchDate,
    int? HomeScore,
    int? AwayScore,
    int? HalfTimeHomeScore,
    int? HalfTimeAwayScore,
    /// <summary>Set when the page says the match was postponed / cancelled / abandoned.</summary>
    string? NotPlayedReason);

public sealed record MatchDetailParse(MatchDetail? Detail, string? ProblemCode, string? Problem)
{
    public bool Ok => Detail is not null && ProblemCode is null;
}

public static class MatchDetailParser
{
    /// <summary>Teams + date only (used for fixtures).</summary>
    public static MatchDetailParse ParseFixture(string html)
    {
        var doc = ParserHelpers.Load(html);
        var teams = ReadTeams(doc.DocumentNode);
        if (teams is null)
            return new(null, IssueCodes.LayoutChanged, "Team names not found on match page.");

        var date = ReadDate(doc.DocumentNode);
        return new(new MatchDetail(teams.Value.home, teams.Value.away, date, null, null, null, null, null), null, null);
    }

    /// <summary>Teams, date, final score, half-time score.</summary>
    public static MatchDetailParse ParseResult(string html)
    {
        var doc = ParserHelpers.Load(html);
        var teams = ReadTeams(doc.DocumentNode);
        if (teams is null)
            return new(null, IssueCodes.LayoutChanged, "Team names not found on match page.");

        var date = ReadDate(doc.DocumentNode);

        var stage = doc.DocumentNode.SelectNodes(Selectors.EventStage)?.FirstOrDefault();
        if (stage is not null)
        {
            var reason = ParserHelpers.Text(stage);
            var detail = new MatchDetail(teams.Value.home, teams.Value.away, date, null, null, null, null, reason);
            return new(detail, IssueCodes.MatchNotPlayed, string.IsNullOrWhiteSpace(reason) ? "Match not played." : reason);
        }

        var scoreNode = doc.DocumentNode.SelectNodes(Selectors.Score)?.FirstOrDefault();
        var score = ParserHelpers.FirstPair(scoreNode?.InnerText);
        if (score is null)
            return new(new MatchDetail(teams.Value.home, teams.Value.away, date, null, null, null, null, null),
                IssueCodes.ParseError, "Final score not found or not numeric.");

        var partial = doc.DocumentNode.SelectNodes(Selectors.PartialScores)?.FirstOrDefault();
        var ht = ParserHelpers.FirstPair(partial?.InnerText);

        if (date is null)
            return new(new MatchDetail(teams.Value.home, teams.Value.away, null, score.Value.home, score.Value.away, ht?.home, ht?.away, null),
                IssueCodes.ParseError, "Match date not found.");

        return new(new MatchDetail(teams.Value.home, teams.Value.away, date, score.Value.home, score.Value.away, ht?.home, ht?.away, null), null, null);
    }

    private static (string home, string away)? ReadTeams(HtmlAgilityPack.HtmlNode root)
    {
        var nodes = root.SelectNodes(Selectors.TeamNodes);
        if (nodes is null || nodes.Count < 2) return null;

        var home = ParserHelpers.Text(nodes[0].FirstChild ?? nodes[0]);
        var away = ParserHelpers.Text(nodes[1].FirstChild ?? nodes[1]);
        return string.IsNullOrEmpty(home) || string.IsNullOrEmpty(away) ? null : (home, away);
    }

    private static DateTime? ReadDate(HtmlAgilityPack.HtmlNode root) =>
        ParserHelpers.DataDt(root.SelectNodes(Selectors.MatchDate)?.FirstOrDefault()?.GetAttributeValue(Selectors.MatchDateAttribute, null));
}
