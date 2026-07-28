using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class PrevGameScore
    {
        public long ID { get; set; }
        public string Country { get; set; }
        public string Division { get; set; }
        public string Seazon { get; set; }
        public DateTime MatchDate { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public decimal? AvgHomeOdds { get; set; }
        public decimal? AvgDrawOdds { get; set; }
        public decimal? AvgAwayOdds { get; set; }
        public int? HOME_GOALS { get; set; }
        public int? AWAY_GOALS { get; set; }
    }

}
