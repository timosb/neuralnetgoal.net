using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer.Models
{
    public class SoccerDataVector
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }
        [StringLength(250)]
        public string Country { get; set; }
        [StringLength(250)]
        public string Division { get; set; }
        [StringLength(250)]
        public string Seazon { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime MatchDate { get; set; }
        [StringLength(500)]
        public string HomeTeam { get; set; }
        [StringLength(500)]
        public string AwayTeam { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? HalfTimeHomeScore { get; set; }
        public int? HalfTimeAwayScore { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AvgHomeOdds { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AvgDrawOdds { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
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
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC { get; set; }
        public int? HIST_TOTAL_GAMES_BTW_DRAWS { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? HIST_TOTAL_GAMES_BTW_DRAWS_PERC { get; set; }
        public int? HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC { get; set; }
        public int? HIST_TOTAL_GAMES_BTW_HOME_SCORE { get; set; }
        public int? HIST_TOTAL_GAMES_BTW_AWAY_SCORE { get; set; }
        public int? HOME_TEAM_LAST_5_GAMES_WINS { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? HOME_TEAM_LAST_5_GAMES_WINS_PERC { get; set; }
        public int? HOME_TEAM_LAST_5_GAMES_DRAWS { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? HOME_TEAM_LAST_5_GAMES_DRAWS_PERC { get; set; }
        public int? HOME_TEAM_LAST_5_GAMES_LOSSES { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? HOME_TEAM_LAST_5_GAMES_LOSSES_PERC { get; set; }
        public int? AWAY_TEAM_LAST_5_GAMES_WINS { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AWAY_TEAM_LAST_5_GAMES_WINS_PERC { get; set; }
        public int? AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC { get; set; }
        public int? AWAY_TEAM_LAST_5_GAMES_LOSSES { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC { get; set; }

    }
}
