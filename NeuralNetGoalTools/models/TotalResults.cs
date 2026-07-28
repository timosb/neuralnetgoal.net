using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class TotalResults
    {
        public string COUNTRY { get; set; }
        public string LEAGUE { get; set; }
        public string HOME { get; set; }
        public string AWAY { get; set; }
        public decimal? BOOK1 { get; set; }
        public decimal? BOOKX { get; set; }
        public decimal? BOOK2 { get; set; }
        public Int64? HWINS { get; set; }
        public Int64? DRAWS { get; set; }
        public Int64? AWINS { get; set; }
        public Int64? UNDER { get; set; }
        public Int64? OVER { get; set; }
        public Int64? GG { get; set; }
        public Int64? NG { get; set; }
        public int? Matches { get; set; }
        public int? Wins { get; set; }
        public int? Draws { get; set; }
        public int? Losses { get; set; }
        //public List<TeamInFavorPastGames> FavorPastGames { get; set;}
        public List<QueryMatches> History { get; set; }
    }
}
