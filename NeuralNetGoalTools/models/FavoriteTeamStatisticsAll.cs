using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class FavoriteTeamStatisticsAll
    {
        public string COUNTRY { get; set; }
        public string LEAGUE { get; set; }
        public DateTime? MATCHDATE { get; set; }
        public string HOME { get; set; }
        public string AWAY { get; set; }
        public int? Matches { get; set; }
        public int? Wins { get; set; }
        public int? Draws { get; set; }
        public int? Losses { get; set; }
    }
}
