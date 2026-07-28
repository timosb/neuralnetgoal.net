using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class TeamInFavorPastGames
    {
        public string Team { get; set; }
        public DateTime? MatchDate { get; set; }
        public string Game { get; set; }
        public string Score { get; set; }
        public DateTime? Next_Match_Date { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

    }
}
