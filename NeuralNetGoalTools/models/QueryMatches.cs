using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class QueryMatches
    {
        public string COUNTRY { get; set; }
        public string LEAGUE { get; set; }
        public DateTime? MATCHDATE { get; set; }
        public string HOME { get; set; }
        public string AWAY { get; set; }
        public decimal? BOOK1 { get; set; }
        public decimal? BOOKX { get; set; }
        public decimal? BOOK2 { get; set; }
        public decimal? NLT1 { get; set; }
        public decimal? NLTX { get; set; }
        public decimal? NLT2 { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
    }
}
