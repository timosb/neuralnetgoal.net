using NeuralNetGoal.Parser.Models;

namespace Tools.NeuralNetGoal.Service.Data;

/// <summary>A row of dbo.SoccerRunningLeagues. The caller (this console) owns this list, the parser never sees the table.</summary>
public sealed class RunningLeague
{
    public int Id { get; set; }
    public string Country { get; set; } = "";
    public string Division { get; set; } = "";
    public string Season { get; set; } = "";
    public string URL { get; set; } = "";

    public LeagueRef ToRef() => new(Country, Division, Season, URL);
    public override string ToString() => $"{Country} / {Division} / {Season}";
}

/// <summary>
/// A row of the feature vector (dbo.DataVectors for training data, dbo.fn_Soccer_fixtures() for fixtures).
/// Property names must match the column names exactly (Dapper maps by name), including the "Seazon" spelling.
/// </summary>
public sealed class SoccerDataVector
{
    public int ID { get; set; }
    public string Country { get; set; } = "";
    public string Division { get; set; } = "";
    public string Seazon { get; set; } = "";
    public DateTime MatchDate { get; set; }
    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public int? HalfTimeHomeScore { get; set; }
    public int? HalfTimeAwayScore { get; set; }
    public decimal? AvgHomeOdds { get; set; }
    public decimal? AvgDrawOdds { get; set; }
    public decimal? AvgAwayOdds { get; set; }
    public int? Home_Team_Total_Matches_Played { get; set; }
    public int? Home_Team_Ranking { get; set; }
    public int? Home_Team_Points { get; set; }
    public int? Home_Team_Matches_Played_At_Home_SoFar { get; set; }
    public int? Home_Team_Matches_Played_At_Home_SoFar_Goals_For { get; set; }
    public int? Home_Team_Matches_Played_At_Home_SoFar_Goals_Against { get; set; }
    public int? Home_Team_Matches_Played_Away_SoFar { get; set; }
    public int? Home_Team_Matches_Played_Away_SoFar_Goals_For { get; set; }
    public int? Home_Team_Matches_Played_Away_SoFar_Goals_Against { get; set; }
    public int? Away_Team_Total_Matches_Played { get; set; }
    public int? Away_Team_Ranking { get; set; }
    public int? Away_Team_points { get; set; }
    public int? Away_Team_Matches_Played_At_Home_SoFar { get; set; }
    public int? Away_Team_Matches_Played_At_Home_SoFar_Goals_For { get; set; }
    public int? Away_Team_Matches_Played_At_Home_SoFar_Goals_Against { get; set; }
    public int? Away_Team_Matches_Played_Away_SoFar { get; set; }
    public int? Away_Team_Matches_Played_Away_SoFar_Goals_For { get; set; }
    public int? Away_Team_Matches_Played_Away_SoFar_Goals_Against { get; set; }
    public int? HIST_TOTAL_GAMES_BTW { get; set; }
    public int? HIST_TOTAL_GAMES_BTW_HOME_WINS { get; set; }
    public decimal? HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC { get; set; }
    public int? HIST_TOTAL_GAMES_BTW_DRAWS { get; set; }
    public decimal? HIST_TOTAL_GAMES_BTW_DRAWS_PERC { get; set; }
    public int? HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS { get; set; }
    public decimal? HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC { get; set; }
    public int? HIST_TOTAL_GAMES_BTW_HOME_SCORE { get; set; }
    public int? HIST_TOTAL_GAMES_BTW_AWAY_SCORE { get; set; }
    public int? HOME_TEAM_LAST_5_GAMES_WINS { get; set; }
    public decimal? HOME_TEAM_LAST_5_GAMES_WINS_PERC { get; set; }
    public int? HOME_TEAM_LAST_5_GAMES_DRAWS { get; set; }
    public decimal? HOME_TEAM_LAST_5_GAMES_DRAWS_PERC { get; set; }
    public int? HOME_TEAM_LAST_5_GAMES_LOSSES { get; set; }
    public decimal? HOME_TEAM_LAST_5_GAMES_LOSSES_PERC { get; set; }
    public int? AWAY_TEAM_LAST_5_GAMES_WINS { get; set; }
    public decimal? AWAY_TEAM_LAST_5_GAMES_WINS_PERC { get; set; }
    public int? AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS { get; set; }
    public decimal? AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC { get; set; }
    public int? AWAY_TEAM_LAST_5_GAMES_LOSSES { get; set; }
    public decimal? AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC { get; set; }
}
