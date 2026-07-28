using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class SuperSummaryModel
    {
        public string COUNTRY { get; set; }
        public string LEAGUE { get; set; }
        public DateTime MatchDate { get; set; }
        public string HOME { get; set; }
        public string AWAY { get; set; }
        public decimal? BOOK1 { get; set; }
        public decimal? BOOKX { get; set; }
        public decimal? BOOK2 { get; set; }
        public decimal? NLT1 { get; set; }
        public decimal? NLTX { get; set; }
        public decimal? NLT2 { get; set; }
        public int? HomeGoals { get; set; }
        public int? AwayGoals { get; set; }
        public int? HomePos { get; set; }
        public int? AwayPos { get; set; }
        public decimal? FavorHistoryGames { get; set; }
        public decimal? FavorHistory1 { get; set; }
        public decimal? FavorHistoryX { get; set; }
        public decimal? FavorHistory2 { get; set; }
        public List<ExtdQueryResult> QueryResults { get; set; }
    }
}
